using UnityEngine;
using System.Collections;
using UnityStandardAssets.ImageEffects;

[ExecuteInEditMode]
[RequireComponent (typeof(Camera))]
[AddComponentMenu ("Image Effects/Color Adjustments/Tonemapping LUT")]
public class TonemappingLut : PostEffectsBase {
	
	public bool enableAdaptive = false;
	public bool enableFilmicCurve = false;
	public bool enableUserLut = false;
	public bool enableColorGrading = false;
	public bool enableColorCurve = false;

	public bool debugClamp = false;

	// the actual texture that we build
	private Texture3D lutTex = null;

	// 1d curves
	private Texture2D lutCurveTex1D = null;

	
	
	// adaptive parameters
	public float middleGrey = 0.18f;
	public float adaptionSpeed = 1.5f;

	public float adaptiveMin = -3.0f;
	public float adaptiveMax = 3.0f;
	public bool adaptiveDebug = false;

	// LUT
	public float lutExposureBias = 0.0f;

	public Color lutWhiteBalance = new Color(1, 1, 1);
	
	public float lutContrast   = 1.0f;
	public float lutSaturation = 1.0f;
	public float lutGamma      = 1.0f;

	public float lutToe       = 0.0f;
	public float lutShoulder      = 0.0f;

	public AnimationCurve remapCurve = new AnimationCurve(new Keyframe(0, 0, 1.0f, 1.0f), new Keyframe(1, 1, 1.0f, 1.0f));

	public Texture2D userLutTex = null;
	private string userLutName = "";
	
	//public float lutHighlightRecovery          = 0.0f;

	public Color lutShadows = new Color(1, 1, 1);
	public Color lutMidtones = new Color(1, 1, 1);
	public Color lutHighlights = new Color(1, 1, 1);

	// LUT cached data
	private float cache_lutContrast = 1.0f;
	private float cache_lutSaturation = 1.0f;
	private float cache_lutGamma = 1.0f;

	private float cache_lutLowY = 1.0f;
	private float cache_lutHighY = .7f;

	private bool cache_enableAdaptive = false;
	private bool cache_enableFilmicCurve = false;
	private bool cache_enableColorGrading = false;
	private bool cache_enableUserLut = false;
	private bool cache_enableColorCurve = false;

	//private float cache_lutHighlightRecovery = 0.0f;

	private Color cache_lutWhiteBalance = new Color(1, 1, 1);
	private Color cache_lutHighlights = new Color(1, 1, 1);
	private Color cache_lutMidtones = new Color(1, 1, 1);
	private Color cache_lutShadows = new Color(1, 1, 1);

	private Keyframe[] cache_remapCurve;

	private string cache_userLutName = "";

	private bool HasCacheChanged()
	{
		if (cache_lutContrast   != lutContrast   ||
			cache_lutSaturation != lutSaturation ||
			cache_lutGamma      != lutGamma      ||
			cache_lutLowY       != lutToe       ||
			cache_lutHighY      != lutShoulder      ||
			cache_lutWhiteBalance != lutWhiteBalance ||
			cache_lutHighlights != lutHighlights ||
			cache_lutMidtones != lutMidtones ||
			cache_lutShadows != lutShadows ||
			cache_enableAdaptive != enableAdaptive ||
			cache_enableFilmicCurve != enableFilmicCurve ||
			cache_enableColorGrading != enableColorGrading ||
			cache_enableUserLut != enableUserLut ||
			cache_enableColorCurve != enableColorCurve)
		{
			return true;
		}

		userLutName = "";
		if (userLutTex != null)
			userLutName = userLutTex.name;

		if (userLutName != cache_userLutName)
			return true;


		// check the keyframes
		if (enableColorCurve)
		{
			if (cache_remapCurve.Length != remapCurve.keys.Length)
			{
				return true;
			}

			for (int i = 0; i < cache_remapCurve.Length; i++)
			{
				if (cache_remapCurve[i].time != remapCurve.keys[i].time ||
					cache_remapCurve[i].value != remapCurve.keys[i].value ||
					cache_remapCurve[i].inTangent != remapCurve.keys[i].inTangent ||
					cache_remapCurve[i].outTangent != remapCurve.keys[i].outTangent)
				{
					return true;
				}
			}
		}
		
		return false;
	}

	private void UpdateCache()
	{
	   cache_lutContrast   = lutContrast  ;
	   cache_lutSaturation = lutSaturation;
	   cache_lutGamma      = lutGamma     ;
	   cache_lutLowY       = lutToe      ;
	   cache_lutHighY      = lutShoulder     ;
	   //cache_lutHighlightRecovery          = lutHighlightRecovery         ;
	   cache_lutWhiteBalance = lutWhiteBalance;
	   cache_lutHighlights = lutHighlights;
	   cache_lutMidtones = lutMidtones;
	   cache_lutShadows = lutShadows;
	   //cache_remapCurve = remapCurve.keys;

	   cache_enableAdaptive = enableAdaptive;
	   cache_enableFilmicCurve = enableFilmicCurve;
	   cache_enableColorGrading = enableColorGrading;
	   cache_enableUserLut = enableUserLut;
	   cache_enableColorCurve = enableColorCurve;

	   cache_userLutName = (userLutTex != null) ? userLutTex.name : "";

	   cache_remapCurve = new Keyframe[remapCurve.keys.Length];
	   for (int i = 0; i < remapCurve.keys.Length; i++)
	   {
		   cache_remapCurve[i] = remapCurve.keys[i];
	   }

	   cache_enableColorCurve = enableColorCurve;
	}

	struct SimplePolyFunc
	{
		// f(x) = signY*A*(signX*x-x0)^b + y0
		public float A;
		public float B;
		public float x0;
		public float y0;
		public float signX;
		public float signY;

		public float logA;

		public float Eval(float x)
		{
			// standard function
			//return signY * A * Mathf.Pow(signX * x - x0, B) + y0;

			// slightly more complicated but numerically stable function
			return signY * Mathf.Exp(logA + B * Mathf.Log(signX * x - x0)) + y0;
		}

		// create a function going from (0,0) to (x_end,y_end) where the 
		// derivative at x_end is m
		public void Initialize(float x_end, float y_end, float m)
		{
			A = 0.0f;
			B = 1.0f;
			x0 = 0.0f;
			y0 = 0.0f;
			signX = 1.0f;
			signY = 1.0f;

			// invalid case, slope must be positive and the
			// y that we are trying to hit must be positve.
			if (m <= 0.0f || y_end <= 0.0f)
			{
				return;
			}

			// also invalid
			if (x_end <= 0.0f)
			{
				return;
			}

			B = (m * x_end) / y_end;

			float p = Mathf.Pow(x_end, B);
			A = y_end / p;
			logA = Mathf.Log(y_end) - B * Mathf.Log(x_end);
		}
	};

	// usual & internal stuff
	public Shader tonemapperLut = null;
	public bool  validRenderTextureFormat = true;
	private Material tonemapMaterial = null;
	private RenderTexture rt = null;
	private RenderTextureFormat rtFormat =  RenderTextureFormat.ARGBHalf;

	private int curveLen = 256;
	private float [] curveData;

	private int userLutDim = 16;
	private Color[] userLutData;
	
	public override bool CheckResources () {
		 CheckSupport (false, true);
	
		tonemapMaterial = CheckShaderAndCreateMaterial(tonemapperLut, tonemapMaterial);

		if (!isSupported)
			ReportAutoDisable ();
		return isSupported;
	}

	float GetHighlightRecovery()
	{
		return Mathf.Max(0.0f,lutShoulder*3.0f);
	}

	float GetWhitePoint()
	{
		return Mathf.Pow(2.0f, Mathf.Max(0.0f, GetHighlightRecovery()));
	}

	float LutToLin(float x, float lutA)
	{
		x = (x >= 1.0f) ? 1.0f : x;
		float temp = x / lutA;
		return temp / (1.0f - temp);
	}

	float LinToLut(float x, float lutA)
	{
		return Mathf.Sqrt(x / (x + lutA));
	}

	float LiftGammaGain(float x, float lift, float invGamma, float gain)
	{
		float xx = Mathf.Sqrt(x);

		//float ret = gain * Mathf.Pow(xx + lift*(1.0f-xx), invGamma);
		float ret = gain * (lift*(1.0f-xx) + Mathf.Pow(xx , invGamma));
		return ret * ret;
		//return gain * (x + lift * Mathf.Pow(1.0f - x,invGamma));
	}

	float LogContrast(float x, float linRef, float contrast)
	{
		x = Mathf.Max(x,1e-5f);

		float logRef = Mathf.Log(linRef);
		float logVal = Mathf.Log(x);
		float logAdj = logRef + (logVal - logRef) * contrast;
		float dstVal = Mathf.Exp(logAdj);
		return dstVal;
	}

	Color NormalizeColor(Color c)
	{
		float sum = (c.r + c.g + c.b)/3.0f;
		if (sum == 0.0f)
			return new Color(1.0f, 1.0f, 1.0f, 1.0f);

		Color ret = new Color();
		ret.r = c.r / sum;
		ret.g = c.g / sum;
		ret.b = c.b / sum;
		ret.a = 1.0f;
		return ret;
	}

	float GetLutA()
	{
		// our basic function is f(x) = A*x/(x+1)
		// we want the function to actually be able to hit 1.0f (to use
		// the full range of the 3D lut) and that's what A is for.

		// tried a bunch numbers and 1.05 seems to work pretty well.
		return 1.05f;
	}


	public void UpdateCurve()
	{
		// initiailize data
		curveData = new float[curveLen];
		for (int i = 0; i < curveLen; i++)
		{
			float t = (float)(i) / (float)(curveLen-1);
			curveData[i] = t;
		}

		if (remapCurve != null && enableColorCurve)
		{
			if (remapCurve.keys.Length < 1)
				remapCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));

			float range = 1.0f;
			if (remapCurve.length > 0)
				range = remapCurve[remapCurve.length - 1].time;

			for (int i = 0; i < curveLen; i++ )
			{
				float t = (float)(i) / (float)(curveLen - 1);
				float c = remapCurve.Evaluate(t * range);
				curveData[i] = c;
			}
		}
	}

	float EvaluateCurve(float srcVal)
	{
		float remapT = srcVal * ((float)curveLen - 1.0f) / (float)(curveLen);
		float fullT = Mathf.Max(0.0f, Mathf.Min((float)(curveLen), remapT * (float)curveLen));

		int x0 = Mathf.FloorToInt(fullT - .5f);
		x0 = (x0 >= 0 ? x0 : 0);
		x0 = (x0 < curveLen ? x0 : curveLen-1);
		int x1 = (x0 < curveLen - 1) ? (x0 + 1) : x0;
		float t = (fullT - .5f) - Mathf.Floor(fullT - .5f);

		float v0 = curveData[x0];
		float v1 = curveData[x1];

		float ret = v0 * (1.0f-t) + v1 * t;
		return ret;
	}

	public void SetIdentityLut()
	{
		int dim = 16;
		Color[] newC = new Color[dim * dim * dim];
		float oneOverDim = 1.0f / (1.0f * dim - 1.0f);

		for (int i = 0; i < dim; i++)
		{
			for (int j = 0; j < dim; j++)
			{
				for (int k = 0; k < dim; k++)
				{
					newC[i + (j * dim) + (k * dim * dim)] = new Color((i * 1.0f) * oneOverDim, (j * 1.0f) * oneOverDim, (k * 1.0f) * oneOverDim, 1.0f);
				}
			}
		}

		userLutData = newC;
		userLutDim = dim;
		userLutName = "";
	}


	private int ClampDim(int srcX)
	{
		int x = srcX;
		x = (x >= 0) ? x : 0;
		x = (x < userLutDim) ? x : (userLutDim - 1);
		return x;
	}

	private Color SampleLutNearest(int r, int g, int b)
	{
		r = (r >= 0) ? r : 0;
		r = (r < userLutDim) ? r : (userLutDim - 1);

		g = (g >= 0) ? g : 0;
		g = (g < userLutDim) ? g : (userLutDim - 1);

		b = (b >= 0) ? b : 0;
		b = (b < userLutDim) ? b : (userLutDim - 1);

		return userLutData[r + (g * userLutDim) + (b * userLutDim * userLutDim)];
	}

	// does the lookup without bounds checking
	private Color SampleLutNearestUnsafe(int r, int g, int b)
	{
		return userLutData[r + (g * userLutDim) + (b * userLutDim * userLutDim)];
	}

	private Color SampleLutLinear(float srcR, float srcG, float srcB)
	{
		float sampleOffset = 0.0f;
		float sampleScale = (float)(userLutDim-1);

		float r = srcR * sampleScale + sampleOffset;
		float g = srcG * sampleScale + sampleOffset;
		float b = srcB * sampleScale + sampleOffset;

		int r0 = Mathf.FloorToInt(r);
		int g0 = Mathf.FloorToInt(g);
		int b0 = Mathf.FloorToInt(b);

		r0 = ClampDim(r0);
		g0 = ClampDim(g0);
		b0 = ClampDim(b0);

		int r1 = ClampDim(r0 + 1);
		int g1 = ClampDim(g0 + 1);
		int b1 = ClampDim(b0 + 1);

		float tr = (r) - (float)r0;
		float tg = (g) - (float)g0;
		float tb = (b) - (float)b0;

		Color c000 = SampleLutNearestUnsafe(r0, g0, b0);
		Color c001 = SampleLutNearestUnsafe(r0, g0, b1);
		Color c010 = SampleLutNearestUnsafe(r0, g1, b0);
		Color c011 = SampleLutNearestUnsafe(r0, g1, b1);
		Color c100 = SampleLutNearestUnsafe(r1, g0, b0);
		Color c101 = SampleLutNearestUnsafe(r1, g0, b1);
		Color c110 = SampleLutNearestUnsafe(r1, g1, b0);
		Color c111 = SampleLutNearestUnsafe(r1, g1, b1);

		Color c00 = Color.Lerp(c000, c001, tb);
		Color c01 = Color.Lerp(c010, c011, tb);
		Color c10 = Color.Lerp(c100, c101, tb);
		Color c11 = Color.Lerp(c110, c111, tb);

		Color c0 = Color.Lerp(c00, c01, tg);
		Color c1 = Color.Lerp(c10, c11, tg);

		Color c = Color.Lerp(c0, c1, tr);

		return c;
	}


	void UpdateUserLut()
	{
		//private int userLutDim = 16;
		//private float[] userLutData;

		// conversion fun: the given 2D texture needs to be of the format
		//  w * h, wheras h is the 'depth' (or 3d dimension 'dim') and w = dim * dim

		if (userLutTex != null)
		{
			int dim = userLutTex.height;

			if (!ValidDimensions(userLutTex))
			{
				Debug.LogWarning("The given 2D texture " + userLutTex.name + " cannot be used as a 3D LUT.  Reverting to identity.");
				SetIdentityLut();
			}
			else
			{
				Color[] c = userLutTex.GetPixels();
				Color[] newC = new Color[c.Length];

				for (int i = 0; i < dim; i++)
				{
					for (int j = 0; j < dim; j++)
					{
						for (int k = 0; k < dim; k++)
						{
							int j_ = dim - j - 1;
							newC[i + (j * dim) + (k * dim * dim)] = c[k * dim + i + j_ * dim * dim];
						}
					}
				}

				userLutDim = dim;
				userLutData = newC;
				userLutName = userLutTex.name;
			}
		}
		else
		{
			// error, something went terribly wrong
			//Debug.LogError("Couldn't color correct with 3D LUT texture. Image Effect will be disabled.");
			SetIdentityLut();
		}
	}


	float EvalFilmicHelper(float srcR, float lutA,
		SimplePolyFunc polyToe,
		SimplePolyFunc polyLinear,
		SimplePolyFunc polyShoulder,
		float x0, float x1, float linearW)
	{
		// figure out the linear value of this 3d texel
		float dstR = LutToLin(srcR, lutA);

		if (enableFilmicCurve)
		{
			// we could allow this to be customized, but most people probably
			// would not understand it and it would just create complexity.
			// 18% grey is the standard film reference grey so let's just go with that.
			float linRef = .18f;
			dstR = LogContrast(dstR, linRef, lutContrast);

			SimplePolyFunc polyR = polyToe;
			if (dstR >= x0)
				polyR = polyLinear;
			if (dstR >= x1)
				polyR = polyShoulder;

			dstR = Mathf.Min(dstR, linearW);
			dstR = polyR.Eval(dstR);
	   
		}


		return dstR;
	}

	float EvalCurveGradingHelper(float srcR, float liftR, float invGammaR, float gainR)
	{
		float dstR = srcR;

		if (enableColorGrading)
		{
			// lift/gamma/gain
			dstR = LiftGammaGain(dstR, liftR, invGammaR, gainR);
		}

		// max with zero
		dstR = Mathf.Max(dstR, 0.0f);

		// overall gamma
		dstR = Mathf.Pow(dstR, lutGamma);

		if (enableColorCurve)
		{
			// apply curves
			dstR = EvaluateCurve(dstR);
		}

		return dstR;
	}

	void CreateFilmicCurveHelper(float lutA,
		SimplePolyFunc polyToe,
		SimplePolyFunc polyLinear,
		SimplePolyFunc polyShoulder,
		float x0, float x1, float linearW,
		float liftR, float invGammaR, float gainR,
		float liftG, float invGammaG, float gainG,
		float liftB, float invGammaB, float gainB)
	{
		int curveLen = 128;

		Color[] newC = new Color[curveLen * 2];
		float oneOverDim = 1.0f / (1.0f * curveLen - 1.0f);

		for (int i = 0; i < curveLen; i++)
		{
			float srcR = (i * 1.0f) * oneOverDim;
			float srcG = (i * 1.0f) * oneOverDim;
			float srcB = (i * 1.0f) * oneOverDim;

			float dstR = EvalFilmicHelper(srcR, lutA,
				polyToe,
				polyLinear,
				polyShoulder,
				x0, x1, linearW);

			float dstG = EvalFilmicHelper(srcG, lutA,
				polyToe,
				polyLinear,
				polyShoulder,
				x0, x1, linearW);

			float dstB = EvalFilmicHelper(srcB, lutA,
				polyToe,
				polyLinear,
				polyShoulder,
				x0, x1, linearW);

			// enable lut
			if (enableUserLut)
			{
				Color c = SampleLutLinear(dstR, dstG, dstB);
				dstR = c.r;
				dstG = c.g;
				dstB = c.b;
			}

			dstR = EvalCurveGradingHelper(dstR, liftR, invGammaR, gainR);
			dstG = EvalCurveGradingHelper(dstG, liftG, invGammaG, gainG);
			dstB = EvalCurveGradingHelper(dstB, liftB, invGammaB, gainB);

			if (enableColorGrading)
			{
				// saturation
				float midVal = (dstR + dstG + dstB) / 3.0f;
				dstR = midVal + (dstR - midVal) * lutSaturation;
				dstG = midVal + (dstG - midVal) * lutSaturation;
				dstB = midVal + (dstB - midVal) * lutSaturation;
			}

			dstR = Mathf.LinearToGammaSpace(dstR);
			dstG = Mathf.LinearToGammaSpace(dstG);
			dstB = Mathf.LinearToGammaSpace(dstB);


			newC[i + 0 * curveLen] = new Color(dstR, dstG, dstB, 1.0f);
			newC[i + 1 * curveLen] = new Color(dstR, dstG, dstB, 1.0f);
		}

		if (lutCurveTex1D)
			DestroyImmediate(lutCurveTex1D);

		lutCurveTex1D = new Texture2D(curveLen, 2, TextureFormat.ARGB32, false);
		lutCurveTex1D.filterMode = FilterMode.Bilinear;
		lutCurveTex1D.wrapMode = TextureWrapMode.Clamp;
		lutCurveTex1D.hideFlags = HideFlags.DontSave;

		lutCurveTex1D.SetPixels(newC);
		lutCurveTex1D.Apply();

	}

	void UpdateLut()
	{
		UpdateUserLut();
		UpdateCurve();       

		float lutA = GetLutA();

		SimplePolyFunc polyToe;
		SimplePolyFunc polyLinear;
		SimplePolyFunc polyShoulder;

		float gammaSpace = 2.2f;

		float x0 = Mathf.Pow(1.0f/3.0f, gammaSpace);
		float shoulderBase = .7f;
		float x1 = Mathf.Pow(shoulderBase, gammaSpace);
		float gammaHighY = Mathf.Pow(shoulderBase, 1.0f + (lutShoulder) * 1.0f);
		float y1 = Mathf.Pow(gammaHighY, gammaSpace);

		float y0 = 0.0f;
		{
			float t = x0 / x1;
			float lin = t * y1;
			float low = lin * (1.0f-lutToe*.5f);
			y0 = low;
		}

		float dx = x1 - x0;
		float dy = y1 - y0;

		float m = 0.0f;
		if (dx > 0 && dy > 0)
			m = dy / dx;

		// linear section, power is 1, slope is m
		polyLinear.x0 = x0;
		polyLinear.y0 = y0;
		polyLinear.A = m;
		polyLinear.B = 1.0f;
		polyLinear.signX = 1.0f;
		polyLinear.signY = 1.0f;
		polyLinear.logA = Mathf.Log(m);

		// toe
		polyToe = polyLinear;
		polyToe.Initialize(x0, y0, m);

		float linearW = GetWhitePoint();

		{
			// shoulder, first think about it "backwards"
			float offsetX = linearW - x1;
			float offsetY = 1.0f - y1;

			polyShoulder = polyLinear;
			polyShoulder.Initialize(offsetX, offsetY, m);

			// flip horizontal
			polyShoulder.signX = -1.0f;
			polyShoulder.x0 = -linearW;

			// flip vertical
			polyShoulder.signY = -1.0f;
			polyShoulder.y0 = 1.0f;
		}

		int dim = 32;
		Color[] newC = new Color[dim * dim * dim];
		float oneOverDim = 1.0f / (1.0f * dim - 1.0f);

		Color normS = NormalizeColor(lutShadows);
		Color normM = NormalizeColor(lutMidtones);
		Color normH = NormalizeColor(lutHighlights);

		float avgS = (normS.r + normS.g + normS.b) / 3.0f;
		float avgM = (normM.r + normM.g + normM.b) / 3.0f;
		float avgH = (normH.r + normH.g + normH.b) / 3.0f;

		// these are magic numbers
		float liftScale = .1f;
		float gammaScale = .5f;
		float gainScale = .5f;

		float liftR = (normS.r - avgS) * liftScale;
		float liftG = (normS.g - avgS) * liftScale;
		float liftB = (normS.b - avgS) * liftScale;

		float gammaR = Mathf.Pow(2.0f, (normM.r - avgM) * gammaScale);
		float gammaG = Mathf.Pow(2.0f, (normM.g - avgM) * gammaScale);
		float gammaB = Mathf.Pow(2.0f, (normM.b - avgM) * gammaScale);

		float gainR = Mathf.Pow(2.0f, (normH.r - avgH) * gainScale);
		float gainG = Mathf.Pow(2.0f, (normH.g - avgH) * gainScale);
		float gainB = Mathf.Pow(2.0f, (normH.b - avgH) * gainScale);

		float minGamma = .01f;
		float invGammaR = 1.0f / Mathf.Max(minGamma, gammaR);
		float invGammaG = 1.0f / Mathf.Max(minGamma, gammaG);
		float invGammaB = 1.0f / Mathf.Max(minGamma, gammaB);

		for (int i = 0; i < dim; i++)
		{
			for (int j = 0; j < dim; j++)
			{
				for (int k = 0; k < dim; k++)
				{
					float srcR = (i * 1.0f) * oneOverDim;
					float srcG = (j * 1.0f) * oneOverDim;
					float srcB = (k * 1.0f) * oneOverDim;


					float dstR = EvalFilmicHelper(srcR, lutA,
						polyToe,
						polyLinear,
						polyShoulder,
						x0, x1, linearW);

					float dstG = EvalFilmicHelper(srcG, lutA,
						polyToe,
						polyLinear,
						polyShoulder,
						x0, x1, linearW);

					float dstB = EvalFilmicHelper(srcB, lutA,
						polyToe,
						polyLinear,
						polyShoulder,
						x0, x1, linearW);

					// enable lut
					if (enableUserLut)
					{
						Color c = SampleLutLinear(dstR, dstG, dstB);
						dstR = c.r;
						dstG = c.g;
						dstB = c.b;
					}

					dstR = EvalCurveGradingHelper(dstR, liftR, invGammaR, gainR);
					dstG = EvalCurveGradingHelper(dstG, liftG, invGammaG, gainG);
					dstB = EvalCurveGradingHelper(dstB, liftB, invGammaB, gainB);
					
					if (enableColorGrading)
					{
						// saturation
						float midVal = (dstR + dstG + dstB) / 3.0f;
						dstR = midVal + (dstR - midVal) * lutSaturation;
						dstG = midVal + (dstG - midVal) * lutSaturation;
						dstB = midVal + (dstB - midVal) * lutSaturation;
					}

					newC[i + (j * dim) + (k * dim * dim)] = new Color(dstR, dstG, dstB, 1.0f);
				}
			}
		}

		if (lutTex)
			DestroyImmediate(lutTex);

		lutTex = new Texture3D(dim, dim, dim, TextureFormat.ARGB32, false);
		lutTex.filterMode = FilterMode.Bilinear;
		lutTex.wrapMode = TextureWrapMode.Clamp;
		lutTex.hideFlags = HideFlags.DontSave;

		lutTex.SetPixels(newC);
		lutTex.Apply();

		if (false)
		{
			// Instad of doing a single 3D lut, I tried doing this as 3x 1D luts.  Or rather,
			// a single lut with separate curves baked into RGB channels.  It wasn't actually faster
			// do it's disabled.  But there are two reasons why in the future it might be useful:

			// 1.  If it turns out that 3x 1D luts are faster on some hardware, it might be worth it.
			// 2.  Updating the 3D LUT is quite slow so you can't change it every frame.  If the
			//        parameters need to lerp than the 1D version might  be worthwhile.
			CreateFilmicCurveHelper(lutA,
			   polyToe,
			   polyLinear,
			   polyShoulder,
			   x0, x1, linearW,
			   liftR, invGammaR, gainR,
			   liftG, invGammaG, gainG,
			   liftB, invGammaB, gainB);
		}
	}

	public bool ValidDimensions(Texture2D tex2d)
	{
		if (!tex2d) return false;
		int h = tex2d.height;
		if (h != Mathf.FloorToInt(Mathf.Sqrt(tex2d.width)))
		{
			return false;
		}
		return true;
	}

	public void Convert(Texture2D temp2DTex)
	{
#if false
		// conversion fun: the given 2D texture needs to be of the format
		//  w * h, wheras h is the 'depth' (or 3d dimension 'dim') and w = dim * dim

		if (temp2DTex)
		{
			int dim = temp2DTex.width * temp2DTex.height;
			dim = temp2DTex.height;

			if (!ValidDimensions(temp2DTex))
			{
				Debug.LogWarning("The given 2D texture " + temp2DTex.name + " cannot be used as a 3D LUT.");
				//basedOnTempTex = "";
				return;
			}

			Color[] c = temp2DTex.GetPixels();
			Color[] newC = new Color[c.Length];

			for (int i = 0; i < dim; i++)
			{
				for (int j = 0; j < dim; j++)
				{
					for (int k = 0; k < dim; k++)
					{
						int j_ = dim - j - 1;
						newC[i + (j * dim) + (k * dim * dim)] = c[k * dim + i + j_ * dim * dim];
					}
				}
			}

			if (converted3DLut)
				DestroyImmediate(converted3DLut);
			converted3DLut = new Texture3D(dim, dim, dim, TextureFormat.ARGB32, false);
			converted3DLut.SetPixels(newC);
			converted3DLut.Apply();
			userLutTexName = temp2DTex.name;
		}
		else
		{
			// error, something went terribly wrong
			//Debug.LogError("Couldn't color correct with 3D LUT texture. Image Effect will be disabled.");
			SetIdentityLut();
			userLutTexName = "";
		}
#endif
	}

	
	void OnDisable () {
		if (rt) {
			DestroyImmediate (rt);
			rt = null;
		}
		if (tonemapMaterial) {
			DestroyImmediate (tonemapMaterial);
			tonemapMaterial = null;
		}

		if (lutTex)
		{
			DestroyImmediate(lutTex);
			lutTex = null;
		}

		if (lutCurveTex1D)
		{
			DestroyImmediate(lutCurveTex1D);
			lutCurveTex1D = null;
		}
	}
	
	bool CreateInternalRenderTexture () {
		 if (rt) {
			return false;
		}
		rtFormat = SystemInfo.SupportsRenderTextureFormat (RenderTextureFormat.RGHalf) ? RenderTextureFormat.RGHalf : RenderTextureFormat.ARGBHalf;
		rt = new RenderTexture(1,1, 0, rtFormat);
		rt.hideFlags = HideFlags.DontSave;
		return true;
	}
	
	// attribute indicates that the image filter chain will continue in LDR
	[ImageEffectTransformsToLDR]
	void OnRenderImage (RenderTexture source, RenderTexture destination) {
		if (CheckResources() == false) {
			Graphics.Blit (source, destination);
			return;
		}

		// clamp values
		{
			lutToe = Mathf.Max(0.0f, Mathf.Min(1.0f, lutToe));
			lutShoulder = Mathf.Max(0.0f, Mathf.Min(1.0f, lutShoulder));
		}

		if (lutTex == null || HasCacheChanged())
		{
			UpdateCache();

			UpdateLut();
		}

		#if UNITY_EDITOR
		validRenderTextureFormat = true;
		if (source.format != RenderTextureFormat.ARGBHalf) {
			validRenderTextureFormat = false;
		}
		#endif
		
		bool  freshlyBrewedInternalRt = CreateInternalRenderTexture (); // this retrieves rtFormat, so should happen before rt allocations

		int srcSize = source.width < source.height ? source.width : source.height;

		int adaptiveSize = 1;
		while (adaptiveSize * 2 < srcSize)
		{
			adaptiveSize *= 2;
		}

		int downsample = 1;
		RenderTexture[] rts = null;
		RenderTexture rtSquared = null;

		if (enableAdaptive)
		{
			rtSquared = RenderTexture.GetTemporary((int)adaptiveSize, (int)adaptiveSize, 0, rtFormat);
			Graphics.Blit(source, rtSquared);

			downsample = (int)Mathf.Log(rtSquared.width * 1.0f, 2);

			int div = 2;
			rts = new RenderTexture[downsample];
			for (int i = 0; i < downsample; i++)
			{
				rts[i] = RenderTexture.GetTemporary(rtSquared.width / div, rtSquared.width / div, 0, rtFormat);
				div *= 2;
			}

			// downsample pyramid
			var lumRt = rts[downsample - 1];
			Graphics.Blit(rtSquared, rts[0], tonemapMaterial, 1);
			if (true)
			{
				for (int i = 0; i < downsample - 1; i++)
				{
					Graphics.Blit(rts[i], rts[i + 1]);
					lumRt = rts[i + 1];
				}
			}

			// we have the needed values, let's apply adaptive tonemapping
			adaptionSpeed = adaptionSpeed < 0.001f ? 0.001f : adaptionSpeed;
			tonemapMaterial.SetFloat("_AdaptionSpeed", adaptionSpeed);

			rt.MarkRestoreExpected(); // keeping luminance values between frames, RT restore expected
		
		#if UNITY_EDITOR
			if (Application.isPlaying && !freshlyBrewedInternalRt)
				Graphics.Blit (lumRt, rt, tonemapMaterial, 2);
			else
				Graphics.Blit (lumRt, rt, tonemapMaterial, 3);
		#else
			Graphics.Blit (lumRt, rt, tonemapMaterial, freshlyBrewedInternalRt ? 3 : 2);
		#endif

			// this code doesn't work,
			if (adaptiveDebug)
			{
				/*
				RenderTexture.active = rt;

				Texture2D readTex;
				//readTex = new Texture2D(1, 1, TextureFormat.ARGB32, false); // works
				//readTex = new Texture2D(1, 1, TextureFormat.RGBAFloat, false); // fails
				//readTex = new Texture2D(1, 1, TextureFormat.RGBAHalf, false); // fails

				readTex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
				readTex.Apply();

				Color c = readTex.GetPixel(0,0);

				Debug.Log("Testing." + c.r); // always 0.8039216
				DestroyImmediate(readTex);
				readTex = null;
				 */
			}
		}

		middleGrey = middleGrey < 0.001f ? 0.001f : middleGrey;
		tonemapMaterial.SetFloat("_HdrParams", middleGrey);
		tonemapMaterial.SetTexture ("_SmallTex", rt);
		tonemapMaterial.SetTexture("_LutTex", lutTex);
		tonemapMaterial.SetTexture("_LutTex1D", lutCurveTex1D);

		tonemapMaterial.SetFloat("_AdaptiveMin", Mathf.Pow(2.0f, adaptiveMin));
		tonemapMaterial.SetFloat("_AdaptiveMax", Mathf.Pow(2.0f, adaptiveMax));

		float lutA = GetLutA();

		float exposureBias = Mathf.Pow(2.0f, enableFilmicCurve ? lutExposureBias : 0.0f);
		Vector4 exposureMult = new Vector4(exposureBias, exposureBias, exposureBias, 1.0f);

		Color linWB = new Color(1.0f,1.0f,1.0f,1.0f);
		linWB.r = Mathf.Pow(lutWhiteBalance.r, 2.2f);
		linWB.g = Mathf.Pow(lutWhiteBalance.g, 2.2f);
		linWB.b = Mathf.Pow(lutWhiteBalance.b, 2.2f);

		Color normWB = NormalizeColor(linWB);
		exposureMult.x *= normWB.r;
		exposureMult.y *= normWB.g;
		exposureMult.z *= normWB.b;

		tonemapMaterial.SetFloat("_LutA", lutA);
		tonemapMaterial.SetVector("_LutExposureMult", exposureMult);
		tonemapMaterial.SetFloat("_LutSaturation", lutSaturation);
		tonemapMaterial.SetFloat("_AdaptionEnabled", enableAdaptive ? 1.0f : 0.0f);

		if (debugClamp)
		{
			Graphics.Blit(source, destination, tonemapMaterial, 4);
		}
		else if (!enableAdaptive)
		{
			Graphics.Blit(source, destination, tonemapMaterial, 5);
		}
		else
		{
			Graphics.Blit (source, destination, tonemapMaterial, 0);
		}
		// cleanup for adaptive

		if (enableAdaptive)
		{
			for (int i = 0; i < downsample; i++)
			{
				RenderTexture.ReleaseTemporary(rts[i]);
			}
			RenderTexture.ReleaseTemporary(rtSquared);
		}
	}
}
