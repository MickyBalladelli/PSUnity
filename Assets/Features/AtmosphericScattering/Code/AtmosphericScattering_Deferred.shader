Shader "Hidden/AtmosphericScattering_Deferred" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "black" {}
	}

	CGINCLUDE
	#pragma vertex vert
	#pragma fragment frag

	#pragma multi_compile _ ATMOSPHERICS ATMOSPHERICS_PER_PIXEL
	#pragma multi_compile _ ATMOSPHERICS_OCCLUSION
	#pragma multi_compile _ ATMOSPHERICS_OCCLUSION_EDGE_FIXUP
	#pragma multi_compile _ ATMOSPHERICS_DEBUG

	#include "UnityCG.cginc"
	#include "AtmosphericScattering.cginc"

	uniform sampler2D		_MainTex;
	uniform float4			_MainTex_TexelSize;
	
	uniform float4x4		_FrustumCornersWS;
	uniform float4			_CameraWS;

	struct v2f {
		float4 pos				: SV_POSITION;
		float2 uv				: TEXCOORD0;
		float2 uv_depth			: TEXCOORD1;
		float4 interpolatedRay	: TEXCOORD2;
	};
	
	v2f vert(appdata_img v) {
		v2f o;
		half index = v.vertex.z;
		v.vertex.z = 0.1;
		o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
		o.uv = v.texcoord.xy;
		o.uv_depth = v.texcoord.xy;
		
#if UNITY_UV_STARTS_AT_TOP
		if (_MainTex_TexelSize.y < 0.f)
			o.uv.y = 1.f - o.uv.y;
#endif
		
		o.interpolatedRay = _FrustumCornersWS[(int)index];
		o.interpolatedRay.w = index;
		
		return o;
	}

	struct ScatterInput {
		float2 pos;
		half4 scatterCoords1;
		half3 scatterCoords2;
	};
	
	half4 frag(
		v2f i,
#ifdef SHADER_API_D3D11
		UNITY_VPOS_TYPE vpos : SV_Position
#else
		UNITY_VPOS_TYPE vpos : VPOS
#endif
	) : SV_Target {
		half4 sceneColor = tex2D(_MainTex, i.uv);	
		float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv_depth);
	
		// Don't double-scatter on skybox
		UNITY_BRANCH
		if(rawDepth > 0.9999999f)
			return sceneColor;

		float depth = Linear01Depth(rawDepth);
		float4 wsDir = depth * i.interpolatedRay;
		float4 wsPos = _CameraWS + wsDir;
		
		// Apply scattering
		ScatterInput si;
		si.pos = vpos.xy; 
		VOLUND_TRANSFER_SCATTER(wsPos.xyz, si);
		VOLUND_APPLY_SCATTER(si, sceneColor.rgb);
		
		return sceneColor;
	}
	ENDCG

	SubShader {
		ZTest Always Cull Off ZWrite Off		
		Pass {
			CGPROGRAM
				#pragma target 5.0
				#pragma only_renderers d3d11
			ENDCG
		}
	}

	SubShader {
		ZTest Always Cull Off ZWrite Off		
		Pass {
			CGPROGRAM
				#pragma target 3.0
				#pragma only_renderers d3d9 opengl
			ENDCG
		}
	}

	Fallback off
}
