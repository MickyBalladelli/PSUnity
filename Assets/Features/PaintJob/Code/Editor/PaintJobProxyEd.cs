using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PaintJobProxy))]
public class PaintJobProxyEd : Editor {
	bool enableAsset;
	string assetPath = "paintjob";

	public override void OnInspectorGUI() {
		GUI.color = new Color(0.7f, 0.8f, 1f);
		if(GUILayout.Button("Select PaintJob"))
			Selection.activeGameObject = (target as Component).transform.parent.gameObject;

		EditorGUILayout.Space();
		GUI.color = Color.white;

		EditorGUILayout.BeginHorizontal();
		{
			enableAsset = EditorGUILayout.Toggle(enableAsset, GUILayout.MaxWidth(20));

			EditorGUILayout.BeginVertical();
			{
				var terrain = (target as Component).GetComponent<Terrain>();

				GUI.enabled = false;
				EditorGUILayout.TextField("Current asset:", terrain ? terrain.terrainData.name : "<none>");
				
				GUI.enabled = enableAsset && terrain;
				assetPath = EditorGUILayout.TextField("Asset name:", assetPath);

				GUI.color = Color.red;
				if(GUILayout.Button("Convert TerrainData Asset")) {
					var name = string.Format("Assets/{0}.asset", assetPath);
					AssetDatabase.CreateAsset(terrain.terrainData, name);
					terrain.terrainData = AssetDatabase.LoadAssetAtPath(name, typeof(TerrainData)) as TerrainData;
				}
			}
			EditorGUILayout.EndVertical();
		}
		EditorGUILayout.EndHorizontal();
	}
}
