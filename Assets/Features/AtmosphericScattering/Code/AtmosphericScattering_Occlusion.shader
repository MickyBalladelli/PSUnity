Shader "Hidden/AtmosphericScattering_Occlusion" {
CGINCLUDE
	#pragma target 3.0
	#pragma only_renderers d3d11 d3d9 opengl
	
	#pragma multi_compile _ ATMOSPHERICS_OCCLUSION
	#pragma multi_compile _ ATMOSPHERICS_OCCLUSION_FULLSKY
	
	#if !defined(SHADER_API_D3D11)
		#undef ATMOSPHERICS_OCCLUSION_FULLSKY
	#endif
	
	/* this forces the HW PCF path required for correctly sampling the cascaded shadow map
	   render texture (a fix is scheduled for 5.2) */
	#pragma multi_compile SHADOWS_NATIVE

	#include "UnityCG.cginc"
	#include "AtmosphericScattering.cginc"

	UNITY_DECLARE_SHADOWMAP	(u_CascadedShadowMap);	
	uniform float3 			u_CameraPosition;
	uniform float3 			u_ViewportCorner;
	uniform float3 			u_ViewportRight;
	uniform float3 			u_ViewportUp;
	uniform sampler2D		u_CollectedOcclusionData;
	uniform float4			u_CollectedOcclusionData_TexelSize;
	uniform float4			u_CollectedOcclusionDataScaledTexelSize;
	uniform float			u_OcclusionSkyRefDistance;
	
	struct v2f {
		float4 pos	: SV_POSITION;
		float2 uv	: TEXCOORD0;
		float3 ray	: TEXCOORD2;
	};
	
	v2f vert(appdata_img v) {
		v2f o;
		o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
		o.uv = v.texcoord.xy;
		o.ray = u_ViewportCorner + o.uv.x * u_ViewportRight + o.uv.y * u_ViewportUp;
		return o;
	}
	
	inline fixed4 getCascadeWeights_splitSpheres(float3 wpos) {
		float3 fromCenter0 = wpos.xyz - unity_ShadowSplitSpheres[0].xyz;
		float3 fromCenter1 = wpos.xyz - unity_ShadowSplitSpheres[1].xyz;
		float3 fromCenter2 = wpos.xyz - unity_ShadowSplitSpheres[2].xyz;
		float3 fromCenter3 = wpos.xyz - unity_ShadowSplitSpheres[3].xyz;
		float4 distances2 = float4(dot(fromCenter0,fromCenter0), dot(fromCenter1,fromCenter1), dot(fromCenter2,fromCenter2), dot(fromCenter3,fromCenter3));
#if !defined(SHADER_API_D3D11)
		fixed4 weights = float4(distances2 < unity_ShadowSplitSqRadii);
		weights.yzw = saturate(weights.yzw - weights.xyz);
#else
		fixed4 weights = float4(distances2 >= unity_ShadowSplitSqRadii);
#endif
		return weights;
	}

	inline float4 getShadowCoord(float4 wpos, fixed4 cascadeWeights) {
#if defined(SHADER_API_D3D11)
		return mul(unity_World2Shadow[(int)dot(cascadeWeights, float4(1,1,1,1))], wpos);
#else
		float3 sc0 = mul(unity_World2Shadow[0], wpos).xyz;
		float3 sc1 = mul(unity_World2Shadow[1], wpos).xyz;
		float3 sc2 = mul(unity_World2Shadow[2], wpos).xyz;
		float3 sc3 = mul(unity_World2Shadow[3], wpos).xyz;
		return float4(sc0 * cascadeWeights[0] + sc1 * cascadeWeights[1] + sc2 * cascadeWeights[2] + sc3 * cascadeWeights[3], 1);
#endif
	}

	float frag_collect(const v2f i, const int it) {
		const float itF = 1.f / (float)it;
		const float itFM1 = 1.f / (float)(it - 1);
		
		float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
		
#if !defined(ATMOSPHERICS_OCCLUSION_FULLSKY)
		UNITY_BRANCH
		if(rawDepth > 0.9999999f)
			return 0.75f;
#endif
			
		float occlusion = 0.f;
#if defined(ATMOSPHERICS_OCCLUSION_FULLSKY)
		UNITY_BRANCH
		if(rawDepth > 0.9999999f) {
			float3 worldDir = i.ray * u_OcclusionSkyRefDistance;			
			float4 worldPos = float4(0.f, 0.f, 0.f, 1.f);
			
			float fracStep = 0.f;
			for(int i = 0; i < it; ++i, fracStep += itF) {
				worldPos.xyz = u_CameraPosition + worldDir * fracStep * fracStep;
				
				float4 cascadeWeights = getCascadeWeights_splitSpheres(worldPos.xyz);
				bool inside = dot(cascadeWeights, float4(1,1,1,1)) < 4;
				float3 samplePos = getShadowCoord(worldPos, cascadeWeights);
				occlusion += inside ? UNITY_SAMPLE_SHADOW(u_CascadedShadowMap, samplePos) : 1.f;
			}
		} else
#endif
		{
			float depth = Linear01Depth(rawDepth);
			float3 worldDir = i.ray * depth;
			
			float4 worldPos = float4(u_CameraPosition + worldDir, 1.f);
			float3 deltaStep = -worldDir * itFM1;
			
			for(int i = 0; i < it; ++i, worldPos.xyz += deltaStep) {
				float4 cascadeWeights = getCascadeWeights_splitSpheres(worldPos.xyz);
				bool inside = dot(cascadeWeights, float4(1,1,1,1)) < 4;
				float3 samplePos = getShadowCoord(worldPos, cascadeWeights);
				occlusion += inside ? UNITY_SAMPLE_SHADOW(u_CascadedShadowMap, samplePos) : 1.f;
			}
		}

		return occlusion * itF;
	}
	
	fixed4 frag_collect64(v2f i) : SV_Target { return frag_collect(i, 64); }
	fixed4 frag_collect164(v2f i) : SV_Target { return frag_collect(i, 164); }
	fixed4 frag_collect244(v2f i) : SV_Target { return frag_collect(i, 244); }

ENDCG

SubShader {
	ZTest Always Cull Off ZWrite Off
	
	Pass {
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag_collect64
		ENDCG
	}
	
	Pass {
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag_collect164
		ENDCG
	}
	
	Pass {
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag_collect244
		ENDCG
	}
}
Fallback off
}

