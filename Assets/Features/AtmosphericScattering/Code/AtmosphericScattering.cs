using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class AtmosphericScattering : MonoBehaviour {
	public enum OcclusionDownscale { x1 = 1, x2 = 2, x4 = 4 }
	public enum OcclusionSamples { x64 = 0, x164 = 1, x244 = 2 }
	public enum ScatterDebugMode { None, Scattering, Occlusion, OccludedScattering, Rayleigh, Mie, Height }
	public enum DepthTexture { Enable, Disable, Ignore }

	[Header("World Components")]
	public Gradient	worldRayleighColorRamp			= new Gradient();
	public float	worldRayleighColorIntensity		= 2f;
	public float	worldRayleighDensity			= 10f;
	public float	worldRayleighExtinctionFactor	= 1.1f;
	public float	worldRayleighIndirectScatter	= 0.33f;
	public Gradient	worldMieColorRamp				= new Gradient();
	public float	worldMieColorIntensity			= 2f;
	public float	worldMieDensity					= 50f;
	public float	worldMieExtinctionFactor		= 0f;
	public float	worldMiePhaseAnisotropy			= 0.76f;
	public float	worldNearScatterPush			= 0f;
	public float	worldNormalDistance				= 1000f;

	[Header("Height Components")]
	public Color	heightRayleighColor		= Color.white;
	public float	heightRayleighIntensity	= 1f;
	public float	heightRayleighDensity	= 10f;
	public float	heightMieDensity		= 0f;
	public float	heightExtinctionFactor	= 1.1f;
	public float	heightSeaLevel			= 0f;
	public float	heightDistance			= 50f;
	public Vector3	heightPlaneShift		= Vector3.zero;
	public float	heightNearScatterPush	= 0f;
	public float	heightNormalDistance	= 1000f;

	[Header("Sky Dome")]
	public Vector3		skyDomeScale		= new Vector3(1f, 0.05f, 1f);
	public Vector3		skyDomeRotation;
	public Transform	skyDomeTrackedYawRotation;
	public bool			skyDomeVerticalFlip;
	public Cubemap		skyDomeCube;
	public float		skyDomeExposure		= 1f;
	public Color		skyDomeTint			= Color.white;
	[HideInInspector] public Vector3 skyDomeOffset;

	[Header("Scatter Occlusion")]
	public bool					useOcclusion = false;
	public float				occlusionBias = 0f;
	public float				occlusionBiasIndirect = 0.6f;
	public float				occlusionBiasClouds = 0.3f;
	public OcclusionDownscale	occlusionDownscale = OcclusionDownscale.x2;
	public OcclusionSamples		occlusionSamples = OcclusionSamples.x64;
	public bool					occlusionDepthFixup = true;
	public float				occlusionDepthThreshold = 25f;
	public bool					occlusionFullSky = true;
	public float				occlusionBiasSkyRayleigh = 0.2f;
	public float				occlusionBiasSkyMie = 0.4f;
	
	[Header("Other")]
	public float			worldScaleExponent = 1.0f;
	public bool				forcePerPixel;
	[Tooltip("Soft clouds need depth values. Ignore means externally controlled.")]
	public DepthTexture		depthTexture;
	public ScatterDebugMode	debugMode;
	
	[HideInInspector] public Shader occlusionShader;

	bool			m_isAwake;

	Camera			m_currentCamera;
	Material		m_occlusionMaterial;

	UnityEngine.Rendering.CommandBuffer m_occlusionCmdAfterShadows, m_occlusionCmdBeforeScreen;
	
	public static AtmosphericScattering instance { get; private set; }

	void Awake() {
		var mf = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
		if(!mf.sharedMesh) {
			mf.sharedMesh = new Mesh();
			mf.sharedMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
			mf.sharedMesh.SetTriangles((int[])null, 0);
		}
		if(!GetComponent<MeshRenderer>()) {
			var mr = gameObject.AddComponent<MeshRenderer>();
			mr.useLightProbes = mr.receiveShadows = false;
			mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
		}

		if(occlusionShader == null)
			occlusionShader = Shader.Find("Hidden/AtmosphericScattering_Occlusion");

		m_occlusionMaterial = new Material(occlusionShader);
		m_occlusionMaterial.hideFlags = HideFlags.HideAndDontSave;

#if UNITY_EDITOR
		if(UnityEditor.GameObjectUtility.AreStaticEditorFlagsSet(gameObject, UnityEditor.StaticEditorFlags.BatchingStatic))
			throw new UnityException("AtmosphericScattering cannot be batching static!");
#endif

		m_isAwake = true;
	}


	void OnEnable() {
		if(!m_isAwake)
			return;

		UpdateKeywords(true);
		UpdateStaticUniforms();

		if(instance && instance != this)
			Debug.LogErrorFormat("Unexpected: AtmosphericScattering.instance already set (to: {0}). Still overriding with: {1}.", instance.name, name);
		
		instance = this;
	}

	void EnsureHookedLightSource(Light light) {
		if(!light)
			return;

		if(light.commandBufferCount == 2)
			return;

		//Debug.Log("Hooking scattering command buffers on light source: " + light.name);

		// NOTE: This doesn't really play nicely with other users of light events.
		//       Currently not an issue since this code was written pre-5.1 (and light events
		//       are a new feature in 5.1), but might need a proper solution in the future.
		light.RemoveAllCommandBuffers();

		if(m_occlusionCmdAfterShadows != null)
			m_occlusionCmdAfterShadows.Dispose();
		if(m_occlusionCmdBeforeScreen != null)
			m_occlusionCmdBeforeScreen.Dispose();

		m_occlusionCmdAfterShadows = new UnityEngine.Rendering.CommandBuffer();
		m_occlusionCmdAfterShadows.name = "Scatter Occlusion Pass 1";
		m_occlusionCmdAfterShadows.SetGlobalTexture("u_CascadedShadowMap", new UnityEngine.Rendering.RenderTargetIdentifier(UnityEngine.Rendering.BuiltinRenderTextureType.CurrentActive));
		m_occlusionCmdBeforeScreen = new UnityEngine.Rendering.CommandBuffer();
		m_occlusionCmdBeforeScreen.name = "Scatter Occlusion Pass 2";

		light.AddCommandBuffer(UnityEngine.Rendering.LightEvent.AfterShadowMap, m_occlusionCmdAfterShadows);
		light.AddCommandBuffer(UnityEngine.Rendering.LightEvent.BeforeScreenspaceMask, m_occlusionCmdBeforeScreen);
	}

	void OnDisable() {
		UpdateKeywords(false);

		if(instance != this) {
			if(instance)
				Debug.LogErrorFormat("Unexpected: AtmosphericScattering.instance set to: {0}, not to: {1}. Leaving alone.", instance.name, name);
		} else {
			instance = null;
		}
	}

	void UpdateKeywords(bool enable) {
		Shader.DisableKeyword("ATMOSPHERICS");
		Shader.DisableKeyword("ATMOSPHERICS_PER_PIXEL");
		Shader.DisableKeyword("ATMOSPHERICS_OCCLUSION");
		Shader.DisableKeyword("ATMOSPHERICS_OCCLUSION_FULLSKY");
		Shader.DisableKeyword("ATMOSPHERICS_OCCLUSION_EDGE_FIXUP");
		Shader.DisableKeyword("ATMOSPHERICS_SUNRAYS");
		Shader.DisableKeyword("ATMOSPHERICS_DEBUG");

		if(enable) {
			if (!forcePerPixel)
				Shader.EnableKeyword("ATMOSPHERICS");
			else
				Shader.EnableKeyword("ATMOSPHERICS_PER_PIXEL");
			
			if(useOcclusion) {
				Shader.EnableKeyword("ATMOSPHERICS_OCCLUSION");

				if(occlusionDepthFixup && occlusionDownscale != OcclusionDownscale.x1)
					Shader.EnableKeyword("ATMOSPHERICS_OCCLUSION_EDGE_FIXUP");

				if(occlusionFullSky)
					Shader.EnableKeyword("ATMOSPHERICS_OCCLUSION_FULLSKY");
			}

			if(debugMode != ScatterDebugMode.None)
				Shader.EnableKeyword("ATMOSPHERICS_DEBUG");
		}
	}

	public void OnValidate() {
		if(!m_isAwake)
			return;

		occlusionBias = Mathf.Clamp01(occlusionBias);
		occlusionBiasIndirect = Mathf.Clamp01(occlusionBiasIndirect);
		occlusionBiasClouds = Mathf.Clamp01(occlusionBiasClouds);
		occlusionBiasSkyRayleigh = Mathf.Clamp01(occlusionBiasSkyRayleigh);
		occlusionBiasSkyMie = Mathf.Clamp01(occlusionBiasSkyMie);
		worldScaleExponent = Mathf.Clamp(worldScaleExponent, 1f, 2f);
		worldNormalDistance = Mathf.Clamp(worldNormalDistance, 1f, 10000f);
		worldNearScatterPush = Mathf.Clamp(worldNearScatterPush, -200f, 300f);
		worldRayleighDensity = Mathf.Clamp(worldRayleighDensity, 0, 1000f);
		worldMieDensity = Mathf.Clamp(worldMieDensity, 0f, 1000f);
		worldRayleighIndirectScatter = Mathf.Clamp(worldRayleighIndirectScatter, 0, 1f);

		heightNormalDistance = Mathf.Clamp(heightNormalDistance, 1f, 10000f);
		heightNearScatterPush = Mathf.Clamp(heightNearScatterPush, -200f, 300f);
		heightRayleighDensity = Mathf.Clamp(heightRayleighDensity, 0, 1000f);
		heightMieDensity = Mathf.Clamp(heightMieDensity, 0, 1000f);
		
		worldMiePhaseAnisotropy = Mathf.Clamp01(worldMiePhaseAnisotropy);
		skyDomeExposure = Mathf.Clamp(skyDomeExposure, 0f, 8f);

		if(instance == this) {
			OnDisable();
			OnEnable();
		}

#if UNITY_EDITOR
		UnityEditor.SceneView.RepaintAll();
#endif
	}

	void OnWillRenderObject() {
		if(!m_isAwake)
			return;

		// Don't do recursive occlusion rendering (should probably disable
		// occlusion on nested cameras)
		if(m_currentCamera)
			return;

		var activeSun = AtmosphericScatteringSun.instance;
		if(!activeSun) {
			// When there's no primary light, mie scattering and occlusion will be disabled, so there's
			// nothing for us to update.
			UpdateDynamicUniforms();
			return;
		}

		EnsureHookedLightSource(activeSun.light);

		m_currentCamera = Camera.current;

		if((SystemInfo.graphicsShaderLevel >= 40 || depthTexture == DepthTexture.Enable) && m_currentCamera.depthTextureMode == DepthTextureMode.None)
			m_currentCamera.depthTextureMode = DepthTextureMode.Depth;
		else if(depthTexture == DepthTexture.Disable && m_currentCamera.depthTextureMode != DepthTextureMode.None)
			m_currentCamera.depthTextureMode = DepthTextureMode.None;

		UpdateDynamicUniforms();

		if(useOcclusion) {
			var camRgt = m_currentCamera.transform.right;
			var camUp = m_currentCamera.transform.up;
			var camFwd = m_currentCamera.transform.forward;
				
			var dy = Mathf.Tan(m_currentCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
			var dx = dy * m_currentCamera.aspect;
				
			var vpCenter = camFwd * m_currentCamera.farClipPlane;
			var vpRight = camRgt * dx * m_currentCamera.farClipPlane;
			var vpUp = camUp * dy * m_currentCamera.farClipPlane;

			m_occlusionMaterial.SetVector("u_CameraPosition", m_currentCamera.transform.position);
			m_occlusionMaterial.SetVector("u_ViewportCorner", vpCenter - vpRight - vpUp);
			m_occlusionMaterial.SetVector("u_ViewportRight", vpRight * 2f);
			m_occlusionMaterial.SetVector("u_ViewportUp", vpUp * 2f);
			var farDist = m_currentCamera ? m_currentCamera.farClipPlane : 1000f;
			var refDist = (Mathf.Min(farDist, QualitySettings.shadowDistance) - 1f) / farDist;
			m_occlusionMaterial.SetFloat("u_OcclusionSkyRefDistance", refDist);

			var srcRect = m_currentCamera.pixelRect;
			var downscale = 1f / (float)(int)occlusionDownscale;
			var occWidth = Mathf.RoundToInt(srcRect.width * downscale);
			var occHeight = Mathf.RoundToInt(srcRect.height * downscale);
			var occlusionId = Shader.PropertyToID("u_OcclusionTexture");	

			m_occlusionCmdBeforeScreen.Clear();
			m_occlusionCmdBeforeScreen.GetTemporaryRT(occlusionId, occWidth, occHeight, 0, FilterMode.Bilinear, RenderTextureFormat.R8, RenderTextureReadWrite.sRGB);
			m_occlusionCmdBeforeScreen.Blit(
                (RenderTargetIdentifier)0, 
				occlusionId,
				m_occlusionMaterial,
				(int)occlusionSamples
			);
			m_occlusionCmdBeforeScreen.SetGlobalTexture(occlusionId, occlusionId);
		}
	}

	void OnRenderObject() {
		if(m_currentCamera == Camera.current)
			m_currentCamera = null;
	}

	void UpdateStaticUniforms() {
		Shader.SetGlobalVector("u_SkyDomeOffset", skyDomeOffset);
		Shader.SetGlobalVector("u_SkyDomeScale", skyDomeScale);
		Shader.SetGlobalTexture("u_SkyDomeCube", skyDomeCube);
		Shader.SetGlobalFloat("u_SkyDomeExposure", skyDomeExposure);
		Shader.SetGlobalColor("u_SkyDomeTint", skyDomeTint);

		Shader.SetGlobalFloat("u_ShadowBias", useOcclusion ? occlusionBias : 1f);
		Shader.SetGlobalFloat("u_ShadowBiasIndirect", useOcclusion ? occlusionBiasIndirect : 1f);
		Shader.SetGlobalFloat("u_ShadowBiasClouds", useOcclusion ? occlusionBiasClouds : 1f);
		Shader.SetGlobalVector("u_ShadowBiasSkyRayleighMie", useOcclusion ? new Vector4(occlusionBiasSkyRayleigh, occlusionBiasSkyMie, 0f, 0f) : Vector4.zero);
		Shader.SetGlobalFloat("u_OcclusionDepthThreshold", occlusionDepthThreshold);

		Shader.SetGlobalFloat("u_WorldScaleExponent", worldScaleExponent);
		
		Shader.SetGlobalFloat("u_WorldNormalDistanceRcp", 1f/worldNormalDistance);
		Shader.SetGlobalFloat("u_WorldNearScatterPush", -Mathf.Pow(Mathf.Abs(worldNearScatterPush), worldScaleExponent) * Mathf.Sign(worldNearScatterPush));
		
		Shader.SetGlobalFloat("u_WorldRayleighDensity", -worldRayleighDensity / 100000f);
		Shader.SetGlobalFloat("u_MiePhaseAnisotropy", worldMiePhaseAnisotropy);
		Shader.SetGlobalVector("u_RayleighInScatterPct", new Vector4(1f - worldRayleighIndirectScatter, worldRayleighIndirectScatter, 0f, 0f));
		
		Shader.SetGlobalFloat("u_HeightNormalDistanceRcp", 1f/heightNormalDistance);
		Shader.SetGlobalFloat("u_HeightNearScatterPush", -Mathf.Pow(Mathf.Abs(heightNearScatterPush), worldScaleExponent) * Mathf.Sign(heightNearScatterPush));
		Shader.SetGlobalFloat("u_HeightRayleighDensity", -heightRayleighDensity / 100000f);
		
		Shader.SetGlobalFloat("u_HeightSeaLevel", heightSeaLevel);
		Shader.SetGlobalFloat("u_HeightDistanceRcp", 1f/heightDistance);
		Shader.SetGlobalVector("u_HeightPlaneShift", heightPlaneShift);
		Shader.SetGlobalVector("u_HeightRayleighColor", (Vector4)heightRayleighColor * heightRayleighIntensity);
		Shader.SetGlobalFloat("u_HeightExtinctionFactor", heightExtinctionFactor);
		Shader.SetGlobalFloat("u_RayleighExtinctionFactor", worldRayleighExtinctionFactor);
		Shader.SetGlobalFloat("u_MieExtinctionFactor", worldMieExtinctionFactor);
		
		var rayleighColorM20 = worldRayleighColorRamp.Evaluate(0.00f);
		var rayleighColorM10 = worldRayleighColorRamp.Evaluate(0.25f);
		var rayleighColorO00 = worldRayleighColorRamp.Evaluate(0.50f);
		var rayleighColorP10 = worldRayleighColorRamp.Evaluate(0.75f);
		var rayleighColorP20 = worldRayleighColorRamp.Evaluate(1.00f);
		
		var mieColorM20 = worldMieColorRamp.Evaluate(0.00f);
		var mieColorO00 = worldMieColorRamp.Evaluate(0.50f);
		var mieColorP20 = worldMieColorRamp.Evaluate(1.00f);
		
		Shader.SetGlobalVector("u_RayleighColorM20", (Vector4)rayleighColorM20 * worldRayleighColorIntensity);
		Shader.SetGlobalVector("u_RayleighColorM10", (Vector4)rayleighColorM10 * worldRayleighColorIntensity);
		Shader.SetGlobalVector("u_RayleighColorO00", (Vector4)rayleighColorO00 * worldRayleighColorIntensity);
		Shader.SetGlobalVector("u_RayleighColorP10", (Vector4)rayleighColorP10 * worldRayleighColorIntensity);
		Shader.SetGlobalVector("u_RayleighColorP20", (Vector4)rayleighColorP20 * worldRayleighColorIntensity);
		
		Shader.SetGlobalVector("u_MieColorM20", (Vector4)mieColorM20 * worldMieColorIntensity);
		Shader.SetGlobalVector("u_MieColorO00", (Vector4)mieColorO00 * worldMieColorIntensity);
		Shader.SetGlobalVector("u_MieColorP20", (Vector4)mieColorP20 * worldMieColorIntensity);

//		Shader.SetGlobalVector("u_SunRayStrengths", new Vector4(sunRayLightenStrength, sunRayDarkenStrength, 0f, 0f));

		Shader.SetGlobalFloat("u_AtmosphericsDebugMode", (int)debugMode);
	}

	void UpdateDynamicUniforms() {
		var activeSun = AtmosphericScatteringSun.instance;
		bool hasSun = !!activeSun;

		var trackedYaw = skyDomeTrackedYawRotation ? skyDomeTrackedYawRotation.eulerAngles.y : 0f;
		Shader.SetGlobalMatrix("u_SkyDomeRotation",
				Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(skyDomeRotation.x, 0f, 0f), Vector3.one)
				* Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, skyDomeRotation.y - trackedYaw, 0f), Vector3.one)
                * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1f, skyDomeVerticalFlip ? -1f : 1f, 1f))

		);

		Shader.SetGlobalVector("u_SunDirection", hasSun ? -activeSun.transform.forward : Vector3.down);	
		Shader.SetGlobalFloat("u_WorldMieDensity", hasSun ? -worldMieDensity / 100000f : 0f);
		Shader.SetGlobalFloat("u_HeightMieDensity", hasSun ? -heightMieDensity / 100000f : 0f);

		var pixelRect = m_currentCamera ? m_currentCamera.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);
		var scale = (float)(int)occlusionDownscale;
		var depthTextureScaledTexelSize = new Vector4(scale / pixelRect.width, scale / pixelRect.height, -scale / pixelRect.width, -scale / pixelRect.height);
		Shader.SetGlobalVector("u_DepthTextureScaledTexelSize", depthTextureScaledTexelSize);
	}
}