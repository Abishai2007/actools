// real reflection
TextureCube gReflectionCubemap;

float3 GetAmbient(float3 normal) {
	float value = abs(gCubemapAmbient);
	float3 gradient = gCubemapAmbient < 0.0 ? (float3)1.0 : gAmbientDown + saturate(normal.y * 0.5 + 0.5) * gAmbientRange;

	[branch]
	if (gCubemapAmbient != 0) {
		float3 refl = gReflectionCubemap.SampleLevel(samAnisotropic, normal, 99).rgb;
		return max(refl, 0.0) / (max(dot(refl, float3(0.299f, 0.587f, 0.114f)), 0.0) + 0.04) * value + gradient * (1.0 - value);
	} else {
		return gradient;
	}
}

float GetFakeHorizon(float3 d, float e) {
	return saturate((d.y + 0.02) * 5.0 * e) * saturate(1 - pow(d.y * 1.5, 2)) * 0.3;
}

float saturate(float v, float e) {
	return saturate(v * e + 0.5);
}

float GetFakeStudioLights(float3 d, float e) {
	return (
		saturate(0.3 - abs(0.6 - d.y), e) +
		saturate(0.1 - abs(0.1 - d.y), e)
		) * saturate(0.3 - abs(0.1 + sin(d.x * 11.0)), e);
}

SamplerState samCubemap {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = WRAP;
	AddressV = WRAP;
};

float3 GetReflection(float3 reflected, float specularExp) {
	[branch]
	if (gCubemapReflections) {
		return gReflectionCubemap.SampleLevel(samCubemap, reflected, saturate(1 - specularExp / 255) * 8).rgb * gReflectionPower;
	} else {
		[flatten]
		if (gFlatMirrored) {
			reflected.y = -reflected.y;
		}

		float edge = specularExp / 30.0 + 1.0;
		float fake = saturate(GetFakeHorizon(reflected, edge) + GetFakeStudioLights(reflected, edge));
		return (gBackgroundColor * (1 - fake) * 1.1 + fake * 1.8) * gReflectionPower;
	}
}

float GetReflectionStrength(float3 normalW, float3 toEyeW) {
	// float rid = 1 - saturate(dot(toEyeW, normalW) - gReflectiveMaterial.FresnelC);

	// float rid = 1 - saturate(dot(toEyeW, normalW));
	// float rim = pow(rid, gReflectiveMaterial.FresnelExp);
	// return min(max(rim, gReflectiveMaterial.FresnelC), gReflectiveMaterial.FresnelMaxLevel);

	float rid = 1 - saturate(dot(toEyeW, normalW));
	float rim = pow(rid, gReflectiveMaterial.FresnelExp);
	return min(rim + gReflectiveMaterial.FresnelC, gReflectiveMaterial.FresnelMaxLevel);

	//float d = dot(toEyeW, normalW);
	//float y = 0.0 < d;
	//return min(exp(log(abs(1.0 - d)) * gReflectiveMaterial.FresnelExp), gReflectiveMaterial.FresnelC) + y;
}

float4 CalculateReflection(float3 posW, float3 normalW) {
	float3 toEyeW = normalize(gEyePosW - posW);
	float3 reflected = reflect(-toEyeW, normalW);
	float3 refl = GetReflection(reflected, gMaterial.SpecularExp);
	float val = GetReflectionStrength(normalW, toEyeW);
	return float4(refl, val);
}

float4 CalculateReflection_Maps(float3 posW, float3 normalW, float specularExpMultiplier) {
	float3 toEyeW = normalize(gEyePosW - posW);
	float3 reflected = reflect(-toEyeW, normalW);
	float3 refl = GetReflection(reflected, (gMaterial.SpecularExp + 400 * GET_FLAG(IS_CARPAINT)) * specularExpMultiplier);
	float val = GetReflectionStrength(normalW, toEyeW);
	return float4(refl, val);
}