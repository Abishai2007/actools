﻿using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class Kn5MaterialSimple : Kn5MaterialSimpleBase, IAcDynamicMaterial {
        /// <summary>
        /// Should be set before Kn5MaterialSimple.Initialize()
        /// </summary>
        protected uint Flags;

        private EffectSimpleMaterial.StandartMaterial _material;
        private IRenderableTexture _txDiffuse;

        internal Kn5MaterialSimple([NotNull] Kn5MaterialDescription description) : base(description) { }

        public override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);

            if (Description.Material?.AlphaTested == true) {
                Flags |= EffectSimpleMaterial.AlphaTest;
            }

            _txDiffuse = GetTexture("txDiffuse", contextHolder);
            _material = new EffectSimpleMaterial.StandartMaterial {
                Ambient = Kn5Material.GetPropertyValueAByName("ksAmbient"),
                Diffuse = Kn5Material.GetPropertyValueAByName("ksDiffuse"),
                Specular = Kn5Material.GetPropertyValueAByName("ksSpecular"),
                SpecularExp = Kn5Material.GetPropertyValueAByName("ksSpecularEXP"),
                Emissive = Kn5Material.GetPropertyValueCByName("ksEmissive"),
                Flags = Flags
            };
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.SimpleTransparent && mode != SpecialRenderMode.Simple && mode != SpecialRenderMode.Outline) return false;

            Effect.FxMaterial.Set(_material);
            Effect.FxDiffuseMap.SetResource(_txDiffuse);

            PrepareStates(contextHolder, mode);
            return true;
        }

        public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechStandard.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        void IAcDynamicMaterial.SetEmissiveNext(Vector3 value, float multipler) {
            var material = _material;
            material.Emissive = material.Emissive * (1f - multipler) + value * multipler;
            Effect.FxMaterial.Set(material);
        }

        void IAcDynamicMaterial.SetRadialSpeedBlurNext(float amount) {}
    }
}
