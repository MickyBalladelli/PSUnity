using UnityEngine;
using System.Collections;

public class PlaneScript : MonoBehaviour {

	// Use this for initialization
	void Start () {
        Renderer renderer = GetComponent<Renderer>();
        Color oldColor = renderer.material.color;
        Color newColor = new Color(oldColor.r, oldColor.b, oldColor.g, 0.5f);
        renderer.material.SetColor("_Color", newColor);
    }
	
	// Update is called once per frame
	void Update () {
	
	}
}
