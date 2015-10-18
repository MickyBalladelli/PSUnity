using UnityEngine;

public class RotateGlobe : MonoBehaviour {
    public float rotationSpeed = 0.1F;
    public float angle = 90F;

    void Update ()
    {
        Quaternion rotation = transform.rotation * Quaternion.Euler(0, angle, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, rotationSpeed * Time.deltaTime);   
    }
}
