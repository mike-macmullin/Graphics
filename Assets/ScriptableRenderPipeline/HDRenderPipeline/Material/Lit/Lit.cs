using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    namespace Lit
    {
        [GenerateHLSL(PackingRules.Exact)]
        public enum MaterialId
        {
            LitStandard = 0,
            LitSSS = 1,
            LitClearCoat = 2,
            LitSpecular = 3,
            LitAniso = 4 // Should be the last as it is not setup by the users but generated based on anisotropy property
        };

        //-----------------------------------------------------------------------------
        // SurfaceData
        //-----------------------------------------------------------------------------

        // Main structure that store the user data (i.e user input of master node in material graph)
        [GenerateHLSL(PackingRules.Exact, false, true, 1000)]
        public struct SurfaceData
        {
            [SurfaceDataAttributes("Base Color")]
            public Vector3 baseColor;
            [SurfaceDataAttributes("Specular Occlusion")]
            public float specularOcclusion;

            [SurfaceDataAttributes("Normal")]
            public Vector3 normalWS;
            [SurfaceDataAttributes("Smoothness")]
            public float perceptualSmoothness;
            [SurfaceDataAttributes("Material ID")]
            public MaterialId materialId;

            [SurfaceDataAttributes("Ambient Occlusion")]
            public float ambientOcclusion;

            // MaterialId dependent attribute

            // standard
            [SurfaceDataAttributes("Tangent")]
            public Vector3 tangentWS;
            [SurfaceDataAttributes("Anisotropy")]
            public float anisotropy; // anisotropic ratio(0->no isotropic; 1->full anisotropy in tangent direction)
            [SurfaceDataAttributes("Metallic")]
            public float metallic;
            [SurfaceDataAttributes("Specular")]
            public float specular; // 0.02, 0.04, 0.16, 0.2

            // SSS
            [SurfaceDataAttributes("Subsurface Radius")]
            public float subsurfaceRadius;
            [SurfaceDataAttributes("Thickness")]
            public float thickness;
            [SurfaceDataAttributes("Subsurface Profile")]
            public int subsurfaceProfile;

            // Clearcoat
            [SurfaceDataAttributes("Coat Normal")]
            public Vector3 coatNormalWS;
            [SurfaceDataAttributes("Coat Smoothness")]
            public float coatPerceptualSmoothness;

            // SpecColor
            [SurfaceDataAttributes("Specular Color")]
            public Vector3 specularColor;
        };

        //-----------------------------------------------------------------------------
        // BSDFData
        //-----------------------------------------------------------------------------

        [GenerateHLSL(PackingRules.Exact, false, true, 1030)]
        public struct BSDFData
        {
            public Vector3 diffuseColor;

            public Vector3 fresnel0;

            public float specularOcclusion;

            public Vector3 normalWS;
            public float perceptualRoughness;
            public float roughness;
            public float materialId;

            // MaterialId dependent attribute

            // standard
            public Vector3 tangentWS;
            public Vector3 bitangentWS;
            public float roughnessT;
            public float roughnessB;
            public float anisotropy;

            // fold into fresnel0

            // SSS
            public float   subsurfaceRadius;
            public float   thickness;
            public int     subsurfaceProfile;
            public bool    enableTransmission; // Read from the SSS profile
            public Vector3 transmittance;

            // Clearcoat
            public Vector3 coatNormalWS;
            public float coatRoughness;

            // SpecColor
            // fold into fresnel0
        };

        //-----------------------------------------------------------------------------
        // RenderLoop management
        //-----------------------------------------------------------------------------

        [GenerateHLSL(PackingRules.Exact)]
        public enum GBufferMaterial
        {
            // Note: This count doesn't include the velocity buffer. On shader and csharp side the velocity buffer will be added by the framework
            Count = (ShaderConfig.k_PackgbufferInU16 == 1) ? 2 : 4
        };

        public partial class RenderLoop : Object
        {
            //-----------------------------------------------------------------------------
            // GBuffer management
            //-----------------------------------------------------------------------------

            public int GetMaterialGBufferCount() { return (int)GBufferMaterial.Count; }

            public void GetMaterialGBufferDescription(out RenderTextureFormat[] RTFormat, out RenderTextureReadWrite[] RTReadWrite)
            {
                RTFormat = new RenderTextureFormat[(int)GBufferMaterial.Count];
                RTReadWrite = new RenderTextureReadWrite[(int)GBufferMaterial.Count];

                if (ShaderConfig.s_PackgbufferInU16 == 1)
                {
                    // TODO: Just discovered that Unity doesn't support unsigned 16 RT format.
                    RTFormat[0] = RenderTextureFormat.ARGBInt; RTReadWrite[0] = RenderTextureReadWrite.Linear;
                    RTFormat[1] = RenderTextureFormat.ARGBInt; RTReadWrite[1] = RenderTextureReadWrite.Linear;
                }
                else
                {
                    RTFormat[0] = RenderTextureFormat.ARGB32; RTReadWrite[0] = RenderTextureReadWrite.sRGB;
                    RTFormat[1] = RenderTextureFormat.ARGB2101010; RTReadWrite[1] = RenderTextureReadWrite.Linear;
                    RTFormat[2] = RenderTextureFormat.ARGB32; RTReadWrite[2] = RenderTextureReadWrite.Linear;
                    RTFormat[3] = RenderTextureFormat.RGB111110Float; RTReadWrite[3] = RenderTextureReadWrite.Linear;
                }
            }

            //-----------------------------------------------------------------------------
            // Init precomputed texture
            //-----------------------------------------------------------------------------

            public bool isInit;

            // For image based lighting
            private Material      m_InitPreFGD;
            private RenderTexture m_PreIntegratedFGD;

            // For area lighting - We pack all texture inside a texture array to reduce the number of resource required
            private Texture2DArray m_LtcData; // 0: m_LtcGGXMatrix - RGBA, 2: m_LtcDisneyDiffuseMatrix - RGBA, 3: m_LtcMultiGGXFresnelDisneyDiffuse - RGB, A unused

            const int k_LtcLUTMatrixDim  =  3; // size of the matrix (3x3)
            const int k_LtcLUTResolution = 64;


            // Load LUT with one scalar in alpha of a tex2D
            void LoadLUT(Texture2DArray tex, int arrayElement, TextureFormat format, float[] LUTScalar)
            {
                const int count = k_LtcLUTResolution * k_LtcLUTResolution;
                Color[] pixels = new Color[count];

                for (int i = 0; i < count; i++)
                {
                    pixels[i] = new Color(0, 0, 0, LUTScalar[i]);
                }

                tex.SetPixels(pixels, arrayElement);
            }

            // Load LUT with 3x3 matrix in RGBA of a tex2D (some part are zero)
            void LoadLUT(Texture2DArray tex, int arrayElement, TextureFormat format, double[,] LUTTransformInv)
            {
                const int count = k_LtcLUTResolution * k_LtcLUTResolution;
                Color[] pixels = new Color[count];

                for (int i = 0; i < count; i++)
                {
                    // Both GGX and Disney Diffuse BRDFs have zero values in columns 1, 3, 5, 7.
                    // Column 8 contains only ones.
                    pixels[i] = new Color((float)LUTTransformInv[i, 0],
                            (float)LUTTransformInv[i, 2],
                            (float)LUTTransformInv[i, 4],
                            (float)LUTTransformInv[i, 6]);
                }

                tex.SetPixels(pixels, arrayElement);
            }

            // Special-case function for 'm_LtcMultiGGXFresnelDisneyDiffuse'.
            void LoadLUT(Texture2DArray tex, int arrayElement, TextureFormat format,   float[] LtcGGXMagnitudeData,
                float[] LtcGGXFresnelData,
                float[] LtcDisneyDiffuseMagnitudeData)
            {
                const int count = k_LtcLUTResolution * k_LtcLUTResolution;
                Color[] pixels = new Color[count];

                for (int i = 0; i < count; i++)
                {
                    // We store the result of the subtraction as a run-time optimization.
                    // See the footnote 2 of "LTC Fresnel Approximation" by Stephen Hill.
                    pixels[i] = new Color(LtcGGXMagnitudeData[i] - LtcGGXFresnelData[i],
                            LtcGGXFresnelData[i], LtcDisneyDiffuseMagnitudeData[i], 1);
                }

                tex.SetPixels(pixels, arrayElement);
            }

            public void Build()
            {
                m_InitPreFGD = Utilities.CreateEngineMaterial("Hidden/HDRenderPipeline/PreIntegratedFGD");

                // TODO: switch to RGBA64 when it becomes available.
                m_PreIntegratedFGD = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                m_PreIntegratedFGD.filterMode = FilterMode.Bilinear;
                m_PreIntegratedFGD.wrapMode = TextureWrapMode.Clamp;
                m_PreIntegratedFGD.Create();

                m_LtcData = new Texture2DArray(k_LtcLUTResolution, k_LtcLUTResolution, 3, TextureFormat.RGBAHalf, false /*mipmap*/, true /* linear */)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                LoadLUT(m_LtcData, 0, TextureFormat.RGBAHalf,   s_LtcGGXMatrixData);
                LoadLUT(m_LtcData, 1, TextureFormat.RGBAHalf,   s_LtcDisneyDiffuseMatrixData);
                // TODO: switch to RGBA64 when it becomes available.
                LoadLUT(m_LtcData, 2, TextureFormat.RGBAHalf,   s_LtcGGXMagnitudeData, s_LtcGGXFresnelData, s_LtcDisneyDiffuseMagnitudeData);

                m_LtcData.Apply();

                isInit = false;
            }

            public void Cleanup()
            {
                Utilities.Destroy(m_InitPreFGD);

                // TODO: how to delete RenderTexture ? or do we need to do it ?
                isInit = false;
            }

            public void RenderInit(Rendering.ScriptableRenderContext renderContext)
            {
                var cmd = new CommandBuffer();
                cmd.name = "Init PreFGD";
                cmd.Blit(null, new RenderTargetIdentifier(m_PreIntegratedFGD), m_InitPreFGD, 0);
                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Dispose();

                isInit = true;
            }

            public void Bind()
            {
                Shader.SetGlobalTexture("_PreIntegratedFGD", m_PreIntegratedFGD);
                Shader.SetGlobalTexture("_LtcData", m_LtcData);
            }
        }
    }
}
