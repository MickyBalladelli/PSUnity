// Smooth Follow from Standard Assets
// Converted to C# because I fucking hate UnityScript and it's inexistant C# interoperability
// If you have C# code and you want to edit SmoothFollow's vars ingame, use this instead.
using UnityEngine;
using System.Collections;

public class SmoothFollow : MonoBehaviour
{

    // The target we are following
    private Transform target;

    // The distance in the x-z plane to the target
    public float distance = 30.0f;
    // the height we want the camera to be above the target
    public float height = 10.0f;
    // How much we 
    public float heightDamping = 10.0f;
    public float rotationDamping = 3.0f;
    private float zoom = 60;
    public float smooth = 1.5F;
    private bool isFollowing = false;
    private Vector3 prevPosition;
    private Quaternion prevRotation;
    private bool updatePrev = false;

    // Place the script in the Camera-Control group in the component menu
    //[AddComponentMenu("Camera-Control/Smooth Follow")]

    public void SetDistance( float d )
    {
        distance = d;
    }
    public bool IsFollowing()
    {
        return isFollowing;
    }
    public void SetTarget( Transform targetTrasform)
    {
        if (targetTrasform == null)
        {
            isFollowing = false;
            target = null;
            updatePrev = true;
        }
        else
        {
            if (isFollowing) // We're already following, ignore
                return;

            isFollowing = true;
            prevPosition = transform.position;
            prevRotation = transform.rotation;
            target = targetTrasform;
            updatePrev = false;
        }

    }
    void LateUpdate()
    {
    
        // Zoom in or out
        Camera camera = GetComponent<Camera>();

        var d = Input.GetAxis("Mouse ScrollWheel");
        if (d > 0f)
        {
            zoom -= 2F;
            if (zoom < 2F)
                zoom = 2F;
        }
        else if (d < 0f)
        {
            zoom += 2F;
            if (zoom > 100F)
                zoom = 100F;
        }
        camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, zoom, Time.deltaTime * smooth);

        if (isFollowing && target)
        {

            // Calculate the current rotation angles
            //float wantedRotationAngle = target.eulerAngles.y;
            float wantedHeight = target.position.y + height;

            float currentRotationAngle = transform.eulerAngles.y;
            float currentHeight = transform.position.y;

            // Damp the rotation around the y-axis
            //currentRotationAngle = Mathf.LerpAngle(currentRotationAngle, wantedRotationAngle, rotationDamping * Time.deltaTime);

            // Damp the height
            currentHeight = Mathf.Lerp(currentHeight, wantedHeight, heightDamping * Time.deltaTime);

            // Convert the angle into a rotation
            var currentRotation = Quaternion.Euler(0, currentRotationAngle, 0);

            // Set the position of the camera on the x-z plane to:
            // distance meters behind the target
            transform.position = target.position;
            transform.position -= currentRotation * Vector3.forward * distance;

            // Set the height of the camera
            transform.position = new Vector3(transform.position.x, currentHeight, transform.position.z);

            // Always look at the target
            transform.LookAt(target);
        }
        if (updatePrev)
        {
            transform.position = prevPosition;
            transform.rotation = prevRotation;
            updatePrev = false;
        }

    }
}