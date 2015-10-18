// Upgrade NOTE: replaced 'PositionFog()' with multiply of UNITY_MATRIX_MVP by position
// Upgrade NOTE: replaced 'V2F_POS_FOG' with 'float4 pos : SV_POSITION'

Shader "EasyRoads3D/EasyRoads3D Surface Transparant" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
}

Category {
	Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"  "Queue"="Overlay+1"}
	LOD 200
	Alphatest Greater 0
	ZWrite Off
	ZTest Always
	ColorMask RGB
	


}

// Fallback to Alpha Vertex Lit
Fallback "Transparent/VertexLit", 2

}