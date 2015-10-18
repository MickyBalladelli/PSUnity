Shader "Hidden/TonemapperLut" {
	Properties {
		_MainTex ("", 2D) = "black" {}
		_SmallTex ("", 2D) = "grey" {}
		_Curve ("", 2D) = "black" {}
	}
	
	CGINCLUDE
	
	#include "UnityCG.cginc"
	 
	struct v2f {
		half4 pos : SV_POSITION;
		half2 uv : TEXCOORD0;
	};
	
	sampler2D _MainTex;
	sampler2D _SmallTex;
	sampler2D _Curve;

	sampler3D _LutTex;
	sampler2D _LutTex1D;

	// the user-defined color lut
	sampler3D _ClutTex;
	half _Scale;
	half _Offset;
	
	half _HdrParams;
	half2 intensity;
	half4 _MainTex_TexelSize;
	half _AdaptionSpeed;
	half _ExposureAdjustment;
	half _RangeScale;
	
	half _AdaptionEnabled;
	half _AdaptiveMin;
	half _AdaptiveMax;

	half _LutA;
	half4 _LutExposureMult;

	v2f vert( appdata_img v ) 
	{
		v2f o;
		o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
		o.uv = v.texcoord.xy;
		return o;
	} 

	half LinToPerceptual(half3 color)
	{
		const half DELTA = 0.001f;
		half lum = log(Luminance(color));
 		return (lum);
	}

	half PerceptualToLin(half f)
	{
		return exp(f);
	}

	half4 fragLog(v2f i) : SV_Target 
	{
		half fLogLumSum = 0.0f;
 
		fLogLumSum += LinToPerceptual(tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2(-1,-1)).rgb);		
		fLogLumSum += LinToPerceptual(tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2(1,1)).rgb);		
		fLogLumSum += LinToPerceptual(tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2(-1,1)).rgb);		
		fLogLumSum += LinToPerceptual(tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2(1,-1)).rgb);		

		half avg = fLogLumSum / 4.0;
		return half4(avg, avg, avg, avg);
	}

	half4 fragExp(v2f i) : SV_Target 
	{
		half lum = 0.0f;
		
		lum += tex2D(_MainTex, i.uv  + _MainTex_TexelSize.xy * half2(-1,-1)).x;	
		lum += tex2D(_MainTex, i.uv  + _MainTex_TexelSize.xy * half2(1,1)).x;	
		lum += tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2(1,-1)).x;	
		lum += tex2D(_MainTex, i.uv  + _MainTex_TexelSize.xy * half2(-1,1)).x;	

		lum = PerceptualToLin(lum / 4.0f);

		return half4(lum, lum, lum, saturate(0.0125 * _AdaptionSpeed));
	}
			
	half4 fragAdaptive1D(v2f i) : SV_Target 
	{
		half avgLum = tex2D(_SmallTex, i.uv).x;
		half4 color = tex2D (_MainTex, i.uv);
		
		half ratio = _HdrParams / avgLum;
		ratio = max(_AdaptiveMin,min(_AdaptiveMax,ratio));
		ratio = _AdaptionEnabled * ratio + 1.0f*(1.0f-_AdaptionEnabled);

		half3 x = color.rgb * ratio;

		x *= _LutExposureMult.xyz;
		
		half pad1D = .5f/128.0f;
		half scale1D = 127.0f/128.0f;

		half3 padX = x*scale1D + pad1D;
		x.r = tex2D(_LutTex1D,half2(padX.r,.5f)).r;
		x.g = tex2D(_LutTex1D,half2(padX.g,.5f)).g;
		x.b = tex2D(_LutTex1D,half2(padX.b,.5f)).b;

		color.rgb = x;

		return color;
	}
	
	half4 fragNonAdaptive(v2f i) : SV_Target 
	{
		half4 color = tex2D (_MainTex, i.uv);
		half3 x = color.rgb;

		x *= _LutExposureMult.xyz;

		// offset and scale
		half pad = .5f/32.0f;
		half scale = 31.0f/(32.0f);
		
		x = _LutA * (x/(1.0f + x));
		x = x*scale + pad;
		x = tex3D(_LutTex,x).xyz;
		
		color.rgb = x;

		return color;
	}

	half4 fragAdaptive(v2f i) : SV_Target 
	{
		half avgLum = tex2D(_SmallTex, i.uv).x;
		half4 color = tex2D (_MainTex, i.uv);
		
		half ratio = _HdrParams / avgLum;
		ratio = max(_AdaptiveMin,min(_AdaptiveMax,ratio));
		ratio = _AdaptionEnabled * ratio + 1.0f*(1.0f-_AdaptionEnabled);

		half3 x = color.rgb * ratio;

		x *= _LutExposureMult.xyz;

		half pad = .5f/32.0f;
		half scale = 31.0f/(32.0f);
		
		// offset and scale
		x = _LutA * (x/(1.0f + x));
		
		x = x*scale + pad;
		x = tex3D(_LutTex,x).xyz;
		
		color.rgb = x;

		return color;
	}
	
	half4 fragDebug(v2f i) : SV_Target 
	{
		half avgLum = tex2D(_SmallTex, i.uv).x;
		half4 color = tex2D (_MainTex, i.uv);
		
		half ratio = _HdrParams / avgLum;
		ratio = max(_AdaptiveMin,min(_AdaptiveMax,ratio));
		ratio = _AdaptionEnabled * ratio + 1.0f*(1.0f-_AdaptionEnabled);

		half3 x = color.rgb * ratio;

		x *= _LutExposureMult.xyz;

		half pad = .5f/32.0f;
		half scale = 31.0f/(32.0f);
		
		// offset and scale
		x = _LutA * (x/(1.0f + x));
		
		x = x*scale + pad;
		x = tex3D(_LutTex,x).xyz;
		
		half highVal = max(x.x,max(x.y,x.z));
		if (highVal >= 254.0f/255.0f)
			x = half3(1.0,.25,1.0);

		color.rgb = x;

		return color;
	}
	
	ENDCG 
	
Subshader {
 // adaptive reinhhard apply
 Pass {
	  ZTest Always Cull Off ZWrite Off

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment fragAdaptive
      ENDCG
  }

  // 1
 Pass {
	  ZTest Always Cull Off ZWrite Off

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment fragLog
      ENDCG
  }  
  // 2
 Pass {
	  ZTest Always Cull Off ZWrite Off
	  Blend SrcAlpha OneMinusSrcAlpha

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment fragExp
      ENDCG
  }  
  // 3 
 Pass {
	  ZTest Always Cull Off ZWrite Off

	  Blend Off   

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment fragExp
      ENDCG
  }  
  // 4 - debugging
 Pass {
	  ZTest Always Cull Off ZWrite Off

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment fragDebug
      ENDCG
  }

 // 5 - non-adaptive
 Pass {
	  ZTest Always Cull Off ZWrite Off

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment fragNonAdaptive
      ENDCG
  }

}

Fallback off
	
} // shader
