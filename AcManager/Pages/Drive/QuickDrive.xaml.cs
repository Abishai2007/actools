﻿using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.UserControls;
using AcManager.Controls.ViewModels;
using AcManager.CustomShowroom;
using AcManager.Internal;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Lists;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive : ILoadableContent {
        public const string PresetableKeyValue = "Quick Drive";
        private const string KeySaveable = "__QuickDrive_Main";

        private ViewModel Model => (ViewModel)DataContext;

        public Task LoadAsync(CancellationToken cancellationToken) {
            return WeatherManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            WeatherManager.Instance.EnsureLoaded();
        }

        private static WeakReference<QuickDrive> _current;

        public void Initialize() {
            OnSizeChanged(null, null);

            DataContext = new ViewModel(null, true,
                    _selectNextCar, _selectNextCarSkinId,
                    track: _selectNextTrack, trackSkin: _selectNextTrackSkin,
                    weatherId: _selectNextWeather?.Id, mode: _selectNextMode);
            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(Model.TrackState, nameof(INotifyPropertyChanged.PropertyChanged),
                    OnTrackStateChanged);
            this.OnActualUnload(() => {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(Model.TrackState, nameof(INotifyPropertyChanged.PropertyChanged),
                        OnTrackStateChanged);
            });

            _current = new WeakReference<QuickDrive>(this);

            InitializeComponent();
            InputBindings.AddRange(new[] {
                new InputBinding(Model.GoCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(Model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(UserPresetsControl.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control)),

                new InputBinding(new DelegateCommand(() => {
                    CustomShowroomWrapper.StartAsync(Model.SelectedCar, Model.SelectedCar.SelectedSkin);
                }), new KeyGesture(Key.H, ModifierKeys.Alt)),
                new InputBinding(new DelegateCommand(() => {
                    CarOpenInShowroomDialog.Run(Model.SelectedCar, Model.SelectedCar.SelectedSkin?.Id);
                }), new KeyGesture(Key.H, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(() => {
                    new CarOpenInShowroomDialog(Model.SelectedCar, Model.SelectedCar.SelectedSkin?.Id).ShowDialog();
                }), new KeyGesture(Key.H, ModifierKeys.Control | ModifierKeys.Shift)),

#if DEBUG
                new InputBinding(new AsyncCommand(() =>
                        LapTimesManager.Instance.AddEntry(
                                Model.SelectedCar.Id, Model.SelectedTrack.IdWithLayout,
                                DateTime.Now, TimeSpan.FromSeconds(MathUtils.Random(10d, 20d)))),
                        new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)),
#endif

                new InputBinding(Model.RandomizeCommand, new KeyGesture(Key.R, ModifierKeys.Alt)),
                new InputBinding(Model.RandomCarSkinCommand, new KeyGesture(Key.R, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomCarCommand, new KeyGesture(Key.D1, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomTrackCommand, new KeyGesture(Key.D2, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomTimeCommand, new KeyGesture(Key.D3, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomWeatherCommand, new KeyGesture(Key.D4, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomTemperatureCommand, new KeyGesture(Key.D5, ModifierKeys.Control | ModifierKeys.Alt)),
            });

            _selectNextCar = null;
            _selectNextCarSkinId = null;
            _selectNextTrack = null;
            _selectNextTrackSkin = null;
            _selectNextWeather = null;
            _selectNextMode = null;

            this.OnActualUnload(() => {
                Model.Unload();
            });
        }

        private void OnTrackStateChanged(object sender, PropertyChangedEventArgs e) {
            Model.SaveLater();
        }

        private DispatcherTimer _realConditionsTimer;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_realConditionsTimer != null) return;

            _realConditionsTimer = new DispatcherTimer();
            _realConditionsTimer.Tick += (o, args) => {
                if (Model.RealConditions) {
                    Model.UpdateConditions();
                }
            };
            _realConditionsTimer.Interval = TimeSpan.FromMinutes(0.5);
            _realConditionsTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (_realConditionsTimer == null) return;
            _realConditionsTimer.Stop();
            _realConditionsTimer = null;
        }

        private void OnModeTabNavigated(object sender, NavigationEventArgs e) {
            var c = ModeTab.Frame.Content as IQuickDriveModeControl;
            if (c != null) {
                c.Model = Model.SelectedModeViewModel;
            }

            // _model.SelectedModeViewModel = (ModeTab.Frame.Content as IQuickDriveModeControl)?.Model;
        }

        private class SaveableData {
            public Uri Mode;
            public string ModeData, CarId, TrackId, WeatherId;
            public bool RealConditions;
            public double Temperature;
            public int Time, TimeMultipler;

            public string TrackPropertiesPresetFilename, TrackPropertiesData;

            [JsonProperty(@"ico")]
            public bool IdealConditions;

            [JsonProperty(@"wsf")]
            public double WindSpeedMin = 10;

            [JsonProperty(@"wst")]
            public double WindSpeedMax = 10;

            [JsonProperty(@"wd")]
            public double WindDirection;

            [JsonProperty(@"rws")]
            public bool RandomWindSpeed;

            [JsonProperty(@"rwd")]
            public bool RandomWindDirection;

            [JsonProperty(@"tpc")]
            public bool TrackPropertiesChanged;

            // Obsolete
            [JsonProperty(@"TrackPropertiesPreset")]
#pragma warning disable 649
            public string ObsTrackPropertiesPreset;
#pragma warning restore 649

            [JsonProperty(@"rcTimezones")]
            public bool? RealConditionsTimezones;

            [JsonProperty(@"rcManTime")]
            public bool? RealConditionsManualTime;

            [JsonProperty(@"rcLw")]
            public bool? RealConditionsLocalWeather;

            [JsonProperty(@"rte")]
            public bool RandomTemperature;

            [JsonProperty(@"rti")]
            public bool RandomTime;

            [JsonProperty(@"crt")]
            public bool CustomRoadTemperature;

            [JsonProperty(@"crtv")]
            public double? CustomRoadTemperatureValue;
        }

        public partial class ViewModel : NotifyPropertyChanged, IUserPresetable {
            private readonly bool _uiMode;

            #region Notifieable Stuff
            private Uri _selectedMode;
            private CarObject _selectedCar;
            private TrackObjectBase _selectedTrack;

            private bool _skipLoading;

            public Uri SelectedMode {
                get => _selectedMode;
                set {
                    if (Equals(value, _selectedMode)) return;
                    _selectedMode = value;
                    OnPropertyChanged();
                    SaveLater();

                    switch (value.OriginalString) {
                        case "/Pages/Drive/QuickDrive_Drift.xaml":
                            SelectedModeViewModel = new QuickDrive_Drift.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_Hotlap.xaml":
                            SelectedModeViewModel = new QuickDrive_Hotlap.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_Practice.xaml":
                            SelectedModeViewModel = new QuickDrive_Practice.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_Race.xaml":
                            SelectedModeViewModel = new QuickDrive_Race.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_Trackday.xaml":
                            SelectedModeViewModel = new QuickDrive_Trackday.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_Weekend.xaml":
                            SelectedModeViewModel = new QuickDrive_Weekend.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_TimeAttack.xaml":
                            SelectedModeViewModel = new QuickDrive_TimeAttack.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_Drag.xaml":
                            SelectedModeViewModel = new QuickDrive_Drag.ViewModel(!_skipLoading);
                            break;

                        default:
                            Logging.Warning("Not supported mode: " + value);
                            SelectedMode = new Uri("/Pages/Drive/QuickDrive_Practice.xaml", UriKind.Relative);
                            break;
                    }
                }
            }

            private AsyncCommand _randomizeCommand;

            public AsyncCommand RandomizeCommand => _randomizeCommand ?? (_randomizeCommand = new AsyncCommand(async () => {
                RandomCarCommand.Execute();
                await Task.Delay(100);
                RandomTrackCommand.Execute();
                await Task.Delay(100);
                RandomTimeCommand.Execute();
                await Task.Delay(100);
                SetRandomWeather(false);
                await Task.Delay(100);
                RandomTemperatureCommand.Execute();
            }));

            public CarObject SelectedCar {
                get => _selectedCar;
                set {
                    if (Equals(value, _selectedCar)) return;
                    _selectedCar = value;
                    // _selectedCar?.LoadSkins();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GoCommand));
                    OnSelectedUpdated();
                    SaveLater();

                    AcContext.Instance.CurrentCar = value;
                }
            }

            private static T GetRandomObject<T>(BaseAcManager<T> manager, string currentId) where T : AcObjectNew {
                var id = manager.WrappersList.Where(x => x.Value.Enabled).Select(x => x.Id).ApartFrom(currentId).RandomElementOrDefault() ?? currentId;
                return manager.GetById(id) ?? manager.GetDefault();
            }

            private DelegateCommand _randomCarCommand;

            public DelegateCommand RandomCarCommand => _randomCarCommand ?? (_randomCarCommand = new DelegateCommand(() => {
                SelectedCar = GetRandomObject(CarsManager.Instance, SelectedCar?.Id);
                if (SelectedCar != null) {
                    SelectedCar.SelectedSkin = GetRandomObject(SelectedCar.SkinsManager, SelectedCar.SelectedSkin?.Id);
                }
            }));

            private DelegateCommand _randomCarSkinCommand;

            public DelegateCommand RandomCarSkinCommand => _randomCarSkinCommand ?? (_randomCarSkinCommand = new DelegateCommand(() => {
                if (SelectedCar != null) {
                    SelectedCar.SelectedSkin = GetRandomObject(SelectedCar.SkinsManager, SelectedCar.SelectedSkin?.Id);
                }
            }));

            public TrackStateViewModel TrackState => TrackStateViewModel.Instance;

            public TrackObjectBase SelectedTrack {
                get => _selectedTrack;
                set {
                    if (Equals(value, _selectedTrack)) return;
                    _selectedTrack = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GoCommand));
                    OnSelectedUpdated();
                    SaveLater();
                    TrackUpdated();
                    FancyBackgroundManager.Instance.ChangeBackground(value?.PreviewImage);
                }
            }

            private DelegateCommand _randomTrackCommand;

            public DelegateCommand RandomTrackCommand => _randomTrackCommand ?? (_randomTrackCommand = new DelegateCommand(() => {
                var track = GetRandomObject(TracksManager.Instance, SelectedTrack.Id);
                SelectedTrack = track.MultiLayouts?.RandomElementOrDefault() ?? track;
            }));

            public int TimeMultiplerMinimum => 0;

            public int TimeMultiplerMaximum => 360;

            public int TimeMultiplerMaximumLimited => 60;

            private int _timeMultiplier;

            public int TimeMultiplier {
                get => _timeMultiplier;
                set {
                    if (value == _timeMultiplier) return;
                    _timeMultiplier = value.Clamp(TimeMultiplerMinimum, TimeMultiplerMaximum);
                    OnPropertyChanged();
                    SaveLater();
                }
            }
            #endregion

            private readonly ISaveHelper _saveable;

            internal void SaveLater() {
                if (!_uiMode) return;

                if (_saveable.SaveLater()) {
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }

            private readonly string _carSetupId, _weatherId;
            private readonly int? _forceTime;

            internal ViewModel(string serializedPreset, bool uiMode, CarObject carObject = null, string carSkinId = null, string carSetupId = null,
                    TrackObjectBase track = null, TrackSkinObject trackSkin = null, string weatherId = null, int? time = null, bool savePreset = false,
                    Uri mode = null) {
                _uiMode = uiMode;
                _carSetupId = carSetupId;
                _weatherId = weatherId;
                _forceTime = time;

                if (trackSkin != null) {
                    var mainLayout = TracksManager.Instance.GetById(trackSkin.TrackId);
                    mainLayout?.ForceSkinEnabled(trackSkin);
                    track = mainLayout;
                }

                _saveable = new SaveHelper<SaveableData>(KeySaveable, () => new SaveableData {
                    RealConditions = RealConditions,
                    IdealConditions = IdealConditions,
                    RealConditionsLocalWeather = RealConditionsLocalWeather,
                    RealConditionsTimezones = RealConditionsTimezones,
                    RealConditionsManualTime = RealConditionsManualTime,
                    Mode = SelectedMode,
                    ModeData = SelectedModeViewModel?.ToSerializedString(),
                    CarId = SelectedCar?.Id,
                    TrackId = SelectedTrack?.IdWithLayout,
                    WeatherId = SelectedWeather is WeatherTypeWrapped wrapped ? $@"*{((int)wrapped.Type).ToInvariantString()}" : SelectedWeatherObject?.Id,
                    TrackPropertiesData = TrackState.ExportToPresetData(),
                    TrackPropertiesChanged = UserPresetsControl.IsChanged(TrackState.PresetableKey),
                    TrackPropertiesPresetFilename = UserPresetsControl.GetCurrentFilename(TrackState.PresetableKey),
                    Temperature = Temperature,
                    Time = Time,
                    TimeMultipler = TimeMultiplier,
                    WindSpeedMin = WindSpeedMin,
                    WindSpeedMax = WindSpeedMax,
                    WindDirection = WindDirection,
                    RandomWindSpeed = RandomWindSpeed,
                    RandomWindDirection = RandomWindDirection,
                    RandomTemperature = RandomTemperature,
                    RandomTime = RandomTime,
                    CustomRoadTemperature = CustomRoadTemperature,
                    CustomRoadTemperatureValue = _customRoadTemperatureValue
                }, o => {
                    TimeMultiplier = o.TimeMultipler;

                    RealConditions = _weatherId == null && o.RealConditions;
                    IdealConditions = _weatherId == null && o.IdealConditions;
                    RealConditionsTimezones = o.RealConditionsTimezones ?? true;
                    RealConditionsLocalWeather = o.RealConditionsLocalWeather ?? false;
                    RealConditionsManualTime = o.RealConditionsManualTime ?? false;

                    Temperature = o.Temperature;
                    Time = o.Time;
                    WindSpeedMin = o.WindSpeedMin;
                    WindSpeedMax = o.WindSpeedMax;
                    WindDirection = o.WindDirection.RoundToInt();
                    RandomWindSpeed = o.RandomWindSpeed;
                    RandomWindDirection = o.RandomWindDirection;

                    RandomTemperature = o.RandomTemperature;
                    RandomTime = o.RandomTime;
                    CustomRoadTemperature = o.CustomRoadTemperature;
                    _customRoadTemperatureValue = o.CustomRoadTemperatureValue;
                    OnPropertyChanged(nameof(CustomRoadTemperatureValue));

                    if (RealConditions) {
                        UpdateConditions();
                    }

                    try {
                        _skipLoading = o.ModeData != null;
                        if (o.Mode != null && o.Mode.OriginalString.Contains('_')) SelectedMode = o.Mode;
                        if (o.ModeData != null) SelectedModeViewModel?.FromSerializedString(o.ModeData);
                    } finally {
                        _skipLoading = false;
                    }

                    if (o.CarId != null) SelectedCar = CarsManager.Instance.GetById(o.CarId) ?? SelectedCar;
                    if (o.TrackId != null) SelectedTrack = TracksManager.Instance.GetLayoutById(o.TrackId) ?? SelectedTrack;

                    if (_weatherId != null) {
                        SelectedWeather = WeatherManager.Instance.GetById(_weatherId);
                    } else if (o.WeatherId == null) {
                        SelectedWeather = WeatherComboBox.RandomWeather;
                    } else if (o.WeatherId.StartsWith(@"*")) {
                        try {
                            SelectedWeather = new WeatherTypeWrapped((WeatherType)(FlexibleParser.TryParseInt(o.WeatherId.Substring(1)) ?? 0));
                        } catch (Exception e) {
                            Logging.Error(e);
                        }
                    } else {
                        SelectedWeather = WeatherManager.Instance.GetById(o.WeatherId) ?? SelectedWeather;
                    }

                    if (o.TrackPropertiesPresetFilename != null && o.TrackPropertiesData != null) {
                        UserPresetsControl.LoadPreset(TrackState.PresetableKey, o.TrackPropertiesPresetFilename, o.TrackPropertiesData, o.TrackPropertiesChanged);
                    } else if (o.TrackPropertiesPresetFilename != null &&
                            (PresetsManager.Instance.HasBuiltInPreset(TrackStateViewModelBase.PresetableCategory, o.TrackPropertiesPresetFilename) ||
                                    File.Exists(o.TrackPropertiesPresetFilename))) {
                        UserPresetsControl.LoadPreset(TrackState.PresetableKey, o.TrackPropertiesPresetFilename);
                    } else if (o.TrackPropertiesData != null) {
                        UserPresetsControl.LoadSerializedPreset(TrackState.PresetableKey, o.TrackPropertiesData);
                    } else if (o.ObsTrackPropertiesPreset != null) {
                        UserPresetsControl.LoadBuiltInPreset(TrackState.PresetableKey, TrackStateViewModelBase.PresetableCategory, o.ObsTrackPropertiesPreset);
                    }
                }, () => {
                    RealConditions = false;
                    IdealConditions = false;
                    RealConditionsTimezones = true;
                    RealConditionsManualTime = false;
                    RealConditionsLocalWeather = false;

                    SelectedMode = new Uri("/Pages/Drive/QuickDrive_Race.xaml", UriKind.Relative);
                    SelectedCar = CarsManager.Instance.GetDefault();
                    SelectedTrack = TracksManager.Instance.GetDefault();
                    SelectedWeather = WeatherManager.Instance.GetDefault();

                    UserPresetsControl.LoadBuiltInPreset(TrackState.PresetableKey, TrackStateViewModelBase.PresetableCategory, "Green");

                    Temperature = 12.0;
                    Time = 12 * 60 * 60;
                    TimeMultiplier = 1;
                    WindSpeedMin = 10;
                    WindSpeedMax = 10;
                    WindDirection = 0;
                    RandomWindSpeed = false;
                    RandomWindDirection = false;

                    RandomTemperature = false;
                    RandomTime = false;
                    CustomRoadTemperature = false;
                    _customRoadTemperatureValue = null;
                    OnPropertyChanged(nameof(CustomRoadTemperatureValue));
                });

                if (string.IsNullOrEmpty(serializedPreset)) {
                    _saveable.LoadOrReset();
                } else {
                    _saveable.Reset();

                    if (savePreset) {
                        _saveable.FromSerializedString(serializedPreset);
                    } else {
                        _saveable.FromSerializedStringWithoutSaving(serializedPreset);
                    }
                }

                if (WindSpeedMin == 10d && WindSpeedMax == 10d) {
                    FancyHints.DoubleSlider.Trigger();
                }

                if (carObject != null) {
                    SelectedCar = carObject;
                    // TODO: skin?
                }

                if (track != null) {
                    SelectedTrack = track;
                }

                if (mode != null) {
                    SelectedMode = mode;
                }

                UpdateConditions();

                //UpdateHierarchicalWeatherList().Forget();
                //WeakEventManager<IBaseAcObjectObservableCollection, EventArgs>.AddHandler(WeatherManager.Instance.WrappersList,
                //       nameof(IBaseAcObjectObservableCollection.CollectionReady), OnWeatherListUpdated);

                FancyHints.MoreDriveAssists.Trigger(TimeSpan.FromSeconds(1d));

                var stored = Stored.Get("windDirectionInDegrees");
                if (stored.Value != null) {
                    FancyHints.DegressWind.MaskAsUnnecessary();
                } else {
                    FancyHints.DegressWind.Trigger(TimeSpan.FromSeconds(1.5d));
                    stored.SubscribeWeak((o, e) => {
                        if (e.PropertyName == nameof(Stored.StoredValue.Value)) {
                            FancyHints.DegressWind.MaskAsUnnecessary();
                        }
                    });
                }
            }

            #region Presets
            bool IUserPresetable.CanBeSaved => true;
            PresetsCategory IUserPresetable.PresetableCategory => new PresetsCategory(PresetableKeyValue);
            string IUserPresetable.PresetableKey => PresetableKeyValue;

            public string ExportToPresetData() {
                return _saveable.ToSerializedString();
            }

            public event EventHandler Changed;

            public void ImportFromPresetData(string data) {
                _saveable.FromSerializedString(data);
            }
            #endregion

            public AssistsViewModel AssistsViewModel => AssistsViewModel.Instance;

            private ICommand _changeCarCommand;

            public ICommand ChangeCarCommand => _changeCarCommand ?? (_changeCarCommand = new DelegateCommand(() => {
                var dialog = new SelectCarDialog(SelectedCar);
                dialog.ShowDialog();
                if (!dialog.IsResultOk || dialog.SelectedCar == null) return;

                var car = dialog.SelectedCar;
                car.SelectedSkin = dialog.SelectedSkin;

                SelectedCar = car;
                FancyHints.DragForContentSection.Trigger();
            }));

            private ICommand _changeTrackCommand;

            public ICommand ChangeTrackCommand => _changeTrackCommand ?? (_changeTrackCommand = new DelegateCommand(() => {
                // var extra = this.SelectedModeViewModel.GetSpecificTrackSelectionPage();
                SelectedTrack = SelectTrackDialog.Show(SelectedTrack);
            }));

            private DelegateCommand _manageCarSetupsCommand;

            public DelegateCommand ManageCarSetupsCommand => _manageCarSetupsCommand ?? (_manageCarSetupsCommand = new DelegateCommand(() => {
                CarSetupsListPage.Open(SelectedCar);
            }));

            private DelegateCommand _manageCarCommand;

            public DelegateCommand ManageCarCommand => _manageCarCommand ?? (_manageCarCommand = new DelegateCommand(() => {
                CarsListPage.Show(SelectedCar);
            }));

            private DelegateCommand _manageTrackCommand;

            public DelegateCommand ManageTrackCommand => _manageTrackCommand ?? (_manageTrackCommand = new DelegateCommand(() => {
                TracksListPage.Show(SelectedTrack);
            }));

            private QuickDriveModeViewModel _selectedModeViewModel;

            private CommandBase _goCommand;

            public ICommand GoCommand => _goCommand ?? (_goCommand =
                    new AsyncCommand(Go, () => SelectedCar != null && SelectedTrack != null && SelectedModeViewModel != null));

            private enum TrackDoesNotFitRespond {
                Cancel, Go, FixAndGo
            }

            private static TrackDoesNotFitRespond ShowTrackDoesNotFitMessage(string message) {
                var dlg = new ModernDialog {
                    Title = ToolsStrings.Common_Warning,
                    Content = new ScrollViewer {
                        Content = new SelectableBbCodeBlock {
                            BbCode = $"Most likely, track won’t work with selected mode: {message.ToSentenceMember()}. Are you sure you want to continue?",
                            Margin = new Thickness(0, 0, 0, 8)
                        },
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                    },
                    MinHeight = 0,
                    MinWidth = 0,
                    MaxHeight = 480,
                    MaxWidth = 640
                };

                dlg.Buttons = new[] {
                    dlg.YesButton,
                    dlg.CreateCloseDialogButton("Yes, And Fix It", false, false, MessageBoxResult.OK),
                    dlg.NoButton
                };

                dlg.ShowDialog();

                switch (dlg.MessageBoxResult) {
                    case MessageBoxResult.Yes:
                        return TrackDoesNotFitRespond.Go;
                    case MessageBoxResult.OK:
                        return TrackDoesNotFitRespond.FixAndGo;
                    case MessageBoxResult.None:
                    case MessageBoxResult.Cancel:
                    case MessageBoxResult.No:
                        return TrackDoesNotFitRespond.Cancel;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            internal async Task Go() {
                var selectedMode = SelectedModeViewModel;
                if (selectedMode == null) return;

                if (SettingsHolder.Drive.QuickDriveCheckTrack) {
                    var doesNotFit = selectedMode.TrackDoesNotFit;
                    if (doesNotFit != null) {
                        var respond = ShowTrackDoesNotFitMessage(doesNotFit.Item1);
                        if (respond == TrackDoesNotFitRespond.Cancel) return;

                        if (respond == TrackDoesNotFitRespond.FixAndGo) {
                            doesNotFit.Item2(SelectedTrack);
                        }
                    }
                }

                var temperature = RandomTemperature ? GetRandomTemperature() : Temperature;
                var weather = SelectedWeatherObject ?? GetRandomWeather(temperature);
                var roadTemperature = CustomRoadTemperature ? CustomRoadTemperatureValue :
                        Game.ConditionProperties.GetRoadTemperature(Time, Temperature, weather?.TemperatureCoefficient ?? 0.0);

                try {
                    await selectedMode.Drive(new Game.BasicProperties {
                        CarId = SelectedCar.Id,
                        CarSkinId = SelectedCar.SelectedSkin?.Id,
                        CarSetupId = _carSetupId,
                        TrackId = SelectedTrack.Id,
                        TrackConfigurationId = SelectedTrack.LayoutId
                    }, AssistsViewModel.ToGameProperties(), new Game.ConditionProperties {
                        AmbientTemperature = temperature,
                        RoadTemperature = roadTemperature,

                        SunAngle = Game.ConditionProperties.GetSunAngle(_forceTime ?? Time),
                        TimeMultipler = TimeMultiplier,
                        CloudSpeed = 0.2,

                        WeatherName = weather?.Id,

                        WindDirectionDeg = RandomWindDirection ? MathUtils.Random(0, 360) : WindDirection,
                        WindSpeedMin = RandomWindSpeed ? 2 : WindSpeedMin,
                        WindSpeedMax = RandomWindSpeed ? 40 : WindSpeedMax,
                    }, TrackState.ToProperties());
                } finally {
                    _goCommand?.RaiseCanExecuteChanged();
                }
            }

            private ICommand _shareCommand;

            public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

            private async Task Share() {
                var data = ExportToPresetData();
                if (data == null) return;
                await SharingUiHelper.ShareAsync(SharedEntryType.QuickDrivePreset,
                        Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(PresetableKeyValue)), null,
                        data);
            }

            private void OnSelectedUpdated() {
                SelectedModeViewModel?.OnSelectedUpdated(SelectedCar, SelectedTrack);
            }

            [CanBeNull]
            public QuickDriveModeViewModel SelectedModeViewModel {
                get => _selectedModeViewModel;
                set {
                    if (Equals(value, _selectedModeViewModel)) return;
                    if (_selectedModeViewModel != null) {
                        _selectedModeViewModel.Changed -= OnModeModelChanged;
                    }

                    _selectedModeViewModel = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GoCommand));

                    if (_selectedModeViewModel != null) {
                        _selectedModeViewModel.Changed += OnModeModelChanged;
                        OnSelectedUpdated();
                    }
                }
            }

            private void OnModeModelChanged(object sender, EventArgs e) {
                SaveLater();
            }

            internal bool Run() {
                if (GoCommand.CanExecute(null)) {
                    GoCommand.Execute(null);
                    return true;
                }

                return false;
            }

            public void Unload() {}
        }

        public static bool Run(
                CarObject car = null, string carSkinId = null,  string carSetupId = null,
                TrackObjectBase track = null, TrackSkinObject trackSkin = null,
                Uri mode = null) {
            return new ViewModel(string.Empty, false, car, carSkinId, carSetupId, track, trackSkin, mode: mode).Run();
        }

        public static bool RunHotlap(
                CarObject car = null, string carSkinId = null, string carSetupId = null,
                TrackObjectBase track = null, TrackSkinObject trackSkin = null) {
            return Run(car, carSkinId, carSetupId, track, trackSkin, new Uri("/Pages/Drive/QuickDrive_Hotlap.xaml", UriKind.Relative));
        }

        public static async Task<bool> RunAsync(
                CarObject car = null, string carSkinId = null, string carSetupId = null,
                TrackObjectBase track = null, TrackSkinObject trackSkin = null,
                string weatherId = null, int? time = null) {
            var model = new ViewModel(string.Empty, false, car, carSkinId, carSetupId, track, trackSkin, weatherId, time);
            if (!model.GoCommand.CanExecute(null)) return false;
            await model.Go();
            return true;
        }

        public static bool RunPreset(string presetFilename,
                CarObject car = null, string carSkinId = null, string carSetupId = null,
                TrackObjectBase track = null, TrackSkinObject trackSkin = null) {
            return new ViewModel(File.ReadAllText(presetFilename), false, car, carSkinId, carSetupId, track, trackSkin).Run();
        }

        public static bool RunSerializedPreset(string preset) {
            return new ViewModel(preset, false).Run();
        }

        public static void LoadPreset(string presetFilename) {
            UserPresetsControl.LoadPreset(PresetableKeyValue, presetFilename);
            NavigateToPage();
        }

        public static void LoadSerializedPreset(string serializedPreset) {
            if (!UserPresetsControl.LoadSerializedPreset(PresetableKeyValue, serializedPreset)) {
                ValuesStorage.Set(KeySaveable, serializedPreset);
            }

            NavigateToPage();
        }

        public static void NavigateToPage() {
            (Application.Current?.MainWindow as MainWindow)?.NavigateTo(new Uri("/Pages/Drive/QuickDrive.xaml", UriKind.Relative));
        }

        private static CarObject _selectNextCar;
        private static string _selectNextCarSkinId;
        private static TrackObjectBase _selectNextTrack;
        private static TrackSkinObject _selectNextTrackSkin;
        private static WeatherObject _selectNextWeather;
        private static Uri _selectNextMode;

        public static void Show(CarObject car = null, string carSkinId = null,
                TrackObjectBase track = null, TrackSkinObject trackSkin = null,
                Uri mode = null, WeatherObject weather = null) {
            QuickDrive current;
            if (_current != null && _current.TryGetTarget(out current) && current.IsLoaded) {
                var vm = current.Model;
                vm.SelectedCar = car ?? vm.SelectedCar;
                if (vm.SelectedCar != null && carSkinId != null) {
                    vm.SelectedCar.SelectedSkin = vm.SelectedCar.GetSkinById(carSkinId);
                }

                vm.SelectedTrack = track ?? vm.SelectedTrack;

                if (weather != null) {
                    vm.SelectedWeather = weather;
                }
            }

            var mainWindow = Application.Current?.MainWindow as MainWindow;
            if (mainWindow == null) return;

            _selectNextCar = car;
            _selectNextCarSkinId = carSkinId;
            _selectNextTrack = track;
            _selectNextTrackSkin = trackSkin;
            _selectNextWeather = weather;
            _selectNextMode = mode;

            NavigateToPage();
        }

        public static void ShowHotlap(CarObject car = null, string carSkinId = null, TrackObjectBase track = null, TrackSkinObject trackSkin = null) {
            Show(car, carSkinId, track, trackSkin, new Uri("/Pages/Drive/QuickDrive_Hotlap.xaml", UriKind.Relative));
        }

        public static IContentLoader ContentLoader { get; } = new ImmediateContentLoader();

        private void OnCarBlockDrop(object sender, DragEventArgs e) {
            var raceGridEntry = e.Data.GetData(RaceGridEntry.DraggableFormat) as RaceGridEntry;
            var carObject = e.Data.GetData(CarObject.DraggableFormat) as CarObject;

            if (raceGridEntry == null && carObject == null) {
                e.Effects = DragDropEffects.None;
                return;
            }

            Model.SelectedCar = carObject ?? raceGridEntry.Car;
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void OnTrackBlockDrop(object sender, DragEventArgs e) {
            var trackObject = e.Data.GetData(TrackObjectBase.DraggableFormat) as TrackObjectBase;

            if (trackObject == null) {
                e.Effects = DragDropEffects.None;
                return;
            }

            Model.SelectedTrack = trackObject;
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void OnCarContextMenu(object sender, ContextMenuButtonEventArgs e) {
            var menu = new ContextMenu()
                    .AddItem("Change car", Model.ChangeCarCommand)
                    .AddItem("Change car to random", Model.RandomCarCommand, @"Ctrl+Alt+1")
                    .AddItem("Change skin to random", Model.RandomCarSkinCommand, @"Ctrl+Alt+R")
                    .AddItem("Randomize everything", Model.RandomizeCommand, @"Alt+R", iconData: (Geometry)TryFindResource(@"ShuffleIconData"))
                    .AddSeparator()
                    .AddItem("Manage setups", () => {
                        CarSetupsListPage.Open(Model.SelectedCar);
                    })
                    .AddItem("Manage skins", () => {
                        CarSkinsListPage.Open(Model.SelectedCar);
                    })
                    .AddSeparator();
            CarBlock.OnShowroomContextMenu(menu, Model.SelectedCar, Model.SelectedCar.SelectedSkin);
            menu.AddSeparator()
                .AddItem("Open car in Content tab", () => {
                    CarsListPage.Show(Model.SelectedCar, Model.SelectedCar.SelectedSkin?.Id);
                })
                .AddItem(AppStrings.Toolbar_Folder, () => {
                    Model.SelectedCar.ViewInExplorer();
                });
            e.Menu = menu;
        }

        private void OnTrackContextMenu(object sender, ContextMenuButtonEventArgs e) {
            var menu = new ContextMenu()
                    .AddItem("Change track", Model.ChangeTrackCommand)
                    .AddItem("Change track to random", Model.RandomTrackCommand, @"Ctrl+Alt+2")
                    .AddItem("Randomize everything", Model.RandomizeCommand, @"Alt+R", iconData: (Geometry)TryFindResource(@"ShuffleIconData"));

            var track = Model.SelectedTrack.MainTrackObject;
            track.SkinsManager.EnsureLoaded();
            if (track.EnabledOnlySkins.Count > 0) {
                menu.AddSeparator();
                foreach (var skinObject in track.EnabledOnlySkins) {
                    var item = new MenuItem {
                        Header = skinObject.DisplayName.ToTitle(),
                        IsCheckable = true,
                        StaysOpenOnClick = true,
                        ToolTip = skinObject.Description
                    };

                    item.SetBinding(MenuItem.IsCheckedProperty, new Binding {
                        Path = new PropertyPath(nameof(skinObject.IsActive)),
                        Source = skinObject
                    });

                    menu.Items.Add(item);
                }
            }

            menu.AddSeparator()
                    .AddItem("Open track in Content tab", () => {
                        TracksListPage.Show(Model.SelectedTrack);
                    }, isEnabled: AppKeyHolder.IsAllRight)
                    .AddItem(AppStrings.Toolbar_Folder, () => {
                        Model.SelectedTrack.ViewInExplorer();
                    });

            e.Menu = menu;
        }

        private void OnCarBlockClick(object sender, RoutedEventArgs e) {
            if (e.Handled) return;
            Model.ChangeCarCommand.Execute(null);
        }

        private void OnTrackBlockClick(object sender, RoutedEventArgs e) {
            if (e.Handled) return;
            Model.ChangeTrackCommand.Execute(null);
        }

        private void OnConditionsContextMenu(object sender, ContextMenuButtonEventArgs e) {
            if (Model.RealConditions) {
                e.Menu = (TryFindResource(@"RealConditionsContextMenu") as ContextMenu)?
                        .AddSeparator()
                        .AddItem("Set random time", Model.RandomTimeCommand, @"Ctrl+Alt+3")
                        .AddItem("Randomize everything", Model.RandomizeCommand, @"Alt+R", iconData: (Geometry)TryFindResource(@"ShuffleIconData"));
            } else {
                e.Menu = new ContextMenu()
                        .AddItem("Set random time", Model.RandomTimeCommand, @"Ctrl+Alt+3")
                        .AddItem("Set random weather", Model.RandomWeatherCommand, @"Ctrl+Alt+4")
                        .AddItem("Set random temperature", Model.RandomTemperatureCommand, @"Ctrl+Alt+5")
                        .AddItem("Randomize everything", Model.RandomizeCommand, @"Alt+R", iconData: (Geometry)TryFindResource(@"ShuffleIconData"));
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            LeftPanel.Width = 180 + ((ActualWidth - 800) / 2d).Clamp(0, 60);

            var cellWidth = (((ActualWidth - 720) / 440).Saturate() * 93 + 120).Round();
            CarCell.Width = cellWidth;
            TrackCell.Width = cellWidth;
        }

        private void OnShowroomButtonClick(object sender, RoutedEventArgs e) {
            CarBlock.OnShowroomButtonClick(Model.SelectedCar, Model.SelectedCar.SelectedSkin);
        }

        private void OnShowroomContextMenu(object sender, MouseButtonEventArgs e) {
            CarBlock.OnShowroomContextMenu(Model.SelectedCar, Model.SelectedCar.SelectedSkin);
        }
    }
}
