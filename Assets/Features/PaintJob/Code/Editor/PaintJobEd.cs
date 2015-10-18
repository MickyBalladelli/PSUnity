using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PaintJob))]
public class PaintJobEd : Editor {
	[MenuItem("GameObject/3D Object/Paintjob")]
	static void CreatePaintJob() {
		Vector3 pos = Vector3.zero;
		if(SceneView.currentDrawingSceneView)
			pos = SceneView.currentDrawingSceneView.pivot;

		Selection.activeGameObject = new GameObject("Paintjob", new [] { typeof(PaintJob) });
		Selection.activeGameObject.transform.position = pos;
		Selection.activeGameObject.transform.localScale = new Vector3(10f, 5f, 10f);
	}

	new PaintJob target { get { return base.target as PaintJob; } }

	public override void OnInspectorGUI() {
		bool hasOpenPaintJob = target.workingObject && target.isPainting;

		if(!hasOpenPaintJob)
			DrawDefaultInspector();
		else
			EditorGUILayout.HelpBox("Can't edit properties while paint job is open.", MessageType.Info);
	
		if(!hasOpenPaintJob) {
			GUI.color = Color.green;
			if(GUILayout.Button("Open Paintjob")) {
				Setup();
				Selection.activeGameObject = target.workingObject;
			}
		} else {
			GUI.color = new Color(0.7f, 0.8f, 1f);
			if(GUILayout.Button("Select Terrain"))
				Selection.activeGameObject = target.workingObject;

			EditorGUILayout.Space();

			GUI.color = Color.white;
			if(GUILayout.Button("Mask Paintjob")) 
				Mask();

			GUI.color = Color.green;
			if(GUILayout.Button("Save & Close Paintjob")) {
				Save();
				Close();
			}

			EditorGUILayout.Space();

			GUI.color = Color.red;
			if(GUILayout.Button("Discard Paintjob Changes")) {
				Discard();
				Setup();
				Close();
			}
		}

		if(target.workingObject) {
			if(!hasOpenPaintJob)
				EditorGUILayout.Space();

			GUI.color = Color.red;
			if(GUILayout.Button("Destroy Paintjob")) {
				Destroy();
				Close();
			}
		}
	}

	float MaskSlope(float y) {
		if(y > target.slopeThresholdUpper)
			return 1f;

		return Mathf.Clamp01((y - target.slopeThresholdLower) / (target.slopeThresholdUpper - target.slopeThresholdLower));
	}

	void Setup() {
		if(target.terrainTemplate == null)
			throw new UnityException("No terrain template assigned.");
		if(target.terrainTemplate.terrainData == null)
			throw new UnityException("Terrain template has no terrain data.");
		if(target.terrainTemplate.materialTemplate == null)
			throw new UnityException("Terrain template has no base material template data.");

		var ooBoundsSize = target.transform.localScale;
		var ooBoundsExtents = ooBoundsSize * 0.5f;

		var targetPos = target.transform.position;
		var targetRight = target.transform.right;
		var targetForward = target.transform.forward;

		var ooPlanes = new [] {
			new Plane( targetRight, targetPos + targetRight * ooBoundsExtents.x),
			new Plane(-targetRight, targetPos - targetRight * ooBoundsExtents.x),
			new Plane( targetForward, targetPos + targetForward * ooBoundsExtents.z),
			new Plane(-targetForward, targetPos - targetForward * ooBoundsExtents.z),
		};

		var aaBounds = new Bounds(Vector3.zero, Vector3.one);
		aaBounds.Encapsulate( targetRight * ooBoundsExtents.x +  Vector3.up * ooBoundsExtents.y +  targetForward * ooBoundsExtents.z);
		aaBounds.Encapsulate(-targetRight * ooBoundsExtents.x + -Vector3.up * ooBoundsExtents.y + -targetForward * ooBoundsExtents.z);
		aaBounds.Encapsulate(-targetRight * ooBoundsExtents.x +  Vector3.up * ooBoundsExtents.y +  targetForward * ooBoundsExtents.z);
		aaBounds.Encapsulate( targetRight * ooBoundsExtents.x + -Vector3.up * ooBoundsExtents.y + -targetForward * ooBoundsExtents.z);

		var aaBoundsSize = aaBounds.size;
		var aaBoundsExtents = aaBounds.extents;

		if(target.autoColliders) {
			// GeometryUtility.TestPlanesAABB won't accept my OO planes, so
			// let's just use a world space AABB test instead.
			var aaBoundsWorld = new Bounds(targetPos + aaBounds.center, aaBounds.size);

			foreach(var mr in Object.FindObjectsOfType<MeshRenderer>())
				if(mr.GetComponent<Collider>() == null)
					if(aaBoundsWorld.Intersects(mr.bounds))
						target.autoColliderList.Add(mr.gameObject.AddComponent<MeshCollider>());
		}

		var terrainCorner = targetPos - aaBoundsExtents;

		var xStepCount = Mathf.NextPowerOfTwo(Mathf.CeilToInt(aaBoundsSize.x * target.density));
		var zStepCount = Mathf.NextPowerOfTwo(Mathf.CeilToInt(aaBoundsSize.z * target.density));

		int ZM = zStepCount;
		int XM = xStepCount;
		int XZM = Mathf.Max(XM, ZM);

		var xStep = (float)XM / (float)XZM * aaBoundsSize.x / (float)xStepCount;
		var zStep = (float)ZM / (float)XZM * aaBoundsSize.z / (float)zStepCount;

		GameObject[] paintingObjects = target.paintingObjects;
		GameObject[] blockingObjects = target.blockingObjects;
		bool hasExplicitPaint = paintingObjects != null && paintingObjects.Length > 0;
		bool hasExplicitBlock = blockingObjects != null && blockingObjects.Length > 0;

		LayerMask paintingLayers = hasExplicitPaint ? (LayerMask)~0 : target.paintingLayers;
		LayerMask blockingLayers = hasExplicitBlock ? (LayerMask)~0 : target.blockingLayers;

		float rayLength = aaBoundsSize.y;
		float heightScale = 1f / rayLength;

		var castRadius = target.castRadius;

		var heightmap = new float[XZM, XZM];
		var maskmap = new float[XZM, XZM];

		var oldSelfTerrainCollider = target.workingObject ? target.workingObject.GetComponent<TerrainCollider>() : null;

		for(int z = 0; z < XZM; ++z) {
			for(int x = 0; x < XZM; ++x) {
				var pos = terrainCorner + new Vector3(x * xStep, aaBoundsSize.y, z * zStep);

				if(ooPlanes[0].GetSide(pos) || ooPlanes[1].GetSide(pos) || ooPlanes[2].GetSide(pos) || ooPlanes[3].GetSide(pos)) {
					heightmap[z, x] = 0f;
					maskmap[z, x] = 0f;
					continue;
				}

				float targetDist = rayLength;
				float targetMask = 1f;

				var rhisDown = Physics.SphereCastAll(pos, castRadius, Vector3.down, rayLength, paintingLayers);
				if(rhisDown.Length > 0) {
					for(int i = 0, n = rhisDown.Length; i < n; ++i)  {
						var rhiDown = rhisDown[i];

						var validTerrain = !(rhiDown.collider is TerrainCollider)
							|| (target.allowTerrainColliders && (oldSelfTerrainCollider == null || rhiDown.collider != oldSelfTerrainCollider));
						if(
							validTerrain
							&& (!hasExplicitPaint || System.Array.IndexOf(paintingObjects, rhiDown.collider.gameObject) != -1)
							&& System.Array.IndexOf(blockingObjects, rhiDown.collider.gameObject) == -1
						) {
							var curDist = pos.y - rhiDown.point.y;// rhiDown.distance + castRadius;
							if(curDist < targetDist) {
								targetMask = MaskSlope(rhiDown.normal.y);
								targetDist = curDist;
							}
						}
					}
				}

				bool hasClearance = true;
				if(targetDist < rayLength && (hasExplicitBlock || blockingLayers.value != 0)) {
					float blockedDist = rayLength;

					var rhisDownBlockers = Physics.SphereCastAll(pos, castRadius, Vector3.down, rayLength, blockingLayers);
					for(int i = 0, n = rhisDownBlockers.Length; i < n; ++i)  {
						var rhiDown = rhisDownBlockers[i];

						if(!(rhiDown.collider is TerrainCollider))
							if(!hasExplicitBlock || System.Array.IndexOf(blockingObjects, rhiDown.collider.gameObject) != -1)
								if(!hasExplicitPaint || System.Array.IndexOf(paintingObjects, rhiDown.collider.gameObject) == -1)
									blockedDist = Mathf.Min(blockedDist, pos.y - rhiDown.point.y /*rhiDown.distance + castRadius*/);
					}

					if(blockedDist < targetDist) {
						const float cBelow = 25f;

						var upPos = pos + Vector3.down * (targetDist + cBelow);
						RaycastHit[] rhisUpBlockers = Physics.SphereCastAll(upPos, castRadius, Vector3.up, targetDist + cBelow, blockingLayers);
						for(int i = 0, n = rhisUpBlockers.Length; i < n && hasClearance; ++i)  {
							var rhiUp = rhisUpBlockers[i];

							if(!(rhiUp.collider is TerrainCollider))
								if(!hasExplicitBlock || System.Array.IndexOf(blockingObjects, rhiUp.collider.gameObject) != -1)
									if(!hasExplicitPaint || System.Array.IndexOf(paintingObjects, rhiUp.collider.gameObject) == -1)
										hasClearance = rhiUp.point.y - upPos.y /*rhiUp.distance + castRadius*/ - cBelow > target.heightClearance;
						}
					}
				}
				
				bool hasValidSample = hasClearance && targetDist != rayLength;
				heightmap[z, x] = hasValidSample ? Mathf.Clamp01(1f - targetDist * heightScale) : 0f;
				maskmap[z, x] = hasValidSample ? targetMask : 0f;
			}
		}

		target.heightData = new float[XZM * XZM];
		System.Buffer.BlockCopy(heightmap, 0, target.heightData, 0, target.heightData.Length * sizeof(float));

		target.maskData = new float[XZM * XZM];
		System.Buffer.BlockCopy(maskmap, 0, target.maskData, 0, target.maskData.Length * sizeof(float));

		var ttc = target.terrainTemplate;
		var ttd = ttc.terrainData;

		if(target.workingObject)
			target.workingObject.transform.position += Vector3.up * target.pickingHackFixBias;

		var oldTC = target.workingObject ? target.workingObject.GetComponent<Terrain>() : null;
		//if(oldTC && oldTC.GetComponent<TerrainCollider>())
		//	oldTC.gameObject.AddComponent<TerrainCollider>();
		var td = oldTC ? oldTC.terrainData : new TerrainData();
		td.SetDetailResolution(XZM, 32);
		td.heightmapResolution = XZM;
		td.SetHeights(0, 0, heightmap);
		td.size = aaBoundsSize;

		td.splatPrototypes = ttd.splatPrototypes;
		td.detailPrototypes = ttd.detailPrototypes;
		td.treePrototypes = ttd.treePrototypes;

		for(int i = 0, offset = 0, step = target.detailLayerElements*sizeof(int), n = td.detailPrototypes.Length, m = target.detailLayerCount; i < n && i < m; ++i, offset += step) {
			var details = td.GetDetailLayer(0, 0, td.detailWidth, td.detailHeight, i);
			System.Buffer.BlockCopy(target.detailData, offset, details, 0, step);
			td.SetDetailLayer(0, 0, i, details);
		}

		if(target.treeInstances != null) {
			var tdi = new TreeInstance[target.treeInstances.Length];
			for(int i = 0, n = target.treeInstances.Length; i < n; ++i)
				tdi[i] = (TreeInstance)target.treeInstances[i];
			td.treeInstances = tdi;
		}

		var terrain = oldTC ? oldTC.gameObject : Terrain.CreateTerrainGameObject(td);
		if(oldTC == null) {
			var helper = terrain.AddComponent<PaintJobProxy>();
			while(UnityEditorInternal.ComponentUtility.MoveComponentUp(helper))
				;
		}
		var tc = terrain.GetComponent<Terrain>();
		tc.detailObjectDistance = 1000f;//ttc.detailObjectDistance;
		tc.detailObjectDensity = ttc.detailObjectDensity;
		target.materialInstance = tc.materialTemplate = new Material(ttc.materialTemplate);
		tc.castShadows = false;

		terrain.transform.parent = target.transform;
		terrain.transform.position = terrainCorner;
		terrain.transform.localRotation = Quaternion.identity;
		terrain.transform.localScale = Vector3.one;
		target.workingObject = terrain;

		target.isPainting = true;
		target.alignedSize = aaBoundsSize;
		target.materialInstance.SetFloat("_HackCullScale", 1f);
		tc.drawHeightmap = true;

		tc.terrainData.wavingGrassAmount = 0f;
		tc.terrainData.wavingGrassSpeed = 0f;
		tc.terrainData.wavingGrassStrength = 0f;
		tc.terrainData.wavingGrassTint = Color.white;
	}

	void Mask() {
		var tc = target.workingObject.GetComponent<Terrain>();
		var td = tc.terrainData;
		var mask = target.maskData;

		var layerCount = td.detailPrototypes.Length;
		for(int i = 0; i < layerCount; ++i) {
			var details = td.GetDetailLayer(0, 0, td.detailWidth, td.detailHeight, i);

			var X = details.GetLength(0);
			var Z = details.GetLength(1);
			
			for(int z = 0; z < Z; ++z) {
				for(int x = 0; x < X; ++x) {
					var idx = z * X + x;
					var m = mask[idx];
					if(m < 1f) {
						var d = details[z, x];
						details[z, x] = Mathf.Min(d, Mathf.RoundToInt(d * m * 16f));
					}
				}
			}

			td.SetDetailLayer(0, 0, i, details);
		}
	}

	void Save() {
		var tc = target.workingObject.GetComponent<Terrain>();
		var td = tc.terrainData;

		target.detailData = null;
		target.detailLayerCount = td.detailPrototypes.Length;
		if(td.detailPrototypes.Length > 0) {
			target.detailLayerElements = td.detailWidth * td.detailHeight;
			target.detailData = new int[target.detailLayerCount * target.detailLayerElements];

			int offset = 0, step = target.detailLayerElements * sizeof(int);
			for(int i = 0, n = target.detailLayerCount; i < n; ++i, offset += step) {
				var details = td.GetDetailLayer(0, 0, td.detailWidth, td.detailHeight, i);
				System.Buffer.BlockCopy(details, 0, target.detailData, offset, step);
			}
		}

		target.treeInstances = new PaintJob.TreeInstance2[td.treeInstances.Length];
		var tdi = td.treeInstances;
		for(int i = 0, n = tdi.Length; i < n; ++i)
		    target.treeInstances[i] = new PaintJob.TreeInstance2(tdi[i]);
	}

	void Close() {
		target.isPainting = false;

		if(target.workingObject) {
			target.workingObject.transform.position += Vector3.down * target.pickingHackFixBias;

			var tc = target.workingObject.GetComponent<Terrain>();
			if(tc)
				tc.drawHeightmap = false;
		}
		
		if(target.materialInstance)
			target.materialInstance.SetFloat("_HackCullScale", 0f);

		foreach(var c in target.autoColliderList)
			Object.DestroyImmediate(c);
		target.autoColliderList.Clear();
	}

	void Discard() {
		Object.DestroyImmediate(target.workingObject);
		target.workingObject = null;
	}

	void Destroy() {
		Object.DestroyImmediate(target.workingObject);
		target.workingObject = null;
		target.alignedSize = Vector3.zero;;
		target.heightData = target.maskData = null;
		target.detailData = null;
		target.detailLayerCount = 0;
		target.treeInstances = null;
		target.materialInstance = null;
	}
}
