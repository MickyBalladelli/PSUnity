using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VegetationBakeData))]
public class VegetationBakeDataEd : Editor {
	new VegetationBakeData target { get { return base.target as VegetationBakeData; } }

	public override void OnInspectorGUI() {
		if(GUILayout.Button("Capture Vegetation")) {
			target.CaptureVegetationSources();
		}

		if(GUILayout.Button("Release Vegetation")) {
			target.ReleaseVegetationSources();
			target.GetComponent<VegetationSystem>().Teardown();
		}

		if(GUILayout.Button("Generate Materials")) {
			target.CreateMaterials();
			if(target.GetComponent<VegetationSystem>().isBuilt)
				target.GetComponent<VegetationSystem>().Rebuild();
		}

		if(GUILayout.Button("Bake Atlas")) {
			target.BakeTextures();
			if(target.GetComponent<VegetationSystem>().isBuilt)
				target.GetComponent<VegetationSystem>().Rebuild();
		}

		if(GUILayout.Button("Bake Ground")) {
 			target.BakeGround();

			if(target.GetComponent<VegetationSystem>().isBuilt)
				target.GetComponent<VegetationSystem>().Rebuild();
		}

		if(GUILayout.Button("Bake Vegetation")) {
			target.BakeVegetation();
			target.GetComponent<VegetationSystem>().Rebuild();
		}

		EditorGUILayout.Space();
		DrawDefaultInspector();
	}
}
