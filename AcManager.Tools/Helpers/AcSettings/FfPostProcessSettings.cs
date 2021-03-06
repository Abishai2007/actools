using System;
using System.IO;
using System.Linq;
using AcManager.Tools.Data;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Physics;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.AcSettings {
    public class FfPostProcessSettings : IniPresetableSettings {
        internal FfPostProcessSettings() : base(@"ff_post_process") {
            RescanLuts();
        }

        public SettingEntry[] Types { get; } = {
            new SettingEntry("GAMMA", "Gamma"),
            new SettingEntry("LUT", "LUT")
        };

        private bool _enabled;

        public bool Enabled {
            get { return _enabled; }
            set {
                if (Equals(value, _enabled)) return;
                _enabled = value;
                OnPropertyChanged();
            }
        }

        private SettingEntry _type;

        public SettingEntry Type {
            get { return _type; }
            set {
                if (!Types.Contains(value)) value = Types[0];
                if (Equals(value, _type)) return;
                _type = value;
                OnPropertyChanged();
            }
        }

        private double _gamma;

        public double Gamma {
            get { return _gamma; }
            set {
                if (Equals(value, _gamma)) return;
                _gamma = value;
                OnPropertyChanged();
            }
        }

        private string _lutName;

        public string LutName {
            get { return _lutName; }
            set {
                if (Equals(value, _lutName)) return;
                _lutName = value;
                OnPropertyChanged();
                ReloadLut();
            }
        }

        private readonly Busy _lutLoading = new Busy();

        private void ReloadLut() {
            _lutLoading.DoDelay(() => {
                try {
                    if (string.IsNullOrWhiteSpace(LutName)) {
                        LutGraphData = null;
                    } else {
                        var filename = Path.Combine(FileUtils.GetDocumentsCfgDirectory(), LutName);
                        if (File.Exists(filename)) {
                            var lut = new LutDataFile(filename).Values;
                            lut.TransformSelf(x => new LutPoint(x.X * 100, x.Y * 100));
                            LutGraphData = new GraphData(lut);
                        } else {
                            LutGraphData = null;
                        }
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                    LutGraphData = null;
                }
            }, 50);
        }

        private readonly Busy _rescanning = new Busy();

        private void RescanLuts() {
            _rescanning.DoDelay(() => {
                LutNames = new DirectoryInfo(FileUtils.GetDocumentsCfgDirectory())
                        .GetFiles("*.lut").Select(x => x.Name).ToArray();
            }, 200);
        }

        private string[] _lutNames;

        public string[] LutNames {
            get { return _lutNames; }
            set {
                if (Equals(value, _lutNames)) return;
                _lutNames = value;
                OnPropertyChanged();
            }
        }

        private GraphData _lutGraphData;

        public GraphData LutGraphData {
            get { return _lutGraphData; }
            set {
                if (Equals(value, _lutGraphData)) return;
                _lutGraphData = value;
                OnPropertyChanged();
            }
        }

        protected override void OnFileChanged(string filename) {
            if (filename.EndsWith(".lut", StringComparison.OrdinalIgnoreCase)) {
                RescanLuts();
            }

            if (string.Equals(Path.GetFileName(filename), LutName, StringComparison.OrdinalIgnoreCase)) {
                ReloadLut();
            }

            base.OnFileChanged(filename);
        }

        protected override void LoadFromIni() {
            Enabled = Ini["HEADER"].GetBool("ENABLED", false);
            Type = Ini["HEADER"].GetEntry("TYPE", Types);
            Gamma = Ini["GAMMA"].GetDouble("VALUE", 0.5);
            LutName = Ini["LUT"].GetNonEmpty("CURVE");
        }

        protected override void SetToIni(IniFile ini) {
            ini["HEADER"].Set("ENABLED", Enabled);
            ini["HEADER"].Set("TYPE", Type);
            ini["GAMMA"].Set("VALUE", Gamma);
            ini["LUT"].Set("CURVE", LutName);
        }

        protected override void InvokeChanged() {
            AcSettingsHolder.Controls.CurrentPresetChanged = true;
        }
    }
}