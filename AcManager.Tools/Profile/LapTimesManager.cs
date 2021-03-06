﻿// #define AC_OLD_DB

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools.LapTimes;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.Profile {
    public class LapTimesManager : NotifyPropertyChanged, IAcIdsProvider {
        public static readonly string CmSourceId = @"Content Manager";
        public static readonly string SidekickSourceId = "Sidekick";
        public static readonly string AcNewSourceId = "AC New";

#if AC_OLD_DB
        public static readonly string AcOldSourceId = "AC Old Database";
#endif

        public static readonly string RaceEssentialsSourceId = "Race Essentials";
        public static readonly string Ov1InfoSourceId = "OV1Info";

        private static LapTimesManager _instance;

        public static LapTimesManager Instance => _instance ?? (_instance = new LapTimesManager());

        private readonly LapTimesSource[] _sources;

        public ICollection<LapTimesSource> Sources => _sources;

        public IEnumerable<LapTimesSource> EnabledSources => _sources.Where(x => x.IsEnabled);

        private LapTimesManager() {
            _sources = new[] {
                new LapTimesSource(CmSourceId,
                        "Own CM storage", "Filled using shared memory or, if disabled, race results file.",
                        "Settings.LapTimesSettings.SourceCm", true, true,
                        null, null),
                new LapTimesSource(AcNewSourceId,
                        "New AC storage", "Used by the original launcher. Much faster and more stable.",
                        "Settings.LapTimesSettings.SourceAcNew", true, true,
                        () => new AcLapTimesNewReader(FileUtils.GetDocumentsDirectory(), this), () => TracksManager.Instance.EnsureLoadedAsync()),
#if AC_OLD_DB
                new LapTimesSource(AcOldSourceId,
                        "Old AC database", "Used by the original launcher. Reading is quite wobbly, so I recommend to keep it disabled.",
                        "Settings.LapTimesSettings.SourceAcDb", false, false,
                        () => new AcLapTimesReader(FileUtils.GetDocumentsDirectory()), null),
#endif
                new LapTimesSource(SidekickSourceId,
                        "Sidekick", "Very good source of lap times, accurate and reliable.",
                        "Settings.LapTimesSettings.SourceSidekick", true, false,
                        () => new SidekickLapTimesReader(
                                Path.Combine(FileUtils.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), "Sidekick"),
                                this), () => TracksManager.Instance.EnsureLoadedAsync()) {
                                    DetailsUrl = "http://www.racedepartment.com/downloads/sidekick.11007/"
                                },
                new LapTimesSource(RaceEssentialsSourceId,
                        "Race Essentials", "Quite good source of lap times as well.",
                        "Settings.LapTimesSettings.SourceRaceEssentials", true, false,
                        () => new SidekickLapTimesReader(
                                Path.Combine(FileUtils.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), "RaceEssentials"),
                                this), () => TracksManager.Instance.EnsureLoadedAsync()),
                new LapTimesSource(Ov1InfoSourceId,
                        "Rivali OV1 Info", "Sadly, there is no information about entries date. If you need a patch for 64-bit AC or layouts support, you can get it [url=\"http://acstuff.ru/f/d/15-rivali-ov1-info\"]here[/url].",
                        "Settings.LapTimesSettings.SourceOv1Info", false, false,
                        () => new Ov1InfoLapTimesReader(
                                Path.Combine(FileUtils.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), "OV1Info"),
                                this), () => TracksManager.Instance.EnsureLoadedAsync()),
            };

            Entries = new BetterObservableCollection<LapTimeEntry>();
        }

        public void SetListener() {
            GameWrapper.Ended += OnRaceFinished;
        }

        public event EventHandler LapTimesChanged;

        private void OnRaceFinished(object sender, GameEndedArgs e) {
            RaiseEntriesChanged();

            var basic = e.StartProperties.BasicProperties;
            var bestLap = e.Result?.GetExtraByType<Game.ResultExtraBestLap>();

            var carId = basic?.CarId;
            var trackId = basic?.TrackId;

            if (trackId != null && basic.TrackConfigurationId != null) {
                trackId = $@"{trackId}/{basic.TrackConfigurationId}";
            }

            TimeSpan? time;

            if (SettingsHolder.Drive.WatchForSharedMemory) {
                var last = PlayerStatsManager.Instance.Last;
                if (last == null) {
                    Logging.Warning("Race finished, but PlayerStatsManager.Instance.Last is missing");
                    return;
                }

                if (carId == null) {
                    carId = last.CarId;
                }

                if (trackId == null) {
                    trackId = last.TrackId;
                }

                var sharedTime = last.BestLap;
                if (!sharedTime.HasValue) {
                    Logging.Warning("No laptime has been set");
                    return;
                }

                time = sharedTime.Value;
            } else {
                time = carId == null || trackId == null || bestLap == null ||
                        bestLap.IsCancelled ? (TimeSpan?)null : bestLap.Time;
            }

            if (time.HasValue && carId != null && trackId != null) {
                AddEntry(carId, trackId, DateTime.Now, time.Value).Forget();
            } else {
                Logging.Warning($"Can’t save new lap time: time={time}, car={carId}, track={trackId}");
            }
        }

        [CanBeNull]
        private LapTimeEntry Get(string carId, string trackId) {
            trackId = trackId.Replace('/', '-');
            return Entries.FirstOrDefault(x => string.Equals(x.CarId, carId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.TrackAcId, trackId, StringComparison.OrdinalIgnoreCase));
        }

        public async Task AddEntry(string carId, string trackId, DateTime date, TimeSpan time) {
            await EnabledSources.Where(x => x.AutoAddEntries).Select(x => x.AddEntryAsync(new LapTimeEntry(x.DisplayName, carId, trackId, date, time))).WhenAll(2);
            RaiseEntriesChanged();
        }

        public BetterObservableCollection<LapTimeEntry> Entries { get; }

        private static void KeepBetterOnes([NotNull] List<LapTimeEntry> result, [NotNull] IEnumerable<LapTimeEntry> additional) {
            foreach (var entry in additional) {
                var existingIndex = result.FindIndex(x => x.Same(entry));
                if (existingIndex != -1) {
                    var existing = result[existingIndex];
                    if (existing.LapTime > entry.LapTime) {
                        result.RemoveAt(existingIndex);
                    } else {
                        continue;
                    }
                }

                result.Add(entry);
            }
        }

        private class LapTimeEntryComparer : IComparer<LapTimeEntry>{
            public int Compare(LapTimeEntry x, LapTimeEntry y) {
                if (x == null) return y == null ? 0 : 1;
                if (y == null) return -1;
                return -x.EntryDate.CompareTo(y.EntryDate);
            }
        }

        private static readonly LapTimeEntryComparer Comparer = new LapTimeEntryComparer();

        private async Task<List<LapTimeEntry>> GetList() {
            var list = new List<LapTimeEntry>();
            foreach (var source in EnabledSources) {
                KeepBetterOnes(list, await source.GetEntriesAsync().ConfigureAwait(false));
            }
            list.Sort(Comparer);
            return list;
        }

        private long _previousChecksum;

        private async Task UpdateAsyncInner() {
            try {
                await Task.Delay(50);

                var s = Stopwatch.StartNew();
                if (Entries.ReplaceIfDifferBy(await GetList())) {
                    LapTimesChanged?.Invoke(this, EventArgs.Empty);
                }

                Logging.Debug($"{s.Elapsed.TotalMilliseconds:F2} ms");
            } finally {
                _updateTask = null;
            }
        }

        private Task _updateTask;

        public Task UpdateAsync() {
            if (_updateTask != null) {
                return _updateTask;
            }

            var checksum = EnabledSources.Aggregate(0L, (current, source) => (current * 397) ^ source.ChangeId);
            if (checksum == _previousChecksum) {
                return Task.Delay(0);
            }

            _previousChecksum = checksum;
            return _updateTask = UpdateAsyncInner();
        }

        public void ClearCache() {
            foreach (var source in EnabledSources) {
                source.ClearCache();
            }
        }

        IReadOnlyList<string> IAcIdsProvider.GetCarIds() {
            return CarsManager.Instance.WrappersList.Select(x => x.Id).ToList();
        }

        IReadOnlyList<Tuple<string, string>> IAcIdsProvider.GetTrackIds() {
            return TracksManager.Instance.LoadedOnly
                                .SelectMany(x => x.MultiLayouts?.Select(y => new Tuple<string, string>(x.Id, y.LayoutId)) ??
                                        new[] { new Tuple<string, string>(x.Id, null) })
                                .ToList();
        }

        public async Task RemoveEntryAsync(LapTimeEntry entry) {
            if (EnabledSources.Any(x => x.ReadOnly)) {
                NonfatalError.Notify("Can’t remove entry from read-only sources",
                        $"Please, disable {EnabledSources.Where(x => x.ReadOnly).Select(x => x.DisplayName).JoinToReadableString()}.", solutions: new[] {
                            new NonfatalErrorSolution("Disable read-only sources", null, token => {
                                foreach (var source in EnabledSources) {
                                    source.IsEnabled = false;
                                }

                                RaiseEntriesChanged();
                                return Task.Delay(0);
                            }),
                        });
                return;
            }

            var car = CarsManager.Instance.GetById(entry.CarId)?.DisplayName ?? entry.CarId;
            var track = TracksManager.Instance.GetLayoutByKunosId(entry.TrackId)?.LayoutName ?? entry.TrackId;
            if (ModernDialog.ShowMessage($"Are you sure you want to remove {car} on {track} lap time?", "Remove Lap Time",
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try {
                using (WaitingDialog.Create("Deleting…")) {
                    var changed = false;

                    foreach (var source in EnabledSources) {
                        changed |= await source.RemoveAsync(entry.CarId, entry.TrackId);
                    }

                    if (changed) {
                        Logging.Debug("Removed");
                        RaiseEntriesChanged();
                    }
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t remove entry", e);
            }
        }

        private bool _raisingEntriesChanged;

        public async void RaiseEntriesChanged() {
            if (_raisingEntriesChanged) return;
            try {
                _raisingEntriesChanged = true;
                await Task.Delay(50);
                await UpdateAsync();
            } finally {
                _raisingEntriesChanged = false;
            }
        }
    }
}
