#ifndef UNDER_WATER_UTILITIES_H
#define UNDER_WATER_UTILITIES_H

#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/HDStencilUsage.cs.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Water/WaterSystemDef.cs.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"

StructuredBuffer<float> _WaterCameraHeightBuffer;

float GetWaterCameraHeight()
{
    return _WaterCameraHeightBuffer[unity_StereoEyeIndex];
}

TEXTURE2D_X_UINT(_WaterRegionTexture);

bool IsUnderWater(uint2 coord)
{
    return LOAD_TEXTURE2D_X(_WaterRegionTexture, coord) == 1;
}

Texture2D<float4> _WaterCausticsDataBuffer;

float EvaluateSimulationCaustics(float3 refractedWaterPosRWS, float refractedWaterDepth, float2 distortedWaterNDC)
{
    // Will hold the results of the caustics evaluation
    float3 causticsValues = 0.0;
    float3 triplanarW = 0.0;
    float causticWeight = 0.0;

    // TODO: Is this worth a multicompile?
    if (_WaterCausticsEnabled)
    {
        // Evaluate the caustics weight
        causticWeight = saturate(refractedWaterDepth / _CausticsPlaneBlendDistance);

        // Evaluate the normal of the surface (using partial derivatives of the absolute world pos is not possible as it is not stable enough)
        NormalData normalData;
        float4 normalBuffer = LOAD_TEXTURE2D_X_LOD(_NormalBufferTexture, distortedWaterNDC * _ScreenSize.xy, 0);
        DecodeFromNormalBuffer(normalBuffer, normalData);

        // Evaluate the triplanar weights
        triplanarW = ComputeTriplanarWeights(_WaterProceduralGeometry ? mul(_WaterSurfaceTransform_Inverse, float4(normalData.normalWS, 0.0)).xyz : normalData.normalWS);

        // Convert the position to absolute world space and move the position to the water local space
        float3 causticPosOS = GetAbsolutePositionWS(refractedWaterPosRWS * _CausticsTilingFactor);
        causticPosOS = _WaterProceduralGeometry ? mul(_WaterSurfaceTransform_Inverse, float4(causticPosOS, 1.0)).xyz : causticPosOS;

        // Evaluate the triplanar coodinates
        float3 sampleCoord = causticPosOS / (_CausticsRegionSize * 0.5) + 0.5;
        float2 uv0, uv1, uv2;
        GetTriplanarCoordinate(sampleCoord, uv0, uv1, uv2);

        // Evaluate the sharpness of the caustics based on the depth
        float sharpness = (1.0 - causticWeight) * _CausticsMaxLOD;

        // sample the caustics texture
        causticsValues.x = SAMPLE_TEXTURE2D_LOD(_WaterCausticsDataBuffer, s_linear_repeat_sampler, uv0, sharpness).x;
        causticsValues.y = SAMPLE_TEXTURE2D_LOD(_WaterCausticsDataBuffer, s_linear_repeat_sampler, uv1, sharpness).x;
        causticsValues.z = SAMPLE_TEXTURE2D_LOD(_WaterCausticsDataBuffer, s_linear_repeat_sampler, uv2, sharpness).x;
    }

    // Evaluate the triplanar weights and blend the samples togheter
    return 1.0 + lerp(0, causticsValues.x * triplanarW.y
            + causticsValues.y * triplanarW.z
            + causticsValues.z * triplanarW.x, causticWeight) * _CausticsIntensity;
}

#endif // UNDER_WATER_UTILITIES_H
