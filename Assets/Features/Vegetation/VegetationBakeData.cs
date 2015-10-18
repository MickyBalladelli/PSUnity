using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.Linq;
#endif

// Vegetation collection and baking.
// 
// This stuff could do with some serious cleaning up, 
// but it'll have to be good enough for government for now.
//
public class VegetationBakeData : MonoBehaviour {
	[System.Serializable]
	public class PhatCell {
		public int			x, z;
		public Bounds		bounds;
		public Mesh			mesh;
		public Mesh			mesh2;
		public MeshRenderer renderer;
		public int[]		indices;
		public uint[]		patches;
		public PaintJob[]	pjobs;
		
		public PhatCell(int x, int z) { this.x = x; this.z = z; }
	}

	public float	cellSize = 16f;
	//public float	cellResolution = 4f;
	public float	cellMinSpriteDensity = 0.5f;
	public float	cellMaxSpriteDensity = 5f;
	public float	cellMinMeshDensity = 0.1f;
	public float	cellMaxMeshDensity = 3f;
	
	public float	patchSize = 1f;
	public int		viewSectors = 10;

	public float	healthyToDryRatio = 0.75f;
	public float	groundToTopColorRatio = 0.4f;
	public Vector2	darkenBaseMaxMin = new Vector2(0.6f, 0.15f);
	
	public TerrainData	terrainTemplate;
	public int			textureAtlasMaxSize = 4096;

	public float		groundColorSampleResolution = 1f;
	public float		groundColorScale = 1f;
	
	public LayerMask	embedObjectLayers;
	public LayerMask	captureGroundLayers = ~0;
	public bool			useLitCapture;
	
	public Shader		growthShader;
	public Shader		captureShader;

	[Header("Generated Data")]
	public Texture2D			growthAtlasDiffuse;
	public Texture2D			growthAtlasNormals;
	public Rect[]				growthAtlasMappings;
	public string[]				growthAtlasMappingNames;
	public Texture2D			capturedColormap;
	public List<PaintJob>		capturedPaintJobs = new List<PaintJob>();
	public List<MeshRenderer>	capturedInstances = new List<MeshRenderer>();
	public Material				growthMaterialOutline;
	public Material				growthMaterialOpaque;
	public Material				growthMaterialHard;
	public Material				growthMaterialOpaqueMesh;

	public const float MPI2 = 6.283185307179f;

	public Bounds			m_bounds;
	public Vector3			m_boundsCorner;
	public int				m_cellsX, m_cellsZ;
	public int				m_patchesInCellSide;
	public float 			m_angleToSector;
	
	public List<PhatCell>	m_phatCells;

	public Color SampleColormap(Vector3 worldPos) {
		var u = (worldPos.x - m_bounds.min.x) / m_bounds.size.x;
		var v = (worldPos.z - m_bounds.min.z) / m_bounds.size.z;
		var c = capturedColormap.GetPixelBilinear(u, v) * groundColorScale;
		c.a = 1f;
		return c;
	}
	
#if UNITY_EDITOR
	public void CaptureVegetationSources() {
		var paintJobs = Object.FindObjectsOfType<PaintJob>();
		var instances = Object.FindObjectsOfType<MeshRenderer>().Where(mr => ((1<<mr.gameObject.layer) & embedObjectLayers) != 0).ToArray();

		foreach(var pj in paintJobs)
			pj.gameObject.SetActive(false);

		foreach(var i in instances)
			i.gameObject.SetActive(false);

		capturedPaintJobs.AddRange(paintJobs);
		capturedInstances.AddRange(instances);

		Debug.LogFormat("Captured {0} PaintJobs ({1} total) and {2} instances ({3} total).",
		                paintJobs.Length, capturedPaintJobs.Count, instances.Length, capturedInstances.Count);

		CalculateBounds();
	}

	public void ReleaseVegetationSources() {
		foreach(var pj in capturedPaintJobs)
			if(pj)
				pj.gameObject.SetActive(true);
		
		foreach(var i in capturedInstances)
			if(i)
				i.gameObject.SetActive(true);

		Debug.LogFormat("Released {0} PaintJobs and {1} instances.",
		                capturedPaintJobs.Count, capturedInstances.Count);

		capturedPaintJobs.Clear();
		capturedInstances.Clear();
		m_bounds.SetMinMax(Vector3.zero, Vector3.zero);
	}

	public void CreateMaterials() {
		var outlinePath = growthMaterialOutline ? UnityEditor.AssetDatabase.GetAssetPath(growthMaterialOutline) : "Assets/vegOutline.mat";
		var opaquePath = growthMaterialOpaque ? UnityEditor.AssetDatabase.GetAssetPath(growthMaterialOpaque) : "Assets/vegOpaque.mat";
		var hardPath = growthMaterialHard ? UnityEditor.AssetDatabase.GetAssetPath(growthMaterialHard) : "Assets/vegHard.mat";
		var opaqueMeshPath = growthMaterialOpaqueMesh ? UnityEditor.AssetDatabase.GetAssetPath(growthMaterialOpaqueMesh) : "Assets/vegOpaqueMesh.mat";

		Object.DestroyImmediate(growthMaterialOutline, true);
		Object.DestroyImmediate(growthMaterialOpaque, true);
		Object.DestroyImmediate(growthMaterialHard, true);
		Object.DestroyImmediate(growthMaterialOpaqueMesh, true);
		
		growthMaterialOutline = new Material(growthShader);
		growthMaterialOpaque = new Material(growthShader);
		growthMaterialHard = new Material(growthShader);
		growthMaterialOpaqueMesh = new Material(growthShader);

		SetupMaterials();

		UnityEditor.AssetDatabase.CreateAsset(growthMaterialOutline, outlinePath);
		UnityEditor.AssetDatabase.CreateAsset(growthMaterialOpaque, opaquePath);
		UnityEditor.AssetDatabase.CreateAsset(growthMaterialHard, hardPath);
		UnityEditor.AssetDatabase.CreateAsset(growthMaterialOpaqueMesh, opaqueMeshPath);
	}
	
	void SetupMaterials() {
		growthMaterialOutline.name = "(procedural undergrowth outline material)";
		growthMaterialOutline.SetTexture("_MainTex",growthAtlasDiffuse);
		growthMaterialOutline.SetTexture("_BmpMap", growthAtlasNormals);
		var uvScale = new Vector2(1f / m_bounds.size.x, 1f / m_bounds.size.z);
		var uvOffset = new Vector2(m_bounds.min.x * -uvScale.x, m_bounds.min.z * -uvScale.y);
		growthMaterialOutline.SetTextureScale("_GITexture", uvScale);
		growthMaterialOutline.SetTextureOffset("_GITexture", uvOffset);
		growthMaterialOutline.DisableKeyword("OPAQUEPASS");
		growthMaterialOutline.SetFloat("_Cutoff", 0.05f);
		growthMaterialOutline.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		growthMaterialOutline.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		growthMaterialOutline.SetInt("_ZWrite", 0);
		growthMaterialOutline.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Less);
		growthMaterialOutline.SetInt("_ColorMask", (int)UnityEngine.Rendering.ColorWriteMask.All);

		growthMaterialOpaque.CopyPropertiesFromMaterial(growthMaterialOutline);
		growthMaterialOpaque.name = "(procedural undergrowth opaque material)";
		growthMaterialOpaque.renderQueue = 2452;
		growthMaterialOpaque.EnableKeyword("OPAQUEPASS");
		growthMaterialOpaque.SetFloat("_Cutoff", 0.65f);
		growthMaterialOpaque.SetFloat("_CutoffNear", 0.20f);
		growthMaterialOpaque.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
		growthMaterialOpaque.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
		growthMaterialOpaque.SetInt("_ZWrite", 1);
		growthMaterialOpaque.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
		growthMaterialOpaque.SetInt("_ColorMask", (int)UnityEngine.Rendering.ColorWriteMask.All);

		growthMaterialHard.CopyPropertiesFromMaterial(growthMaterialOpaque);
		growthMaterialHard.name = "(procedural undergrowth hard material)";
		growthMaterialHard.SetFloat("_Cutoff", 0.4f);

		growthMaterialOpaqueMesh.CopyPropertiesFromMaterial(growthMaterialOpaque);
		growthMaterialOpaqueMesh.name = "(procedural undergrowth opaque mesh material)";
		growthMaterialOpaqueMesh.renderQueue = 2451;
	}

	#region BakeVegetation	
	public void BakeVegetation() {
		CaptureVegetationSources();

		m_cellsX = Mathf.CeilToInt(m_bounds.size.x / cellSize);
		m_cellsZ = Mathf.CeilToInt(m_bounds.size.z / cellSize);
		m_patchesInCellSide = Mathf.CeilToInt(cellSize / patchSize);
		m_angleToSector = (viewSectors - 1e-6f) / MPI2;
		
		Profiler.BeginSample("Cell Setup");
		CreateCellCandidates();
		PopulateCellCandidates();
		Profiler.EndSample();

		Debug.Log(string.Format("Vegetation baked: {0} populated / {1} total cells.", m_phatCells.Count, m_cellsX * m_cellsZ));
	}

	void CreateCellCandidates() {
		Profiler.BeginSample("Create Candidates");
		
		// TODO: Test against PaintJob OOBB instead of AABB for tighter culling.
		
		m_phatCells = new List<PhatCell>();
		
		var cellPaintJobs = new List<PaintJob>();
		
		var cellSz = new Vector3(cellSize, m_bounds.size.y, cellSize);
		var boundsCorner = m_bounds.center - m_bounds.extents;
		var adjustedBoundsCorner = boundsCorner + cellSz * 0.5f;
		for(int z = 0; z < m_cellsZ; ++z) {
			for(int x = 0; x < m_cellsX; ++x) {
				var cellOffset = new Vector3(x * cellSize, 0f, z * cellSize);
				var cellBounds = new Bounds(adjustedBoundsCorner + cellOffset, cellSz);
				
				foreach(var pj in capturedPaintJobs)
					if(cellBounds.Intersects(new Bounds(pj.transform.position, pj.alignedSize)))
						cellPaintJobs.Add(pj);
				
				if(cellPaintJobs.Count > 0) {
					var cell = new PhatCell(x, z);
					cell.pjobs = cellPaintJobs.ToArray();
					m_phatCells.Add(cell);
					cellPaintJobs.Clear();
				}
			}
		}
		
		Profiler.EndSample();
	}

	#region Rand Helpers
	static		System.Random ms_random;
	float		RandVal_ZO { get{ return Mathf.Max(0f, (float)(ms_random.NextDouble() - (double)1e-5)); }} //TODO: Proper rand distribution
	float		RandVal_PM { get{ return RandVal_ZO * 2f - 1f; }}
	Vector3		RandX0Z_ZO { get{ return new Vector3(RandVal_ZO, 0, RandVal_ZO); }}
	Vector3		RandX0Z_PM { get{ return new Vector3(RandVal_PM, 0, RandVal_PM); }}
	Vector3		RandXYZ_ZO { get{ return new Vector3(RandVal_ZO, RandVal_ZO, RandVal_ZO); }}
	Vector3		RandXYZ_PM { get{ return new Vector3(RandVal_PM, RandVal_PM, RandVal_PM); }}
	Quaternion	RandYaw_PM { get{ return Quaternion.Euler(0f, RandVal_PM * 180f , 0f); }}
	#endregion

	void PopulateCellCandidates() {
		Profiler.BeginSample("Populate Candidates");

		ms_random = new System.Random(1235798);
		
		var detailPrototypes = terrainTemplate.detailPrototypes;
		
		var spriteSamplesInCell = Mathf.RoundToInt(cellSize * cellMaxSpriteDensity);
		spriteSamplesInCell *= spriteSamplesInCell;

		var meshSamplesInCell = Mathf.RoundToInt(cellSize * cellMaxMeshDensity);
		meshSamplesInCell *= meshSamplesInCell;
		
		var meshBuilder = new MeshBuilder();
		var meshBuilder2 = new MeshBuilder();
		
		var maxInstancesInPatch = Mathf.CeilToInt(spriteSamplesInCell * m_patchesInCellSide);
		var patches = new int[m_patchesInCellSide, m_patchesInCellSide, 1 + maxInstancesInPatch];
		
		var boundsCorner = m_bounds.center - m_bounds.extents;
		
		for(int cellIdx = 0; cellIdx < m_phatCells.Count; ) {
			Profiler.BeginSample("Cell");
			var phatCell = m_phatCells[cellIdx];
			
			var cellOffset = new Vector3(phatCell.x * cellSize, 0f, phatCell.z * cellSize);
			var cellCorner = boundsCorner + cellOffset;
			
			float minY = float.MaxValue, maxY = float.MinValue;
			
			foreach(var pj in phatCell.pjobs) {
				Profiler.BeginSample("Paintjob");
				
				var pjCorner = pj.transform.position - pj.alignedSize * 0.5f;
				var pjSizeRcp = new Vector3(1f/pj.alignedSize.x, 1f, 1f/pj.alignedSize.z);
				
				for(int s = 0, sidx = 0; s < spriteSamplesInCell; ++s) {
for(int l = 0, ln = pj.detailLayerCount; l < ln; ++l) {
	var detailLayer = detailPrototypes[l];
	if(detailLayer.usePrototypeMesh)
		continue;

					var subCellSampleRand = RandX0Z_ZO;
					var subCellSampleOffset = subCellSampleRand * cellSize;
					var subCellSamplePos = cellCorner + subCellSampleOffset;
					
					var patchRelativePos = Vector3.Scale(subCellSamplePos - pjCorner, pjSizeRcp);
					
					if(patchRelativePos.x < 0f || patchRelativePos.x > 1f || patchRelativePos.z < 0f || patchRelativePos.z > 1f)
						continue;
					
					var patchDimension = Mathf.RoundToInt(Mathf.Sqrt(pj.detailLayerElements));
					var patchOffset = Mathf.Clamp(Mathf.RoundToInt(patchDimension * patchRelativePos.x), 0, patchDimension);
					patchOffset += Mathf.RoundToInt(patchDimension * Mathf.Round(patchDimension * patchRelativePos.z));
					patchOffset = Mathf.Clamp(patchOffset, 0, pj.detailLayerElements - 1);
					
					if(pj.maskData[patchOffset] <= 0f)
						continue;
					
					int totalLayersValue = 0;
					for(int lc = 0, lcn = pj.detailLayerCount; lc < lcn; ++lc) {
						var layerValue2 = pj.detailData[lc * pj.detailLayerElements + patchOffset];
						totalLayersValue += layerValue2;
					}
					float totalLayersScale = Mathf.Clamp01(totalLayersValue / 4f);
					
					var heightY = (pj.heightData[patchOffset] - 0.5f) * pj.alignedSize.y + pj.transform.position.y;
					var placePos = new Vector3(subCellSamplePos.x, heightY, subCellSamplePos.z);
//					for(int l = 0, ln = pj.detailLayerCount; l < ln; ++l) {
//						var detailLayer = detailPrototypes[l];
//						if(detailLayer.usePrototypeMesh)
//							continue;

						var layerValue = pj.detailData[l * pj.detailLayerElements + patchOffset];
						if(layerValue > 0) {
							// TODO: check density vs probability

							var subPlacePos = placePos;
							var spriteColB = SampleColormap(subPlacePos);
							var healthCol = Color.Lerp(detailLayer.dryColor, detailLayer.healthyColor, healthyToDryRatio);// Mathf.Clamp01(0.5f + ssn / (float)maxInstances));
							var spriteColT = Color.Lerp(spriteColB, healthCol, groundToTopColorRatio);

							var darkenBase = Mathf.Lerp(darkenBaseMaxMin.x, darkenBaseMaxMin.y, totalLayersScale);
							//spriteColB.r *= darkenBase;
							//spriteColB.g *= darkenBase;
							//spriteColB.b *= darkenBase;
							spriteColB.a = darkenBase;

							//if(darkenBase < darkenBaseMaxMin.y)
							if(totalLayersScale < 0f || totalLayersScale > 1f)
								Debug.LogWarningFormat("SuperDark: {0}  {1}  {2}", darkenBase, totalLayersValue, totalLayersScale);
							
							var spriteWidth = Mathf.Lerp(detailLayer.minWidth, detailLayer.maxWidth, RandVal_ZO);
							var spriteHeight = Mathf.Lerp(detailLayer.minHeight, detailLayer.maxHeight, RandVal_ZO);
							var spriteBB = detailLayer.renderMode == DetailRenderMode.GrassBillboard;
							var spriteRot = /*spriteBB ? Quaternion.identity :*/ RandYaw_PM;
							var vtxStart = meshBuilder.PushSprite(growthAtlasMappings[l], spriteWidth, spriteHeight, subPlacePos, spriteRot, spriteColB, spriteColT, spriteBB, sidx++);
							
							var patchIdxX = Mathf.FloorToInt(subCellSampleRand.x * m_patchesInCellSide);
							var patchIdxZ = Mathf.FloorToInt(subCellSampleRand.z * m_patchesInCellSide);
							var patchIdxNext = 1 + patches[patchIdxX, patchIdxZ, 0];
							patches[patchIdxX, patchIdxZ, patchIdxNext] = vtxStart;
							patches[patchIdxX, patchIdxZ, 0] = patchIdxNext;
							
							minY = Mathf.Min(minY, subPlacePos.y);
							maxY = Mathf.Max(maxY, subPlacePos.y + spriteHeight);
						}
					}
				}

				for(int s = 0, sidx = 0; s < meshSamplesInCell; ++s) {
for(int l = 0, ln = pj.detailLayerCount; l < ln; ++l) {
	var detailLayer = detailPrototypes[l];
	if(!detailLayer.usePrototypeMesh)
		continue;
					var subCellSampleRand = RandX0Z_ZO;
					var subCellSampleOffset = subCellSampleRand * cellSize;
					var subCellSamplePos = cellCorner + subCellSampleOffset;
					
					var patchRelativePos = Vector3.Scale(subCellSamplePos - pjCorner, pjSizeRcp);
					
					if(patchRelativePos.x < 0f || patchRelativePos.x > 1f || patchRelativePos.z < 0f || patchRelativePos.z > 1f)
						continue;
					
					var patchDimension = Mathf.RoundToInt(Mathf.Sqrt(pj.detailLayerElements));
					var patchOffset = Mathf.Clamp(Mathf.RoundToInt(patchDimension * patchRelativePos.x), 0, patchDimension);
					patchOffset += Mathf.RoundToInt(patchDimension * Mathf.Round(patchDimension * patchRelativePos.z));
					patchOffset = Mathf.Clamp(patchOffset, 0, pj.detailLayerElements - 1);
					
					if(pj.maskData[patchOffset] <= 0f)
						continue;
					
					int totalLayersValue = 0;
					for(int lc = 0, lcn = pj.detailLayerCount; lc < lcn; ++lc) {
						var layerValue2 = pj.detailData[lc * pj.detailLayerElements + patchOffset];
						totalLayersValue += layerValue2;
					}
					float totalLayersScale = Mathf.Clamp01(totalLayersValue / 4f);
					
					var heightY = (pj.heightData[patchOffset] - 0.5f) * pj.alignedSize.y + pj.transform.position.y;
					var placePos = new Vector3(subCellSamplePos.x, heightY, subCellSamplePos.z);
//					for(int l = 0, ln = pj.detailLayerCount; l < ln; ++l) {
//						var detailLayer = detailPrototypes[l];
//						if(!detailLayer.usePrototypeMesh)
//							continue;

						var layerValue = pj.detailData[l * pj.detailLayerElements + patchOffset];
						if(layerValue > 0) {
							// TODO: check density vs probability

							var meshScale = new Vector3(
								Mathf.Lerp(detailLayer.minWidth, detailLayer.minWidth, RandVal_ZO),
								Mathf.Lerp(detailLayer.minHeight, detailLayer.maxHeight, RandVal_ZO),
								Mathf.Lerp(detailLayer.minWidth, detailLayer.minWidth, RandVal_ZO)
							);
							
							var subPlacePos = placePos;
							var spriteColB = SampleColormap(subPlacePos);
							var healthCol = Color.Lerp(detailLayer.dryColor, detailLayer.healthyColor, healthyToDryRatio);// Mathf.Clamp01(0.5f + ssn / (float)maxInstances));
							var spriteColT = Color.Lerp(spriteColB, healthCol, groundToTopColorRatio);
							
							var darkenBase = Mathf.Lerp(darkenBaseMaxMin.x, darkenBaseMaxMin.y, totalLayersScale);
							//spriteColB.r *= darkenBase;
							//spriteColB.g *= darkenBase;
							//spriteColB.b *= darkenBase;
							spriteColB.a = darkenBase;
							
							//if((layerValue / 16f) < (Random.value - 0.35f))
							meshBuilder2.PushMesh(
								detailLayer.prototype.GetComponent<MeshFilter>().sharedMesh,
								growthAtlasMappings[l], 
							    subPlacePos,
								RandYaw_PM,
							    meshScale, 
								RandVal_ZO * 0.42f,
								spriteColB,
								spriteColT,
								sidx++
							);
						}
					}
				}

				var embedBounds = new Bounds(
					cellCorner + new Vector3(cellSize * 0.5f, 0f, cellSize * 0.5f),
					new Vector3(cellSize, 100f, cellSize)
					);

				var embedInstanceIdx = 0;
				foreach(var ei in capturedInstances) {
					var embedPos = ei.transform.position;
					
					if(/*!ei.enabled ||*/ !embedBounds.Contains(embedPos))
						continue;

					//public float darkenBaseMesh = 0.3f;
					//public float meshBaseToGroundRatio = 0.3f;

					var spriteColB = SampleColormap(embedPos);
					var spriteColT = Color.Lerp(spriteColB, Color.white, 0.3f);
					
					var darkenBase =  0.3f;//Mathf.Lerp(0.6f, 0.15f, totalLayersScale);
					spriteColB.a = darkenBase;
					
					var atlasName = ei.sharedMaterial.mainTexture.name;
					var atlasIndex = 0;
					
					for(; atlasIndex < growthAtlasMappingNames.Length; ++atlasIndex)
						if(growthAtlasMappingNames[atlasIndex] == atlasName)
							break;
					
					meshBuilder2.PushMesh(ei.GetComponent<MeshFilter>().sharedMesh, growthAtlasMappings[atlasIndex],
					                      embedPos, ei.transform.rotation, ei.transform.localScale, 
					                      0f, spriteColB, spriteColT, embedInstanceIdx++
					                      );
					
					//ei.enabled = false;
				}
				
				Profiler.EndSample();
			}
			
			if(!meshBuilder.IsEmpty || !meshBuilder2.IsEmpty) {
				//Debug.Log(string.Format("x: {0} z: {1} camD: {2} scaledSpriteDensity: {3},  scaledMeshDensity: {4}, scaledMaxSprites: {5}, scaledMaxMeshes: {6}", cell.x, cell.z, cellDistance, cellScaledSpriteDensity, cellScaledMeshDensity, subCellMaxSpriteInstances, subCellMaxMeshInstances));
				//Debug.LogFormat("x: {0} z: {1}", phatCell.x, phatCell.z);
				var dbgName = string.Format("Cell {0}x{1}: ", phatCell.x, phatCell.z);

				phatCell.bounds.SetMinMax(new Vector3(cellCorner.x, minY, cellCorner.z), new Vector3(cellCorner.x + cellSize, maxY, cellCorner.z + cellSize));
				phatCell.patches = meshBuilder.SortPatches(patches);
				
				if(!meshBuilder.IsEmpty) {
					phatCell.mesh = meshBuilder.Realize(dbgName + "Sprites");
					phatCell.indices = phatCell.mesh.triangles;
				}
				
				if(!meshBuilder2.IsEmpty)
					phatCell.mesh2 = meshBuilder2.Realize(dbgName + "Meshes");
				
				meshBuilder.Reset();
				meshBuilder2.Reset();
				
				for(int pz = 0, pzn = patches.GetLength(1); pz < pzn; ++pz)
					for(int px = 0, pxn = patches.GetLength(0); px < pxn; ++px)
						patches[px, pz, 0] = 0;
				
				++cellIdx;
			} else {
				//Debug.LogFormat("Culling empty cell at {0}, {1}", phatCell.x, phatCell.z);
				m_phatCells[cellIdx] = m_phatCells[m_phatCells.Count - 1];
				m_phatCells.RemoveAt(m_phatCells.Count - 1);
			}
			
			Profiler.EndSample();
		}
		
		Profiler.EndSample();
	}
	#endregion

	public void BakeGround() {
		var groundX = Mathf.RoundToInt(m_bounds.size.x * groundColorSampleResolution);
		var groundZ = Mathf.RoundToInt(m_bounds.size.z * groundColorSampleResolution);
		var rt = new RenderTexture(groundX, groundZ, 24, RenderTextureFormat.ARGB32);

		var cgo = new GameObject("_Vegetation Capture Cam");
		cgo.transform.position = m_bounds.center + Vector3.up * m_bounds.extents.y;
		cgo.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

		var cam = cgo.AddComponent<Camera>();
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = Color.gray;
		cam.orthographic = true;
		cam.orthographicSize = m_bounds.extents.z;
		cam.nearClipPlane = 0f;
		cam.farClipPlane = m_bounds.size.y;
		cam.useOcclusionCulling = false;
		cam.cullingMask = captureGroundLayers & ~embedObjectLayers;
		cam.aspect = m_bounds.size.x / m_bounds.size.z;
		cam.targetTexture = rt;
		cam.enabled = false;
		if(useLitCapture) {
			var hadScattering = Shader.IsKeywordEnabled("ATMOSPHERICS");
			Shader.DisableKeyword("ATMOSPHERICS");
			
			cam.Render();

			if(hadScattering)
				Shader.EnableKeyword("ATMOSPHERICS");
		} else {
			cam.RenderWithShader(captureShader, "RenderType");
		}

		if(capturedColormap)
			Object.DestroyImmediate(capturedColormap);

		RenderTexture.active = rt;
		capturedColormap = new Texture2D(groundX, groundZ, TextureFormat.ARGB32, false);
		capturedColormap.filterMode = FilterMode.Bilinear;
		capturedColormap.wrapMode = TextureWrapMode.Clamp;
		capturedColormap.ReadPixels(new Rect(0f, 0f, (float)groundX, (float)groundZ), 0, 0);
		capturedColormap.Apply(false, false);

		cam.targetTexture = null;
		RenderTexture.active = null;
		Object.DestroyImmediate(cgo);
		Object.DestroyImmediate(rt);
	}

	public void BakeTextures() {
		List<Texture2D> sourcesD = new List<Texture2D>();
		List<Texture2D> sourcesN = new List<Texture2D>();
		
		foreach(var layer in terrainTemplate.detailPrototypes) {
			if(layer.usePrototypeMesh) {
				var mat = layer.prototype.GetComponent<Renderer>().sharedMaterial;
				sourcesD.Add(mat.mainTexture as Texture2D);
				sourcesN.Add(mat.GetTexture("_BumpMap") as Texture2D);
			} else {
				var diff = layer.prototypeTexture;
				sourcesD.Add(diff);
				var diffPath = UnityEditor.AssetDatabase.GetAssetPath(diff);
				var norm = UnityEditor.AssetDatabase.LoadAssetAtPath(diffPath.Replace("DiffuseMap", "NormalsMap"), typeof(Texture2D)) as Texture2D;
				if(norm == null)
					Debug.LogWarning("Failed to find normal map for texture: " + diffPath);
				sourcesN.Add(norm);
			}
		}
		
		List<Texture2D> roSourcesD = new List<Texture2D>();
		List<Texture2D> roSourcesN = new List<Texture2D>();
		
		for(int i = 0, n = sourcesD.Count; i < n; ++i) {
			var srcD = sourcesD[i];
			var impD = UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(srcD)) as UnityEditor.TextureImporter;
			if(!impD.isReadable) {
				Debug.Log("Force-importing atlas source to make readable: " + srcD.name);
				impD.isReadable = true;
				
				UnityEditor.AssetDatabase.ImportAsset(impD.assetPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
				srcD = UnityEditor.AssetDatabase.LoadMainAssetAtPath(impD.assetPath) as Texture2D;
				roSourcesD.Add(srcD);
				
				sourcesD[i] = srcD;
			}
		}
		for(int i = 0, n = sourcesN.Count; i < n; ++i) {
			var srcN = sourcesN[i];
			var impN = UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(srcN)) as UnityEditor.TextureImporter;
			if(!impN) {
				Debug.LogFormat("Failed to find normal map: {0} ({1})", srcN, sourcesD[i]);
				continue;
			}
			if(!impN.isReadable) {
				Debug.Log("Force-importing atlas source to make readable: " + srcN.name);
				impN.isReadable = true;
				impN.normalmap = false;
				
				UnityEditor.AssetDatabase.ImportAsset(impN.assetPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
				srcN = UnityEditor.AssetDatabase.LoadMainAssetAtPath(impN.assetPath) as Texture2D;
				roSourcesN.Add(srcN);
				
				sourcesN[i] = srcN;
			}
		}

		string atlasDiffusePath = "Assets/VegetationDiffuse.png";
		string atlasNormalsPath = "Assets/VegetationNormal.png";
		
		if(growthAtlasDiffuse) {
			atlasDiffusePath = UnityEditor.AssetDatabase.GetAssetPath(growthAtlasDiffuse);
			Object.DestroyImmediate(growthAtlasDiffuse, true);
		}
		if(growthAtlasNormals) {
			atlasNormalsPath = UnityEditor.AssetDatabase.GetAssetPath(growthAtlasNormals);
			Object.DestroyImmediate(growthAtlasNormals, true);
		}

		growthAtlasNormals = PackTextures(sourcesN, true, atlasNormalsPath);
		growthAtlasDiffuse = PackTextures(sourcesD, false, atlasDiffusePath);
		
		foreach(var src in roSourcesD) {
			var imp = UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(src)) as UnityEditor.TextureImporter;
			imp.isReadable = false;
			UnityEditor.AssetDatabase.ImportAsset(imp.assetPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
		}
		foreach(var src in roSourcesN) {
			var imp = UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(src)) as UnityEditor.TextureImporter;
			imp.isReadable = false;
			imp.normalmap = true;
			UnityEditor.AssetDatabase.ImportAsset(imp.assetPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
		}
	}

	// Buggy PackTextures requires us to resort to native IO (mipmaps are broken. TODO: report this bug)
	Texture2D PackTextures(List<Texture2D> sources, bool normals, string atlasPath) {
		// Force uncompressed atlas so we can save it out and reimport it for proper mipmapping.
		sources.Add(new Texture2D(1, 1, TextureFormat.ARGB32, false));
		
		var packedTexture = new Texture2D(textureAtlasMaxSize, textureAtlasMaxSize, TextureFormat.ARGB32, false);
		growthAtlasMappings = packedTexture.PackTextures(sources.ToArray(), 16, textureAtlasMaxSize, false);
		growthAtlasMappingNames = new string[sources.Count];
		for(int i = 0; i < sources.Count; ++i)
			growthAtlasMappingNames[i] = sources[i].name;
		
		// Do a little re-import dance to work-around PackTextures mipmapping bug.
		UnityEditor.AssetDatabase.DeleteAsset(atlasPath);
		System.IO.File.WriteAllBytes(atlasPath, packedTexture.EncodeToPNG());
		UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.Default);
		var impIt = UnityEditor.AssetImporter.GetAtPath(atlasPath) as UnityEditor.TextureImporter;
		impIt.textureFormat = UnityEditor.TextureImporterFormat.AutomaticCompressed;
		impIt.maxTextureSize = textureAtlasMaxSize;
		impIt.filterMode = FilterMode.Trilinear;
		impIt.mipmapEnabled = true;
		impIt.mipmapFilter = UnityEditor.TextureImporterMipFilter.BoxFilter;
		impIt.normalmap = normals;
		impIt.normalmapFilter = UnityEditor.TextureImporterNormalFilter.Standard;
		impIt.convertToNormalmap = false;
		UnityEditor.AssetDatabase.ImportAsset(impIt.assetPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
		var importedTexture = UnityEditor.AssetDatabase.LoadAssetAtPath(impIt.assetPath, typeof(Texture2D)) as Texture2D;
		return importedTexture;
	}
#endif//UNITY_EDITOR

	void CalculateBounds() {
		m_bounds = new Bounds(capturedPaintJobs[0].transform.position, Vector3.zero);
		foreach(var pj in capturedPaintJobs)
			m_bounds.Encapsulate(new Bounds(pj.transform.position, pj.alignedSize));
		m_boundsCorner = m_bounds.center - m_bounds.extents;
	}

	#region MeshBuilder
	class MeshBuilder {
		List<Vector3>	vertices = new List<Vector3>();
		List<Color>		colors = new List<Color>();
		List<Vector3>	normals = new List<Vector3>();
		List<Vector4>	tangents = new List<Vector4>();
		List<Vector2>	uv = new List<Vector2>();
		List<Vector2>	uv2 = new List<Vector2>();
		List<Vector2>	uv3 = new List<Vector2>();
		List<Vector2>	uv4 = new List<Vector2>();
		List<int>		indices = new List<int> ();
		
		public bool IsEmpty { get { return vertices.Count == 0; }}
		
		public uint PushMesh(Mesh m, Rect atlas, Vector3 pos, Quaternion rot, Vector3 scale, float bend, Color colB, Color colT, int index) {
			var mat = Matrix4x4.TRS(pos, rot, scale);
			var bendDir = rot * Vector3.forward;
			
			var vtxStart = vertices.Count;
			var verts = m.vertices;
			var norms = m.normals;
			var tans = m.tangents;
			var uvs = m.uv;
			
			float maxY = 0.25f;
			for(int i = 0, n = verts.Length; i < n; ++i)
				maxY = Mathf.Max(maxY, verts[i].y);
			
			for(int i = 0, n = verts.Length; i < n; ++i) {
				var localY = Mathf.Max(0f, verts[i].y);
				var bendDist = bend * localY * localY;
				var blendY = Mathf.Clamp01(localY / maxY);

				vertices.Add(mat.MultiplyPoint(verts[i] + bendDir * bendDist));
				normals.Add(mat.MultiplyVector(norms[i]));
				tangents.Add(mat.MultiplyVector(tans[i]));
				colors.Add(Color.Lerp(colB, colT, blendY));
				uv.Add(new Vector2(uvs[i].x * atlas.width + atlas.xMin, uvs[i].y * atlas.height + atlas.yMin));
				uv2.Add(new Vector2(pos.x, localY));
				uv3.Add(new Vector2((float)index, -1f));
				uv4.Add(new Vector2(pos.y, pos.z));
			}
			
			var idcs = m.triangles;
			var idxStart = indices.Count;
			var idxCount = idcs.Length;
			for(int i = 0, n = idxCount; i < n; ++i)
				indices.Add(idcs[i] + vtxStart);
			
			return (((uint)idxStart & 0xFFFF) << 16) | ((uint)idxCount & 0xFFFF);
		}
		
		public int PushSprite(Rect atlas, float width, float height, Vector3 pos, Quaternion rot, Color colB, Color colT, bool billboard, int index) {
			var vtxStart = vertices.Count;

			var nrm = rot * Vector3.back;
			
			vertices.Add(pos);
			vertices.Add(pos);
			vertices.Add(pos);
			vertices.Add(pos);
			
			colors.Add(colB);
			colors.Add(colB);
			colors.Add(colT);
			colors.Add(colT);
			
			normals.Add(nrm);
			normals.Add(nrm);
			normals.Add(nrm);
			normals.Add(nrm);
			
			uv.Add(new Vector2(atlas.xMin, atlas.yMin));
			uv.Add(new Vector2(atlas.xMax, atlas.yMin));
			uv.Add(new Vector2(atlas.xMax, atlas.yMax));
			uv.Add(new Vector2(atlas.xMin, atlas.yMax));
			
			float hW = width * 0.5f;
			uv2.Add(new Vector2(-hW,     0f));
			uv2.Add(new Vector2( hW,     0f));
			uv2.Add(new Vector2( hW, height));
			uv2.Add(new Vector2(-hW, height));
			
			uv3.Add(new Vector2((float)index, billboard ? 1f : 0f));
			uv3.Add(new Vector2((float)index, billboard ? 1f : 0f));
			uv3.Add(new Vector2((float)index, billboard ? 1f : 0f));
			uv3.Add(new Vector2((float)index, billboard ? 1f : 0f));
			
			tangents.Add(Vector4.zero);
			tangents.Add(Vector4.zero);
			tangents.Add(Vector4.zero);
			tangents.Add(Vector4.zero);
			
			indices.Add(vtxStart + 0);
			indices.Add(vtxStart + 1);
			indices.Add(vtxStart + 2);
			indices.Add(vtxStart + 2);
			indices.Add(vtxStart + 3);
			indices.Add(vtxStart + 0);
			
			return vtxStart;
		}
		
		public uint[] SortPatches(int[,,] patches) {
			indices.Clear();
			
			int xn = patches.GetLength(0);
			int zn = patches.GetLength(1);
			
			int[] starts = new int[xn * zn];
			int[] counts = new int[xn * zn];
			
			for(int z = 0; z < zn; ++z) {
				for(int x = 0; x < xn; ++x) {
					var outIdx = z * xn + x;
					var instanceCount = patches[x, z, 0];
					
					starts[outIdx] = indices.Count; 
					
					for(int i = 0; i < instanceCount; ++i) {
						var vtxStart = patches[x, z, 1 + i];
						indices.Add(vtxStart + 0);
						indices.Add(vtxStart + 1);
						indices.Add(vtxStart + 2);
						indices.Add(vtxStart + 2);
						indices.Add(vtxStart + 3);
						indices.Add(vtxStart + 0);

						#if _DBG_ORDER_COLOR
						for(int c = vtxStart; c < vtxStart + 4; ++c)
							colors[c] = new Color(x/(float)xn, z/(float)zn, outIdx / (float)(xn * zn), 1f);
						#endif
					}
					counts[outIdx] = indices.Count - starts[outIdx];
				}
			}
			
			var outlist = new uint[xn * zn];
			for(int i = 0, n = xn * zn; i < n; ++i) {
				if(counts[i] > 0)
					outlist[i] = ((uint)starts[i] << 12) | (uint)counts[i];
			}
			
			return outlist;
		}

		void ClampInputs(string dbgName) {
			if(vertices.Count <= 65000)
				return;

			Debug.LogErrorFormat("Mesh generated more than 65K verts for a cell. ('{0}' has {1} vertices, {2} indices)", dbgName, vertices.Count, indices.Count);

			vertices.RemoveRange(65000, vertices.Count - 65000);
			colors.RemoveRange(65000, colors.Count - 65000);
			normals.RemoveRange(65000, normals.Count - 65000);
			tangents.RemoveRange(65000, tangents.Count - 65000);
			uv.RemoveRange(65000, uv.Count - 65000);
			uv2.RemoveRange(65000, uv2.Count - 65000);
			uv3.RemoveRange(65000, uv3.Count - 65000);
			if(uv4.Count > 0)
				uv4.RemoveRange(65000, uv4.Count - 65000);
			for(int i = 0; i < indices.Count; ++i)
				indices[i] = Mathf.Min(indices[i], 64999);
		}
		
		public Mesh Realize(string dbgName) {
			ClampInputs(dbgName);

			var m = new Mesh();
			m.name = dbgName;
			m.vertices = vertices.ToArray();
			m.colors = colors.ToArray();
			m.normals = normals.ToArray();
			m.tangents = tangents.ToArray();
			m.uv = uv.ToArray();
			m.uv2 = uv2.ToArray();
			m.uv3 = uv3.ToArray();
			if(uv4.Count > 0)
				m.uv4 = uv4.ToArray();
			m.triangles = indices.ToArray();
			m.RecalculateBounds();
			var mb = m.bounds;
			mb.Encapsulate(mb.center + mb.extents + new Vector3(2f, 3f, 2f));
			mb.Encapsulate(mb.center - mb.extents - new Vector3(2f, 3f, 2f));
			m.bounds = mb;
			return m;
		}
		
		public void Reset() {
			vertices.Clear();
			colors.Clear();
			normals.Clear();
			tangents.Clear();
			uv.Clear();
			uv2.Clear();
			uv3.Clear();
			uv4.Clear();
			indices.Clear();
		}
	}
	#endregion
}
