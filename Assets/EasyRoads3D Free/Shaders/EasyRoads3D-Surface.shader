Shader "EasyRoads3D/EasyRoads3D Surface" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
}
SubShader {
	Tags { "Queue"="Overlay+25" }
	LOD 200
//	Offset -5, -5
ZTest Always

CGPROGRAM
#pragma surface surf Lambert

fixed4 _Color;

struct Input {
	float2 uv_MainTex;
};

void surf (Input IN, inout SurfaceOutput o) {
	fixed4 c = _Color;
	o.Albedo = c.rgb;
	o.Alpha = c.a;
}
ENDCG
}

Fallback "VertexLit"
}




