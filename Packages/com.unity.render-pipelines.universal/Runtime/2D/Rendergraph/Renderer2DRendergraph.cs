using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    internal sealed partial class Renderer2D
    {
        private class Attachments
        {
            internal TextureHandle backBufferColor;

            internal TextureHandle colorAttachment;
            internal TextureHandle depthAttachment;

            internal TextureHandle intermediateDepth; // intermediate depth for usage with render texture scale
            internal TextureHandle normalTexture;
            internal TextureHandle[] lightTextures = new TextureHandle[4];

            internal TextureHandle internalColorLut;
            internal TextureHandle afterPostProcessColor;
            internal TextureHandle upscaleTexture;
            internal TextureHandle cameraSortingLayerTexture;

            internal TextureHandle overlayUITexture;
            internal TextureHandle debugScreenTexture;
        }

        private Attachments m_Attachments = new Attachments();
        private RTHandle m_RenderGraphCameraColorHandle;
        private RTHandle m_RenderGraphCameraDepthHandle;
        private RTHandle m_CameraSortingLayerHandle;

        private DrawNormal2DPass m_NormalPass = new DrawNormal2DPass();
        private DrawLight2DPass m_LightPass = new DrawLight2DPass();
        private DrawRenderer2DPass m_RendererPass = new DrawRenderer2DPass();

        bool ppcUpscaleRT = false;

        void CreateResources(RenderGraph renderGraph, ref RenderingData renderingData)
        {
            ref var cameraData = ref renderingData.cameraData;
            ref var cameraTargetDescriptor = ref cameraData.cameraTargetDescriptor;
            var cameraTargetFilterMode = FilterMode.Bilinear;

#if UNITY_EDITOR
            // The scene view camera cannot be uninitialized or skybox when using the 2D renderer.
            if (cameraData.cameraType == CameraType.SceneView)
            {
                renderingData.cameraData.camera.clearFlags = CameraClearFlags.SolidColor;
            }
#endif

            bool forceCreateColorTexture = false;

            // Pixel Perfect Camera doesn't support camera stacking.
            if (cameraData.renderType == CameraRenderType.Base && cameraData.resolveFinalTarget)
            {
                cameraData.camera.TryGetComponent<PixelPerfectCamera>(out var ppc);
                if (ppc != null && ppc.enabled)
                {
                    if (ppc.offscreenRTSize != Vector2Int.zero)
                    {
                        forceCreateColorTexture = true;

                        // Pixel Perfect Camera may request a different RT size than camera VP size.
                        // In that case we need to modify cameraTargetDescriptor here so that all the passes would use the same size.
                        cameraTargetDescriptor.width = ppc.offscreenRTSize.x;
                        cameraTargetDescriptor.height = ppc.offscreenRTSize.y;
                    }

                    cameraTargetFilterMode = FilterMode.Point;
                    ppcUpscaleRT = ppc.gridSnapping == PixelPerfectCamera.GridSnapping.UpscaleRenderTexture || ppc.requiresUpscalePass;

                    if (ppc.requiresUpscalePass)
                    {
                        var upscaleDescriptor = cameraTargetDescriptor;
                        upscaleDescriptor.width = ppc.refResolutionX * ppc.pixelRatio;
                        upscaleDescriptor.height = ppc.refResolutionY * ppc.pixelRatio;
                        upscaleDescriptor.depthBufferBits = 0;

                        m_Attachments.upscaleTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, upscaleDescriptor, "_UpscaleTexture", true, ppc.finalBlitFilterMode);
                    }
                }
            }

            var renderTextureScale = m_Renderer2DData.lightRenderTextureScale;
            var width = (int)(renderingData.cameraData.cameraTargetDescriptor.width * renderTextureScale);
            var height = (int)(renderingData.cameraData.cameraTargetDescriptor.height * renderTextureScale);

            // Intermediate depth desc (size of renderTextureScale)
            {
                var depthDescriptor = cameraTargetDescriptor;
                depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                depthDescriptor.depthBufferBits = k_DepthBufferBits;
                depthDescriptor.width = width;
                depthDescriptor.height = height;
                if (!cameraData.resolveFinalTarget && m_UseDepthStencilBuffer)
                    depthDescriptor.bindMS = depthDescriptor.msaaSamples > 1 && !SystemInfo.supportsMultisampleAutoResolve && (SystemInfo.supportsMultisampledTextures != 0);

                m_Attachments.intermediateDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthDescriptor, "DepthTexture", true);
            }

            // Normal and Light desc
            {
                var desc = new RenderTextureDescriptor(width, height);
                desc.graphicsFormat = cameraTargetDescriptor.graphicsFormat;
                desc.useMipMap = false;
                desc.autoGenerateMips = false;
                desc.depthBufferBits = 0;
                desc.msaaSamples = renderingData.cameraData.cameraTargetDescriptor.msaaSamples;
                desc.dimension = TextureDimension.Tex2D;

                m_Attachments.normalTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_NormalMap", true);

                for (var i = 0; i < m_Attachments.lightTextures.Length; i++)
                {
                    m_Attachments.lightTextures[i] = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, RendererLighting.k_ShapeLightTextureIDs[i], false, FilterMode.Bilinear);
                }
            }

            // Camera Sorting Layer desc
            if (m_Renderer2DData.useCameraSortingLayerTexture)
            {
                var descriptor = cameraTargetDescriptor;
                CopyCameraSortingLayerPass.ConfigureDescriptor(m_Renderer2DData.cameraSortingLayerDownsamplingMethod, ref descriptor, out var filterMode);
                RenderingUtils.ReAllocateIfNeeded(ref m_CameraSortingLayerHandle, descriptor, filterMode, TextureWrapMode.Clamp, name: CopyCameraSortingLayerPass.k_CameraSortingLayerTexture);
                m_Attachments.cameraSortingLayerTexture = renderGraph.ImportTexture(m_CameraSortingLayerHandle);
            }

            // now create the attachments
            if (cameraData.renderType == CameraRenderType.Base) // require intermediate textures
            {
                m_CreateColorTexture = forceCreateColorTexture
                  || cameraData.postProcessEnabled
                  || cameraData.isHdrEnabled
                  || cameraData.isSceneViewCamera
                  || !cameraData.isDefaultViewport
                  || cameraData.requireSrgbConversion
                  || !cameraData.resolveFinalTarget
                  || m_Renderer2DData.useCameraSortingLayerTexture
                  || !Mathf.Approximately(cameraData.renderScale, 1.0f);

                m_CreateDepthTexture = (!cameraData.resolveFinalTarget && m_UseDepthStencilBuffer) || createColorTexture;

                // Camera Target Color
                if (m_CreateColorTexture)
                {
                    cameraTargetDescriptor.useMipMap = false;
                    cameraTargetDescriptor.autoGenerateMips = false;
                    cameraTargetDescriptor.depthBufferBits = (int)DepthBits.None;

                    RenderingUtils.ReAllocateIfNeeded(ref m_RenderGraphCameraColorHandle, cameraTargetDescriptor, cameraTargetFilterMode, TextureWrapMode.Clamp, name: "_CameraTargetAttachment");
                }

                // Camera Target Depth
                if (m_CreateDepthTexture)
                {
                    var depthDescriptor = cameraData.cameraTargetDescriptor;
                    depthDescriptor.useMipMap = false;
                    depthDescriptor.autoGenerateMips = false;
                    if (!cameraData.resolveFinalTarget && m_UseDepthStencilBuffer)
                        depthDescriptor.bindMS = depthDescriptor.msaaSamples > 1 && !SystemInfo.supportsMultisampleAutoResolve && (SystemInfo.supportsMultisampledTextures != 0);

                    depthDescriptor.graphicsFormat = GraphicsFormat.None;
                    depthDescriptor.depthStencilFormat = k_DepthStencilFormat;

                    RenderingUtils.ReAllocateIfNeeded(ref m_RenderGraphCameraDepthHandle, depthDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_CameraDepthAttachment");
                }
            }
            else // Overlay camera
            {
                cameraData.baseCamera.TryGetComponent<UniversalAdditionalCameraData>(out var baseCameraData);
                var baseRenderer = (Renderer2D)baseCameraData.scriptableRenderer;

                m_RenderGraphCameraColorHandle = baseRenderer.m_RenderGraphCameraColorHandle;
                m_RenderGraphCameraDepthHandle = baseRenderer.m_RenderGraphCameraDepthHandle;

                m_CreateColorTexture = baseRenderer.m_CreateColorTexture;
                m_CreateDepthTexture = baseRenderer.m_CreateDepthTexture;
            }

            m_Attachments.colorAttachment = renderGraph.ImportTexture(m_RenderGraphCameraColorHandle);
            m_Attachments.depthAttachment = renderGraph.ImportTexture(m_RenderGraphCameraDepthHandle);

            RenderTargetIdentifier targetId = cameraData.targetTexture != null ? new RenderTargetIdentifier(cameraData.targetTexture) : BuiltinRenderTextureType.CameraTarget;
            m_Attachments.backBufferColor = renderGraph.ImportBackbuffer(targetId);

            var postProcessDesc = PostProcessPass.GetCompatibleDescriptor(cameraTargetDescriptor, cameraTargetDescriptor.width, cameraTargetDescriptor.height, cameraTargetDescriptor.graphicsFormat, DepthBits.None);
            m_Attachments.afterPostProcessColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, postProcessDesc, "_AfterPostProcessTexture", true);
        }

        internal override void OnRecordRenderGraph(RenderGraph renderGraph, ScriptableRenderContext context,  ref RenderingData renderingData)
        {
            CreateResources(renderGraph, ref renderingData);
            SetupRenderGraphCameraProperties(renderGraph, ref renderingData, false);

            OnBeforeRendering(renderGraph);

            OnMainRendering(renderGraph, ref renderingData);

            OnAfterRendering(renderGraph, ref renderingData);
        }

        private void OnBeforeRendering(RenderGraph renderGraph)
        {
            m_LightPass.Setup(renderGraph, ref m_Renderer2DData);
        }

        private void OnMainRendering(RenderGraph renderGraph, ref RenderingData renderingData)
        {
            ref var cameraData = ref renderingData.cameraData;
            RTClearFlags clearFlags = RTClearFlags.None;

            if (cameraData.renderType == CameraRenderType.Base)
                clearFlags = RTClearFlags.All;
            else if (cameraData.clearDepth)
                clearFlags = RTClearFlags.Depth;

            // Color Grading LUT
            bool requiredColorGradingLutPass = cameraData.postProcessEnabled && m_PostProcessPasses.isCreated;

            if (requiredColorGradingLutPass)
                m_PostProcessPasses.colorGradingLutPass.Render(renderGraph, out m_Attachments.internalColorLut, ref renderingData);

            var cameraSortingLayerBoundsIndex = Render2DLightingPass.GetCameraSortingLayerBoundsIndex(m_Renderer2DData);

            RendererLighting.lightBatch.Reset();

            // Main render passes
            var layerBatches = LayerUtility.CalculateBatches(m_Renderer2DData.lightCullResult, out var batchCount);
            for (var i = 0; i < batchCount; i++)
            {
                ref var layerBatch = ref layerBatches[i];

                // Normal Pass
                m_NormalPass.Render(renderGraph, ref renderingData, ref layerBatch, m_Attachments.normalTexture, m_Attachments.intermediateDepth);

                // TODO: replace with clear mrt in light pass
                // Clear Light Textures
                ClearLightTextures(renderGraph, ref m_Renderer2DData, ref layerBatch);

                // Light Pass
                m_LightPass.Render(renderGraph, ref m_Renderer2DData, ref layerBatch, m_Attachments.lightTextures, m_Attachments.normalTexture, m_Attachments.intermediateDepth);

                // Clear camera targets
                if (i == 0 && clearFlags != RTClearFlags.None)
                    ClearTargets2DPass.Render(renderGraph, m_Attachments.colorAttachment, m_Attachments.depthAttachment, clearFlags, renderingData.cameraData.backgroundColor);

                LayerUtility.GetFilterSettings(ref m_Renderer2DData, ref layerBatch, cameraSortingLayerBoundsIndex, out var filterSettings);

                // Default Render Pass
                m_RendererPass.Render(renderGraph, ref renderingData, ref m_Renderer2DData, ref layerBatch, ref filterSettings, m_Attachments.colorAttachment, m_Attachments.depthAttachment, m_Attachments.lightTextures);

                // Camera Sorting Layer Pass
                if (m_Renderer2DData.useCameraSortingLayerTexture)
                {
                    // Split Render Pass if CameraSortingLayer is in the middle of a batch
                    if (cameraSortingLayerBoundsIndex >= layerBatch.layerRange.lowerBound && cameraSortingLayerBoundsIndex < layerBatch.layerRange.upperBound)
                    {
                        m_CopyCameraSortingLayerPass.Render(renderGraph, ref renderingData, m_Attachments.colorAttachment, m_Attachments.cameraSortingLayerTexture);

                        filterSettings.sortingLayerRange = new SortingLayerRange((short)(cameraSortingLayerBoundsIndex + 1), layerBatch.layerRange.upperBound);
                        m_RendererPass.Render(renderGraph, ref renderingData, ref m_Renderer2DData, ref layerBatch, ref filterSettings, m_Attachments.colorAttachment, m_Attachments.depthAttachment, m_Attachments.lightTextures);
                    }
                    else if (cameraSortingLayerBoundsIndex == layerBatch.layerRange.upperBound)
                    {
                        m_CopyCameraSortingLayerPass.Render(renderGraph, ref renderingData, m_Attachments.colorAttachment, m_Attachments.cameraSortingLayerTexture);
                    }
                }

                // Light Volume Pass
                m_LightPass.Render(renderGraph, ref m_Renderer2DData, ref layerBatch, m_Attachments.colorAttachment, m_Attachments.normalTexture, m_Attachments.depthAttachment, true);
            }

            bool shouldRenderUI = cameraData.rendersOverlayUI;
            bool outputToHDR = cameraData.isHDROutputActive;
            if (shouldRenderUI && outputToHDR)
            {
                m_DrawOffscreenUIPass.RenderOffscreen(renderGraph, out m_Attachments.overlayUITexture, ref renderingData);
            }
        }

        private void OnAfterRendering(RenderGraph renderGraph, ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            bool drawGizmos = UniversalRenderPipelineDebugDisplaySettings.Instance.renderingSettings.sceneOverrideMode == DebugSceneOverrideMode.None;

            if (drawGizmos)
                DrawRenderGraphGizmos(renderGraph, m_Attachments.colorAttachment, m_Attachments.depthAttachment, GizmoSubset.PreImageEffects, ref renderingData);

            DebugHandler debugHandler = ScriptableRenderPass.GetActiveDebugHandler(ref renderingData);
            bool resolveToDebugScreen = debugHandler != null && debugHandler.WriteToDebugScreenTexture(ref renderingData.cameraData);
            // Allocate debug screen texture if HDR debug views are enabled.
            if (resolveToDebugScreen)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                HDRDebugViewPass.ConfigureDescriptor(ref descriptor);
                m_Attachments.debugScreenTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_DebugScreenTexture", false);
            }

            bool applyPostProcessing = renderingData.postProcessingEnabled && m_PostProcessPasses.isCreated;

            cameraData.camera.TryGetComponent<PixelPerfectCamera>(out var ppc);
            bool isPixelPerfectCameraEnabled = ppc != null && ppc.enabled && ppc.cropFrame != PixelPerfectCamera.CropFrame.None;
            bool requirePixelPerfectUpscale = isPixelPerfectCameraEnabled && ppc.requiresUpscalePass;

            // When using Upscale Render Texture on a Pixel Perfect Camera, we want all post-processing effects done with a low-res RT,
            // and only upscale the low-res RT to fullscreen when blitting it to camera target. Also, final post processing pass is not run in this case,
            // so FXAA is not supported (you don't want to apply FXAA when everything is intentionally pixelated).
            bool requireFinalPostProcessPass = renderingData.cameraData.resolveFinalTarget && !ppcUpscaleRT && applyPostProcessing && cameraData.antialiasing == AntialiasingMode.FastApproximateAntialiasing;

            bool hasPassesAfterPostProcessing = activeRenderPassQueue.Find(x => x.renderPassEvent == RenderPassEvent.AfterRenderingPostProcessing) != null;
            bool needsColorEncoding = DebugHandler == null || !DebugHandler.HDRDebugViewIsActive(ref cameraData);

            var finalTextureHandle = m_Attachments.colorAttachment;

            if (applyPostProcessing)
            {
                postProcessPass.RenderPostProcessingRenderGraph(renderGraph, in m_Attachments.colorAttachment, in m_Attachments.internalColorLut, m_Attachments.overlayUITexture, in m_Attachments.afterPostProcessColor, ref renderingData, true, resolveToDebugScreen, needsColorEncoding);
                finalTextureHandle = m_Attachments.afterPostProcessColor;
            }

            if (isPixelPerfectCameraEnabled)
            {
                // Do PixelPerfect upscaling when using the Stretch Fill option
                if (requirePixelPerfectUpscale)
                {
                    m_UpscalePass.Render(renderGraph, ref cameraData, ref renderingData, in finalTextureHandle, in m_Attachments.upscaleTexture);
                    finalTextureHandle = m_Attachments.upscaleTexture;
                }

                ClearTargets2DPass.Render(renderGraph, m_Attachments.backBufferColor, TextureHandle.nullHandle, RTClearFlags.Color, Color.black);
            }

            // We need to switch the "final" blit target to debugScreenTexture if HDR debug views are enabled.
            var finalBlitTarget = resolveToDebugScreen ? m_Attachments.debugScreenTexture : m_Attachments.backBufferColor;
            if (requireFinalPostProcessPass)
            {
                postProcessPass.RenderFinalPassRenderGraph(renderGraph, in finalTextureHandle, m_Attachments.overlayUITexture, in finalBlitTarget, ref renderingData, needsColorEncoding);
                finalTextureHandle = finalBlitTarget;
            }
            else
            {
                m_FinalBlitPass.Render(renderGraph, ref renderingData, finalTextureHandle, finalBlitTarget, m_Attachments.overlayUITexture);
                finalTextureHandle = finalBlitTarget;
            }

            // We can explicitely render the overlay UI from URP when HDR output is not enabled.
            // SupportedRenderingFeatures.active.rendersUIOverlay should also be set to true.
            bool shouldRenderUI = renderingData.cameraData.rendersOverlayUI;
            bool outputToHDR = renderingData.cameraData.isHDROutputActive;
            if (shouldRenderUI && !outputToHDR)
                m_DrawOverlayUIPass.RenderOverlay(renderGraph, in finalTextureHandle, ref renderingData);

            // If HDR debug views are enabled, DebugHandler will perform the blit from debugScreenTexture (== finalTextureHandle) to backBufferColor.
            DebugHandler?.Setup(ref renderingData);
            DebugHandler?.Render(renderGraph, ref renderingData, finalTextureHandle, m_Attachments.overlayUITexture, m_Attachments.backBufferColor);

            if (drawGizmos)
                DrawRenderGraphGizmos(renderGraph, m_Attachments.backBufferColor, m_Attachments.depthAttachment, GizmoSubset.PostImageEffects, ref renderingData);
        }

        internal override void OnFinishRenderGraphRendering(ref RenderingData renderingData)
        {
        }

        private void ClearLightTextures(RenderGraph graph, ref Renderer2DData rendererData, ref LayerBatch layerBatch)
        {
            var blendStylesCount = rendererData.lightBlendStyles.Length;
            for (var blendStyleIndex = 0; blendStyleIndex < blendStylesCount; blendStyleIndex++)
            {
                if ((layerBatch.lightStats.blendStylesUsed & (uint)(1 << blendStyleIndex)) == 0)
                    continue;

                Light2DManager.GetGlobalColor(layerBatch.startLayerID, blendStyleIndex, out var color);
                ClearTargets2DPass.Render(graph, m_Attachments.lightTextures[blendStyleIndex], TextureHandle.nullHandle, RTClearFlags.Color, color);
            }
        }

        private void CleanupRenderGraphResources()
        {
            m_RenderGraphCameraColorHandle?.Release();
            m_RenderGraphCameraDepthHandle?.Release();
            m_CameraSortingLayerHandle?.Release();
            m_LightPass.Dispose();
        }
    }

    class ClearTargets2DPass
    {
        static private ProfilingSampler s_ClearProfilingSampler = new ProfilingSampler("Clear Targets");
        private class PassData
        {
            internal RTClearFlags clearFlags;
            internal Color clearColor;
        }

        internal static void Render(RenderGraph graph, in TextureHandle colorHandle, in TextureHandle depthHandle, RTClearFlags clearFlags, Color clearColor)
        {
            Debug.Assert(colorHandle.IsValid(), "Trying to clear an invalid render color target");

            if (clearFlags != RTClearFlags.Color)
                Debug.Assert(depthHandle.IsValid(), "Trying to clear an invalid depth target");

            using (var builder = graph.AddRasterRenderPass<PassData>("Clear Target", out var passData, s_ClearProfilingSampler))
            {
                builder.UseTextureFragment(colorHandle, 0);
                if (depthHandle.IsValid())
                    builder.UseTextureFragmentDepth(depthHandle, IBaseRenderGraphBuilder.AccessFlags.Write);
                passData.clearFlags = clearFlags;
                passData.clearColor = clearColor;

                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.ClearRenderTarget(data.clearFlags, data.clearColor, 1, 0);
                });
            }
        }
    }
}
