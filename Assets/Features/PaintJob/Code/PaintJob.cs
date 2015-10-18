using UnityEngine;
using System.Collections.Generic;

public class PaintJob : MonoBehaviour {
	[System.Serializable] public struct TreeInstance2 {
		public Color32 color;			
		public Color32 lightmapColor;
		public int prototypeIndex;
		public float rotation;
		public Vector3 position;
		public float widthScale;
		public float heightScale;

		public TreeInstance2(TreeInstance i) {
			color = i.color; lightmapColor = i.lightmapColor; prototypeIndex = i.prototypeIndex; rotation = i.rotation; position = i.position; widthScale = i.widthScale; heightScale = i.heightScale;
		}

		static public explicit operator TreeInstance(TreeInstance2 i) {
			var o = new TreeInstance();
			o.color = i.color; o.lightmapColor = i.lightmapColor; o.prototypeIndex = i.prototypeIndex; o.rotation = i.rotation; o.position = i.position; o.widthScale = i.widthScale; o.heightScale = i.heightScale;
			return o;
		}
	}

	public Terrain terrainTemplate;

	public LayerMask paintingLayers = ~0;
	public LayerMask blockingLayers = 0;

	public GameObject[] paintingObjects = new GameObject[0];
	public GameObject[] blockingObjects = new GameObject[0];

	public float density = 4f;
	public float heightClearance = 0.5f;
	public float castRadius = 0.15f;
	public float slopeThresholdLower = 0.4f;
	public float slopeThresholdUpper = 0.6f;

	public float pickingHackFixBias = 0.03f;
	
	public bool autoColliders = true;
	public bool allowTerrainColliders = false;
	
	[HideInInspector] public GameObject workingObject;
	[HideInInspector] public bool isPainting;
	[HideInInspector] public Vector3 alignedSize;
	[HideInInspector] public float[] heightData;
	[HideInInspector] public float[] maskData;
	[HideInInspector] public int[] detailData;
	[HideInInspector] public int detailLayerElements;
	[HideInInspector] public int detailLayerCount;
	[HideInInspector] public TreeInstance2[] treeInstances;
	[HideInInspector] public Material materialInstance;
	[HideInInspector] public List<MeshCollider> autoColliderList = new List<MeshCollider>();
	
	void OnValidate() {
		var e = transform.rotation.eulerAngles;
		if(!Mathf.Approximately(e.x, 0f) || !Mathf.Approximately(e.z, 0f))
			transform.rotation = Quaternion.Euler(new Vector3(0f, e.y, 0f));
	}

	void OnDrawGizmos() {
		Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);

		Gizmos.matrix = Matrix4x4.TRS(
			transform.position + new Vector3(0f, transform.localScale.y * 0.5f - 0.1f, 0f),
			transform.rotation,
			new Vector3(2.5f, 0.2f, 2.5f)
		);
		Gizmos.DrawCube(Vector3.zero, Vector3.one);

		Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
		Gizmos.matrix = Matrix4x4.TRS(
			transform.position,
			transform.rotation,
			transform.localScale
		);
		Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
	}

	void OnDrawGizmosSelected() {
		if(isPainting) {
			var c = Color.cyan; c.a = 0.5f; Gizmos.color = c;
			Gizmos.DrawWireCube(transform.position, alignedSize);
		}

		Gizmos.color = new Color(0.3f, 0.4f, 1.0f, 1f);
		Gizmos.matrix = Matrix4x4.TRS(
			transform.position,
			transform.rotation,
			transform.localScale
			);
		Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
	}
}
