using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    internal class DecalPreviewPass : ScriptableRenderPass
    {
        private FilteringSettings m_FilteringSettings;
        private List<ShaderTagId> m_ShaderTagIdList;
        private ProfilingSampler m_ProfilingSampler;

        public DecalPreviewPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            ConfigureInput(ScriptableRenderPassInput.Depth); // Require depth

            m_ProfilingSampler = new ProfilingSampler("Decal Preview Render");
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, -1);

            m_ShaderTagIdList = new List<ShaderTagId>();
            m_ShaderTagIdList.Add(new ShaderTagId(DecalShaderPassNames.DecalScreenSpaceMesh));
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
            DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);
            var param = new RendererListParams(renderingData.cullResults, drawingSettings, m_FilteringSettings);
            var rl = context.CreateRendererList(ref param);


            CommandBuffer cmd = renderingData.commandBuffer;
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                cmd.DrawRendererList(rl);
            }
        }
    }
}
