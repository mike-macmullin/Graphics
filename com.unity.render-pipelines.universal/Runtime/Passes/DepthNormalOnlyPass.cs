using System;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal.Internal
{
    public class DepthNormalOnlyPass : ScriptableRenderPass
    {
        internal List<ShaderTagId> shaderTagIds { get; set; }

        private RTHandle depthHandle { get; set; }
        private RTHandle normalHandle { get; set; }
        private RTHandle renderingLayersHandle { get; set; }
        internal bool enableRenderingLayers { get; set; } = false;
        private FilteringSettings m_FilteringSettings;

        // Constants
        private static readonly List<ShaderTagId> k_DepthNormals = new List<ShaderTagId> { new ShaderTagId("DepthNormals"), new ShaderTagId("DepthNormalsOnly") };
        private static readonly RTHandle[] k_ColorAttachment1 = new RTHandle[1];
        private static readonly RTHandle[] k_ColorAttachment2 = new RTHandle[2];

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

        public static GraphicsFormat GetGraphicsFormat()
        {

            if (RenderingUtils.SupportsGraphicsFormat(GraphicsFormat.R8G8B8A8_SNorm, FormatUsage.Render))
                return GraphicsFormat.R8G8B8A8_SNorm; // Preferred format
            else if (RenderingUtils.SupportsGraphicsFormat(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.Render))
                return GraphicsFormat.R16G16B16A16_SFloat; // fallback
            else
                return GraphicsFormat.R32G32B32A32_SFloat; // fallback
        }

        /// <summary>
        /// Configure the pass
        /// </summary>
        public void Setup(RTHandle depthHandle, RTHandle normalHandle)
        {
            this.depthHandle = depthHandle;
            this.normalHandle = normalHandle;
            this.shaderTagIds = k_DepthNormals;
        }

        /// <summary>
        /// Configure the pass
        /// </summary>
        public void Setup(RTHandle depthHandle, RTHandle normalHandle, RTHandle decalLayerHandle)
        {
            this.renderingLayersHandle = decalLayerHandle;
            this.enableRenderingLayers = true;

            Setup(depthHandle, normalHandle);
        }


        /// <inheritdoc/>
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RTHandle[] colorHandles;
            if (this.enableRenderingLayers)
            {
                k_ColorAttachment2[0] = normalHandle;
                k_ColorAttachment2[1] = renderingLayersHandle;
                colorHandles = k_ColorAttachment2;
            }
            else
            {
                k_ColorAttachment1[0] = normalHandle;
                colorHandles = k_ColorAttachment1;
            }


            if (renderingData.cameraData.renderer.useDepthPriming && (renderingData.cameraData.renderType == CameraRenderType.Base || renderingData.cameraData.clearDepth))
                ConfigureTarget(colorHandles, renderingData.cameraData.renderer.cameraDepthTargetHandle);
            else
                ConfigureTarget(colorHandles, depthHandle);

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
                    cmd.SetGlobalTexture("_CameraDecalLayersTexture", renderingLayersHandle);

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
            normalHandle = null;
            depthHandle = null;
            renderingLayersHandle = null;
        }
    }
}
