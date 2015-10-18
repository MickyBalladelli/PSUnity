Shader "Volund/Standard Scatter (Specular, Surface)" {
Properties {
	_Color("Color", Color) = (1,1,1,1)
	_MainTex("Albedo", 2D) = "white" {}
	
	_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

	_Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
	_SpecColor("Specular", Color) = (0.2,0.2,0.2)
	_SpecGlossMap("Specular", 2D) = "white" {}

	_BumpScale("Scale", Float) = 1.0
	_BumpMap("Normal Map", 2D) = "bump" {}
	
	_Parallax ("Height Scale", Range (0.005, 0.08)) = 0.02
	_ParallaxMap ("Height Map", 2D) = "black" {}
	
	_OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
	_OcclusionMap("Occlusion", 2D) = "white" {}
	
	_EmissionColor("Color", Color) = (0,0,0)
	_EmissionMap("Emission", 2D) = "white" {}
	
	_DetailMask("Detail Mask", 2D) = "white" {}
	
	_DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
	_DetailNormalMapScale("Scale", Float) = 1.0
	_DetailNormalMap("Normal Map", 2D) = "bump" {}

	[Enum(UV0,0,UV1,1)] _UVSec ("UV Set for secondary textures", Float) = 0

	// Blending state
	[HideInInspector] _Mode ("__mode", Float) = 0.0
	[HideInInspector] _SrcBlend ("__src", Float) = 1.0
	[HideInInspector] _DstBlend ("__dst", Float) = 0.0
	[HideInInspector] _ZWrite ("__zw", Float) = 1.0
	
	// Volund properties
	[HideInInspector] _CullMode ("__cullmode", Float) = 2.0
	[HideInInspector] _SmoothnessInAlbedo ("__smoothnessinalbedo", Float) = 0.0
	_MetaAlbedoDesaturation("Meta Albedo Desaturation", Range (0.0, 1.0)) = 0.0
	[HDR] _MetaAlbedoTint("Meta Albedo Tint", Color) = (1.0,1.0,1.0,1.0)
	[HDR] _MetaAlbedoAdd("Meta Albedo Add", Color) = (0.0,0.0,0.0,0.0)
}

Category {
	Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }
	Blend [_SrcBlend] [_DstBlend]
	ZWrite [_ZWrite]
	Cull [_CullMode]

	CGINCLUDE
		// - Physically based Standard lighting model, specular workflow
		// - 'fullforwardshadows' to enable shadows on all light types
		// - 'addshadow' to ensure alpha test works for depth/shadow passes
		// - 'keepalpha' to allow alpha blended output options
		// - Custom vertex function to setup detail UVs as expected by Standard shader,
		//   and to setup atmospheric scattering.
		// - Custom finalcolor function to output controlled final alpha, and to apply
		//   atmospheric scattering.
		// - 'exclude_path:prepass' since we have no use for this legacy path
		#pragma surface SurfSpecular StandardSpecular fullforwardshadows addshadow keepalpha vertex:StandardScatterSurfaceVertex finalcolor:StandardScatterSurfaceSpecularFinal exclude_path:prepass
		
		// Standard shader feature variants
		#pragma shader_feature _NORMALMAP
		#pragma shader_feature _ _ALPHATEST_ON
		#pragma shader_feature _SPECGLOSSMAP
		#pragma shader_feature _DETAIL_MULX2
		
		// Standard, but unused in this project
		//#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
		//#pragma shader_feature _EMISSION
		//#pragma shader_feature _PARALLAXMAP
				
		// Volund additional variants
		#pragma shader_feature SMOOTHNESS_IN_ALBEDO
		#pragma multi_compile _ ATMOSPHERICS_DEBUG
		
		// Unused in this project (always set to desired config)
//		#pragma multi_compile _ ATMOSPHERICS ATMOSPHERICS_PER_PIXEL
//		#pragma multi_compile _ ATMOSPHERICS_OCCLUSION
//		#pragma multi_compile _ ATMOSPHERICS_OCCLUSION_EDGE_FIXUP
		#pragma multi_compile _ ATMOSPHERICS FOG_EXP2
		#define ATMOSPHERICS_OCCLUSION
		#define ATMOSPHERICS_OCCLUSION_EDGE_FIXUP

		// Include scattering functions
		#include "../Code/AtmosphericScattering.cginc"

		struct ScatterInput
		{
			float4	texcoord;

		#ifdef _PARALLAXMAP
			half3	viewDir;
		#endif
			float	vface : VFACE;

			SURFACE_SCATTER_COORDS
		#if !defined(ATMOSPHERICS) && defined(FOG_EXP2)
			#define fogCoord scatterCoords1.x
		#endif
		};
		#define Input ScatterInput

		// Include all the Standard shader surface helpers
		#include "UnityStandardSurface.cginc"

		void StandardScatterSurfaceVertex (inout appdata_full v, out Input o) {
			StandardSurfaceVertex(v, o);
			
			SURFACE_SCATTER_TRANSFER(mul(_Object2World, v.vertex).xyz, o);
		#if !defined(ATMOSPHERICS) && defined(FOG_EXP2)
			UNITY_TRANSFER_FOG(o, mul(UNITY_MATRIX_MVP, v.vertex));
		#endif
		}

		void StandardScatterSurfaceSpecularFinal(Input IN, SurfaceOutputStandardSpecular o, inout half4 color) {
			StandardSurfaceSpecularFinal(IN, o, color);
			SURFACE_SCATTER_APPLY(IN, color.rgb);
		#if !defined(ATMOSPHERICS) && defined(FOG_EXP2)
			UNITY_APPLY_FOG(IN.fogCoord, color.rgb);
		#endif
		}
		
		uniform float	_CullMode;
		uniform float	_MetaAlbedoDesaturation;
		uniform float3	_MetaAlbedoTint;
		uniform float3	_MetaAlbedoAdd;

		// Our main surface entry point
		void SurfSpecular(Input IN, inout SurfaceOutputStandardSpecular o) {
			StandardSurfaceSpecular(IN, o);

			// Experimental normal flipping
			if(_CullMode < 0.5f)
				o.Normal.z *= IN.vface;

			// Optionally sample smoothness from albedo texture alpha channel instead of sg texture
#ifdef SMOOTHNESS_IN_ALBEDO
			o.Smoothness = tex2D(_MainTex, IN.texcoord.xy).a;
#endif

#ifdef UNITY_PASS_META
			// Apply additional color controls to meta pass albedo output.
			o.Albedo = lerp(o.Albedo, Luminance(o.Albedo).rrr, _MetaAlbedoDesaturation) * _MetaAlbedoTint + _MetaAlbedoAdd;
#endif

		}
	ENDCG

	SubShader {
		LOD 400
		
		CGPROGRAM
			// Use shader model 5.0 to get access to gather4 instruction (Unity has no 4.1 profile)
			#pragma target 5.0
			#pragma only_renderers d3d11
		ENDCG
	}

	SubShader {
		LOD 300
		
		CGPROGRAM
			// Use shader model 3.0 to get nicer looking lighting (PBS toggles internally on shader model)
			#pragma target 3.0
			#pragma only_renderers d3d9 opengl			
		ENDCG
	}
	
	CustomEditor "VolundMultiStandardShaderGUI"
	FallBack "Diffuse"
}
}
