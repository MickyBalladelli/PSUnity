#ifndef FILE_UNDERGROWTH_CGINC
#define FILE_UNDERGROWTH_CGINC

#if defined(ATMOSPHERICS)
	#include "../AtmosphericScattering/Code/AtmosphericScattering.cginc"
	#define HAS_FEATURE_ATMOSPHERICS
#endif

//half4 _Color;
sampler2D _MainTex;
float4 _MainTex_ST;

sampler2D	_BmpMap;
half		_BmpScale;

half _Cutoff;
half _CutoffNear;

sampler2D _OpacityTex;
half _Transmission;
half _DirectOcclusionBoost;

sampler2D _GITexture;
half4 _GITexture_ST;

half _SpecPower;

sampler3D	_DitherMaskLOD;

#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "Lighting.cginc"
#include "UnityPBSLighting.cginc"
#include "UnityStandardBRDF.cginc"

struct VertexOutputForwardBase {
	float4 pos							: SV_POSITION;
	half4 tex							: TEXCOORD0;
	half4 vcolor						: TEXCOORD1;
	half3 normalWorld					: TEXCOORD2;
	half3 posWorld						: TEXCOORD3;
	LIGHTING_COORDS(4,5)
	half2 XheightYcutoff				: TEXCOORD6;
	half4 tangentWorld					: TEXCOORD7;
#ifdef HAS_FEATURE_ATMOSPHERICS
	VOLUND_SCATTER_COORDS(8, 9)
#else
	UNITY_FOG_COORDS(8)
#endif
};

VertexOutputForwardBase vertForwardBase(a2v v) {
	VertexOutputForwardBase o;
	UNITY_INITIALIZE_OUTPUT(VertexOutputForwardBase, o);

	float fade;
	buildVertexPos(v, v.vertex, v.normal, v.tangent, fade);
	
	o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
	o.tex.xy = v.uv.xy;
	o.tex.zw = fade;
	o.vcolor = v.color * /*_Color **/ half4(2.f, 2.f, 2.f, 1.f);
	o.posWorld = mul(_Object2World, v.vertex);
	o.normalWorld = mul((float3x3)_Object2World, v.normal);
	float a = dot(_WorldSpaceLightPos0.xyz, o.posWorld.xyz - _WorldSpaceCameraPos.xyz);
	o.normalWorld = normalize(o.normalWorld + float3(0.f, 0.033f, 0.f) * max(0.f, a));
	o.tangentWorld = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
	o.XheightYcutoff.x = v.uv2.y;
	o.XheightYcutoff.y = _Cutoff;
#ifdef OPAQUEPASS
		o.XheightYcutoff.y = lerp(_CutoffNear, o.XheightYcutoff.y, saturate(o.pos.z/30.f));
#endif

	TRANSFER_VERTEX_TO_FRAGMENT(o);
#ifdef HAS_FEATURE_ATMOSPHERICS
	VOLUND_TRANSFER_SCATTER(o.posWorld.xyz, o);
#else
	UNITY_TRANSFER_FOG(o, o.pos);
#endif
	return o;   
}

struct iscatter {
	float2 pos;
	half4 scatterCoords1;
#if defined(ATMOSPHERICS_OCCLUSION)
	half3 scatterCoords2;
#endif
};

half4 fragForwardBase(VertexOutputForwardBase i,
	float vface : VFACE
#ifndef SHADER_API_D3D11
	, UNITY_VPOS_TYPE vpos : VPOS
#endif
) : SV_Target {

// Not yet handled
#ifndef USING_DIRECTIONAL_LIGHT
	return 0;
#endif
	
	half3 lightColor = _LightColor0.rgb;
	half3 lightDir = _WorldSpaceLightPos0.xyz;

	half3 eyeVec = normalize(i.posWorld.xyz - _WorldSpaceCameraPos.xyz);
	

	half4 mainTex = tex2D(_MainTex, i.tex.xy);
	mainTex.a *= i.tex.z; // fade

	clip(mainTex.a - i.XheightYcutoff.y);
		
	half3 specColor = 0;
	
	half atten = LIGHT_ATTENUATION(i);
	//atten *= i.vcolor.a;
	atten = min(i.vcolor.a, atten);

	half3 ambient = tex2D(_GITexture, i.posWorld.xz * _GITexture_ST.xy + _GITexture_ST.zw).rgb;

	float4 output = mainTex;

	half3 viewDir = -eyeVec;
	half3 halfDir = normalize (lightDir + viewDir);

	half3 normalTangent = UnpackScaleNormal(tex2D (_BmpMap, i.tex.xy), 1.f);
	//i.normalWorld = normalize(i.normalWorld + float3(0.f, 0.5f, 0.f));

	float3 tnrm = i.normalWorld;
	if(dot(viewDir, tnrm) < 0)
		tnrm = -tnrm;
		
	float3x3 tanWorld = transpose(float3x3(i.tangentWorld.xyz, -cross(i.tangentWorld.xyz, -i.normalWorld), tnrm));

	float3 worldBump = mul(tanWorld, normalTangent); 

	float dLH = pow(BlinnTerm(worldBump, halfDir), _SpecPower);
	float dLN = dot(worldBump, lightDir); 
	if(dLN > 0.f)
		dLN = (dLN + 0.5) / 1.5;
	else
		dLN = 0.2 - dLN * 0.6;
//		dLN = 0.2 + (dLN + 1.0) / 2.0;
		
	i.XheightYcutoff.x = saturate(i.XheightYcutoff.x * i.XheightYcutoff.x * i.XheightYcutoff.x);

	dLH *= atten * i.XheightYcutoff.x;
	dLN *= (atten + _DirectOcclusionBoost);

	float transmitted = min(1.f, pow(max(0.f, -dot(viewDir.xz, lightDir.xz)), 4.f) * 0.95f + 0.0f);
	if(dLN < 0)
		dLN *= -transmitted;;
		
	output.rgb = mainTex.rgb * i.vcolor.rgb * (dLN * lightColor + ambient) + dLH * lightColor * _SpecColor.rgb;

#if defined(FORWARD_ADD) && !(defined(DBG_NONE) || defined(DBG_LIGHTING))
	return float4(0,0,0,0);
#endif
#ifdef DBG_ALBEDO
	return mainTex;
#endif
#ifdef DBG_VTXCOLOR
	return half4(i.vcolor.rgb, mainTex.a);
#endif
#ifdef DBG_DIFFUSE
	return half4(mainTex.rgb * i.vcolor.rgb, mainTex.a);
#endif
#ifdef DBG_VTXOCCLUSION
	return half4(i.vcolor.aaa, mainTex.a);
#endif
#ifdef DBG_INDIRECT
	return half4(ambient, mainTex.a);
#endif
#ifdef DBG_SPECULAR
	return half4(dLH * lightColor * _SpecColor.rgb, mainTex.a);
#endif
#ifdef DBG_LIGHTING
	//return half4(dLN.rrr, mainTex.a);
	return half4(dLN * lightColor + ambient + dLH * lightColor * _SpecColor.rgb, mainTex.a);
#endif

#ifdef FORWARD_ADD
	output.rgb = mainTex.rgb * i.vcolor.rgb * (dLN * lightColor) + dLH * lightColor * _SpecColor.rgb;
	return output;
#endif

#ifdef HAS_FEATURE_ATMOSPHERICS
	iscatter is;
#ifndef SHADER_API_D3D11
	is.pos = vpos.xy;
#else
	is.pos = i.pos;
#endif
	is.scatterCoords1 = i.scatterCoords1;
#if defined(ATMOSPHERICS_OCCLUSION)
	is.scatterCoords2 = i.scatterCoords2;
#endif
	VOLUND_APPLY_SCATTER(is, output.rgb);
#else
	UNITY_APPLY_FOG(i.fogCoord, output.rgb);
#endif

	return output;
}


#endif//FILE_UNDERGROWTH_CGINC