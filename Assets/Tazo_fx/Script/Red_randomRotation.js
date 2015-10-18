var rotationMaxX:float=0;
var rotationMaxY:float=360;
var rotationMaxZ:float=0;

function Start ()
{
var rotX = Random.Range(-rotationMaxX, rotationMaxX);
var rotY = Random.Range(-rotationMaxY, rotationMaxY);
var rotZ = Random.Range(-rotationMaxZ, rotationMaxZ);

transform.Rotate(rotX, rotY, rotZ);

}

