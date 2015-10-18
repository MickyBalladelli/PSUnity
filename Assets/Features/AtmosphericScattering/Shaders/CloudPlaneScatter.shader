Shader "Volund/Cloud Scatter" {
Properties {
	_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	_MainTex ("Particle Texture", 2D) = "white" {}
	_InvFade ("Soft Particles Factor", Range(0.01,3.0)) = 1.0
	_ScrollUV ("Scroll UVs (units/sec: U, V, _, _)", Vector) = (0,0,0,0)
}

Category {
	Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
	Blend SrcAlpha OneMinusSrcAlpha
	Cull Off Lighting Off ZWrite Off
 
	SubShader {
		Pass {
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_particles
			#pragma multi_compile _ ATMOSPHERICS ATMOSPHERICS_PER_PIXEL
			#pragma multi_compile _ ATMOSPHERICS_OCCLUSION
			#pragma multi_compile _ ATMOSPHERICS_OCCLUSION_EDGE_FIXUP
			#pragma multi_compile _ ATMOSPHERICS_DEBUG

			#pragma only_renderers d3d11 d3d9 opengl

			#include "UnityCG.cginc"
			#include "../Code/AtmosphericScattering.cginc"

			sampler2D _MainTex;
			fixed4 _TintColor;
			float2 _ScrollUV;
			
			struct appdata_t {
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				VOLUND_SCATTER_COORDS(1,3)
				#ifdef SOFTPARTICLES_ON
				float4 projPos : TEXCOORD2;
				#endif
			};
			
			float4 _MainTex_ST;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
				#ifdef SOFTPARTICLES_ON
				o.projPos = ComputeScreenPos (o.pos);
				COMPUTE_EYEDEPTH(o.projPos.z);
				#endif
				o.color = v.color;
				o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex) + _ScrollUV.xy * _Time.y;
				VOLUND_TRANSFER_SCATTER(mul(_Object2World, v.vertex), o);
				return o;
			}

			float _InvFade;
			
			fixed4 frag (v2f i) : SV_Target
			{
				#ifdef SOFTPARTICLES_ON
				float sceneZ = LinearEyeDepth (SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
				float partZ = i.projPos.z;
				float fade = saturate (_InvFade * (sceneZ-partZ));
				i.color.a *= fade;
				#endif
				
				fixed4 col = 2.0f * i.color * _TintColor * tex2D(_MainTex, i.texcoord);
				VOLUND_CLOUD_SCATTER(i, col);
				clip(col.a - 0.01f);
				return col;
			}
			ENDCG 
		}
	}	
}
}

