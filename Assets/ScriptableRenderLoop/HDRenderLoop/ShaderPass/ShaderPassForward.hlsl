#if SHADERPASS != SHADERPASS_FORWARD
#error SHADERPASS_is_not_correctly_define
#endif

float4 Frag(PackedVaryings packedInput) : SV_Target
{
    FragInputs input = UnpackVaryings(packedInput);

    // input.unPositionSS is SV_Position
    PositionInputs posInput = GetPositionInput(input.unPositionSS.xy, _ScreenSize.zw);
    UpdatePositionInput(input.unPositionSS.z, input.unPositionSS.w, input.positionWS, posInput);
    float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);

	SurfaceData surfaceData;
	BuiltinData builtinData;
	GetSurfaceAndBuiltinData(input, V, posInput, surfaceData, builtinData);

	BSDFData bsdfData = ConvertSurfaceDataToBSDFData(surfaceData);
	PreLightData preLightData = GetPreLightData(V, posInput, bsdfData);

	float3 diffuseLighting;
	float3 specularLighting;
    float3 bakeDiffuseLighting = GetBakedDiffuseLigthing(surfaceData, builtinData, bsdfData, preLightData);
    LightLoop(V, posInput, preLightData, bsdfData, bakeDiffuseLighting, diffuseLighting, specularLighting);

	return float4(diffuseLighting + specularLighting, builtinData.opacity);
}

