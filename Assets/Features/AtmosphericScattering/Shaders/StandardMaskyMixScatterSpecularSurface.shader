Shader "Volund/Standard MaskyMix Scatter (Specular, Surface)" {
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
	[HideInInspector] _SmoothnessInAlbedo ("__smoothnessinalbedo", Float) = 0.0
	_MaskyMixAlbedo("MaskyMix Albedo (RGB) Gloss(A)", 2D) = "white" {}
	_MaskyMixUVTile("MaskyMix UV Tile", Range (1.0, 200.0)) = 10.0
	_MaskyMixColor("MaskyMix Color (2x)", Color) = (0.5, 0.5, 0.5, 0.5)
	_MaskyMixSpecColor("MaskyMix Spec Color", Color) = (0.2,0.2,0.2)	
	_MaskyMixBumpMap("MaskyMix Bump", 2D) = "bump" {}
	_MaskyMixBumpScale("MaskyMix Bump Scale", Range (0.1, 2.0)) = 1.0		
	_MaskyMixWorldDirection("MaskyMix World Dir", Vector) = (0,1,0,0)
	_MaskyMixMask("MaskyMix Mask (R)", 2D) = "white" {}
	_MaskyMixMaskTile("MaskyMix Mask UV Tile", Range (1.0, 100.0)) = 10.0
	_MaskyMixMaskFalloff("MaskyMix Mask Falloff", Range (0.0001, 2.5)) = 0.1
	_MaskyMixMaskThresholdLow("MaskyMix Mask Threshold Low", Range (0.0, 1.0)) = 0.5
	_MaskyMixMaskThresholdHi("MaskyMix Mask Threshold Hi", Range (0.0, 1.0)) = 0.6
	_MetaAlbedoDesaturation("Meta Albedo Desaturation", Range (0.0, 1.0)) = 0.0
	[HDR] _MetaAlbedoTint("Meta Albedo Tint", Color) = (1.0,1.0,1.0,1.0)
	[HDR] _MetaAlbedoAdd("Meta Albedo Add", Color) = (0.0,0.0,0.0,0.0)
}

Category {
	Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }
	Blend [_SrcBlend] [_DstBlend]
	ZWrite [_ZWrite]

	CGINCLUDE
		// - Physically based Standard lighting model, specular workflow
		// - 'fullforwardshadows' to enable shadows on all light types
		// - 'addshadow' to ensure alpha test works for depth/shadow passes
		// - 'keepalpha' to allow alpha blended output options
		// - Custom vertex function to setup detail UVs as expected by Standard shader,
		//   and to setup masky and atmospheric scattering.
		// - Custom finalcolor function to output controlled final alpha, and to apply
		//   atmospheric scattering.
		// - 'exclude_path:prepass' since we have no use for this legacy path
		#pragma surface SurfSpecular StandardSpecular fullforwardshadows addshadow keepalpha vertex:MaskyMixVertex finalcolor:StandardScatterSurfaceSpecularFinal exclude_path:prepass
		
		// Standard shader feature variants
		#pragma shader_feature _NORMALMAP
		#pragma shader_feature _ _ALPHATEST_ON
		#pragma shader_feature _DETAIL_MULX2

		// Standard, but unused in this project
		//#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
		//#pragma shader_feature _SPECGLOSSMAP
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
		
		// Full vertex color optional to avoid running out of interpolators in certain variants
		// (which we'd then have to exclude or no-op for the shader to compile)
		//#define USE_VERTEX_COLOR
		
		// We need vertex color for masky mixing
		struct SurfInput {
			float4	texcoord;
	#ifdef _PARALLAXMAP
			half3	viewDir;
	#endif
	
	#ifdef USE_VERTEX_COLOR
			half4	vcolor;
	#else
			half	vcolor;
	#endif
	
			float3	worldNormal;
			INTERNAL_DATA
			
			SURFACE_SCATTER_COORDS
		#if !defined(ATMOSPHERICS) && defined(FOG_EXP2)
			#define fogCoord scatterCoords1.x
		#endif
		};
		#define Input SurfInput

		// Include all the Standard shader surface helpers
		#include "UnityStandardSurface.cginc"
		
		// Our masky uniforms
		uniform sampler2D	_MaskyMixAlbedo;
		uniform float4		_MaskyMixColor;
		uniform float4		_MaskyMixSpecColor;
		uniform float		_MaskyMixUVTile;
		uniform sampler2D	_MaskyMixBumpMap;
		uniform float		_MaskyMixBumpScale;
		uniform float3		_MaskyMixWorldDirection;
		uniform sampler2D	_MaskyMixMask;
		uniform float		_MaskyMixMaskTile;
		uniform float		_MaskyMixMaskFalloff;
		uniform float		_MaskyMixMaskThresholdLow;
		uniform float		_MaskyMixMaskThresholdHi;
		
		// Meta uniforms
		uniform float		_MetaAlbedoDesaturation;
		uniform float3		_MetaAlbedoTint;
		uniform float3		_MetaAlbedoAdd;

		// Custom vertex function to setup vertex color
		void MaskyMixVertex (inout appdata_full v, out Input o)
		{
			// Invoke the actual vertex mod function
			StandardSurfaceVertex (v, o);
			
			// Setup vertex color
			o.vcolor.r = saturate(v.color.a + _MaskyMixColor.a * 2.f - 1.f);
	#ifdef USE_VERTEX_COLOR
			o.vcolor.gba = v.color.rgb * _MaskyMixColor.rgb * 2.f;
	#endif
			
			// Scatter transfer
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
		
		// Our main surface entry point
		void SurfSpecular(Input IN, inout SurfaceOutputStandardSpecular o)
		{
			StandardSurfaceSpecular(IN, o);

			// Optionally sample smoothness from albedo texture alpha channel instead of sg texture
#ifdef SMOOTHNESS_IN_ALBEDO
			o.Smoothness = tex2D(_MainTex, IN.texcoord.xy).a;
#endif

			// Apply masky-mixing (unless in meta pass - see below)
#ifndef UNITY_PASS_META
			float2 mainUV = IN.texcoord.xy * _MaskyMixUVTile;
			float2 maskUV = IN.texcoord.xy * _MaskyMixMaskTile;

			float3 normalTangent = UnpackScaleNormal(tex2D(_MaskyMixBumpMap, mainUV), _MaskyMixBumpScale);
			float3 normalTangentBase = UnpackScaleNormal(tex2D(_BumpMap, IN.texcoord.xy), _BumpScale);
			normalTangent = BlendNormals(normalTangentBase, normalTangent);
			
			float4 albedoGloss = tex2D(_MaskyMixAlbedo, mainUV);
#ifdef USE_VERTEX_COLOR
			albedoGloss.rgb *= IN.vcolor.gba;
#else
			albedoGloss.rgb *= _MaskyMixColor.rgb * 2.f;
#endif
			
			float mask = tex2D(_MaskyMixMask, maskUV).r;		
			float facingFactor = dot(_MaskyMixWorldDirection, WorldNormalVector(IN, o.Normal));
			float finalMask = mask * IN.vcolor.r * max(0.f, facingFactor);
			float sharpMask = pow(finalMask, _MaskyMixMaskFalloff);
			bool isMossy = sharpMask > _MaskyMixMaskThresholdLow;

			UNITY_BRANCH
			if(isMossy) {
				float softMask = smoothstep(_MaskyMixMaskThresholdLow, _MaskyMixMaskThresholdHi, sharpMask);
				o.Normal = lerp(o.Normal, normalTangent, softMask);
				o.Albedo = lerp(o.Albedo, albedoGloss.rgb, softMask);
				o.Specular = lerp(o.Specular, _MaskyMixSpecColor.rgb, softMask);
				o.Smoothness = lerp(o.Smoothness, albedoGloss.a, softMask);				
			}
#else //UNITY_PASS_META
			// Unity doesn't actually pass proper world transformations to the meta pass, so instead of 
			// normal-based maskymixing, we'll just have to make do with some additional albedo color controls.
			o.Albedo = lerp(o.Albedo, Luminance(o.Albedo).rrr, _MetaAlbedoDesaturation) * _MetaAlbedoTint + _MetaAlbedoAdd;
#endif //UNITY_PASS_META
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
