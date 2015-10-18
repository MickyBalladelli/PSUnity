#ifndef UNITY_STANDARD_SURFACE_INCLUDED
#define UNITY_STANDARD_SURFACE_INCLUDED

//-------------------------------------------------------------------------------------
#include "UnityPBSLighting.cginc"
#include "UnityStandardInput.cginc"


//-------------------------------------------------------------------------------------
// Shared PBS surface setup

// Define input struct unless it's already defined
#ifndef Input
	struct Input
	{
		float4	texcoord;

	#ifdef _PARALLAXMAP
		half3	viewDir;
	#endif
	};
#endif

void StandardSurfaceVertex (inout appdata_full v, out Input o)
{
	UNITY_INITIALIZE_OUTPUT(Input, o);
	
	// Setup UVs to the format expected by Standard input functions.
	o.texcoord.xy = TRANSFORM_TEX(v.texcoord, _MainTex);
	o.texcoord.zw = TRANSFORM_TEX(((_UVSec == 0) ? v.texcoord : v.texcoord1), _DetailAlbedoMap);
}


//-------------------------------------------------------------------------------------
// Metallic workflow

void StandardSurface (Input IN, inout SurfaceOutputStandard o) {
#ifdef _PARALLAXMAP
	IN.texcoord = Parallax(IN.texcoord, IN.viewDir);
#endif
	
	o.Alpha = Alpha(IN.texcoord.xy);
#if defined(_ALPHATEST_ON)
	clip(o.Alpha - _Cutoff);
#endif

	o.Albedo = Albedo(IN.texcoord.xyzw);	

#ifdef _NORMALMAP
	o.Normal = NormalInTangentSpace(IN.texcoord.xyzw);
#endif

	half2 metallicGloss = MetallicGloss(IN.texcoord.xy);
	o.Metallic = metallicGloss.x;
	o.Smoothness = metallicGloss.y;

	o.Occlusion = Occlusion(IN.texcoord.xy);
	
#ifdef _EMISSION
	o.Emission = Emission(IN.texcoord.xy);
#endif
}

inline void StandardSurfaceFinal (Input IN, SurfaceOutputStandard o, inout half4 color)
{
#if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
	color.a = Alpha(IN.texcoord.xy);
#else
	UNITY_OPAQUE_ALPHA(color.a);
#endif
}

//-------------------------------------------------------------------------------------
// Specular workflow

void StandardSurfaceSpecular (Input IN, inout SurfaceOutputStandardSpecular o) {
#ifdef _PARALLAXMAP
	IN.texcoord = Parallax(IN.texcoord, IN.viewDir);
#endif
	
	o.Alpha = Alpha(IN.texcoord.xy);
#if defined(_ALPHATEST_ON)
	clip(o.Alpha - _Cutoff);
#endif

	o.Albedo = Albedo(IN.texcoord.xyzw);	

#ifdef _NORMALMAP
	o.Normal = NormalInTangentSpace(IN.texcoord.xyzw);
#endif

	half4 specGloss = SpecularGloss(IN.texcoord.xy);
	o.Specular = specGloss.rgb;
	o.Smoothness = specGloss.a;

	o.Occlusion = Occlusion(IN.texcoord.xy);
	
#ifdef _EMISSION
	o.Emission = Emission(IN.texcoord.xy);
#endif
}

inline void StandardSurfaceSpecularFinal (Input IN, SurfaceOutputStandardSpecular o, inout half4 color)
{	
#if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
	color.a = o.Alpha;
#else
	UNITY_OPAQUE_ALPHA(color.a);
#endif
}

#endif // UNITY_STANDARD_SURFACE_INCLUDED
