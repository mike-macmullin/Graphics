using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    internal partial class UniversalRenderPipelineAssetUI
    {
        public static class Styles
        {
            // Groups
            public static GUIContent renderingSettingsText = EditorGUIUtility.TrTextContent("Rendering", "Settings that control the core part of the pipeline rendered frame.");
            public static GUIContent qualitySettingsText = EditorGUIUtility.TrTextContent("Quality", "Settings that control the quality level of the Render pipeline, improving performance and graphics quality.");
            public static GUIContent lightingSettingsText = EditorGUIUtility.TrTextContent("Lighting", "Settings that affect the lighting in the Scene");
            public static GUIContent shadowSettingsText = EditorGUIUtility.TrTextContent("Shadows", "Settings that configure how shadows look and behave, and can be used to balance between the visual quality and performance of shadows.");
            public static GUIContent postProcessingSettingsText = EditorGUIUtility.TrTextContent("Post-processing", "Settings that allow for fine tuning of post-processing effects in the Scene when this Render Pipeline Asset is in use.");
            public static GUIContent advancedSettingsText = EditorGUIUtility.TrTextContent("Advanced");
            public static GUIContent adaptivePerformanceText = EditorGUIUtility.TrTextContent("Adaptive Performance");

            // Rendering
            public static GUIContent rendererHeaderText = EditorGUIUtility.TrTextContent("Renderer List", "Lists all the renderers available to this Render Pipeline Asset.");
            public static GUIContent rendererDefaultText = EditorGUIUtility.TrTextContent("Default", "This renderer is currently the default for the render pipeline.");
            public static GUIContent rendererSetDefaultText = EditorGUIUtility.TrTextContent("Set Default", "Makes this renderer the default for the render pipeline.");
            public static GUIContent rendererSettingsText = EditorGUIUtility.TrIconContent("_Menu", "Opens settings for this renderer.");
            public static GUIContent rendererMissingText = EditorGUIUtility.TrIconContent("console.warnicon.sml", "Renderer missing. Click this to select a new renderer.");
            public static GUIContent rendererDefaultMissingText = EditorGUIUtility.TrIconContent("console.erroricon.sml", "Default renderer missing. Click this to select a new renderer.");
            public static GUIContent requireDepthTextureText = EditorGUIUtility.TrTextContent("Depth Texture", "If enabled the pipeline will generate camera's depth that can be bound in shaders as _CameraDepthTexture.");
            public static GUIContent requireOpaqueTextureText = EditorGUIUtility.TrTextContent("Opaque Texture", "If enabled the pipeline will copy the screen to texture after opaque objects are drawn. For transparent objects this can be bound in shaders as _CameraOpaqueTexture.");
            public static GUIContent opaqueDownsamplingText = EditorGUIUtility.TrTextContent("Opaque Downsampling", "The downsampling method that is used for the opaque texture");
            public static GUIContent supportsTerrainHolesText = EditorGUIUtility.TrTextContent("Terrain Holes", "When disabled, Universal Rendering Pipeline removes all Terrain hole Shader variants when you build for the Unity Player. This decreases build time.");
            public static GUIContent srpBatcher = EditorGUIUtility.TrTextContent("SRP Batcher", "If enabled, the render pipeline uses the SRP batcher.");
            public static GUIContent storeActionsOptimizationText = EditorGUIUtility.TrTextContent("Store Actions", "Sets the store actions policy on tile based GPUs. Affects render targets memory usage and will impact performance.");
            public static GUIContent dynamicBatching = EditorGUIUtility.TrTextContent("Dynamic Batching", "If enabled, the render pipeline will batch drawcalls with few triangles together by copying their vertex buffers into a shared buffer on a per-frame basis.");
            public static GUIContent debugLevel = EditorGUIUtility.TrTextContent("Debug Level", "Controls the level of debug information generated by the render pipeline. When Profiling is selected, the pipeline provides detailed profiling tags.");
            public static GUIContent shaderVariantLogLevel = EditorGUIUtility.TrTextContent("Shader Variant Log Level", "Controls the level logging in of shader variants information is outputted when a build is performed. Information will appear in the Unity console when the build finishes.");

            // Quality
            public static GUIContent hdrText = EditorGUIUtility.TrTextContent("HDR", "Controls the global HDR settings.");
            public static GUIContent msaaText = EditorGUIUtility.TrTextContent("Anti Aliasing (MSAA)", "Controls the global anti aliasing settings.");
            public static GUIContent renderScaleText = EditorGUIUtility.TrTextContent("Render Scale", "Scales the camera render target allowing the game to render at a resolution different than native resolution. UI is always rendered at native resolution.");
            public static GUIContent shaderQualityText = EditorGUIUtility.TrTextContent("Shader Quality", "Controls the shading quality of various shaders. Higher quality has lower performance. ");

            // Main light
            public static GUIContent mainLightRenderingModeText = EditorGUIUtility.TrTextContent("Main Light", "Main light is the brightest directional light.");
            public static GUIContent supportsMainLightShadowsText = EditorGUIUtility.TrTextContent("Cast Shadows", "If enabled the main light can be a shadow casting light.");
            public static GUIContent mainLightShadowmapResolutionText = EditorGUIUtility.TrTextContent("Shadow Resolution", "Resolution of the main light shadowmap texture. If cascades are enabled, cascades will be packed into an atlas and this setting controls the maximum shadows atlas resolution.");

            // Additional lights
            public static GUIContent addditionalLightsRenderingModeText = EditorGUIUtility.TrTextContent("Additional Lights", "Additional lights support.");
            public static GUIContent perObjectLimit = EditorGUIUtility.TrTextContent("Per Object Limit", "Maximum amount of additional lights. These lights are sorted and culled per-object.");
            public static GUIContent supportsAdditionalShadowsText = EditorGUIUtility.TrTextContent("Cast Shadows", "If enabled shadows will be supported for spot lights.\n");
            public static GUIContent additionalLightsShadowmapResolution = EditorGUIUtility.TrTextContent("Shadow Atlas Resolution", "All additional lights are packed into a single shadowmap atlas. This setting controls the atlas size.");
            public static GUIContent additionalLightsShadowResolutionTiers = EditorGUIUtility.TrTextContent("Shadow Resolution Tiers", $"Additional Lights Shadow Resolution Tiers. Rounded to the next power of two, and clamped to be at least {UniversalAdditionalLightData.AdditionalLightsShadowMinimumResolution}.");
            public static GUIContent[] additionalLightsShadowResolutionTierNames =
            {
                new GUIContent("Low"),
                new GUIContent("Medium"),
                new GUIContent("High")
            };
            public static GUIContent additionalLightsCookieResolution = EditorGUIUtility.TrTextContent("Cookie Atlas Resolution", "All additional lights are packed into a single cookie atlas. This setting controls the atlas size.");
            public static GUIContent additionalLightsCookieFormat = EditorGUIUtility.TrTextContent("Cookie Atlas Format", "All additional lights are packed into a single cookie atlas. This setting controls the atlas format.");

            // Reflection Probes
            public static GUIContent reflectionProbesSettingsText = EditorGUIUtility.TrTextContent("Reflection Probes");
            public static GUIContent reflectionProbeBlendingText = EditorGUIUtility.TrTextContent("Probe Blending", "If enabled smooth transitions will be created between reflection probes.");
            public static GUIContent reflectionProbeBoxProjectionText = EditorGUIUtility.TrTextContent("Box Projection", "If enabled reflections appear based on the object’s position within the probe’s box, while still using a single probe as the source of the reflection.");

            // Additional lighting settings
            public static GUIContent mixedLightingSupportLabel = EditorGUIUtility.TrTextContent("Mixed Lighting", "Makes the render pipeline include mixed-lighting Shader Variants in the build.");
            public static GUIContent supportsLightLayers = EditorGUIUtility.TrTextContent("Light Layers", "When enabled, UniversalRP uses rendering layers instead of culling mask for the purpose of selecting how lights affect groups of geometry. For deferred rendering, an extra render target is allocated.");

            // Shadow settings
            public static GUIContent shadowWorkingUnitText = EditorGUIUtility.TrTextContent("Working Unit", "The unit in which Unity measures the shadow cascade distances. The exception is Max Distance, which will still be in meters.");
            public static GUIContent shadowDistanceText = EditorGUIUtility.TrTextContent("Max Distance", "Maximum shadow rendering distance.");
            public static GUIContent shadowCascadesText = EditorGUIUtility.TrTextContent("Cascade Count", "Number of cascade splits used for directional shadows.");
            public static GUIContent shadowDepthBias = EditorGUIUtility.TrTextContent("Depth Bias", "Controls the distance at which the shadows will be pushed away from the light. Useful for avoiding false self-shadowing artifacts.");
            public static GUIContent shadowNormalBias = EditorGUIUtility.TrTextContent("Normal Bias", "Controls distance at which the shadow casting surfaces will be shrunk along the surface normal. Useful for avoiding false self-shadowing artifacts.");
            public static GUIContent supportsSoftShadows = EditorGUIUtility.TrTextContent("Soft Shadows", "If enabled pipeline will perform shadow filtering. Otherwise all lights that cast shadows will fallback to perform a single shadow sample.");
            public static GUIContent conservativeEnclosingSphere = EditorGUIUtility.TrTextContent("Conservative Enclosing Sphere", "Enable this option to improve shadow frustum culling and prevent Unity from excessively culling shadows in the corners of the shadow cascades. Disable this option only for compatibility purposes of existing projects created in previous Unity versions.");

            // Post-processing
            public static GUIContent colorGradingMode = EditorGUIUtility.TrTextContent("Grading Mode", "Defines how color grading will be applied. Operators will react differently depending on the mode.");
            public static GUIContent colorGradingLutSize = EditorGUIUtility.TrTextContent("LUT size", "Sets the size of the internal and external color grading lookup textures (LUTs).");
            public static GUIContent useFastSRGBLinearConversion = EditorGUIUtility.TrTextContent("Fast sRGB/Linear conversions", "Use faster, but less accurate approximation functions when converting between the sRGB and Linear color spaces.");
            public static string colorGradingModeWarning = "HDR rendering is required to use the high dynamic range color grading mode. The low dynamic range will be used instead.";
            public static string colorGradingModeSpecInfo = "The high dynamic range color grading mode works best on platforms that support floating point textures.";
            public static string colorGradingLutSizeWarning = "The minimal recommended LUT size for the high dynamic range color grading mode is 32. Using lower values will potentially result in color banding and posterization effects.";
            public static GUIContent volumeFrameworkUpdateMode = EditorGUIUtility.TrTextContent("Volume Update Mode", "Select how Unity updates Volumes: every frame or when triggered via scripting. In the Editor, Unity updates Volumes every frame when not in the Play mode.");

            // Adaptive performance settings
            public static GUIContent useAdaptivePerformance = EditorGUIUtility.TrTextContent("Use adaptive performance", "Allows Adaptive Performance to adjust rendering quality during runtime");

            // Renderer List Messages
            public static GUIContent rendererListDefaultMessage =
                EditorGUIUtility.TrTextContent("Cannot remove Default Renderer",
                    "Removal of the Default Renderer is not allowed. To remove, set another Renderer to be the new Default and then remove.");

            public static GUIContent rendererMissingDefaultMessage =
                EditorGUIUtility.TrTextContent("Missing Default Renderer\nThere is no default renderer assigned, so Unity can’t perform any rendering. Set another renderer to be the new Default, or assign a renderer to the Default slot.");
            public static GUIContent rendererMissingMessage =
                EditorGUIUtility.TrTextContent("Missing Renderer(s)\nOne or more renderers are either missing or unassigned.  Switching to these renderers at runtime can cause issues.");
            public static GUIContent lightlayersUnsupportedMessage =
                EditorGUIUtility.TrTextContent("Some Graphics API(s) in the Player Graphics APIs list are incompatible with Light Layers.  Switching to these Graphics APIs at runtime can cause issues: ");
            public static GUIContent rendererUnsupportedAPIMessage =
                EditorGUIUtility.TrTextContent("Some Renderer(s) in the Renderer List are incompatible with the Player Graphics APIs list.  Switching to these renderers at runtime can cause issues.\n\n");

            // Dropdown menu options
            public static string[] mainLightOptions = { "Disabled", "Per Pixel" };
            public static string[] volumeFrameworkUpdateOptions = { "Every Frame", "Via Scripting" };
            public static string[] opaqueDownsamplingOptions = { "None", "2x (Bilinear)", "4x (Box)", "4x (Bilinear)" };
        }
    }
}
