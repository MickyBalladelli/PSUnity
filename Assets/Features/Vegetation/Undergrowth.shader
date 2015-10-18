Shader "Volund/Undergrowth" {
	Properties {
		[LM_Albedo] [LM_Transparency] _Color("Color", Color) = (1,1,1)	
		[LM_Albedo] _MainTex("Diffuse", 2D) = "white" {}
		[LM_TransparencyCutOff] _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		_CutoffNear("Alpha Cutoff Near", Range(0.0, 1.0)) = 0.5
		
		_BmpScale("Scale", Float) = 1.0
		_BmpMap("Normal Map", 2D) = "white" {}

		_GITexture("GI Texture", 2D) = "gray" {}
		_OpacityTex("Opacity Map", 2D) = "white" {}
		_Transmission("Transmission", Range(0.0, 2.0)) = 1.0
		
		_SpecPower("Specular Power", Range(1.0, 128.0)) = 12.0
		_SpecColor("Specular", Color) = (0.2,0.2,0.2)	

		[HideInInspector] _SrcBlend ("__sb", Float) = 5.0
		[HideInInspector] _DstBlend ("__db", Float) = 10.0
		[HideInInspector] _ZWrite ("__zw", Float) = 0.0
		[HideInInspector] _ZTest ("__zt", Float) = 4.0
		[HideInInspector] _ColorMask ("__cm", Float) = 15.0
		[HideInInspector] _DirectOcclusionBoost ("__meh", Float) = 0.0		
		[HideInInspector] _SpriteCameraUp  ("__meh", Float) = 0.0
		[HideInInspector] _DensityFalloffDistance ("__meh", Float) = 0.0
		[HideInInspector] _PruneFreeDistance ("__meh", Float) = 0.0
		[HideInInspector] _PruneDistanceScale ("__meh", Float) = 0.0
		[HideInInspector] _PruneRefCountsSpriteMesh ("__meh", Vector) = (0,0,0,0)
	}

	SubShader {
		Tags { "Queue"="AlphaTest+25" "RenderType"="Transparent" }

CGINCLUDE

struct a2v {
	float4 vertex	: POSITION;
	float4 color	: COLOR;
	float3 normal	: NORMAL;
	float4 tangent	: TANGENT;
	float2 uv		: TEXCOORD0;
	float2 uv2		: TEXCOORD1;
	float2 uv3		: TEXCOORD2;
	float2 uv4		: TEXCOORD3;
};

uniform int		_SpriteCameraUp;
uniform float	_DensityFalloffDistance;
uniform float	_PruneFreeDistance;
uniform float	_PruneDistanceScale;
uniform float2	_PruneRefCountsSpriteMesh;

void buildVertexPos(a2v v, out float4 vtxPos, out float3 vtxNrm, out float4 vtxTan, out float fade) {
	// TODO: This "procedural wind animation" is just a random mess. Better than nothing, 
	//       but not by much - both 'expensive', and totally without external control.
	float2 animOffset1 = float2(0.f, 0.f);
	float2 animOffset2 = v.vertex.xx * 1.f + v.vertex.zz * 0.5f;
	animOffset2.x = (-1.f - pow(abs(sin(_Time.y/2 + animOffset2.x)), 4.0f) - sin(_Time.y)*0.05) * 0.15 * v.uv2.y * v.uv2.y;
	animOffset2.y = sin(_Time.y + animOffset2.y) * 0.1 * v.uv2.y * v.uv2.y;

	const bool isMesh = v.uv3.y < -0.5f;
	const bool isBillboard = v.uv3.y > 0.5f;

	float3 basePos;
	if(isMesh) {
		vtxPos = v.vertex;
		vtxNrm = v.normal.xyz;
		vtxTan = v.tangent;
		
		basePos = float3(v.uv2.x, v.uv4.x, v.uv4.y);
	} else {
		const float3 camUp = UNITY_MATRIX_IT_MV[1].xyz;
		const float3 camRight = UNITY_MATRIX_IT_MV[0].xyz;
		
		float3 up = float3(0.f, 1.f, 0.f);
#ifndef SHADOWCASTER
		if(_SpriteCameraUp == 1) {
			if(isBillboard)
				up = camUp;
			else
				up = (camUp + up) * 0.7f;
		}
#endif
		const float3 right = isBillboard ? camRight : cross(v.normal.xyz, up);
		
		vtxPos = v.vertex;
		vtxPos.xyz += right * v.uv2.x + up * v.uv2.y;
		
		vtxNrm = v.normal.xyz;
		vtxTan.xyz = right;
		vtxTan.w = 1.f;
		
		basePos = v.vertex.xyz;
	}
	
	vtxPos.xz += animOffset1 + animOffset2;
	
	// "Distance to object"
	float baseDist = length(_WorldSpaceCameraPos.xyz - basePos);

	// Prune density based on distance.	
	fade = 1.f;
	float pruneDist = baseDist - _PruneFreeDistance;
	if(pruneDist > 0.f) {
		pruneDist *= _PruneDistanceScale;
		
		const float FadeThreshold = 50.f;
		float maxRef = isMesh ? _PruneRefCountsSpriteMesh.x : _PruneRefCountsSpriteMesh.y;
		if((maxRef - v.uv3.x) < pruneDist) {
			if((maxRef - v.uv3.x + FadeThreshold) < pruneDist) {
				vtxPos = 0;
			} else {
				fade = 1.f - saturate((pruneDist - (maxRef - v.uv3.x)) / FadeThreshold);
			}
		}
	}
	
	// Fade/scale based on distance
	float fadeDist = saturate(baseDist / _DensityFalloffDistance);
	float fadeFalloff = pow(fadeDist, 4.f);
	vtxPos.xyz = lerp(vtxPos.xyz, basePos.xyz, fadeFalloff) * (fadeFalloff >= 0.999f ? 0.f : 1.f);
	fade *= 1.f - fadeFalloff;
}

ENDCG
		Pass {
			Name "FORWARD" 
			Tags { "LightMode" = "ForwardBase" }

			Cull Off
			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]
			ZTest [_ZTest]
			ColorMask [_ColorMask]

			CGPROGRAM

			#pragma target 3.0
			#pragma only_renderers d3d11 d3d9 opengl
			
			#pragma multi_compile_fwdbase nolightmap
			#pragma multi_compile _ OPAQUEPASS
			//#define DBG_NONE
			#pragma multi_compile DBG_NONE DBG_ALBEDO DBG_VTXCOLOR DBG_DIFFUSE DBG_VTXOCCLUSION DBG_INDIRECT DBG_SPECULAR DBG_LIGHTING
			
			#pragma multi_compile _ ATMOSPHERICS FOG_EXP2
			#define ATMOSPHERICS_OCCLUSION
			#define ATMOSPHERICS_OCCLUSION_EDGE_FIXUP

			// Unused in this project (always set to desired config)
//			#pragma multi_compile _ ATMOSPHERICS ATMOSPHERICS_PER_PIXEL
//			#pragma multi_compile _ ATMOSPHERICS_OCCLUSION
//			#pragma multi_compile _ ATMOSPHERICS_OCCLUSION_EDGE_FIXUP
							
			#pragma vertex vertForwardBase
			#pragma fragment fragForwardBase
			
			//#define _ALPHATEST_ON
			//#define _ALPHABLEND_ON
			#include "Undergrowth.cginc"

			ENDCG
		}

		Pass {
			Name "FORWARD_DELTA"
			Tags { "LightMode" = "ForwardAdd" }

			Cull Off
			//Blend SrcAlpha OneMinusSrcAlpha
			//Blend [_SrcBlend] One
			Blend SrcAlpha One
			//Cull [_CullMode]
			ZWrite Off
			ZTest LEqual

			CGPROGRAM

			#pragma target 3.0
			#pragma only_renderers d3d11 d3d9 opengl
			
			#pragma multi_compile_fwdadd_fullshadows nolightmap
			#pragma multi_compile _ OPAQUEPASS
			#define DBG_NONE
			//#pragma multi_compile DBG_NONE DBG_ALBEDO DBG_VTXCOLOR DBG_DIFFUSE DBG_VTXOCCLUSION DBG_INDIRECT DBG_SPECULAR DBG_LIGHTING
			
			#pragma multi_compile _ ATMOSPHERICS FOG_EXP2
			#define ATMOSPHERICS_OCCLUSION
			#define ATMOSPHERICS_OCCLUSION_EDGE_FIXUP

			// Unused in this project (always set to desired config)
//			#pragma multi_compile _ ATMOSPHERICS ATMOSPHERICS_PER_PIXEL
//			#pragma multi_compile _ ATMOSPHERICS_OCCLUSION
//			#pragma multi_compile _ ATMOSPHERICS_OCCLUSION_EDGE_FIXUP
							
			#pragma vertex vertForwardBase
			#pragma fragment fragForwardBase
			
			//#define _ALPHATEST_ON
			//#define _ALPHABLEND_ON
			#define FORWARD_ADD
			#include "Undergrowth.cginc"

			ENDCG
		}
		
		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			
			Cull Off
			ZWrite On ZTest LEqual

			CGPROGRAM
			
			#pragma target 3.0
			#pragma only_renderers d3d11 d3d9 opengl

			#pragma multi_compile_shadowcaster
			#pragma multi_compile _ OPAQUEPASS
			#pragma multi_compile SHADOWCASTER

			#pragma vertex vertShadowCaster2
			#pragma fragment fragShadowCaster2

//#if defined(OPAQUEPASS)
			#define _ALPHATEST_ON
//#else
//			#define _ALPHABLEND_ON
//			//#define UNITY_STANDARD_USE_DITHER_MASK 1
//#endif
			#include "UnityStandardShadow.cginc"
	
			uniform half _CutoffNear;
			
			void vertShadowCaster2(
					a2v v,
					#ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
						out VertexOutputShadowCaster o,
					#endif
					out float4 opos : SV_POSITION)
			{
				float3 n;
				float4 t;
				float a;
				buildVertexPos(v, v.vertex, n, t, a);

				VertexInput vi;
				vi.vertex = v.vertex;
				vi.normal = v.normal;
				vi.uv0 = v.uv.xy;
				vertShadowCaster(
					vi,
					#ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
						o,
					#endif
					opos
				);
				o.tex = v.uv.xy;
			}
			
			half4 fragShadowCaster2 (
				#ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
					VertexOutputShadowCaster i
				#endif
				#ifdef UNITY_STANDARD_USE_DITHER_MASK
					, UNITY_VPOS_TYPE vpos : VPOS
				#endif
			) : SV_Target
			{
				#if defined(UNITY_STANDARD_USE_SHADOW_UVS)
					half alpha = tex2D(_MainTex, i.tex).a;// * _Color.a;
					#if defined(_ALPHATEST_ON)
					#ifdef OPAQUEPASS
						clip (alpha - _CutoffNear);
					#else
						clip (alpha - _Cutoff);
					#endif
					#endif
					#if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
						#if defined(UNITY_STANDARD_USE_DITHER_MASK)
							// Use dither mask for alpha blended shadows, based on pixel position xy
							// and alpha level. Our dither texture is 4x4x16.
							half alphaRef = tex3D(_DitherMaskLOD, float3(vpos.xy*0.25,alpha*0.9375)).a;
							clip (alphaRef - 0.01);
						#else
							clip(alpha - 0.50f);
							//clip (alpha - _Cutoff);
						#endif
					#endif
				#endif // #if defined(UNITY_STANDARD_USE_SHADOW_UVS)

				SHADOW_CASTER_FRAGMENT(i)
			}	

			ENDCG
		}

	}

	FallBack Off
}

