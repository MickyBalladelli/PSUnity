Shader "Hidden/Volund/UndergrowthGroundCapture" {
SubShader {
	Tags { "RenderType"="Opaque" }
	
	Pass {  
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma only_renderers d3d11 d3d9 opengl

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Color;
			
			v2f vert(appdata_t v) {
				v2f o;
				o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				o.texcoord = v.texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
				return o;
			}
			
			fixed4 frag(v2f i) : SV_Target {
				fixed4 col = tex2D(_MainTex, i.texcoord) * _Color * 2.f;
				return col;
			}
		ENDCG
	}
}}