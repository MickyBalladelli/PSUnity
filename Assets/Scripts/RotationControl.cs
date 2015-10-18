using UnityEngine;
using System.Collections;

public class RotationControl : MonoBehaviour {

	private int direction = 1; //Up 1, Down 0

	// Update is called once per frame
	void Update () 
	{
		transform.Rotate (new Vector3 (15, 30, 45) * Time.deltaTime);

		if (transform.position.y >= 3.0F) 
		{
			direction = 0;
		} 
		if (transform.position.y <= 0.0F) 
		{
			direction = 1;
		} 

		if (direction == 1)
		{
			transform.position += Vector3.up * 0.1F;
		}
		else
		{
			transform.position += Vector3.down * 0.1F;
		}

		foreach (Transform child in transform) {
			child.position += Vector3.up * 0.1F;
		}
	}
}
