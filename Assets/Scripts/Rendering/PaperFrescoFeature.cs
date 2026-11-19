using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aloysius.SunCourt
{
    /// <summary>
    /// URP 14 renderer feature. A camera must explicitly carry PaperFrescoCamera;
    /// other scene, preview, reflection, and game cameras are unaffected.
    /// </summary>
    public sealed class PaperFrescoFeature : ScriptableRendererFeature
    {
        public Shader postShader;

        Material material;
        PaperPass pass;

        public override void Create()
        {
            pass?.Dispose();
            pass = new PaperPass();
            EnsureMaterial();
        }

        bool EnsureMaterial()
        {
            if (postShader == null)
                return false;
            if (material != null && material.shader == postShader)
                return true;
            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(postShader);
            return material != null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (camera == null || camera.cameraType != CameraType.Game ||
                renderingData.cameraData.renderType != CameraRenderType.Base)
                return;
            if (!camera.TryGetComponent(out PaperFrescoCamera settings) ||
                !settings.isActiveAndEnabled || settings.effectStrength <= 0f || !EnsureMaterial())
                return;

            if (pass == null)
                pass = new PaperPass();
            pass.Setup(material, settings);
            // Color also forces a sampleable intermediate camera color target in URP 14.
            pass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            pass = null;
            CoreUtils.Destroy(material);
            material = null;
        }

        sealed class PaperPass : ScriptableRenderPass
        {
            readonly ProfilingSampler profiler = new ProfilingSampler("Sun Court paper and ink");
            Material effect;
            PaperFrescoCamera settings;
            RTHandle colorCopy;

            static readonly int EffectStrength = Shader.PropertyToID("_EffectStrength");
            static readonly int InkStrength = Shader.PropertyToID("_InkStrength");
            static readonly int LineWidth = Shader.PropertyToID("_LineWidth");
            static readonly int DepthThreshold = Shader.PropertyToID("_DepthThreshold");
            static readonly int InkColor = Shader.PropertyToID("_InkColor");
            static readonly int PaperStrength = Shader.PropertyToID("_PaperStrength");
            static readonly int PaperScale = Shader.PropertyToID("_PaperScale");
            static readonly int PaletteCompression = Shader.PropertyToID("_PaletteCompression");
            static readonly int Warmth = Shader.PropertyToID("_Warmth");

            public PaperPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            }

            public void Setup(Material sourceMaterial, PaperFrescoCamera cameraSettings)
            {
                effect = sourceMaterial;
                settings = cameraSettings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;
                descriptor.bindMS = false;
                RenderingUtils.ReAllocateIfNeeded(ref colorCopy, descriptor, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: "_SunCourtPaperColorCopy");
                // The pass uses explicit targets for its copy and composite operations.
                ResetTarget();
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (effect == null || settings == null || colorCopy == null)
                    return;

                effect.SetFloat(EffectStrength, settings.effectStrength);
                effect.SetFloat(InkStrength, settings.inkStrength);
                effect.SetFloat(LineWidth, settings.lineWidth);
                effect.SetFloat(DepthThreshold, settings.depthThreshold);
                effect.SetColor(InkColor, settings.inkColor);
                effect.SetFloat(PaperStrength, settings.paperStrength);
                effect.SetFloat(PaperScale, settings.paperScale);
                effect.SetFloat(PaletteCompression, settings.paletteCompression);
                effect.SetFloat(Warmth, settings.warmth);

                RTHandle cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profiler))
                {
                    // Read and write targets must differ. Sampling the active camera
                    // target while drawing back into it causes undefined feedback.
                    Blitter.BlitCameraTexture(cmd, cameraColor, colorCopy, 0f, true);
                    Blitter.BlitCameraTexture(cmd, colorCopy, cameraColor, effect, 0);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                colorCopy?.Release();
                colorCopy = null;
            }
        }
    }
}
