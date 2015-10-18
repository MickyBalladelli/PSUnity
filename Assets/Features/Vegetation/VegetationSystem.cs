using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using PhatCell = VegetationBakeData.PhatCell;

[ExecuteInEditMode]
public class VegetationSystem : MonoBehaviour {
	public enum RenderMode { Hybrid, Sorted, Tested }
	public enum DebugMode { DBG_NONE, DBG_ALBEDO, DBG_VTXCOLOR, DBG_DIFFUSE, DBG_VTXOCCLUSION, DBG_INDIRECT, DBG_SPECULAR, DBG_LIGHTING }

	public VegetationBakeData vegetationData;

	public RenderMode renderMode;

	public float cullingDistance = 200f;
	public float densityFalloffDistance = 200f;
	public float lodCullFactor = 0.15f;

	public float	pruneFreeDistance = 15f;
	public float	pruneDistanceScale = 33f;
	public Vector2	pruneRefCountsSpriteMesh = new Vector2(700f, 2200f);

	public float indirectResolution = 0.25f;
	public float indirectColorScale = 1f;
	public float indirectProbeDirection = 0.8f;
	public int indirectMaxUpdatesPerFrame = 100;
	public int indirectMaxUpdatesPerFrameForce = 1750;
	
	public bool receiveShadows = true;
	public bool castShadows = false;
	public bool castShadowsOutline = false;

	public int prePassCount = 6;

	public bool useSpriteCameraUp = true;

	public Color specColor = Color.gray;
	public float specPower = 12f;
	public Color specColorMesh = Color.gray;
	public float specPowerMesh = 12f;
	public float directOcclusionBoost = 0.15f;

	public DebugMode debugMode = DebugMode.DBG_NONE;
	public bool debugHideWireframe;
	public bool debugShowCellsInNonPlay;

	[System.Serializable]
	public struct SlimCell {
		public byte		sector;
		public uint[]	patches;
		public int[]	sindices, dindices;
		public Mesh		mesh;
		
		public SlimCell(PhatCell ph) {
			sector = 0xFF;
			patches = ph.patches;
			sindices = ph.indices;
			dindices = sindices != null ? (int[])sindices.Clone() : null;
			mesh = ph.mesh;
		}
	}
	
	[HideInInspector][SerializeField] SlimCell[]	m_slimCells;

	[HideInInspector][SerializeField] MeshFilter[]	m_nearMeshes;

	[HideInInspector][SerializeField] int			m_dataPointsInSectorView;
	[HideInInspector][SerializeField] byte[]		m_sectorViewOrder;
	[HideInInspector][SerializeField] Vector2[]		m_cellCenterArray;
	[HideInInspector][SerializeField] byte[]		m_cellSectorArray;
	[HideInInspector][SerializeField] uint[]		m_cellSortArray;

	[HideInInspector][SerializeField] int			m_indCellsX, m_indCellsZ;
	[HideInInspector][SerializeField] Vector3[]		m_indSamplePoints;
	[HideInInspector][SerializeField] Texture2D		m_indirectLighting;
	
	[HideInInspector][SerializeField] public bool	isBuilt;

	Coroutine	m_indirectCoroutine;
	Vector3		m_editorPrevLightDirection;
	float		m_editorPrevLightBounce;
	Color		m_editorPrevLightColor;
	RenderMode	m_renderMode;

	Vector3	Vector3XZ(Vector3 v) { return new Vector3(v.x, 0f, v.z); }
	Vector2	Vector2XZ(Vector3 v) { return new Vector2(v.x, v.z); }
	int		IndSampleIndex(int x, int z) { return z * m_indCellsX + x; }
	
	void Start() {
		debugMode = DebugMode.DBG_NONE;
		
		if(isBuilt && Application.isPlaying)
			m_indirectCoroutine = StartCoroutine(UpdateIndirectTexture(true));
	}

	void OnValidate() {
		UpdateMaterials();
		indirectProbeDirection = Mathf.Clamp01(indirectProbeDirection);
	}

	void OnEnable() {
		OnValidate();
	}

	public void UpdateMaterials() {
		if(isBuilt) {
			UpdateMaterial(vegetationData.growthMaterialOutline, false);
			UpdateMaterial(vegetationData.growthMaterialOpaque, false);
			UpdateMaterial(vegetationData.growthMaterialHard, false);
			UpdateMaterial(vegetationData.growthMaterialOpaqueMesh, true);
			UpdateKeywords(vegetationData.growthMaterialOutline);
			UpdateKeywords(vegetationData.growthMaterialOpaque);
			UpdateKeywords(vegetationData.growthMaterialHard);
			UpdateKeywords(vegetationData.growthMaterialOpaqueMesh);
		}
	}
	public void SetRenderMode(RenderMode mode) {
		if(mode != m_renderMode) {
			while(transform.childCount > 0)
				Object.DestroyImmediate(transform.GetChild(0).gameObject);

			SetupRenderObjects();
		}
	}

	public void Rebuild() {
		Teardown();

		Profiler.BeginSample("Sorting Setup");
		CreateSortingStructures();
		Profiler.EndSample();
		
		Profiler.BeginSample("Indirect Setup");
		SetupIndirectTexture();
		Profiler.EndSample();

		UpdateMaterial(vegetationData.growthMaterialOutline, false);
		UpdateMaterial(vegetationData.growthMaterialOpaque, false);
		UpdateMaterial(vegetationData.growthMaterialHard, false);
		UpdateMaterial(vegetationData.growthMaterialOpaqueMesh, true);
		UpdateKeywords(vegetationData.growthMaterialOutline);
		UpdateKeywords(vegetationData.growthMaterialOpaque);
		UpdateKeywords(vegetationData.growthMaterialHard);
		UpdateKeywords(vegetationData.growthMaterialOpaqueMesh);

		Profiler.BeginSample("Rendering Setup");
		SetupRenderObjects();
		Profiler.EndSample();

		CreateWillRenderProxy();

		isBuilt = true;
	}

	public void Teardown() {
		isBuilt = false;

		m_slimCells = null;
		m_nearMeshes = null;
		m_sectorViewOrder = null;
		m_cellCenterArray = null;
		m_cellSectorArray = null;
		m_cellSortArray = null;		
		m_indSamplePoints = null;
		m_editorPrevLightDirection = Vector3.up;
		
		if(m_indirectCoroutine != null)
			StopCoroutine(m_indirectCoroutine);
		m_indirectCoroutine = null;

		Object.DestroyImmediate(m_indirectLighting);
		m_indirectLighting = null;

		foreach(var pc in vegetationData.m_phatCells)
			pc.renderer = null;

		while(transform.childCount > 0)
			Object.DestroyImmediate(transform.GetChild(0).gameObject);
	}

	void CreateSortingStructures() {
		Profiler.BeginSample("Sorting structures");
		
		var cellCount = vegetationData.m_phatCells.Count;
		m_cellCenterArray = new Vector2[cellCount];
		m_cellSectorArray = new byte[cellCount];
		m_cellSortArray = new uint[cellCount];
		m_slimCells = new SlimCell[cellCount];
		
		for(int i = 0; i < cellCount; ++i) {
			var phatCell = vegetationData.m_phatCells[i];
			m_cellCenterArray[i] = new Vector2(phatCell.bounds.center.x, phatCell.bounds.center.z);
			m_slimCells[i] = new SlimCell(phatCell);
			m_cellSortArray[i] = (uint)i;
			// m_cellSectorArray is filled each frame update (so is m_cellSortArray but depends on prev data)
		}

		m_dataPointsInSectorView = vegetationData.m_patchesInCellSide * vegetationData.m_patchesInCellSide;
		m_sectorViewOrder = new byte[vegetationData.viewSectors * m_dataPointsInSectorView];
		var cellCenter = new Vector2(vegetationData.cellSize * 0.5f, vegetationData.cellSize * 0.5f);
		var halfPatch = new Vector2(vegetationData.patchSize * 0.5f, vegetationData.patchSize * 0.5f);
		var patchSortList = new uint[vegetationData.m_patchesInCellSide * vegetationData.m_patchesInCellSide];
		for(int s = 0; s < vegetationData.viewSectors; ++s) {
			var dir = SectorCenterDir(s);
			var point = cellCenter + dir * vegetationData.cellSize;
			
			for(int z = 0; z < vegetationData.m_patchesInCellSide; ++z) {
				for(int x = 0; x < vegetationData.m_patchesInCellSide; ++x) {
					var patchCenter = new Vector2(x * vegetationData.patchSize, z * vegetationData.patchSize) + halfPatch;
					var patchDistance = Vector2.Distance(patchCenter, point);
					//Debug.LogFormat("S {0} X {1} Z {2} D {3} DI{4}", s, x, z, patchDistance, Mathf.RoundToInt(patchDistance * 100f));
					var patchIdx = z * vegetationData.m_patchesInCellSide + x;
					var patchSort = ((uint)Mathf.RoundToInt(patchDistance * 250f) << 16) | (uint)patchIdx;
					patchSortList[patchIdx] = patchSort;
				}
			}
			
			System.Array.Sort(patchSortList);
			
			//TODO: Check byte overflow
			
			for(int p = 0, pn = patchSortList.Length; p < pn; ++p)
				m_sectorViewOrder[s * m_dataPointsInSectorView + p] = (byte)patchSortList[pn - p - 1];
		}
		
		Profiler.EndSample();
	}

	byte SectorFromVector(Vector2 v) {
		var vn = v.normalized;
		float a = Mathf.Acos(vn.x);
		float b = vn.y >= 0f ? a : VegetationBakeData.MPI2 - a;

if ((b * vegetationData.m_angleToSector) >= (float)vegetationData.viewSectors)
	Debug.LogErrorFormat ("OOB: {0}  {1} {2}", b * vegetationData.m_angleToSector, b, vegetationData.m_angleToSector);

		return (byte)Mathf.FloorToInt(b * vegetationData.m_angleToSector);
	}

	Vector2 SectorCenterDir(int s) {
		float d = 360f / (float)vegetationData.viewSectors;
		float a = s * d + d * 0.5f;
		return Vector2XZ(Quaternion.Euler(0f, -a, 0f) * Vector3.right);
	}

	void SetupIndirectTexture() {
		m_indCellsX = 1 + Mathf.CeilToInt(vegetationData.m_bounds.size.x * indirectResolution);
		m_indCellsZ = 1 + Mathf.CeilToInt(vegetationData.m_bounds.size.z * indirectResolution);

		m_indirectLighting = new Texture2D(m_indCellsX, m_indCellsZ, TextureFormat.RGB24, false);
		m_indirectLighting.filterMode = FilterMode.Bilinear;
		m_indirectLighting.wrapMode = TextureWrapMode.Clamp;

		// Need more reliable height info here.. (something matching paintjob filtering)
		m_indSamplePoints = new Vector3[m_indCellsX * m_indCellsZ];

		var height = vegetationData.m_bounds.size.y;
		var corner = new Vector3(vegetationData.m_boundsCorner.x, vegetationData.m_boundsCorner.y + height, vegetationData.m_boundsCorner.z); 
		var stepX = vegetationData.m_bounds.size.x / (float)m_indCellsX;
		var stepZ = vegetationData.m_bounds.size.z / (float)m_indCellsZ;

		for(int z = 0; z < m_indCellsZ; ++z) {
			for(int x = 0; x < m_indCellsX; ++x) {
				var offset = new Vector3(x * stepX, 0f, z * stepZ);
				var pos = corner + offset;

				RaycastHit rhiSamplePoint;
				if(Physics.Raycast(pos, Vector3.down, out rhiSamplePoint, height * 1.5f))
					m_indSamplePoints[IndSampleIndex(x, z)] = rhiSamplePoint.point + Vector3.up * 0.25f;
				else
					m_indSamplePoints[IndSampleIndex(x, z)] = pos + Vector3.down * height * 0.25f;
			}
		}

		ForceUpdateIngameIndirect();
	}

	public void ForceUpdateIngameIndirect() {
		if(Application.isPlaying) {
			if(m_indirectCoroutine != null)
				StopCoroutine(m_indirectCoroutine);

			m_indirectCoroutine = StartCoroutine(UpdateIndirectTexture(true, indirectMaxUpdatesPerFrameForce));
		}
	}

	IEnumerator UpdateIndirectTexture(bool loop, int forceIterations = -1) {
		float fSqrtPI = Mathf.Sqrt(Mathf.PI);
		float fC0 = 1.0f / (2.0f*fSqrtPI);
		float fC1 = (float)Mathf.Sqrt(3.0f)  / (3.0f*fSqrtPI);
		float fC2 = (float)Mathf.Sqrt(15.0f) / (8.0f*fSqrtPI);
		float fC3 = (float)Mathf.Sqrt(5.0f)  / (16.0f*fSqrtPI);
		float fC4 = 0.5f * fC2;
		var aSample = new float[27];
		var avCoeff = new Vector4[7];
		
		if(LightmapSettings.lightProbes == null || LightmapSettings.lightProbes.count == 0) {
			Debug.LogWarning("No light probes found. Setting indirect light to scaled gray.");

			for(int z = 0; z < m_indCellsZ; ++z)
				for(int x = 0; x < m_indCellsX; ++x)
					m_indirectLighting.SetPixel(x, z, Color.gray * indirectColorScale);
					
			m_indirectLighting.Apply(false, false);
			yield break;
		}

		UnityEngine.Rendering.SphericalHarmonicsL2 shL2;
		//var ambientLight = RenderSettings.ambientLight;
		Vector3 vRGB;
		Color cRGB = new Color(0f, 0f, 0f, 1f);

		int maxIterations = forceIterations > 0 ? forceIterations : indirectMaxUpdatesPerFrame;
	loopyloop:
		//Debug.LogFormat("Cells to update {0} at {1} iters. {2} frames.", m_indCellsX * m_indCellsZ, maxIterations, m_indCellsX * m_indCellsZ / maxIterations);
		int yieldCounter = 0;
		for(int z = 0; z < m_indCellsZ; ++z) {
			for(int x = 0; x < m_indCellsX; ++x) {
				//Profiler.BeginSample("SampleGICell");

				LightProbes.GetInterpolatedProbe(m_indSamplePoints[IndSampleIndex(x, z)], null, out shL2);
				var ambProbe = RenderSettings.ambientProbe;

				shL2 = shL2 + ambProbe;
if(false) {
				for(int c0 = 0; c0 < 9; ++c0)
					for(int c1 = 0; c1 < 3; ++c1)
					aSample[c0 * 3 + c1] = shL2[c1, c0];

				for(int iC = 0; iC < 3; ++iC) {                              
					avCoeff[iC].y =-fC1 * aSample[iC+3];               
					avCoeff[iC].w = fC0 * aSample[iC+0] - fC3*aSample[iC+18];
				}
				
				avCoeff[6].x = fC4 * aSample[24];
				avCoeff[6].y = fC4 * aSample[25];
				avCoeff[6].z = fC4 * aSample[26];          
				
				vRGB.x = avCoeff[0].y + avCoeff[0].w;
				vRGB.y = avCoeff[1].y + avCoeff[1].w;
				vRGB.z = avCoeff[2].y + avCoeff[2].w;
				vRGB.x -= avCoeff[6].x;
				vRGB.y -= avCoeff[6].y;
				vRGB.z -= avCoeff[6].z;
				vRGB *= 2f;
} else {
				for(int c0 = 0; c0 < 9; ++c0)
					for(int c1 = 0; c1 < 3; ++c1)
						aSample[c0 * 3 + c1] = shL2[c1, c0];

				for ( int iC=0; iC<3; iC++ ) {              
					avCoeff[iC].x =-fC1 * aSample[iC+9];
					avCoeff[iC].y =-fC1 * aSample[iC+3];
					avCoeff[iC].z = fC1 * aSample[iC+6];               
					avCoeff[iC].w = fC0 * aSample[iC+0] - fC3*aSample[iC+18];
				}
				
				for ( int iC=0; iC<3; iC++ )   
				{
					avCoeff[iC+3].x =        fC2 * aSample[iC+12];
					avCoeff[iC+3].y =       -fC2 * aSample[iC+15];
					avCoeff[iC+3].z = 3.0f * fC3 * aSample[iC+18];
					avCoeff[iC+3].w =       -fC2 * aSample[iC+21];
				}
				
				avCoeff[6].x = fC4 * aSample[24];
				avCoeff[6].y = fC4 * aSample[25];
				avCoeff[6].z = fC4 * aSample[26];
				avCoeff[6].w = 1.0f;
				
				var vRGBUp = ShadeSH9(avCoeff, new Vector4(0f, 1f, 0f, 1f));
				var vRGBDown = ShadeSH9(avCoeff, new Vector4(0f, -1f, 0f, 1f));
				
				// up and down flipped in coeffs?
				vRGB = Vector3.Lerp(vRGBDown, vRGBUp, indirectProbeDirection);

				//var basePos = m_indSamplePoints[IndSampleIndex(x, z)] + Vector3.one;
				//Debug.DrawLine(basePos, basePos + Vector3.up * 0.75f, new Color(vRGBUp.x, vRGBUp.y, vRGBUp.z, 1f), 15f, true);
				//Debug.DrawLine(basePos, basePos - Vector3.up * 0.75f, new Color(vRGBDown.x, vRGBDown.y, vRGBDown.z, 1f), 15f, true);
}

				cRGB.r = vRGB.x * indirectColorScale;
				cRGB.g = vRGB.y * indirectColorScale;
				cRGB.b = vRGB.z * indirectColorScale;
				m_indirectLighting.SetPixel(x, z, cRGB);
				
				//Profiler.EndSample();
				if(++yieldCounter > maxIterations) {
					yieldCounter = 0;
					yield return null;
				}
			}
		}
		
		//Profiler.BeginSample("UploadGIData");
		m_indirectLighting.Apply(false, false);

		// Re-assign for 'editor weirdness'?
		vegetationData.growthMaterialOutline.SetTexture("_GITexture", m_indirectLighting);
		vegetationData.growthMaterialOpaque.SetTexture("_GITexture", m_indirectLighting);
		vegetationData.growthMaterialOpaqueMesh.SetTexture("_GITexture", m_indirectLighting);
		vegetationData.growthMaterialHard.SetTexture("_GITexture", m_indirectLighting);

		//Debug.Log("Updating vegetation GI");

		//Profiler.EndSample();
		yield return null;

		maxIterations = indirectMaxUpdatesPerFrame;
		
		if(loop)
			goto loopyloop;
	}

	Vector3 ShadeSH9(Vector4[] avCoeff, Vector4 vNormal) {
		// Linear and constant polynomial terms
		Vector3 vRGB;
		vRGB.x = Vector4.Dot( avCoeff[0], vNormal );
		vRGB.y = Vector4.Dot( avCoeff[1], vNormal );
		vRGB.z = Vector4.Dot( avCoeff[2], vNormal );
		
		// 4 of the quadratic polynomials
		Vector4 vB;
		vB.x = vNormal.x*vNormal.y;
		vB.y = vNormal.y*vNormal.z;
		vB.z = vNormal.z*vNormal.z;
		vB.w = vNormal.z*vNormal.x;
		
		vRGB.x += Vector4.Dot( avCoeff[3], vB );
		vRGB.y += Vector4.Dot( avCoeff[4], vB );
		vRGB.z += Vector4.Dot( avCoeff[5], vB );
		
		// Final quadratic polynomial
		float fC = vNormal.x*vNormal.x - vNormal.y*vNormal.y;
		vRGB.x += fC * avCoeff[6].x;
		vRGB.y += fC * avCoeff[6].y;
		vRGB.z += fC * avCoeff[6].z;

		return vRGB;
	}

	void UpdateMaterial(Material m, bool mesh) {
		m.SetTexture("_GITexture", m_indirectLighting);
		m.SetColor("_SpecColor", mesh ? specColorMesh : specColor);
		m.SetFloat("_SpecPower", mesh ? specPowerMesh : specPower);
		m.SetFloat("_DirectOcclusionBoost", directOcclusionBoost);
		m.SetInt("_SpriteCameraUp", useSpriteCameraUp ? 1 : 0);
		m.SetFloat("_DensityFalloffDistance", densityFalloffDistance);
		m.SetFloat("_PruneFreeDistance", pruneFreeDistance);
		m.SetFloat("_PruneDistanceScale", pruneDistanceScale);
		m.SetVector("_PruneRefCountsSpriteMesh", pruneRefCountsSpriteMesh);
	}

	void UpdateKeywords(Material m) {
		foreach(var n in System.Enum.GetNames(typeof(DebugMode)))
			m.DisableKeyword(n);
			
		m.EnableKeyword(debugMode.ToString());
	}

	void SetupRenderObjects() {
		var globalCastShadows = (castShadows && renderMode != RenderMode.Sorted) || castShadowsOutline ? UnityEngine.Rendering.ShadowCastingMode.TwoSided : UnityEngine.Rendering.ShadowCastingMode.Off;
		var globalMat = renderMode == RenderMode.Sorted ? vegetationData.growthMaterialOutline : (renderMode == RenderMode.Tested ? vegetationData.growthMaterialHard : vegetationData.growthMaterialOpaque);
		var hasNear = renderMode != RenderMode.Tested;
		var nearCastShadows = (castShadows && renderMode == RenderMode.Sorted) ? UnityEngine.Rendering.ShadowCastingMode.TwoSided : UnityEngine.Rendering.ShadowCastingMode.Off;
		var nearMat = renderMode == RenderMode.Sorted ? vegetationData.growthMaterialOpaque : vegetationData.growthMaterialOutline;
		
		foreach(var pc in vegetationData.m_phatCells) { 
			if(pc.mesh == null)
				continue;

			var go = new GameObject(string.Format("g_cell_{0:00}_{1:00}__v_{2:00000}", pc.x, pc.z, pc.mesh.vertexCount));
			go.layer = gameObject.layer;
			go.AddComponent<MeshFilter>().sharedMesh = pc.mesh;
			var goMR = pc.renderer = go.AddComponent<MeshRenderer>();
			AddLODCuller(go, goMR);
			if(renderMode == RenderMode.Sorted)
				goMR.sortingLayerName = "GrassCell";
			goMR.sharedMaterial = globalMat;
			goMR.receiveShadows = receiveShadows;
			goMR.shadowCastingMode = globalCastShadows;
			goMR.useLightProbes = false;
			goMR.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
			go.transform.parent = transform;
#if UNITY_EDITOR
			UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, UnityEditor.StaticEditorFlags.OccludeeStatic);
			UnityEditor.EditorUtility.SetSelectedWireframeHidden(goMR, debugHideWireframe);
#endif
		}

		if(hasNear) {
			m_nearMeshes = new MeshFilter[Mathf.Min(prePassCount, vegetationData.m_phatCells.Count)];
			for(int i = 0, n = m_nearMeshes.Length; i < n; ++i) {
				var go = new GameObject("n_cell_variable");
				go.layer = gameObject.layer;
				m_nearMeshes[i] = go.AddComponent<MeshFilter>();
				var goMR = go.AddComponent<MeshRenderer>();
				AddLODCuller(go, goMR);
				if(renderMode == RenderMode.Hybrid)
					goMR.sortingLayerName = "GrassCell2";
				goMR.sharedMaterial = nearMat;
				goMR.receiveShadows = receiveShadows;
				goMR.shadowCastingMode = nearCastShadows;
				goMR.useLightProbes = false;
				goMR.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
				go.transform.parent = transform;
	#if UNITY_EDITOR
				UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, UnityEditor.StaticEditorFlags.OccludeeStatic);
				UnityEditor.EditorUtility.SetSelectedWireframeHidden(goMR, debugHideWireframe);
	#endif
			}
		}

		foreach(var pc in vegetationData.m_phatCells) {
			if(pc.mesh2 == null)
				continue;

			var go = new GameObject(string.Format("g_cell_M_{0:00}_{1:00}__v_{2:00000}", pc.x, pc.z, pc.mesh2.vertexCount));
			go.layer = gameObject.layer;
			go.AddComponent<MeshFilter>().sharedMesh = pc.mesh2;
			var goMR = go.AddComponent<MeshRenderer>();
			AddLODCuller(go, goMR);
			goMR.sharedMaterial = vegetationData.growthMaterialOpaqueMesh;
			goMR.receiveShadows = receiveShadows;
			goMR.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.TwoSided : UnityEngine.Rendering.ShadowCastingMode.Off;
			goMR.useLightProbes = false;
			goMR.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
			go.transform.parent = transform;
#if UNITY_EDITOR
			UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, UnityEditor.StaticEditorFlags.OccludeeStatic);
			UnityEditor.EditorUtility.SetSelectedWireframeHidden(goMR, debugHideWireframe);
#endif
		}

		m_renderMode = renderMode;
	}

	void AddLODCuller(GameObject go, MeshRenderer mr) {
		var lg = go.AddComponent<LODGroup>();
		lg.SetLODs(new[]{new LOD(lodCullFactor, new[]{mr})});
	}

	void CreateWillRenderProxy() {
		var mf = GetComponent<MeshFilter>();
		if(!mf)
			mf = gameObject.AddComponent<MeshFilter>();

		if(mf.sharedMesh == null) {
			var m = new Mesh();
			m.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
			mf.sharedMesh = m;
		}

		var mr = GetComponent<MeshRenderer>();
		if(!mr) {
			mr = gameObject.AddComponent<MeshRenderer>();		
			mr.useLightProbes = false;
			mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
			mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			mr.receiveShadows = false;
		}
	}
	
	void SortPatches(Vector3 point) {
		if(m_renderMode == RenderMode.Tested)
			return;

		Profiler.BeginSample("Vegetation Sort Patches");

		for(int i = 0, n = m_slimCells.Length; i < n; ++i) {
			var slimCell = m_slimCells[i];
			var sector = m_cellSectorArray[i];

			if(slimCell.sector != sector && slimCell.mesh && slimCell.sindices != null) {
				Profiler.BeginSample("Reordering cell");

				var orderOffset = m_dataPointsInSectorView * sector;

				var ptch = slimCell.patches;
				var sidc = slimCell.sindices;
				var didc = slimCell.dindices;
				for(int o = 0, off = 0, on = m_dataPointsInSectorView; o < on; ++o) {
					var patch = ptch[m_sectorViewOrder[orderOffset + o]];

					if(patch != 0u) {
						var start = (int)(patch >> 12);
						var count = (int)(patch & 0xFFF);
						System.Buffer.BlockCopy(sidc, start << 2, didc, off << 2, count << 2);
						off += count;
					}
				}

				slimCell.mesh.triangles = didc;

#if _DBG_ORDER_COLOR
				var col = slimCell.mesh.colors;
				var den = 1f / (float)(didc.Length/6);
				for(int ii = 0; ii < didc.Length; ++ii) {
					var ccc = col[didc[ii]];
					ccc.a = (ii/6) * den;
					col[didc[ii]] = ccc;
				}
				slimCell.mesh.colors = col;
				slimCell.mesh.UploadMeshData(false);
#endif

				slimCell.sector = sector;
				m_slimCells[i] = slimCell;

				Profiler.EndSample();
			}
		}
	
		Profiler.EndSample();
	}

	void SortCells(Vector3 point, Plane[] cullPlanes) {
		Profiler.BeginSample("Vegetation Sort Cells");

		for(int i = 0, n = m_cellSortArray.Length; i < n; ++i) {
			var sortedIdx = (int)(m_cellSortArray[i] & 0x7FF);
			var phatCell = vegetationData.m_phatCells[sortedIdx];

			var cellToPoint = Vector2XZ(point - phatCell.bounds.center);
			var d2 = cellToPoint.sqrMagnitude;
			m_cellSortArray[i] = (((uint)Mathf.Clamp(Mathf.RoundToInt(d2 * 10f), 0, 0x1FFFFF)) << 11) | (uint)sortedIdx;

			m_cellSectorArray[sortedIdx] = SectorFromVector(cellToPoint);
		}

		if(m_renderMode != RenderMode.Tested) {
			// Might want to do an explicit pre-sort with something that handles a random
			// dataset well when camera cuts between locations. 
			InsertionSortInPlace(m_cellSortArray);
			
			for(int i = 0, n = m_cellSortArray.Length, j = 0; i < n; ++i) {
				var c = (int)(m_cellSortArray[i] & 0x7FF);
				var pc = vegetationData.m_phatCells[c];
				if(!pc.renderer)
					continue;

				if(m_renderMode == RenderMode.Sorted)
					pc.renderer.sortingOrder = n - i;

				if(j < prePassCount) {
					if(GeometryUtility.TestPlanesAABB(cullPlanes, pc.renderer.bounds)) {
						var m = pc.mesh; 
						var pp = m_nearMeshes[j++];
						if(pp.sharedMesh != m) {
							pp.sharedMesh = m;
							pp.GetComponent<LODGroup>().RecalculateBounds(); // TODO: check how expensive this really is
						}
						if(m_renderMode == RenderMode.Hybrid)
							pp.GetComponent<MeshRenderer>().sortingOrder = n - i;
					}
				}
			}
		}

		Profiler.EndSample();
	}
	
	public void TickIndirectLighting(bool forced = false) {
		// Only update indirect on lighting changes in non-play mode
		// (it runs in a coroutine in playmode)
		if(Application.isEditor && !Application.isPlaying) {
			var updateGI = forced;
			if(!updateGI) {
				foreach(var l in Object.FindObjectsOfType<Light>()) {
					if(l.type != LightType.Directional || l.bounceIntensity <= 1e-4f)
						continue;
					
					if(l.transform.forward != m_editorPrevLightDirection || l.bounceIntensity != m_editorPrevLightBounce || l.color != m_editorPrevLightColor) {
						m_editorPrevLightDirection = l.transform.forward;
						m_editorPrevLightBounce = l.bounceIntensity;
						m_editorPrevLightColor = l.color;
						updateGI = true;
						break;
					}
				}
			}

			if(updateGI) {				
				for(var it = UpdateIndirectTexture(false); it.MoveNext();)
					;
			}
		}
	}
	
	void OnWillRenderObject() {
		if(!isBuilt)
			return;

		TickIndirectLighting();
		TickUpdate(Camera.current);
	}

	void TickUpdate(Camera cam) {
		var camPos = cam.transform.position;
		var camPlanes = GeometryUtility.CalculateFrustumPlanes(cam);
		SortCells(camPos, camPlanes);
		SortPatches(camPos);
	}

	static public void InsertionSortInPlace(uint[] a) {
		for(int i = 1, n = a.Length; i < n; ++i) {
			var t = a[i];
			int j = i;
			for(; j > 0 && a[j-1] > t; --j)
				a[j] = a[j-1];
			a[j] = t;
		}
	}

#if UNITY_EDITOR
	void OnDrawGizmosSelected() {
		if(!isBuilt)
			return;

		if(Application.isPlaying || debugShowCellsInNonPlay) {
			Gizmos.color = Color.gray;
			foreach(var pc in vegetationData.m_phatCells)
				Gizmos.DrawWireCube(pc.bounds.center, pc.bounds.size);

			Gizmos.color = Color.green;
			Gizmos.DrawWireCube(vegetationData.m_bounds.center, vegetationData.m_bounds.size);
		}

		if(!Application.isPlaying) {
			Gizmos.color = Color.green;
			Gizmos.DrawWireCube(vegetationData.m_bounds.center, vegetationData.m_bounds.size);
		}
	}

	void OnDrawGizmos() {
		Gizmos.color = Color.green;
		Gizmos.DrawSphere(transform.position, 1f);
	}

	public void OnSceneGUI() {
		if(!isBuilt)
			return;

		if(Application.isPlaying || debugShowCellsInNonPlay) {
			var cam = Camera.main;
			var cam2 = Camera.current;

			for(int i = 0, n = m_cellSortArray.Length; i < n; ++i) {
				var d = (int)(m_cellSortArray[i] >> 11);
				var c = (int)(m_cellSortArray[i] & 0x7FF);

				var pc = vegetationData.m_phatCells[c];
				var v = pc.bounds.center - cam.transform.position;
				if(Vector3.Dot(cam2.transform.forward, pc.bounds.center - cam2.transform.position) > 0f)
					UnityEditor.Handles.Label(pc.bounds.center, "D: " + d + "\nS: " + SectorFromVector(Vector2XZ(-v)), "Box");
			}
		}
	}
#endif //UNITY_EDITOR
}