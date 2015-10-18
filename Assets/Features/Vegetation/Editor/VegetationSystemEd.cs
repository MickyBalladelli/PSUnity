using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VegetationSystem))]
public class VegetationSystemEd : Editor {
	new VegetationSystem target { get { return base.target as VegetationSystem; } }

	public override void OnInspectorGUI() {
		DrawDefaultInspector();

		EditorGUILayout.Space();
		
		if(GUILayout.Button("Rebuild System"))
			target.Rebuild();

		if(GUILayout.Button("Teardown System"))
			target.Teardown();

		if(GUILayout.Button("Force Full GI Update"))
			target.TickIndirectLighting(true);
	}

	void OnSceneGUI() {
		target.OnSceneGUI();
	}
}
