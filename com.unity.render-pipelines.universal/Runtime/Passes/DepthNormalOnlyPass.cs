using System;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal.Internal
{
    public class DepthNormalOnlyPass : ScriptableRenderPass
    {
        internal RenderTextureDescriptor normalDescriptor { get; set; }
        internal RenderTextureDescriptor depthDescriptor { get; set; }
        internal RenderTextureDescriptor renderingLayersDescriptor { get; set; }
        internal bool allocateDepth { get; set; } = true;
        internal bool allocateNormal { get; set; } = true;
        internal bool enableRenderingLayers { get; set; } = false;
        internal List<ShaderTagId> shaderTagIds { get; set; }

        private RenderTargetHandle depthHandle { get; set; }
        private RenderTargetHandle normalHandle { get; set; }
        private RenderTargetHandle renderingLayerslHandle { get; set; }
        private FilteringSettings m_FilteringSettings;
        private int m_RendererMSAASamples = 1;

        // Constants
        private const int k_DepthBufferBits = 32;
        private static readonly List<ShaderTagId> k_DepthNormals = new List<ShaderTagId> { new ShaderTagId("DepthNormals"), new ShaderTagId("DepthNormalsOnly") };
        private static readonly RenderTargetIdentifier[] k_ColorAttachment1 = new RenderTargetIdentifier[1];
        private static readonly RenderTargetIdentifier[] k_ColorAttachment2 = new RenderTargetIdentifier[2];

        /// <summary>
        /// Create the DepthNormalOnlyPass
        /// </summary>
        public DepthNormalOnlyPass(RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask)
        {
            base.profilingSampler = new ProfilingSampler(nameof(DepthNormalOnlyPass));
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            renderPassEvent = evt;
            useNativeRenderPass = false;
        }

        /// <summary>
        /// Configure the pass
        /// </summary>
        public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthHandle, RenderTargetHandle normalHandle)
        {
            // Find compatible render-target format for storing normals.
            // Shader code outputs normals in signed format to be compatible with deferred gbuffer layout.
            // Deferred gbuffer format is signed so that normals can be blended for terrain geometry.
            GraphicsFormat normalsFormat;
            if (RenderingUtils.SupportsGraphicsFormat(GraphicsFormat.R8G8B8A8_SNorm, FormatUsage.Render))
                normalsFormat = GraphicsFormat.R8G8B8A8_SNorm; // Preferred format
            else if (RenderingUtils.SupportsGraphicsFormat(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.Render))
                normalsFormat = GraphicsFormat.R16G16B16A16_SFloat; // fallback
            else
                normalsFormat = GraphicsFormat.R32G32B32A32_SFloat; // fallback

            this.depthHandle = depthHandle;

            m_RendererMSAASamples = baseDescriptor.msaaSamples;

            baseDescriptor.colorFormat = RenderTextureFormat.Depth;
            baseDescriptor.depthBufferBits = k_DepthBufferBits;

            // Never have MSAA on this depth texture. When doing MSAA depth priming this is the texture that is resolved to and used for post-processing.
            baseDescriptor.msaaSamples = 1;// Depth-Only pass don't use MSAA

            depthDescriptor = baseDescriptor;

            this.normalHandle = normalHandle;
            baseDescriptor.graphicsFormat = normalsFormat;
            baseDescriptor.depthBufferBits = 0;
            normalDescriptor = baseDescriptor;

            this.allocateDepth = true;
            this.allocateNormal = true;
            this.shaderTagIds = k_DepthNormals;
        }

        /// <summary>
        /// Configure the pass
        /// </summary>
        public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthHandle,
            RenderTargetHandle normalHandle, RenderTargetHandle decalLayerHandle)
        {
            this.renderingLayerslHandle = decalLayerHandle;
            baseDescriptor.graphicsFormat = GraphicsFormat.R16_UNorm;
            baseDescriptor.depthBufferBits = 0;
            renderingLayersDescriptor = baseDescriptor;

            this.enableRenderingLayers = true;

            Setup(baseDescriptor, depthHandle, normalHandle);
        }


        /// <inheritdoc/>
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            int colorAttachmentCount = 1;

            if (this.allocateNormal)
            {
                RenderTextureDescriptor desc = normalDescriptor;
                desc.msaaSamples = renderingData.cameraData.renderer.useDepthPriming ? m_RendererMSAASamples : 1;
                cmd.GetTemporaryRT(normalHandle.id, desc, FilterMode.Point);
            }
            if (this.enableRenderingLayers)
            {
                RenderTextureDescriptor desc = renderingLayersDescriptor;
                desc.msaaSamples = renderingData.cameraData.renderer.useDepthPriming ? m_RendererMSAASamples : 1;
                cmd.GetTemporaryRT(renderingLayerslHandle.id, desc, FilterMode.Point);
                colorAttachmentCount++;
            }
            if (this.allocateDepth)
                cmd.GetTemporaryRT(depthHandle.id, depthDescriptor, FilterMode.Point);

            var colorBufferIntendifiers = colorAttachmentCount == 1 ? k_ColorAttachment1 : k_ColorAttachment2;
            colorBufferIntendifiers[0] = new RenderTargetIdentifier(normalHandle.Identifier(), 0, CubemapFace.Unknown, -1);
            if (colorAttachmentCount > 1)
                colorBufferIntendifiers[1] = new RenderTargetIdentifier(renderingLayerslHandle.Identifier(), 0, CubemapFace.Unknown, -1);

            if (renderingData.cameraData.renderer.useDepthPriming && (renderingData.cameraData.renderType == CameraRenderType.Base || renderingData.cameraData.clearDepth))
            {
                ConfigureTarget(
                    colorBufferIntendifiers,
                    new RenderTargetIdentifier(renderingData.cameraData.renderer.cameraDepthTarget, 0, CubemapFace.Unknown, -1)
                );
            }
            else
            {
                ConfigureTarget(
                    colorBufferIntendifiers,
                    new RenderTargetIdentifier(depthHandle.Identifier(), 0, CubemapFace.Unknown, -1)
                );
            }

            ConfigureClear(ClearFlag.All, Color.black);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // NOTE: Do NOT mix ProfilingScope with named CommandBuffers i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.DepthNormalPrepass)))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CoreUtils.SetKeyword(cmd, "_DECAL_LAYERS", this.enableRenderingLayers);



                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(this.shaderTagIds, ref renderingData, sortFlags);
                drawSettings.perObjectData = PerObjectData.None;

                ref CameraData cameraData = ref renderingData.cameraData;
                Camera camera = cameraData.camera;

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);

                // todo check if add normal here too
                if (this.enableRenderingLayers)
                    cmd.SetGlobalTexture("_CameraDecalLayersTexture", renderingLayerslHandle.id);

            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
            {
                throw new ArgumentNullException("cmd");
            }

            if (depthHandle != RenderTargetHandle.CameraTarget)
            {
                if (this.allocateNormal)
                    cmd.ReleaseTemporaryRT(normalHandle.id);
                if (this.enableRenderingLayers)
                    cmd.ReleaseTemporaryRT(renderingLayerslHandle.id);
                if (this.allocateDepth)
                    cmd.ReleaseTemporaryRT(depthHandle.id);
                normalHandle = RenderTargetHandle.CameraTarget;
                renderingLayerslHandle = RenderTargetHandle.CameraTarget;
                depthHandle = RenderTargetHandle.CameraTarget;
            }
        }
    }
}
