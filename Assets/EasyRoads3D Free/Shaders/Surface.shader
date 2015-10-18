// Upgrade NOTE: replaced 'PositionFog()' with multiply of UNITY_MATRIX_MVP by position
// Upgrade NOTE: replaced 'V2F_POS_FOG' with 'float4 pos : SV_POSITION'

Shader "EasyRoads3D/EasyRoads3D Surface" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_MainTex ("Base (RGB)", 2D) = "white" {}
}

Category {
	/* Upgrade NOTE: commented out, possibly part of old style per-pixel lighting: Blend AppSrcAdd AppDstAdd */
	Fog { Color [_AddFog] }
	Tags { "Queue"="Overlay+1"} 
	ZTest Always

	
	// ------------------------------------------------------------------
	// Radeon 7000
	
	Category {
		Material {
			Diffuse [_Color]
			Emission [_PPLAmbient]
		}
		Lighting On

	}
}

Fallback "VertexLit", 2

}
