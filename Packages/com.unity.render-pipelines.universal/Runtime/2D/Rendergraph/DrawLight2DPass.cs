using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal class DrawLight2DPass : ScriptableRenderPass
    {
        static readonly string k_LightPass = "Light2D Pass";
        static readonly string k_LightVolumetricPass = "Light2D Volumetric Pass";

        private static readonly ProfilingSampler m_ProfilingSampler = new ProfilingSampler(k_LightPass);
        private static readonly ProfilingSampler m_ProfilingSamplerVolume = new ProfilingSampler(k_LightVolumetricPass);
        internal static readonly int k_InverseHDREmulationScaleID = Shader.PropertyToID("_InverseHDREmulationScale");
        internal static readonly string k_NormalMapID = "_NormalMap";
        internal static readonly string k_ShadowMapID = "_ShadowTex";
        internal static readonly string k_LightLookupID = "_LightLookup";
        internal static readonly string k_FalloffLookupID = "_FalloffLookup";

        TextureHandle[] intermediateTexture = new TextureHandle[1];
        internal static RTHandle m_FallOffRTHandle = null;
        internal static RTHandle m_LightLookupRTHandle = null;
        private int lightLookupInstanceID;
        private int fallOffLookupInstanceID;

        internal static MaterialPropertyBlock s_PropertyBlock = new MaterialPropertyBlock();

        public void Setup(RenderGraph renderGraph, ref Renderer2DData rendererData)
        {
            // Reallocate external texture if needed
            var fallOffLookupTexture = Light2DLookupTexture.GetFallOffLookupTexture();
            if (fallOffLookupInstanceID != fallOffLookupTexture.GetInstanceID())
            {
                m_FallOffRTHandle = RTHandles.Alloc(fallOffLookupTexture);
                fallOffLookupInstanceID = fallOffLookupTexture.GetInstanceID();
            }

            var lightLookupTexture = Light2DLookupTexture.GetLightLookupTexture();
            if (lightLookupInstanceID != lightLookupTexture.GetInstanceID())
            {
                m_LightLookupRTHandle = RTHandles.Alloc(lightLookupTexture);
                lightLookupInstanceID = lightLookupTexture.GetInstanceID();
            }

            foreach (var light in rendererData.lightCullResult.visibleLights)
            {
                if (light.useCookieSprite && light.m_CookieSpriteTexture != null)
                    light.m_CookieSpriteTextureHandle = renderGraph.ImportTexture(light.m_CookieSpriteTexture);
            }
        }

        public void Dispose()
        {
            m_FallOffRTHandle?.Release();
            m_LightLookupRTHandle?.Release();
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            throw new NotImplementedException();
        }

        private static void Execute(RasterCommandBuffer cmd, PassData passData, ref LayerBatch layerBatch)
        {
            cmd.SetGlobalFloat(k_InverseHDREmulationScaleID, 1.0f / passData.rendererData.hdrEmulationScale);

            for (var i = 0; i < layerBatch.activeBlendStylesIndices.Length; ++i)
            {
                var blendStyleIndex = layerBatch.activeBlendStylesIndices[i];
                var blendOpName = passData.rendererData.lightBlendStyles[blendStyleIndex].name;
                cmd.BeginSample(blendOpName);

                if (!passData.isVolumetric)
                    RendererLighting.EnableBlendStyle(cmd, i, true);

                var lights = passData.layerBatch.lights;

                for (int j = 0; j < lights.Count; ++j)
                {
                    var light = lights[j];

                    // Check if light is valid
                    if (light == null ||
                        light.lightType == Light2D.LightType.Global ||
                        light.blendStyleIndex != blendStyleIndex)
                        continue;

                    // Check if light is volumetric
                    if (passData.isVolumetric &&
                        (light.volumeIntensity <= 0.0f ||
                        !light.volumetricEnabled ||
                        layerBatch.endLayerValue != light.GetTopMostLitLayer()))
                        continue;

                    var lightMaterial = passData.rendererData.GetLightMaterial(light, passData.isVolumetric);
                    var lightMesh = light.lightMesh;

                    // For Batching.
                    var index = light.batchSlotIndex;
                    var slotIndex = RendererLighting.lightBatch.SlotIndex(index);
                    bool canBatch = RendererLighting.lightBatch.CanBatch(light, lightMaterial, index, out int lightHash);

                    bool breakBatch = !canBatch;
                    if (breakBatch && LightBatch.isBatchingSupported)
                        RendererLighting.lightBatch.Flush(cmd);

                    // Set material properties
                    lightMaterial.SetTexture(k_LightLookupID, passData.lightLookUp);
                    lightMaterial.SetTexture(k_FalloffLookupID, passData.fallOffLookUp);

                    if (passData.layerBatch.lightStats.useNormalMap)
                        s_PropertyBlock.SetTexture(k_NormalMapID, passData.normalMap);

                    if (passData.layerBatch.lightStats.useShadows)
                        s_PropertyBlock.SetTexture(k_ShadowMapID, passData.shadowMap);

                    if (!passData.isVolumetric || (passData.isVolumetric && light.volumetricEnabled))
                        RendererLighting.SetCookieShaderProperties(light, s_PropertyBlock);

                    // Set shader global properties
                    RendererLighting.SetPerLightShaderGlobals(cmd, light, slotIndex, passData.isVolumetric, false, LightBatch.isBatchingSupported);

                    if (light.normalMapQuality != Light2D.NormalMapQuality.Disabled || light.lightType == Light2D.LightType.Point)
                        RendererLighting.SetPerPointLightShaderGlobals(cmd, light, slotIndex, LightBatch.isBatchingSupported);

                    if (LightBatch.isBatchingSupported)
                    {
                        RendererLighting.lightBatch.AddBatch(light, lightMaterial, light.GetMatrix(), lightMesh, 0, lightHash, index);
                        RendererLighting.lightBatch.Flush(cmd);
                    }
                    else
                    {
                        cmd.DrawMesh(lightMesh, light.GetMatrix(), lightMaterial, 0, 0, s_PropertyBlock);
                    }
                }

                RendererLighting.EnableBlendStyle(cmd, i, false);
                cmd.EndSample(blendOpName);
            }
        }

        class PassData
        {
            internal LayerBatch layerBatch;
            internal Renderer2DData rendererData;
            internal bool isVolumetric;

            internal TextureHandle normalMap;
            internal TextureHandle shadowMap;
            internal TextureHandle fallOffLookUp;
            internal TextureHandle lightLookUp;
        }

        public void Render(RenderGraph graph, ContextContainer frameData, Renderer2DData rendererData, ref LayerBatch layerBatch, int batchIndex, bool isVolumetric = false)
        {
            Universal2DResourceData resourceData = frameData.Get<Universal2DResourceData>();

            if (!layerBatch.lightStats.useLights ||
                isVolumetric && !layerBatch.lightStats.useVolumetricLights)
                return;

            using (var builder = graph.AddRasterRenderPass<PassData>(!isVolumetric ? k_LightPass : k_LightVolumetricPass, out var passData, !isVolumetric ? m_ProfilingSampler : m_ProfilingSamplerVolume))
            {
                intermediateTexture[0] = resourceData.activeColorTexture;
                var lightTextures = !isVolumetric ? resourceData.lightTextures[batchIndex] : intermediateTexture;
                var depthTexture = !isVolumetric ? resourceData.intermediateDepth : resourceData.activeDepthTexture;

                for (var i = 0; i < lightTextures.Length; i++)
                    builder.UseTextureFragment(lightTextures[i], i);

                builder.UseTextureFragmentDepth(depthTexture);

                if (layerBatch.lightStats.useNormalMap)
                    builder.UseTexture(resourceData.normalsTexture[batchIndex]);

                if (layerBatch.lightStats.useShadows)
                    builder.UseTexture(resourceData.shadowsTexture);

                foreach (var light in layerBatch.lights)
                {
                    if (!light.m_CookieSpriteTextureHandle.IsValid())
                        continue;

                    if (!isVolumetric || (isVolumetric && light.volumetricEnabled))
                        builder.UseTexture(light.m_CookieSpriteTextureHandle);
                }

                passData.layerBatch = layerBatch;
                passData.rendererData = rendererData;
                passData.isVolumetric = isVolumetric;
                passData.normalMap = layerBatch.lightStats.useNormalMap ? resourceData.normalsTexture[batchIndex] : TextureHandle.nullHandle;
                passData.shadowMap = layerBatch.lightStats.useShadows ? resourceData.shadowsTexture : TextureHandle.nullHandle;
                passData.fallOffLookUp = graph.ImportTexture(m_FallOffRTHandle);
                passData.lightLookUp = graph.ImportTexture(m_LightLookupRTHandle);

                builder.UseTexture(passData.fallOffLookUp);
                builder.UseTexture(passData.lightLookUp);

                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Execute(context.cmd, data, ref data.layerBatch);
                });
            }
        }
    }
}
