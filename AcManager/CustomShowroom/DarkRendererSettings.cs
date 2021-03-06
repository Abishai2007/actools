﻿using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Render.Kn5SpecificForwardDark.Lights;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SlimDX;

namespace AcManager.CustomShowroom {
    public enum CarAmbientShadowsMode {
        [Description("Attached Shadow")]
        Attached = 0,

        [Description("Simple Occlusion")]
        Occlusion = 1,

        [Description("Blurred Occlusion")]
        Blurred = 2
    }

    public class DarkRendererSettings : INotifyPropertyChanged, IUserPresetable, IDarkLightsDescriptionProvider {
        public static readonly string DefaultKey = "__DarkRendererSettings";
        public static readonly string DefaultPresetableKeyValue = "Custom Showroom";

        [NotNull]
        public DarkKn5ObjectRenderer Renderer { get; }

        public static SettingEntry[] MsaaModes { get; } = {
            new SettingEntry(0, ToolsStrings.Common_Disabled),
            new SettingEntry(2, @"2xMSAA"),
            new SettingEntry(4, @"4xMSAA"),
            new SettingEntry(8, @"8xMSAA"),
        };

        public static SettingEntry[] SsaaModes { get; } = {
            new SettingEntry(1, ToolsStrings.Common_Disabled),
            new SettingEntry(2, @"2x"),
            new SettingEntry(4, @"4x"),
            new SettingEntry(8, @"8x"),
        };

        public static SettingEntry[] SsaaModesExtended { get; } = SsaaModes.Append(new SettingEntry(16, @"16x")).ToArray();

        public static int[] ExtraShadowResolutionsValues { get; } = {
            256, 512, 1024, 2048, 4096, 8192
        };

        public static SettingEntry[] ShadowResolutions { get; } = {
            new SettingEntry(1024, "1024×1024"),
            new SettingEntry(2048, "2048×2048"),
            new SettingEntry(4096, "4096×4096"),
            new SettingEntry(8192, "8192×8192")
        };

        public static SettingEntry[] CubemapReflectionResolutions { get; } = {
            new SettingEntry(256, "256×256"),
            new SettingEntry(512, "512×512"),
            new SettingEntry(1024, "1024×1024"),
            new SettingEntry(2048, "2048×2048")
        };

        public static ToneMappingFn[] ToneMappings { get; } = EnumExtension.GetValues<ToneMappingFn>();

        public static AoType[] AoTypes { get; } = DarkKn5ObjectRenderer.ProductionReadyAo;

        public static CarAmbientShadowsMode[] CarAmbientShadowsModes { get; } = EnumExtension.GetValues<CarAmbientShadowsMode>();

        public static DarkLightType[] LightTypes { get; } = EffectDarkMaterial.EnableAdditionalAreaLights.AsBoolean()
                ? EnumExtension.GetValues<DarkLightType>()
                : EnumExtension.GetValues<DarkLightType>().ApartFrom(DarkLightType.Sphere, DarkLightType.Tube, DarkLightType.LtcTube).ToArray();

        public static void ResetHeavy() {
            if (!ValuesStorage.Contains(DefaultKey)) return;

            try {
                var data = JsonConvert.DeserializeObject<SaveableData>(ValuesStorage.GetString(DefaultKey));
                data.MsaaMode = 0;
                data.SsaaMode = 1;
                data.ShadowMapSize = 2048;
                data.CubemapReflectionMapSize = 1024;
                data.CubemapReflectionFacesPerFrame = 2;
                data.UsePcss = false;
                data.UseSslr = false;
                data.UseAo = false;
                data.UseDof = false;
                data.UseAccumulationDof = false;
                data.FlatMirrorBlurred = false;
                ValuesStorage.Set(DefaultKey, JsonConvert.SerializeObject(data));
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        protected class SaveableData {
            public virtual Color AmbientDownColor { get; set; } = Color.FromRgb(150, 180, 180);
            public virtual Color AmbientUpColor { get; set; } = Color.FromRgb(180, 180, 150);
            public virtual Color BackgroundColor { get; set; } = Color.FromRgb(220, 220, 220);
            public virtual Color LightColor { get; set; } = Color.FromRgb(200, 200, 180);

            public virtual int MsaaMode { get; set; } = 0;
            public virtual int SsaaMode { get; set; } = 1;
            public virtual int ShadowMapSize { get; set; } = 2048;
            public int CubemapReflectionMapSize { get; set; } = 1024;
            public int CubemapReflectionFacesPerFrame { get; set; } = 2;

            public virtual string ShowroomId { get; set; }
            public virtual string ColorGrading { get; set; }

            public virtual ToneMappingFn ToneMapping { get; set; } = ToneMappingFn.Filmic;
            public virtual AoType AoType { get; set; } = AoType.Ssao;
            public CarAmbientShadowsMode CarAmbientShadowsMode { get; set; } = CarAmbientShadowsMode.Attached;

            [JsonProperty("CubemapAmbientValue")]
            public virtual float CubemapAmbient { get; set; } = 0.5f;
            public virtual bool CubemapAmbientWhite { get; set; } = true;
            public virtual bool EnableShadows { get; set; } = true;
            public virtual bool FlatMirror { get; set; }
            public virtual bool FlatMirrorBlurred { get; set; } = true;
            public virtual bool UseBloom { get; set; } = true;
            public virtual bool UseColorGrading { get; set; }
            public virtual bool UseFxaa { get; set; } = true;
            public virtual bool UsePcss { get; set; }
            public virtual bool UseSmaa { get; set; }
            public virtual bool UseAo { get; set; }
            public virtual bool UseSslr { get; set; }
            public virtual bool ReflectionCubemapAtCamera { get; set; } = true;
            public virtual bool ReflectionsWithShadows { get; set; }

            public virtual float AmbientBrightness { get; set; } = 2f;
            public virtual float BackgroundBrightness { get; set; } = 1f;
            public virtual float FlatMirrorReflectiveness { get; set; } = 0.6f;
            public virtual float LightBrightness { get; set; } = 1.5f;
            public virtual float Lightθ { get; set; } = 50f;
            public virtual float Lightφ { get; set; } = 104f;
            public virtual float MaterialsReflectiveness { get; set; } = 1f;
            public virtual float ToneExposure { get; set; } = 1.0f;
            public virtual float ToneGamma { get; set; } = 1f;
            public virtual float ToneWhitePoint { get; set; } = 1.66f;
            public virtual float PcssSceneScale { get; set; } = 0.06f;
            public virtual float PcssLightScale { get; set; } = 2f;
            public virtual float BloomRadiusMultiplier { get; set; } = 1f;
            public virtual float SsaoOpacity { get; set; } = 0.3f;

            public virtual bool UseDof { get; set; } = false;
            public virtual float DofFocusPlane { get; set; } = 1.6f;
            public virtual float DofScale { get; set; } = 1f;
            public virtual bool UseAccumulationDof { get; set; } = false;
            public virtual int AccumulationDofIterations { get; set; } = 100;
            public virtual float AccumulationDofApertureSize { get; set; } = 0.01f;

            [CanBeNull]
            public virtual JObject[] ExtraLights { get; set; }

            [CanBeNull]
            private static byte[] Compress(byte[] data) {
                if (data == null) return null;

                try {
                    using (var memory = new MemoryStream()) {
                        using (var deflate = new DeflateStream(memory, CompressionMode.Compress)) {
                            deflate.Write(data, 0, data.Length);
                        }
                        return memory.ToArray();
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                    return null;
                }
            }

            [CanBeNull]
            private static byte[] Decompress(byte[] data) {
                if (data == null) return null;

                try {
                    using (var memory = new MemoryStream(data)) {
                        return new DeflateStream(memory, CompressionMode.Decompress).ReadAsBytesAndDispose();
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                    return null;
                }
            }

            [JsonIgnore]
            public byte[] ColorGradingData {
                get => ColorGrading == null ? null : Decompress(Convert.FromBase64String(ColorGrading));
                set {
                    var c = Compress(value);
                    ColorGrading = c == null ? null : Convert.ToBase64String(c);
                }
            }
        }

        [CanBeNull]
        private ISaveHelper _saveable;

        protected void SaveLater() {
            if (_saveable?.SaveLater() == true) {
                Changed?.Invoke(this, new EventArgs());
            }
        }

        protected void ResetQuality() {
            LoadQuality(CreateSaveableData());
        }

        protected void ResetScene() {
            LoadScene(CreateSaveableData());
        }

        protected void ResetHdr() {
            LoadHdr(CreateSaveableData());
        }

        protected void ResetExtra() {
            LoadExtra(CreateSaveableData());
        }

        protected virtual SaveableData CreateSaveableData() {
            return new SaveableData();
        }

        protected virtual void Reset(bool saveLater) {
            ResetQuality();
            ResetScene();
            ResetHdr();
            ResetExtra();

            if (saveLater) {
                SaveLater();
            }
        }

        private DelegateCommand _resetQualityCommand;

        public DelegateCommand ResetQualityCommand => _resetQualityCommand ?? (_resetQualityCommand = new DelegateCommand(ResetQuality));

        private DelegateCommand _resetSceneCommand;

        public DelegateCommand ResetSceneCommand => _resetSceneCommand ?? (_resetSceneCommand = new DelegateCommand(ResetScene));

        private DelegateCommand _resetHdrCommand;

        public DelegateCommand ResetHdrCommand => _resetHdrCommand ?? (_resetHdrCommand = new DelegateCommand(ResetHdr));

        private DelegateCommand _resetExtraCommand;

        public DelegateCommand ResetExtraCommand => _resetExtraCommand ?? (_resetExtraCommand = new DelegateCommand(ResetExtra));

        [NotNull]
        protected SaveableData Save([NotNull] SaveableData obj) {
            obj.AmbientDownColor = AmbientDownColor;
            obj.AmbientUpColor = AmbientUpColor;
            obj.BackgroundColor = BackgroundColor;
            obj.LightColor = LightColor;

            obj.MsaaMode = MsaaMode.IntValue ?? -1;
            obj.SsaaMode = SsaaMode.IntValue ?? -1;
            obj.ShadowMapSize = ShadowMapSize.IntValue ?? -1;
            obj.CubemapReflectionMapSize = CubemapReflectionMapSize.IntValue ?? -1;

            obj.ShowroomId = (Showroom as ShowroomObject)?.Id;
            obj.ColorGradingData = Renderer.UseColorGrading ? Renderer.ColorGradingData : null;

            obj.CubemapAmbient = Renderer.CubemapAmbient;
            obj.CubemapAmbientWhite = Renderer.CubemapAmbientWhite;
            obj.CubemapReflectionFacesPerFrame = Renderer.CubemapReflectionFacesPerFrame;
            obj.ReflectionCubemapAtCamera = Renderer.ReflectionCubemapAtCamera;
            obj.ReflectionsWithShadows = Renderer.ReflectionsWithShadows;
            obj.EnableShadows = Renderer.EnableShadows;
            obj.FlatMirror = Renderer.FlatMirror;
            obj.FlatMirrorBlurred = Renderer.FlatMirrorBlurred;
            obj.UseBloom = Renderer.UseBloom;
            obj.UseColorGrading = Renderer.UseColorGrading;
            obj.UseFxaa = Renderer.UseFxaa;
            obj.UsePcss = Renderer.UsePcss;
            obj.UseSmaa = Renderer.UseSmaa;
            obj.UseAo = Renderer.UseAo;
            obj.UseSslr = Renderer.UseSslr;

            obj.ToneMapping = Renderer.ToneMapping;
            obj.AoType = Renderer.AoType;
            obj.CarAmbientShadowsMode = CarAmbientShadowsMode;
            obj.ExtraLights = Renderer.SerializeLights(DarkLightTag.Extra);

            obj.AmbientBrightness = Renderer.AmbientBrightness;
            obj.BackgroundBrightness = Renderer.BackgroundBrightness;
            obj.FlatMirrorReflectiveness = Renderer.FlatMirrorReflectiveness;
            obj.LightBrightness = Renderer.LightBrightness;
            obj.Lightθ = LightθDeg;
            obj.Lightφ = LightφDeg;
            obj.MaterialsReflectiveness = Renderer.MaterialsReflectiveness;
            obj.ToneExposure = Renderer.ToneExposure;
            obj.ToneGamma = Renderer.ToneGamma;
            obj.ToneWhitePoint = Renderer.ToneWhitePoint;
            obj.BloomRadiusMultiplier = Renderer.BloomRadiusMultiplier;

            obj.PcssSceneScale = Renderer.PcssSceneScale;
            obj.PcssLightScale = Renderer.PcssLightScale;
            obj.SsaoOpacity = Renderer.AoOpacity;

            obj.UseDof = Renderer.UseDof;
            obj.DofFocusPlane = Renderer.DofFocusPlane;
            obj.DofScale = Renderer.DofScale;
            obj.UseAccumulationDof = Renderer.UseAccumulationDof;
            obj.AccumulationDofIterations = Renderer.AccumulationDofIterations;
            obj.AccumulationDofApertureSize = Renderer.AccumulationDofApertureSize;
            return obj;
        }

        protected void LoadQuality(SaveableData o) {
            MsaaMode = MsaaModes.GetByIdOrDefault<SettingEntry, int?>(o.MsaaMode);
            SsaaMode = SsaaModesExtended.GetByIdOrDefault<SettingEntry, int?>(o.SsaaMode);
            ShadowMapSize = ShadowResolutions.GetByIdOrDefault<SettingEntry, int?>(o.ShadowMapSize);
            CubemapReflectionMapSize = CubemapReflectionResolutions.GetByIdOrDefault<SettingEntry, int?>(o.CubemapReflectionMapSize);

            Renderer.EnableShadows = o.EnableShadows;
            Renderer.UseBloom = o.UseBloom;
            Renderer.UseFxaa = o.UseFxaa;
            Renderer.UsePcss = o.UsePcss;
            Renderer.UseSmaa = o.UseSmaa;
            Renderer.UseAo = o.UseAo;
            Renderer.UseSslr = o.UseSslr;

            Renderer.AoType = o.AoType;
            Renderer.CubemapReflectionFacesPerFrame = o.CubemapReflectionFacesPerFrame;
        }

        private async Task LoadShowroom(string showroomId) {
            if (showroomId == null) {
                Showroom = null;
            } else {
                var showroom = ShowroomsManager.Instance.GetById(showroomId);
                if (showroom == null) {
                    if (showroomId == "at_previews" && CmPreviewsTools.MissingShowroomHelper != null) {
                        await CmPreviewsTools.MissingShowroomHelper.OfferToInstall("Kunos Previews Showroom (AT Previews Special)", "at_previews",
                                "http://www.assettocorsa.net/assetto-corsa-v1-5-dev-diary-part-33/");
                        Showroom = ShowroomsManager.Instance.GetById(showroomId);
                        if (Showroom != null) return;
                    } else {
                        Showroom = null;
                    }

                    ModernDialog.ShowMessage($"Showroom “{showroomId}” is missing");
                } else {
                    Showroom = showroom;
                }
            }
        }

        protected void LoadScene(SaveableData o) {
            LoadShowroom(o.ShowroomId).Forget();

            AmbientDownColor = o.AmbientDownColor;
            AmbientUpColor = o.AmbientUpColor;
            BackgroundColor = o.BackgroundColor;
            LightColor = o.LightColor;

            Renderer.CubemapAmbient = o.CubemapAmbient;
            Renderer.CubemapAmbientWhite = o.CubemapAmbientWhite;
            Renderer.ReflectionCubemapAtCamera = o.ReflectionCubemapAtCamera;
            Renderer.ReflectionsWithShadows = o.ReflectionsWithShadows;
            Renderer.FlatMirror = o.FlatMirror;
            Renderer.FlatMirrorBlurred = o.FlatMirrorBlurred;

            Renderer.AmbientBrightness = o.AmbientBrightness;
            Renderer.BackgroundBrightness = o.BackgroundBrightness;
            Renderer.FlatMirrorReflectiveness = o.FlatMirrorReflectiveness;
            Renderer.LightBrightness = o.LightBrightness;
            LightθDeg = o.Lightθ;
            LightφDeg = o.Lightφ;

            CarAmbientShadowsMode = o.CarAmbientShadowsMode;
            Renderer.DeserializeLights(DarkLightTag.Extra, o.ExtraLights);
        }

        protected void LoadHdr(SaveableData o) {
            Renderer.ColorGradingData = o.ColorGradingData;

            Renderer.UseColorGrading = o.UseColorGrading;
            Renderer.ToneMapping = o.ToneMapping;

            Renderer.ToneExposure = o.ToneExposure;
            Renderer.ToneGamma = o.ToneGamma;
            Renderer.ToneWhitePoint = o.ToneWhitePoint;
        }

        protected void LoadExtra(SaveableData o) {
            Renderer.MaterialsReflectiveness = o.MaterialsReflectiveness;
            Renderer.BloomRadiusMultiplier = o.BloomRadiusMultiplier;
            Renderer.PcssSceneScale = o.PcssSceneScale;
            Renderer.PcssLightScale = o.PcssLightScale;
            Renderer.AoOpacity = o.SsaoOpacity;

            Renderer.UseDof = o.UseDof;
            Renderer.DofFocusPlane = o.DofFocusPlane;
            Renderer.DofScale = o.DofScale;
            Renderer.UseAccumulationDof = o.UseAccumulationDof;
            Renderer.AccumulationDofIterations = o.AccumulationDofIterations;
            Renderer.AccumulationDofApertureSize = o.AccumulationDofApertureSize;
        }

        protected void Load(SaveableData o) {
            LoadQuality(o);
            LoadScene(o);
            LoadHdr(o);
            LoadExtra(o);
        }

        [NotNull]
        protected virtual ISaveHelper CreateSaveable() {
            return new SaveHelper<SaveableData>(DefaultKey, () => Save(CreateSaveableData()), Load, () => {
                Reset(false);
            });
        }

        internal bool HasSavedData {
            get {
                if (_saveable == null) {
                    _saveable = CreateSaveable();
                }

                return _saveable.HasSavedData;
            }
        }

        /// <summary>
        /// Don’t forget to call it in overrided versions.
        /// </summary>
        public void Initialize(bool reset) {
            // ReSharper disable once VirtualMemberCallInConstructor
            _saveable = CreateSaveable();
            if (reset) {
                _saveable.Reset();
            } else {
                _saveable.LoadOrReset();
            }

            UpdateColors();
            SyncAll();

            ExtraLights = new BetterObservableCollection<DarkLightBase>(Renderer.Lights.Where(x => x.Tag == DarkLightTag.Extra));
            CarLights = new BetterObservableCollection<DarkLightBase>(Renderer.Lights.Where(x => x.Tag == DarkLightTag.Car));
            ShowroomLights = new BetterObservableCollection<DarkLightBase>(Renderer.Lights.Where(x => x.Tag == DarkLightTag.Showroom));

            ExtraLights.CollectionChanged += OnLightsCollectionChanged;
            CarLights.CollectionChanged += OnLightsCollectionChanged;
            ShowroomLights.CollectionChanged += OnLightsCollectionChanged;

            Renderer.PropertyChanged += OnRendererPropertyChanged;
            Renderer.LightPropertyChanged += OnLightPropertyChanged;
        }

        public BetterObservableCollection<DarkLightBase> ExtraLights { get; private set; }

        public BetterObservableCollection<DarkLightBase> CarLights { get; private set; }

        public BetterObservableCollection<DarkLightBase> ShowroomLights { get; private set; }

        private bool _ignore, _delay;

        private async void OnLightsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            if (_delay || _ignore) return;

            try {
                _delay = true;
                await Task.Delay(1);
            } finally {
                _delay = false;
            }

            if (_ignore) return;

            try {
                _ignore = true;

                var updatedList = Renderer.Lights.Where(x => x.Tag == DarkLightTag.Main).ToList();

                foreach (var light in CarLights) {
                    if (updatedList.Contains(light)) continue;
                    light.Tag = DarkLightTag.Car;
                    ExtraLights.Remove(light);
                    ShowroomLights.Remove(light);
                    updatedList.Add(light);
                }

                updatedList.AddRange(Renderer.Lights.Where(x => x.Tag > DarkLightTag.Car && x.Tag < DarkLightTag.Showroom));

                foreach (var light in ShowroomLights) {
                    if (updatedList.Contains(light)) continue;
                    light.Tag = DarkLightTag.Showroom;
                    ExtraLights.Remove(light);
                    updatedList.Add(light);
                }

                updatedList.AddRange(Renderer.Lights.Where(x => x.Tag > DarkLightTag.Showroom && x.Tag < DarkLightTag.Extra));

                foreach (var light in ExtraLights) {
                    if (updatedList.Contains(light)) continue;
                    light.Tag = DarkLightTag.Extra;
                    updatedList.Add(light);
                }

                if (!updatedList.SequenceEqual(Renderer.Lights)) {
                    Renderer.Lights = updatedList.ToArray();
                }
            } finally {
                _ignore = false;
            }
        }

        static DarkRendererSettings() {
            Draggable.RegisterDraggable<DarkLightBase>();
        }

        private void UpdateLights() {
            if (_ignore) return;

            try {
                _ignore = true;
                ExtraLights.ReplaceIfDifferBy(Renderer.Lights.Where(x => x.Tag == DarkLightTag.Extra));
                CarLights.ReplaceIfDifferBy(Renderer.Lights.Where(x => x.Tag == DarkLightTag.Car));
                ShowroomLights.ReplaceIfDifferBy(Renderer.Lights.Where(x => x.Tag == DarkLightTag.Showroom));
            } finally {
                _ignore = false;
            }
        }

        private void OnLightPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (((DarkLightBase)sender).Tag != DarkLightTag.Extra) {
                ActionExtension.InvokeInMainThread(SaveLater);
            }

            if (e.PropertyName == nameof(DarkLightBase.Tag)) {
                UpdateLights();
            }
        }

        public DarkRendererSettings(DarkKn5ObjectRenderer renderer) : this(renderer, DefaultPresetableKeyValue) {
            Initialize(false);
        }

        protected DarkRendererSettings(DarkKn5ObjectRenderer renderer, string presetableKeyValue) {
            _presetableKeyValue = presetableKeyValue;
            Renderer = renderer;
            Renderer.SetLightsDescriptionProvider(this);
            TryToGuessCarLights = ValuesStorage.GetBool(KeyTryToGuessCarLights, true);
        }

        protected virtual void OnRendererPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(Renderer.CubemapAmbient):
                case nameof(Renderer.CubemapAmbientWhite):
                case nameof(Renderer.EnableShadows):
                case nameof(Renderer.FlatMirror):
                case nameof(Renderer.FlatMirrorBlurred):
                case nameof(Renderer.UseBloom):
                case nameof(Renderer.UseColorGrading):
                case nameof(Renderer.UseFxaa):
                case nameof(Renderer.UsePcss):
                case nameof(Renderer.UseSmaa):
                case nameof(Renderer.UseAo):
                case nameof(Renderer.AoType):
                case nameof(Renderer.UseSslr):
                case nameof(Renderer.ToneMapping):
                case nameof(Renderer.ColorGradingData):
                case nameof(Renderer.AmbientBrightness):
                case nameof(Renderer.BackgroundBrightness):
                case nameof(Renderer.FlatMirrorReflectiveness):
                case nameof(Renderer.LightBrightness):
                case nameof(Renderer.MaterialsReflectiveness):
                case nameof(Renderer.ToneExposure):
                case nameof(Renderer.ToneGamma):
                case nameof(Renderer.ToneWhitePoint):
                case nameof(Renderer.ReflectionCubemapAtCamera):
                case nameof(Renderer.ReflectionsWithShadows):
                case nameof(Renderer.BloomRadiusMultiplier):
                case nameof(Renderer.PcssLightScale):
                case nameof(Renderer.PcssSceneScale):
                case nameof(Renderer.AoOpacity):
                case nameof(Renderer.UseDof):
                case nameof(Renderer.DofFocusPlane):
                case nameof(Renderer.DofScale):
                case nameof(Renderer.UseAccumulationDof):
                case nameof(Renderer.AccumulationDofApertureSize):
                case nameof(Renderer.AccumulationDofIterations):
                case nameof(Renderer.CubemapReflectionFacesPerFrame):
                    ActionExtension.InvokeInMainThread(SaveLater);
                    break;

                case nameof(Renderer.Lights):
                    ActionExtension.InvokeInMainThread(() => {
                        UpdateLights();
                        SaveLater();
                    });
                    break;

                case nameof(Renderer.MsaaSampleCount):
                case nameof(Renderer.UseMsaa):
                    ActionExtension.InvokeInMainThread(SyncMsaaMode);
                    break;

                // case nameof(Renderer.ResolutionMultiplier):
                //    SyncSsaaMode();
                //    break;

                case nameof(Renderer.UseSsaa):
                    ActionExtension.InvokeInMainThread(SyncUseSsaa);
                    break;

                case nameof(Renderer.ShadowMapSize):
                    ActionExtension.InvokeInMainThread(SyncShadowMapSize);
                    break;

                case nameof(Renderer.CubemapReflectionMapSize):
                    ActionExtension.InvokeInMainThread(SyncCubemapReflectionsMapSize);
                    break;

                case nameof(Renderer.Light):
                    ActionExtension.InvokeInMainThread(SyncLight);
                    break;

                case nameof(Renderer.UseCorrectAmbientShadows):
                case nameof(Renderer.BlurCorrectAmbientShadows):
                    ActionExtension.InvokeInMainThread(SyncCarAmbientShadowsMode);
                    break;
            }
        }

        private void SyncAll() {
            SyncMsaaMode();
            SyncUseSsaa();
            //SyncSsaaMode();
            SyncShadowMapSize();
            SyncCubemapReflectionsMapSize();
            SyncCarAmbientShadowsMode();
            SyncLight();
        }

        private void SyncMsaaMode() {
            _msaaMode = Renderer.UseMsaa != true ? MsaaModes[0] : MsaaModes.GetByIdOrDefault<SettingEntry, int?>(Renderer.MsaaSampleCount);
            OnPropertyChanged(nameof(MsaaMode));
        }

        protected void SyncUseSsaa() {
            if (!Renderer.UseSsaa) {
                _ssaaMode = SsaaModesExtended[0];
                OnPropertyChanged(nameof(SsaaMode));
            } else if (_ssaaMode == SsaaModesExtended[0]) {
                _ssaaMode = SsaaModesExtended.GetByIdOrDefault<SettingEntry, int?>(Math.Pow(Renderer.ResolutionMultiplier, 2d).RoundToInt());
                OnPropertyChanged(nameof(SsaaMode));
            }
        }

        /*protected void SyncSsaaMode() {
            _ssaaMode = SsaaModesExtended.GetByIdOrDefault<SettingEntry, int?>(Math.Pow(Renderer.ResolutionMultiplier, 2d).RoundToInt());
            OnPropertyChanged(nameof(SsaaMode));
        }*/

        private void SyncShadowMapSize() {
            _shadowMapSize = ShadowResolutions.GetByIdOrDefault<SettingEntry, int?>(Renderer.ShadowMapSize);
            OnPropertyChanged(nameof(ShadowMapSize));
        }

        private void SyncCubemapReflectionsMapSize() {
            _cubemapReflectionMapSize = CubemapReflectionResolutions.GetByIdOrDefault<SettingEntry, int?>(Renderer.CubemapReflectionMapSize);
            OnPropertyChanged(nameof(CubemapReflectionMapSize));
        }

        private void SyncCarAmbientShadowsMode() {
            _carAmbientShadowsMode = Renderer.UseCorrectAmbientShadows ?
                    Renderer.BlurCorrectAmbientShadows ? CarAmbientShadowsMode.Blurred : CarAmbientShadowsMode.Occlusion :
                    CarAmbientShadowsMode.Attached;
            OnPropertyChanged(nameof(CarAmbientShadowsMode));
        }

        private void SyncLight() {
            var v = Renderer.Light;
            _lightθ = v.Y.Acos();
            _lightφ = v.X == 0f && v.Z == 0f ? 0f : MathF.AngleFromXY(v.X, v.Z);
            OnPropertyChanged(nameof(LightθDeg));
            OnPropertyChanged(nameof(LightφDeg));
        }

        private SettingEntry _msaaMode = MsaaModes[0];

        public SettingEntry MsaaMode {
            get => _msaaMode;
            set {
                if (!MsaaModes.Contains(value)) value = MsaaModes[0];
                if (Equals(value, _msaaMode)) return;
                _msaaMode = value;
                OnPropertyChanged();

                Renderer.MsaaSampleCount = value.IntValue > 0 ? value.IntValue ?? 4 : 4;
                Renderer.UseMsaa = value.IntValue > 0;
            }
        }

        private SettingEntry _ssaaMode = SsaaModesExtended[0];

        public SettingEntry SsaaMode {
            get => _ssaaMode;
            set {
                if (!SsaaModesExtended.Contains(value)) value = SsaaModesExtended[0];
                if (Equals(value, _ssaaMode)) return;
                _ssaaMode = value;
                OnPropertyChanged();
                Renderer.ResolutionMultiplier = GetRendererResolutionMultipler();
            }
        }

        protected virtual double GetRendererResolutionMultipler() {
            return Math.Sqrt(SsaaMode.IntValue ?? 1);
        }

        private SettingEntry _shadowMapSize = ShadowResolutions[1];

        public SettingEntry ShadowMapSize {
            get => _shadowMapSize;
            set {
                if (!ShadowResolutions.Contains(value)) value = ShadowResolutions[1];
                if (Equals(value, _shadowMapSize)) return;
                _shadowMapSize = value;
                OnPropertyChanged();

                Renderer.ShadowMapSize = value.IntValue ?? 2048;
            }
        }

        private SettingEntry _cubemapReflectionMapSize;

        public SettingEntry CubemapReflectionMapSize {
            get => _cubemapReflectionMapSize;
            set {
                if (!CubemapReflectionResolutions.Contains(value)) value = CubemapReflectionResolutions[2];
                if (Equals(value, _cubemapReflectionMapSize)) return;
                _cubemapReflectionMapSize = value;
                OnPropertyChanged();

                Renderer.CubemapReflectionMapSize = value.IntValue ?? 1024;
            }
        }

        private CarAmbientShadowsMode _carAmbientShadowsMode;

        public CarAmbientShadowsMode CarAmbientShadowsMode {
            get => _carAmbientShadowsMode;
            set {
                if (Equals(value, _carAmbientShadowsMode)) return;
                _carAmbientShadowsMode = value;
                OnPropertyChanged();

                Renderer.UseCorrectAmbientShadows = value != CarAmbientShadowsMode.Attached;
                Renderer.BlurCorrectAmbientShadows = value == CarAmbientShadowsMode.Blurred;
            }
        }

        protected static Vector3 GetLightDirection(float θdeg, float φdeg) {
            var θ = (90f - θdeg).ToRadians();
            var φ = φdeg.ToRadians();

            var sinθ = θ.Sin();
            var cosθ = θ.Cos();
            var sinφ = φ.Sin();
            var cosφ = φ.Cos();

            return new Vector3(sinθ * cosφ, cosθ, sinθ * sinφ);
        }

        private void SetLight() {
            Renderer.Light = GetLightDirection(LightθDeg, LightφDeg);
        }

        private float _lightθ;

        public float LightθDeg {
            get => 90f - _lightθ.ToDegrees();
            set {
                value = (90f - value).ToRadians();
                if (Equals(value, _lightθ)) return;
                _lightθ = value;
                OnPropertyChanged();
                SetLight();
            }
        }

        private float _lightφ;

        public float LightφDeg {
            get => _lightφ.ToDegrees();
            set {
                value = value.ToRadians();
                if (Equals(value, _lightφ)) return;
                _lightφ = value;
                OnPropertyChanged();
                SetLight();
            }
        }

        private object _showroom = BetterComboBox.NullValue;

        [CanBeNull]
        public object Showroom {
            get => _showroom;
            set {
                value = value ?? BetterComboBox.NullValue;
                if (Equals(value, _showroom)) return;
                _showroom = value;
                OnPropertyChanged();
                Renderer.SetShowroom((value as ShowroomObject)?.Kn5Filename);
            }
        }

        private DelegateCommand _setColorGradingTextureCommand;

        public DelegateCommand SetColorGradingTextureCommand => _setColorGradingTextureCommand ?? (_setColorGradingTextureCommand = new DelegateCommand(() => {
            try {
                var dialog = new OpenFileDialog {
                    Filter = FileDialogFilters.TexturesAllFilter,
                    Title = "Select Color Grading Texture"
                };

                if (dialog.ShowDialog() == true) {
                    Renderer.ColorGradingData = File.ReadAllBytes(dialog.FileName);
                    Renderer.LoadColorGradingData();
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t load color grading texture", "Make sure it’s a volume DDS texture.", e);
            }
        }));

        #region Lights
        private const string KeyTryToGuessCarLights = "__TryToGuessCarLights";
        private bool? _tryToGuessCarLights;

        public bool TryToGuessCarLights {
            get => _tryToGuessCarLights ?? false;
            set {
                if (Equals(value, _tryToGuessCarLights)) return;
                _tryToGuessCarLights = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyTryToGuessCarLights, value);
                Renderer.TryToGuessCarLightsIfMissing = value;
            }
        }

        string IDarkLightsDescriptionProvider.GetFilename(string id) {
            return FilesStorage.Instance.GetContentFilesFiltered($@"{id}.json", ContentCategory.CustomShowroomLights).Select(x => x.Filename).FirstOrDefault();
        }

        private DelegateCommand _addLightCommand;

        public DelegateCommand AddLightCommand => _addLightCommand ?? (_addLightCommand = new DelegateCommand(() => {
            Renderer.AddLight();
        }));

        private DelegateCommand _saveCarLightsCommand;

        public DelegateCommand SaveCarLightsCommand => _saveCarLightsCommand ?? (_saveCarLightsCommand = new DelegateCommand(() => {
            if (Renderer.CarNode == null) return;

            var filename = Path.Combine(Renderer.CarNode.RootDirectory, "ui", "cm_lights.json");
            FileUtils.EnsureFileDirectoryExists(filename);

            var array = new JArray();
            foreach (var light in Renderer.SerializeLights(DarkLightTag.Car)) {
                array.Add(light);
            }

            File.WriteAllText(filename, array.ToString(Formatting.Indented));
        }, () => Renderer.CarNode != null)).ListenOnWeak(Renderer, nameof(Renderer.CarNode));

        private DelegateCommand _saveShowroomLightsCommand;

        public DelegateCommand SaveShowroomLightsCommand => _saveShowroomLightsCommand ?? (_saveShowroomLightsCommand = new DelegateCommand(() => {
            if (Renderer.ShowroomNode == null) return;

            var filename = Path.Combine(Renderer.ShowroomNode.RootDirectory, "ui", "cm_lights.json");
            FileUtils.EnsureFileDirectoryExists(filename);

            var array = new JArray();
            foreach (var light in Renderer.SerializeLights(DarkLightTag.Showroom)) {
                array.Add(light);
            }

            File.WriteAllText(filename, array.ToString(Formatting.Indented));
        }, () => Renderer.ShowroomNode != null)).ListenOnWeak(Renderer, nameof(Renderer.ShowroomNode));

        private DelegateCommand _loadCarLightsCommand;

        public DelegateCommand LoadCarLightsCommand => _loadCarLightsCommand ?? (_loadCarLightsCommand = new DelegateCommand(() => {
            if (Renderer.CarNode == null) return;
            Renderer.LoadObjLights(DarkLightTag.Car, Renderer.CarNode.RootDirectory);
        }, () => Renderer.CarNode != null)).ListenOnWeak(Renderer, nameof(Renderer.CarNode));

        private DelegateCommand _loadShowroomLightsCommand;

        public DelegateCommand LoadShowroomLightsCommand => _loadShowroomLightsCommand ?? (_loadShowroomLightsCommand = new DelegateCommand(() => {
            if (Renderer.ShowroomNode == null) return;
            Renderer.LoadObjLights(DarkLightTag.Showroom, Renderer.ShowroomNode.RootDirectory);
        }, () => Renderer.ShowroomNode != null)).ListenOnWeak(Renderer, nameof(Renderer.ShowroomNode));
        #endregion

        #region Visual params, colors
        private void UpdateColors() {
            if (BackgroundColor != Colors.Transparent) {
                Renderer.BackgroundColor = BackgroundColor.ToColor();
            } else {
                OnPropertyChanged(nameof(BackgroundColor));
            }

            if (LightColor != Colors.Transparent) {
                Renderer.LightColor = LightColor.ToColor();
            } else {
                OnPropertyChanged(nameof(LightColor));
            }

            if (AmbientDownColor != Colors.Transparent) {
                Renderer.AmbientDown = AmbientDownColor.ToColor();
            } else {
                OnPropertyChanged(nameof(AmbientDownColor));
            }

            if (AmbientUpColor != Colors.Transparent) {
                Renderer.AmbientUp = AmbientUpColor.ToColor();
            } else {
                OnPropertyChanged(nameof(AmbientUpColor));
            }
        }

        public Color BackgroundColor {
            get => Renderer.BackgroundColor.ToColor();
            set {
                if (Equals(value, BackgroundColor)) return;
                Renderer.BackgroundColor = value.ToColor();
                OnPropertyChanged();
            }
        }

        public Color LightColor {
            get => Renderer.LightColor.ToColor();
            set {
                if (Equals(value, LightColor)) return;
                Renderer.LightColor = value.ToColor();
                OnPropertyChanged();
            }
        }

        public Color AmbientDownColor {
            get => Renderer.AmbientDown.ToColor();
            set {
                if (Equals(value, AmbientDownColor)) return;
                Renderer.AmbientDown = value.ToColor();
                OnPropertyChanged();
            }
        }

        public Color AmbientUpColor {
            get => Renderer.AmbientUp.ToColor();
            set {
                if (Equals(value, AmbientUpColor)) return;
                Renderer.AmbientUp = value.ToColor();
                OnPropertyChanged();
            }
        }
        #endregion

        #region Presets
        public bool CanBeSaved => true;

        private readonly string _presetableKeyValue;
        public string PresetableKey => _presetableKeyValue;
        PresetsCategory IUserPresetable.PresetableCategory => new PresetsCategory(_presetableKeyValue);

        public string ExportToPresetData() {
            return _saveable?.ToSerializedString();
        }

        public event EventHandler Changed;

        public void ImportFromPresetData(string data) {
            _saveable?.FromSerializedString(data);
        }
        #endregion

        #region Share
        private ICommand _shareCommand;

        public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

        protected virtual async Task Share() {
            var data = ExportToPresetData();
            if (data == null) return;
            await SharingUiHelper.ShareAsync(SharedEntryType.CustomShowroomPreset,
                    Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(_presetableKeyValue)), null,
                    data);
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null, bool save = true) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (save) {
                SaveLater();
            }
        }
    }
}