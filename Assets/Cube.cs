using UnityEngine;
using System.Collections;

public class Cube : MonoBehaviour {

    public float rotationSpeed = 1.6F;
    public float angle = 90F;


    // Use this for initialization
    void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
        Quaternion rotation = transform.rotation * Quaternion.Euler(0, angle, 0);

        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 1.6F * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.R))
        {
            GetComponent<Renderer>().material.color = Color.red;
        }
        if (Input.GetKeyDown(KeyCode.G))
        {
            GetComponent<Renderer>().material.color = Color.green;
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            GetComponent<Renderer>().material.color = Color.blue;
        }

    }
}
