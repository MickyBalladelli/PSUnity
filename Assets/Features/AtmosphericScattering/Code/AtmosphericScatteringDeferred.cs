using UnityEngine;

[ExecuteInEditMode]
public class AtmosphericScatteringDeferred : UnityStandardAssets.ImageEffects.PostEffectsBase {
	[HideInInspector] public Shader deferredFogShader = null;

	Material m_fogMaterial;
	
	public override bool CheckResources() {
		CheckSupport (true);
		
		if(!deferredFogShader)
			deferredFogShader = Shader.Find("Hidden/AtmosphericScattering_Deferred");

		m_fogMaterial = CheckShaderAndCreateMaterial(deferredFogShader, m_fogMaterial);
		
		if(!isSupported)
			ReportAutoDisable();

		return isSupported;
	}
	
	[ImageEffectOpaque]
	void OnRenderImage(RenderTexture source, RenderTexture destination) {
		Camera cam = GetComponent<Camera>();
		
		if(!CheckResources() || !cam || cam.actualRenderingPath != RenderingPath.DeferredShading) {
			Graphics.Blit (source, destination);
			return;
		}

		Transform camtr = cam.transform;
		float camNear = cam.nearClipPlane;
		float camFar = cam.farClipPlane;
		float camFov = cam.fieldOfView;
		float camAspect = cam.aspect;
		
		Matrix4x4 frustumCorners = Matrix4x4.identity;
		
		float fovWHalf = camFov * 0.5f;
		
		Vector3 toRight = camtr.right * camNear * Mathf.Tan (fovWHalf * Mathf.Deg2Rad) * camAspect;
		Vector3 toTop = camtr.up * camNear * Mathf.Tan (fovWHalf * Mathf.Deg2Rad);
		
		Vector3 topLeft = (camtr.forward * camNear - toRight + toTop);
		float camScale = topLeft.magnitude * camFar/camNear;
		
		topLeft.Normalize();
		topLeft *= camScale;
		
		Vector3 topRight = camtr.forward * camNear + toRight + toTop;
		topRight.Normalize();
		topRight *= camScale;
		
		Vector3 bottomRight = camtr.forward * camNear + toRight - toTop;
		bottomRight.Normalize();
		bottomRight *= camScale;
		
		Vector3 bottomLeft = camtr.forward * camNear - toRight - toTop;
		bottomLeft.Normalize();
		bottomLeft *= camScale;
		
		frustumCorners.SetRow(0, topLeft);
		frustumCorners.SetRow(1, topRight);
		frustumCorners.SetRow(2, bottomRight);
		frustumCorners.SetRow(3, bottomLeft);
		
		var camPos= camtr.position;
		m_fogMaterial.SetMatrix("_FrustumCornersWS", frustumCorners);
		m_fogMaterial.SetVector("_CameraWS", camPos);

		CustomGraphicsBlit(source, destination, m_fogMaterial, 0);
	}
	
	static void CustomGraphicsBlit(RenderTexture src, RenderTexture dst, Material mat, int pass) {
		RenderTexture.active = dst;
		
		mat.SetTexture("_MainTex", src);
		
		GL.PushMatrix();
		GL.LoadOrtho();
		
		mat.SetPass(pass);
		
		GL.Begin(GL.QUADS);
		
		GL.MultiTexCoord2(0, 0.0f, 0.0f);
		GL.Vertex3(0.0f, 0.0f, 3.0f); // BL
		
		GL.MultiTexCoord2(0, 1.0f, 0.0f);
		GL.Vertex3(1.0f, 0.0f, 2.0f); // BR
		
		GL.MultiTexCoord2(0, 1.0f, 1.0f);
		GL.Vertex3(1.0f, 1.0f, 1.0f); // TR
		
		GL.MultiTexCoord2(0, 0.0f, 1.0f);
		GL.Vertex3(0.0f, 1.0f, 0.0f); // TL
		
		GL.End();
		GL.PopMatrix();
	}
}
