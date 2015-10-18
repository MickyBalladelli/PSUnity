#ifndef FILE_ATMOSPHERICSCATTERING
#define FILE_ATMOSPHERICSCATTERING

#include "UnityCG.cginc"

#define M_PI 3.141592657f

#define ATMOSPHERICS_DBG_NONE					0
#define ATMOSPHERICS_DBG_SCATTERING				1
#define ATMOSPHERICS_DBG_OCCLUSION				2
#define ATMOSPHERICS_DBG_OCCLUDEDSCATTERING		3
#define ATMOSPHERICS_DBG_RAYLEIGH				4
#define ATMOSPHERICS_DBG_MIE					5
#define ATMOSPHERICS_DBG_HEIGHT					6
uniform int u_AtmosphericsDebugMode;

uniform float3		u_SunDirection;

uniform float		u_ShadowBias;
uniform float		u_ShadowBiasIndirect;
uniform float		u_ShadowBiasClouds;
uniform half		u_OcclusionDepthThreshold;
UNITY_DECLARE_TEX2D (u_OcclusionTexture);
uniform half4		u_OcclusionTexture_TexelSize;
uniform half4		u_DepthTextureScaledTexelSize;
uniform sampler2D	_CameraDepthTexture;

uniform float		u_WorldScaleExponent;
uniform float		u_WorldNormalDistanceRcp;
uniform float		u_WorldNearScatterPush;
uniform float		u_WorldRayleighDensity;
uniform float		u_WorldMieDensity;

uniform float3		u_RayleighColorM20;
uniform float3		u_RayleighColorM10;
uniform float3		u_RayleighColorO00;
uniform float3		u_RayleighColorP10;
uniform float3		u_RayleighColorP20;
uniform float3		u_RayleighColorP45;

uniform float3		u_MieColorM20;
uniform float3		u_MieColorO00;
uniform float3		u_MieColorP20;
uniform float3		u_MieColorP45;
		
uniform float		u_HeightNormalDistanceRcp;
uniform float		u_HeightNearScatterPush;
uniform float		u_HeightRayleighDensity;
uniform float		u_HeightMieDensity;
uniform float		u_HeightSeaLevel;
uniform float3		u_HeightPlaneShift;
uniform float		u_HeightDistanceRcp;

uniform float		u_RayleighCoeffScale;
uniform float3		u_RayleighSunTintIntensity;
uniform float2		u_RayleighInScatterPct;

uniform float		u_MieCoeffScale;
uniform float3		u_MieSunTintIntensity;
uniform float		u_MiePhaseAnisotropy;

uniform float		u_HeightExtinctionFactor;
uniform float		u_RayleighExtinctionFactor;
uniform float		u_MieExtinctionFactor;

uniform float4		u_HeightRayleighColor;

float henyeyGreenstein(float g, float cosTheta) {
	float gSqr = g * g;
	float a1 = (1.f - gSqr);
	float a2 = (2.f + gSqr);
	float b1 = 1.f + cosTheta * cosTheta;
	float b2 = pow(1.f + gSqr - 2.f * g * cosTheta, 1.5f);
	return (a1 / a2) * (b1 / b2); 
}

float rayleighPhase(float cosTheta) {
	const float f = 3.f / (16.f * M_PI);
	return f + f * cosTheta * cosTheta;
}

float miePhase(float cosTheta, float anisotropy) {
	const float f = 3.f / (8.f * M_PI);
	return f * henyeyGreenstein(anisotropy, cosTheta);
}

float heightDensity(float h, float H) {
	return exp(-h/H);
}

float3 WorldScale(float3 p) {
	p.xz = sign(p.xz) * pow(abs(p.xz), u_WorldScaleExponent);
	return p;
}

void _VolundTransferScatter(float3 _worldPos, out half4 coords1, out half4 coords2, out half4 coords3) {
	const float3 worldPos = WorldScale(_worldPos);
	const float3 worldCamPos = WorldScale(_WorldSpaceCameraPos.xyz);

	const float c_MieScaleHeight = 1200.f;
	const float worldRayleighDensity = 1.f;
	const float worldMieDensity = heightDensity(worldPos.y, c_MieScaleHeight);

	const float3 worldVec = worldPos.xyz - worldCamPos.xyz;
	const float worldVecLen = length(worldVec);
	const float3 worldDir = worldVec / worldVecLen;

	const float3 worldDirUnscaled = normalize(_worldPos - _WorldSpaceCameraPos.xyz);
		
	const float viewSunCos = dot(worldDirUnscaled, u_SunDirection);
	const float rayleighPh = min(1.f, rayleighPhase(viewSunCos) * 12.f);
	const float miePh = miePhase(viewSunCos, u_MiePhaseAnisotropy);

	const float angle20 = 0.324f / 1.5f;
	const float angle10 = 0.174f / 1.5f;
	const float angleY = worldDir.y * saturate(worldVecLen / 250.0);
	
	float3 rayleighColor;
	if(angleY >= angle10) rayleighColor = lerp(u_RayleighColorP10, u_RayleighColorP20, saturate((angleY - angle10) / (angle20 - angle10)));
	else if(angleY >= 0.f) rayleighColor = lerp(u_RayleighColorO00, u_RayleighColorP10, angleY / angle10);
	else if(angleY >= -angle10) rayleighColor = lerp(u_RayleighColorM10, u_RayleighColorO00, (angleY + angle10) / angle10);
	else rayleighColor = lerp(u_RayleighColorM20, u_RayleighColorM10, saturate((angleY + angle20) / (angle20 - angle10)));
	
	float3 mieColor;
	if(angleY >= 0.f) mieColor = lerp(u_MieColorO00, u_MieColorP20, saturate(angleY / angle20));
	else mieColor = lerp(u_MieColorM20, u_MieColorO00, saturate((angleY + angle20) / angle20));

	const float pushedDistance = max(0.f, worldVecLen + u_WorldNearScatterPush);
	const float pushedDensity = /*heightDensity **/ pushedDistance /** exp(-worldPos.y / 8000.f)*/;
	const float rayleighScatter = (1.f - exp(u_WorldRayleighDensity * pushedDensity)) * rayleighPh;
#ifdef IS_RENDERING_SKY
	const float mieScatter = (1.f - exp(u_WorldMieDensity * pushedDensity));
#else
	const float mieScatter = (1.f - exp(u_WorldMieDensity * pushedDensity)) * miePh;
#endif

	const float heightShift = dot(worldVec, u_HeightPlaneShift);
	const float heightScaledOffset = (worldPos.y - heightShift - u_HeightSeaLevel) * u_HeightDistanceRcp;
	const float heightDensity = exp(-heightScaledOffset);
	const float pushedHeightDistance = max(0.f, worldVecLen + u_HeightNearScatterPush);
	const float heightScatter = (1.f - exp(u_HeightRayleighDensity * pushedHeightDistance)) * heightDensity;
#ifdef IS_RENDERING_SKY
	const float heightMieScatter = (1.f - exp(u_HeightMieDensity * pushedHeightDistance)) * heightDensity;
#else
	const float heightMieScatter = (1.f - exp(u_HeightMieDensity * pushedHeightDistance)) * heightDensity * miePh;
#endif

	rayleighColor = lerp(Luminance(rayleighColor).rrr, rayleighColor, saturate(pushedDistance * u_WorldNormalDistanceRcp));
 	float3 heightRayleighColor = lerp(Luminance(u_HeightRayleighColor.xyz).rrr, u_HeightRayleighColor.xyz, saturate(pushedHeightDistance * u_HeightNormalDistanceRcp));
	
	coords1.rgb = rayleighScatter * rayleighColor;
	coords1.a = rayleighScatter;

	coords3.rgb = saturate(heightScatter) * heightRayleighColor;
	coords3.a = heightScatter;

	coords2.rgb = mieScatter * mieColor + saturate(heightMieScatter) * mieColor;
	coords2.a = mieScatter;
}

void VolundTransferScatter(float3 worldPos, out half4 coords1) {
	 half4 c1, c2, c3;
	 _VolundTransferScatter(worldPos, c1, c2, c3);
	 
#ifdef IS_RENDERING_SKY
	coords1.rgb = c3.rgb;
	coords1.a = max(0.f, 1.f - c3.a * u_HeightExtinctionFactor);
#else
	coords1.rgb = c1.rgb;
	coords1.rgb += c3.rgb;
	coords1.a = max(0.f, 1.f - c1.a * u_RayleighExtinctionFactor - c3.a * u_HeightExtinctionFactor);
#endif

	coords1.rgb += c2.rgb;
	coords1.a *= max(0.f, 1.f - c2.a * u_MieExtinctionFactor);

#ifdef ATMOSPHERICS_DEBUG
	if(u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_RAYLEIGH)
		coords1.rgb = c1.rgb;
	else if(u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_MIE)
		coords1.rgb = c2.rgb;
	else if(u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_HEIGHT)
		coords1.rgb = c3.rgb;
#endif
}

half2 UVFromPos(half2 pos) {
#if defined(UNITY_PASS_FORWARDBASE)
	return pos;
#else
	return pos / _ScreenParams.xy;
#endif
}

half3 VolundApplyScatter(half4 coords1, half2 pos, half3 color) {
#ifdef ATMOSPHERICS_DEBUG
	if(u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_OCCLUSION)
		return 1;
	else if(u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_SCATTERING || u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_OCCLUDEDSCATTERING)
		return coords1.rgb;
	else if(u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_RAYLEIGH || u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_MIE || u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_HEIGHT)
		return coords1.rgb;
#endif

	return color * coords1.a + coords1.rgb;
}

half3 VolundApplyScatterAdd(half coords1, half3 color) {
	return color * coords1;
}

void VolundTransferScatterOcclusion(float3 worldPos, out half4 coords1, out half3 coords2) {
	 half4 c1, c2, c3;
	 _VolundTransferScatter(worldPos, c1, c2, c3);
	 
	coords1.rgb = c1.rgb * u_RayleighInScatterPct.x;
	coords1.a = max(0.f, 1.f - c1.a * u_RayleighExtinctionFactor - c3.a * u_HeightExtinctionFactor);

	coords1.rgb += c2.rgb;
	coords1.a *= max(0.f, 1.f - c2.a * u_MieExtinctionFactor);
	
	coords2.rgb = c3.rgb + c1.rgb * u_RayleighInScatterPct.y;

#ifdef ATMOSPHERICS_DEBUG
	if(u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_RAYLEIGH)
		coords1.rgb = c1.rgb;
	else if(u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_MIE)
		coords1.rgb = c2.rgb;
	else if(u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_HEIGHT)
		coords1.rgb = c3.rgb;
#endif
}

inline float4 LinearEyeDepth4(float4 z) { return float4(1.0, 1.0, 1.0, 1.0) / (_ZBufferParams.zzzz * z + _ZBufferParams.wwww); }

half VolundSampleScatterOcclusion(half2 pos) {
#if defined(ATMOSPHERICS_OCCLUSION)
	half2 uv = UVFromPos(pos);
#if defined(ATMOSPHERICS_OCCLUSION_EDGE_FIXUP) && defined(SHADER_API_D3D11) && SHADER_TARGET > 40
	half4 baseUV = half4(uv.x, uv.y, 0.f, 0.f);

	float cDepth = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, baseUV);
	cDepth = LinearEyeDepth(cDepth);

	float4 xDepth;
	baseUV.xy = uv + u_DepthTextureScaledTexelSize.zy; xDepth.x = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, baseUV);
	baseUV.xy = uv + u_DepthTextureScaledTexelSize.xy; xDepth.y = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, baseUV);
	baseUV.xy = uv + u_DepthTextureScaledTexelSize.xw; xDepth.z = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, baseUV);
	baseUV.xy = uv + u_DepthTextureScaledTexelSize.zw; xDepth.w = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, baseUV);
	
	xDepth = LinearEyeDepth4(xDepth);
		
	float4 diffDepth = xDepth - cDepth.rrrr;
	float4 maskDepth = abs(diffDepth) < u_OcclusionDepthThreshold;
	float maskWeight = dot(maskDepth, maskDepth);
	
	UNITY_BRANCH
	if(maskWeight == 4.f || maskWeight == 0.f) {
		return u_OcclusionTexture.SampleLevel(sampleru_OcclusionTexture, uv, 0.f);
	} else {
		float4 occ = u_OcclusionTexture.Gather(sampleru_OcclusionTexture, uv);
		
		float4 fWeights;
		fWeights.xy = frac(uv * u_OcclusionTexture_TexelSize.zw - 0.5f);
		fWeights.zw = float2(1.f, 1.f) - fWeights.xy;
		
		float4 mfWeights = float4(fWeights.z * fWeights.y, fWeights.x * fWeights.y, fWeights.x * fWeights.w, fWeights.z * fWeights.w);
		return dot(occ, mfWeights * maskDepth) / dot(mfWeights, maskDepth);
	}
#else
	return UNITY_SAMPLE_TEX2D(u_OcclusionTexture, uv).r;
#endif
#else //defined(ATMOSPHERICS_OCCLUSION)
	return 1.f;
#endif //defined(ATMOSPHERICS_OCCLUSION)
}

half3 VolundApplyScatterOcclusion(half4 coords1, half3 coords2, half2 pos, half3 color) {
	float occlusion = VolundSampleScatterOcclusion(pos);
	
#ifdef ATMOSPHERICS_DEBUG
	if(u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_SCATTERING)
		return coords1.rgb + coords2.rgb;
	else if(u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_OCCLUSION)
		return occlusion;
	else if(u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_OCCLUDEDSCATTERING)
		return coords1.rgb * min(1.f, occlusion + u_ShadowBias)  + coords2.rgb * min(1.f, occlusion + u_ShadowBiasIndirect);
	else if(u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_RAYLEIGH || u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_MIE || u_AtmosphericsDebugMode == ATMOSPHERICS_DBG_HEIGHT)
		return coords1.rgb;
#endif

	return
		color * coords1.a
		+ coords1.rgb * min(1.f, occlusion + u_ShadowBias) + coords2.rgb * min(1.f, occlusion + u_ShadowBiasIndirect);
	;
}

half VolundCloudOcclusion(half2 pos) {
#if defined(ATMOSPHERICS_OCCLUSION)
	return min(1.f, VolundSampleScatterOcclusion(pos) + u_ShadowBiasClouds);
#else
	return 1.f;
#endif
}


half4 VolundApplyCloudScatter(half4 coords1, half4 color) {
#if defined(DBG_ATMOSPHERICS_SCATTERING) || defined(DBG_ATMOSPHERICS_OCCLUDEDSCATTERING)
	return half4(coords1.rgb, color.a);
#elif defined(DBG_ATMOSPHERICS_OCCLUSION)
	return 1;
#endif

	color.rgb = color.rgb * coords1.a + coords1.rgb;
	return color;
}

half4 VolundApplyCloudScatterOcclusion(half4 coords1, half3 coords2, half2 pos, half4 color) {
	float occlusion = VolundSampleScatterOcclusion(pos);
#ifdef ATMOSPHERICS_OCCLUSION_DEBUG2
	color.rgb = coords1.rgb * min(1.f, occlusion + u_ShadowBias) + coords2.rgb * min(1.f, occlusion + u_ShadowBiasIndirect);
	return color;
#endif
#ifdef ATMOSPHERICS_OCCLUSION_DEBUG
	return occlusion;
#endif

	color.rgb = color.rgb * coords1.a + coords1.rgb * min(1.f, occlusion + u_ShadowBias) + coords2.rgb * min(1.f, occlusion + u_ShadowBiasIndirect);
	
	half cloudOcclusion = min(1.f, occlusion + u_ShadowBiasClouds);
	color.a *= cloudOcclusion;

	return color;
}

// Original vert/frag macros
#if defined(ATMOSPHERICS_OCCLUSION)
	#define VOLUND_SCATTER_COORDS(idx1, idx2) half4 scatterCoords1 : TEXCOORD##idx1; half3 scatterCoords2 : TEXCOORD##idx2;
	#if defined(ATMOSPHERICS_PER_PIXEL)
		#define VOLUND_TRANSFER_SCATTER(pos, o) o.scatterCoords1 = pos.xyzz; o.scatterCoords2 = pos.xyz;
		#define VOLUND_APPLY_SCATTER(i, color) VolundTransferScatterOcclusion(i.scatterCoords1.xyz, i.scatterCoords1, i.scatterCoords2); color = VolundApplyScatterOcclusion(i.scatterCoords1, i.scatterCoords2, i.pos.xy, color)
		#define VOLUND_CLOUD_SCATTER(i, color) VolundTransferScatterOcclusion(i.scatterCoords1.xyz, i.scatterCoords1, i.scatterCoords2); color = VolundApplyCloudScatterOcclusion(i.scatterCoords1, i.scatterCoords2, i.pos.xy, color)
	#else
		#define VOLUND_TRANSFER_SCATTER(pos, o) VolundTransferScatterOcclusion(pos, o.scatterCoords1, o.scatterCoords2)
		#define VOLUND_APPLY_SCATTER(i, color) color = VolundApplyScatterOcclusion(i.scatterCoords1, i.scatterCoords2, i.pos.xy, color)
		#define VOLUND_CLOUD_SCATTER(i, color) color = VolundApplyCloudScatterOcclusion(i.scatterCoords1, i.scatterCoords2, i.pos.xy, color)
	#endif
#else
	#define VOLUND_SCATTER_COORDS(idx1, idx2) half4 scatterCoords1 : TEXCOORD##idx1;
	#if defined(ATMOSPHERICS_PER_PIXEL)
		#define VOLUND_TRANSFER_SCATTER(pos, o) o.scatterCoords1 = pos.xyzz;
		#define VOLUND_APPLY_SCATTER(i, color) VolundTransferScatter(i.scatterCoords1.xyz, i.scatterCoords1); color = VolundApplyScatter(i.scatterCoords1, i.pos.xy, color);
		#define VOLUND_CLOUD_SCATTER(i, color) VolundTransferScatter(i.scatterCoords1.xyz, i.scatterCoords1); color = VolundApplyCloudScatter(i.scatterCoords1, color);
	#else
		#define VOLUND_TRANSFER_SCATTER(pos, o) VolundTransferScatter(pos, o.scatterCoords1)
		#define VOLUND_APPLY_SCATTER(i, color) color = VolundApplyScatter(i.scatterCoords1, i.pos.xy, color)
		#define VOLUND_CLOUD_SCATTER(i, color) color = VolundApplyCloudScatter(i.scatterCoords1, color)
	#endif
#endif

// Surface shader macros (specifically do nothing for deferred as that needs post-support)
#if defined(ATMOSPHERICS)
	#if defined(UNITY_PASS_FORWARDBASE) && defined(ATMOSPHERICS_OCCLUSION)
		#define SURFACE_SCATTER_COORDS				float3 worldPos; half4 scatterCoords1; half3 scatterCoords2;
		#define SURFACE_SCATTER_TRANSFER(pos, o)	VolundTransferScatterOcclusion(pos, o.scatterCoords1, o.scatterCoords2)
													/* we can't fit screenPos interpolator, so calculate per-pixel. if only we had vpos available.. */
		#define SURFACE_SCATTER_APPLY(i, color)		{ \
														float4 scatterPos = ComputeScreenPos(mul(UNITY_MATRIX_VP, float4(i.worldPos, 1.f))); \
														color = VolundApplyScatterOcclusion(i.scatterCoords1, i.scatterCoords2, scatterPos.xy / scatterPos.w, color); \
													}
	#elif defined(UNITY_PASS_FORWARDBASE)
		#define SURFACE_SCATTER_COORDS 				float3 worldPos; half4 scatterCoords1; half scatterCoords2;
		#define SURFACE_SCATTER_TRANSFER(pos, o)	VolundTransferScatter(pos, o.scatterCoords1)
		#define SURFACE_SCATTER_APPLY(i, color)		{ \
														float4 scatterPos = ComputeScreenPos(mul(UNITY_MATRIX_VP, float4(i.worldPos, 1.f))); \
														color = VolundApplyScatter(i.scatterCoords1, scatterPos.xy / scatterPos.w, color); \
													}
	#elif defined(UNITY_PASS_FORWARDADD)
		#define SURFACE_SCATTER_COORDS 				float3 worldPos; half scatterCoords1; half scatterCoords2;
		#define SURFACE_SCATTER_TRANSFER(pos, o)	{ half4 scatterCoords; VolundTransferScatter(pos, scatterCoords); o.scatterCoords1 = scatterCoords.a; }
		#define SURFACE_SCATTER_APPLY(i, color)		color = VolundApplyScatterAdd(i.scatterCoords1, color)
	#endif
#elif defined(ATMOSPHERICS_PER_PIXEL)
	#if defined(UNITY_PASS_FORWARDBASE) && defined(ATMOSPHERICS_OCCLUSION)
		#define SURFACE_SCATTER_COORDS				float3 worldPos; half scatterCoords1; half scatterCoords2;
		#define SURFACE_SCATTER_TRANSFER(pos, o)
		#define SURFACE_SCATTER_APPLY(i, color)		{ \
														float4 scatterPos = ComputeScreenPos(mul(UNITY_MATRIX_VP, float4(i.worldPos, 1.f))); \
														half4 scatterCoords1; half3 scatterCoords2; VolundTransferScatterOcclusion(i.worldPos, scatterCoords1, scatterCoords2); \
														color = VolundApplyScatterOcclusion(scatterCoords1, scatterCoords2, scatterPos.xy / scatterPos.w, color); \
													}
	#elif defined(UNITY_PASS_FORWARDBASE)
		#define SURFACE_SCATTER_COORDS 				float3 worldPos; half scatterCoords1; half scatterCoords2;
		#define SURFACE_SCATTER_TRANSFER(pos, o)
		#define SURFACE_SCATTER_APPLY(i, color)		{ \
														half4 scatterCoords1; VolundTransferScatter(i.worldPos, scatterCoords1); \
														float4 scatterPos = ComputeScreenPos(mul(UNITY_MATRIX_VP, float4(i.worldPos, 1.f))); \
														color = VolundApplyScatter(scatterCoords1, scatterPos.xy / scatterPos.w, color); \
													}
	#elif defined(UNITY_PASS_FORWARDADD)
		#define SURFACE_SCATTER_COORDS 				float3 worldPos; half scatterCoords1; half scatterCoords2;
		#define SURFACE_SCATTER_TRANSFER(pos, o)
		#define SURFACE_SCATTER_APPLY(i, color)		{ \
														half4 scatterCoords1; VolundTransferScatter(i.worldPos, scatterCoords1); \
														color = VolundApplyScatterAdd(scatterCoords1.a, color); \
													}
	#endif
#endif

#if !defined(SURFACE_SCATTER_COORDS)
												/* surface shader analysis currently forces us to include stuff even when unused */
												/* we also have to convince the analyzer to not optimize out stuff we need */
	#define SURFACE_SCATTER_COORDS				float3 worldPos; half4 scatterCoords1; half3 scatterCoords2;
	#define SURFACE_SCATTER_TRANSFER(pos, o)	o.scatterCoords1.r = o.scatterCoords2.r = pos.x;
	#define SURFACE_SCATTER_APPLY(i, color)		color += (i.worldPos + i.scatterCoords1.xyz + i.scatterCoords2.xyz) * 0.000001f
#endif

#endif //FILE_ATMOSPHERICSCATTERING