using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
    using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace Roam
{
    /// <summary>
    /// Enhanced depth-based texture effect with animation curve control for precise depth falloff
    /// </summary>
    public class DepthTexture : ScriptableRendererFeature
    {
        DepthTextureRenderPass pass;

        public override void Create()
        {
            pass = new DepthTextureRenderPass();
            name = "Depth Texture";
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var settings = VolumeManager.instance.stack.GetComponent<DepthTextureSettings>();

            if (settings != null && settings.IsActive())
            {
                pass.ConfigureInput(ScriptableRenderPassInput.Depth);
                renderer.EnqueuePass(pass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            pass.Dispose();
            base.Dispose(disposing);
        }

        class DepthTextureRenderPass : ScriptableRenderPass
        {
            private Material material;
            private RTHandle tempTexHandle;
            private Texture2D curveLUT;
            private int lastLUTResolution = 0;

            public DepthTextureRenderPass()
            {
                profilingSampler = new ProfilingSampler("Depth Texture");

#if UNITY_6000_0_OR_NEWER
                requiresIntermediateTexture = true;
#endif
            }

            private void CreateMaterial()
            {
                var shader = Shader.Find("Roam/DepthTexture");

                if (shader == null)
                {
                    Debug.LogError("Cannot find shader: \"Roam/DepthTexture\".");
                    return;
                }

                material = new Material(shader);
            }

            /// <summary>
            /// Convert AnimationCurve to 1D texture lookup table for shader sampling
            /// </summary>
            private void UpdateCurveLUT(AnimationCurve curve, int resolution)
            {
                if (curveLUT == null || lastLUTResolution != resolution)
                {
                    if (curveLUT != null)
                        Object.DestroyImmediate(curveLUT);

                    curveLUT = new Texture2D(resolution, 1, TextureFormat.RFloat, false, true)
                    {
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear
                    };
                    lastLUTResolution = resolution;
                }

                for (int i = 0; i < resolution; i++)
                {
                    float t = i / (float)(resolution - 1);
                    float value = Mathf.Clamp01(curve.Evaluate(t));
                    curveLUT.SetPixel(i, 0, new Color(value, value, value, 1));
                }

                curveLUT.Apply();
            }

            private static RenderTextureDescriptor GetCopyPassDescriptor(RenderTextureDescriptor descriptor)
            {
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = (int)DepthBits.None;
                return descriptor;
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                ResetTarget();

                var descriptor = GetCopyPassDescriptor(cameraTextureDescriptor);
                RenderingUtils.ReAllocateIfNeeded(ref tempTexHandle, descriptor);

                base.Configure(cmd, cameraTextureDescriptor);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (renderingData.cameraData.isPreviewCamera)
                    return;

                if (material == null)
                    CreateMaterial();

                var settings = VolumeManager.instance.stack.GetComponent<DepthTextureSettings>();
                renderPassEvent = settings.renderPassEvent.value;

                // Update curve LUT
                UpdateCurveLUT(settings.depthCurve.value, settings.curveLUTResolution.value);

                CommandBuffer cmd = CommandBufferPool.Get();

                // Set shader properties
                material.SetColor("_NearColor", settings.nearColor.value);
                material.SetColor("_FarColor", settings.farColor.value);
                material.SetTexture("_DepthCurveLUT", curveLUT);
                material.SetFloat("_ShadowInfluence", settings.shadowInfluence.value);
                material.SetFloat("_NearDistance", settings.nearDistance.value);
                material.SetFloat("_FarDistance", settings.farDistance.value);

                RTHandle cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

                using (new ProfilingScope(cmd, profilingSampler))
                {
                    Blit(cmd, cameraTargetHandle, tempTexHandle);
                    Blit(cmd, tempTexHandle, cameraTargetHandle, material, 0);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                tempTexHandle?.Release();
                if (curveLUT != null)
                    Object.DestroyImmediate(curveLUT);
            }

#if UNITY_6000_0_OR_NEWER

            private class CopyPassData
            {
                public TextureHandle inputTexture;
            }

            private class MainPassData
            {
                public Material material;
                public TextureHandle inputTexture;
            }

            private static void ExecuteCopyPass(RasterCommandBuffer cmd, RTHandle source)
            {
                Blitter.BlitTexture(cmd, source, new Vector4(1, 1, 0, 0), 0.0f, false);
            }

            private static void ExecuteMainPass(RasterCommandBuffer cmd, RTHandle source, Material material)
            {
                Blitter.BlitTexture(cmd, source, new Vector4(1, 1, 0, 0), material, 0);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if(material == null)
                    CreateMaterial();

                var settings = VolumeManager.instance.stack.GetComponent<DepthTextureSettings>();
                renderPassEvent = settings.renderPassEvent.value;

                // Update curve LUT
                UpdateCurveLUT(settings.depthCurve.value, settings.curveLUTResolution.value);

                // Set shader properties
                material.SetColor("_NearColor", settings.nearColor.value);
                material.SetColor("_FarColor", settings.farColor.value);
                material.SetTexture("_DepthCurveLUT", curveLUT);
                material.SetFloat("_ShadowInfluence", settings.shadowInfluence.value);
                material.SetFloat("_NearDistance", settings.nearDistance.value);
                material.SetFloat("_FarDistance", settings.farDistance.value);

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                UniversalRenderer renderer = (UniversalRenderer)cameraData.renderer;
                var colorCopyDescriptor = GetCopyPassDescriptor(cameraData.cameraTargetDescriptor);
                TextureHandle copiedColor = TextureHandle.nullHandle;

                copiedColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, colorCopyDescriptor, "_DepthTextureColorCopy", false);

                using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("DepthTexture_CopyColor", out var passData, profilingSampler))
                {
                    passData.inputTexture = resourceData.activeColorTexture;
                    builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(copiedColor, 0, AccessFlags.Write);
                    builder.SetRenderFunc((CopyPassData data, RasterGraphContext context) => ExecuteCopyPass(context.cmd, data.inputTexture));
                }

                using (var builder = renderGraph.AddRasterRenderPass<MainPassData>("DepthTexture_MainPass", out var passData, profilingSampler))
                {
                    passData.material = material;
                    passData.inputTexture = copiedColor;
                    builder.UseTexture(copiedColor, AccessFlags.Read);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderFunc((MainPassData data, RasterGraphContext context) => ExecuteMainPass(context.cmd, data.inputTexture, data.material));
                }
            }

#endif
        }
    }
}
