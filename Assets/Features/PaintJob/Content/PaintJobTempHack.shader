Shader "Hidden/Volund/Reproject" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Color ("Main Color", Color) = (1,1,1,1)
		_HackCullScale("HackCullScale", float) = 1.0		
	}
	SubShader {
		Tags { "RenderType"="OpaqueTerrainWrap" }
		LOD 200
		
		Offset -1, -1
		
		CGPROGRAM
		#pragma surface surf Lambert vertex:vert

		#include "UnityCG.cginc"
			
		struct Input {
			float2 uv_MainTex;
		};

		uniform float _HackCullScale;
		sampler2D _MainTex;
		uniform fixed4 _Color;

		void vert (inout appdata_full v, out Input o) {
			o.uv_MainTex = v.texcoord;
			v.vertex *= _HackCullScale;
		}

		void surf (Input IN, inout SurfaceOutput o) {
			half4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			o.Alpha = c.a;
		}
		ENDCG
	} 
	Fallback "Legacy Shaders/VertexLit"
}
