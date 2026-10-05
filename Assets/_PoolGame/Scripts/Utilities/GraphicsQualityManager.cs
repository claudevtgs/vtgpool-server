using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VTG.Pool.Save;
using VTG.Pool.VFX;

namespace VTG.Pool.Presentation
{
    public enum GraphicsTier
    {
        Low,
        Medium,
        High,
        Ultra
    }

    /// <summary>
    /// Low/Medium/High/Ultra graphics profiles (MASTER_PROMPT section 25). Works on a runtime copy of the active
    /// URP asset (the project asset is never modified) and adjusts render scale, MSAA, HDR, shadows,
    /// shadow-casting lamps, post-processing and particle effects. The choice is stored in the player settings.
    /// </summary>
    public sealed class GraphicsQualityManager : MonoBehaviour
    {

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Volume postProcessVolume;
        [SerializeField] private VFXManager vfx;
        [Tooltip("Lamps ordered by importance; the first casts soft shadows on every tier above Low.")]
        [SerializeField] private Light[] tableLamps = Array.Empty<Light>();

        private UniversalRenderPipelineAsset runtimeAsset;
        private RenderPipelineAsset originalAsset;

        public GraphicsTier Tier { get; private set; } = GraphicsTier.High;

        public event Action<GraphicsTier> TierChanged;

        public void Configure(Camera cameraToUse, Volume volume, VFXManager effects, Light[] lamps)
        {
            targetCamera = cameraToUse;
            postProcessVolume = volume;
            vfx = effects;
            tableLamps = lamps;
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            originalAsset = QualitySettings.renderPipeline;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset current)
            {
                runtimeAsset = Instantiate(current);
                runtimeAsset.name = current.name + " (runtime)";
                QualitySettings.renderPipeline = runtimeAsset;
            }

            Apply(ResolveTier(SaveSystem.Settings.graphicsTier), false);
        }

        private void OnDestroy()
        {
            // Restore the project setting so the editor is unchanged after Play mode.
            QualitySettings.renderPipeline = originalAsset;
            if (runtimeAsset != null)
            {
                Destroy(runtimeAsset);
            }
        }

        public void Cycle() => Apply((GraphicsTier)(((int)Tier + 1) % 4));

        /// <summary>Settings value -1 means "choose by platform".</summary>
        public static GraphicsTier ResolveTier(int settingsTier)
        {
            if (settingsTier >= 0)
            {
                return (GraphicsTier)Mathf.Clamp(settingsTier, 0, 3);
            }

            return Application.isMobilePlatform ? GraphicsTier.Medium : GraphicsTier.High;
        }

        public void Apply(GraphicsTier tier) => Apply(tier, true);

        public void Apply(GraphicsTier tier, bool persist)
        {
            Tier = tier;
            if (persist && SaveSystem.Settings.graphicsTier != (int)tier)
            {
                SaveSystem.UpdateSettings(s => s.graphicsTier = (int)tier);
            }

            if (runtimeAsset != null)
            {
                runtimeAsset.renderScale = tier == GraphicsTier.Low ? 0.8f : 1f;
                runtimeAsset.msaaSampleCount = tier switch
                {
                    GraphicsTier.Low => 1,
                    GraphicsTier.Medium => 2,
                    GraphicsTier.High => 4,
                    _ => 8
                };
                runtimeAsset.supportsHDR = tier >= GraphicsTier.Medium;
                runtimeAsset.shadowDistance = tier switch
                {
                    GraphicsTier.Low => 6f,
                    GraphicsTier.Medium => 8f,
                    _ => 10f
                };
                runtimeAsset.shadowCascadeCount = tier >= GraphicsTier.High ? 2 : 1;
            }

            for (int i = 0; i < tableLamps.Length; i++)
            {
                Light lamp = tableLamps[i];
                if (lamp == null)
                {
                    continue;
                }

                bool castsShadow = tier switch
                {
                    GraphicsTier.Low => false,
                    GraphicsTier.Medium => i == 0,
                    GraphicsTier.High => i < 2,
                    _ => true
                };
                lamp.shadows = castsShadow ? (tier >= GraphicsTier.High ? LightShadows.Soft : LightShadows.Hard) : LightShadows.None;
            }

            if (postProcessVolume != null)
            {
                postProcessVolume.enabled = tier != GraphicsTier.Low;
            }

            if (targetCamera != null)
            {
                UniversalAdditionalCameraData data = targetCamera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = tier != GraphicsTier.Low;
                data.antialiasing = tier == GraphicsTier.Low ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            }

            if (vfx != null)
            {
                vfx.EffectsEnabled = tier != GraphicsTier.Low;
            }

            TierChanged?.Invoke(tier);
        }
    }
}
