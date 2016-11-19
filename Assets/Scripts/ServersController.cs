using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Threading;

public class InstanceData
{
    public string name;
    public string role;
    public string description;
    public string status;
    public string message;
}
public class SiteInstanceData
{
    public string name;
    public string description;
    public GameObject instance;
    public GameObject canvas;
}

public class ServersController : MonoBehaviour 
{
    public GameObject serverPrefab0;
    public GameObject serverPrefab1;
    public GameObject serverPrefab2;
    public GameObject sitePrefab;
    public GameObject canvasPrefab;
    public GUISkin skin;
    public float smooth = 1.5F;

    private List<string> commands = new List<string>();     // Local copy of commands received, this list is emptied once treated.
	private List<GameObject> instances = new List<GameObject>();
	private NavMeshAgent agent;
	private GameObject R2D2;
	private Vector3 initR2D2Pos;
	private int curInstance = 0;
	private float countdown = 2.0f;
	//private string message = "";
	private List<InstanceData> messageList = new List<InstanceData>();                          // List of messages displayed by R2D2
    private List<InstanceData> instanceList = new List<InstanceData>();             // Server data array
    private List<SiteInstanceData> siteInstanceList = new List<SiteInstanceData>(); // Site data array
    bool displayInfo = true;
    private float yPos = 0.6F;
    public DateTime lastUpdate = new DateTime();

    MainController mainController;

    private void Start()
	{
        mainController = MainController.Instance;
        if (mainController)
        {
            mainController.DisplayIsUpdating(false);

            foreach (string c in mainController.commands)
            {
                commands.Add(c);
            }
        }

        R2D2 = GameObject.FindGameObjectWithTag("R2D2");

        if (R2D2 != null)
        {
            agent = R2D2.GetComponent<NavMeshAgent>();
            initR2D2Pos = R2D2.transform.position;
        }
    }

    // FixedUpdate is called at fixed intervals (you can define the interval in Edit -> Project Settings -> Time -> Fixed Timestep). 
    private void FixedUpdate()
    {

    }
	private void LateUpdate()
	{
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            SmoothFollow sf = Camera.main.GetComponent<SmoothFollow>();

            if (sf.IsFollowing() == false)
                sf.SetTarget(R2D2.transform);
            else
                sf.SetTarget(null);

        }
        // Move R2D2
        if (instances.Count > 0 && R2D2 != null)
        {
            if (CompareVectors(agent.destination, initR2D2Pos))
            {
                agent.SetDestination(instances[curInstance].transform.position + instances[curInstance].transform.forward);
            }
            else if (agent.remainingDistance == 0F)
            {
                countdown -= Time.deltaTime;

                if (countdown <= 0.0f)
                {
                    string now = DateTime.Now.ToString("H:mm:ss");
                    countdown = 2.0f;
                    InstanceData message = new InstanceData();
                    message.message = now + " R2D2: " + instanceList[curInstance].description;
                    message.status = instanceList[curInstance].status;

                    messageList.Add(message);

                    if (curInstance + 1 == instances.Count)
                        curInstance = 0;
                    else
                        curInstance++;
                    agent.SetDestination(instances[curInstance].transform.position + instances[curInstance].transform.forward);

                    if (messageList.Count > 5)
                        messageList.RemoveAt(0);
                }
            }
        }

        // Check if new commands arrived
        if (mainController && lastUpdate != mainController.lastUpdate)
        {
			commands.Clear();

            foreach (string c in mainController.commands)
            {
                commands.Add(c);
            }
            lastUpdate = mainController.lastUpdate;
        }
        // Deal with received commands
        foreach (string elem in commands)
		{
			string[] data = elem.Split(';');

			if (data.Length > 1)
			{
				string command = data[0];

				if (command == "SERVER_1.0")
				{
					Text nameText, roleText, locationText;
					string x = data[1];
					string z = data[2];
					string rotation = data[3];
					string name = data[4];
					string role = data[5];
					string location = data[6];
					string status = data[7]; // Status can be 0,1,2,3,4 (4 being the most critical: fire)
                    string type = data[8];   // Type can be 0,1,2
                    string description = data[9];

                    bool found = false;
                    foreach (InstanceData i in instanceList)
                    {
                        if (i.name == name)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (found == false)
                    {
                        InstanceData instanceData = new InstanceData();
                        instanceData.description = description;
                        instanceData.name = name;
                        instanceData.role = role;
                        instanceData.status = status;

                        instanceList.Add(instanceData);

                        //Debug.LogFormat("Command {0} {1} {2} {3} {4} {5}", command, name, x, z, status, description);
                        GameObject serverPrefab;
                        switch (type)
                        {
                            default:
                            case "0":
                                serverPrefab = serverPrefab0;
                                break;
                            case "1":
                                serverPrefab = serverPrefab1;
                                break;
                            case "2":
                                serverPrefab = serverPrefab2;
                                break;
                        }

                        // Special case for type 2 as the original model is weirdo
                        GameObject instance;
                        if (type != "2")
                        {
                            instance = (GameObject)Instantiate(serverPrefab, new Vector3(int.Parse(x), yPos, int.Parse(z)), new Quaternion(0, 0, 0, 0));
                            instance.transform.Rotate(new Vector3(0, 180 + int.Parse(rotation), 0));
                        }
                        else
                        {
                            float type2yPos = 3.3F;
                            instance = (GameObject)Instantiate(serverPrefab, new Vector3(int.Parse(x), type2yPos, int.Parse(z)), new Quaternion(0, 0, 0, 0));

                            instance.transform.Rotate(new Vector3(0, -140 + int.Parse(rotation), 0));
                        }
                        instances.Add(instance);
                        Transform[] allChildren = instance.GetComponentsInChildren<Transform>();
                        foreach (Transform child in allChildren)
                        {
                            if (child.name == "Name")
                            {
                                nameText = child.GetComponent<Text>();
                                nameText.text = name;
                                if (status.Contains("4"))
                                {
                                    nameText.color = Color.red;
                                }
                                else if (status.Contains("3"))
                                {
                                    Color color = new Color();
                                    ColorUtility.TryParseHtmlString("#FF8F18FF", out color);
                                    nameText.color = color;
                                }
                                else if (status.Contains("2"))
                                {
                                    nameText.color = Color.yellow;
                                }
                                else if (status.Contains("1"))
                                {
                                    nameText.color = Color.blue;
                                }
                                else if (status.Contains("0"))
                                {
                                    nameText.color = Color.green;
                                }
                            }
                            if (child.name == "Role")
                            {
                                roleText = child.GetComponent<Text>();
                                roleText.text = role;
                                roleText.color = Color.white;
                            }
                            if (child.name == "Location")
                            {
                                locationText = child.GetComponent<Text>();
                                locationText.text = location;
                                locationText.color = Color.white;
                            }

                            if (child.name.Contains("FireComplex"))
                            {
                                GameObject fire = child.gameObject;
                                if (status.Contains("4"))
                                {
                                    fire.SetActive(true);
                                }
                                else
                                {
                                    fire.SetActive(false);
                                }
                            }
                            if (child.name.Contains("ServerSmoke"))
                            {
                                GameObject smoke = child.gameObject;
                                if (status.Contains("3"))
                                {
                                    smoke.SetActive(true);
                                }
                                else
                                {
                                    smoke.SetActive(false);
                                }
                            }
                            if (child.name.Contains("Flare"))
                            {
                                GameObject flare = child.gameObject;
                                if (status.Contains("2"))
                                {
                                    flare.SetActive(true);
                                }
                                else
                                {
                                    flare.SetActive(false);
                                }
                            }
                        }
                    }
                    else
                    {
                        int count = 0;
                        foreach (GameObject instance in instances)
                        {
                            Transform[] allChildren = instance.GetComponentsInChildren<Transform>();
                            foreach (Transform child in allChildren)
                            {
                                if (child.name == "Name")
                                {
                                    nameText = child.GetComponent<Text>();

                                    if (nameText.text == name)
                                    {
                                        instanceList[count].description = description;

                                        GameObject fire = instance.transform.Find("FireComplex").gameObject;
                                        GameObject smoke = instance.transform.Find("ServerSmoke").gameObject;
                                        GameObject flare = instance.transform.Find("Flare").gameObject;

                                        if (type != "2")
                                        {
                                            instance.transform.position = new Vector3(int.Parse(x), yPos, int.Parse(z));
                                        }
                                        else
                                        {
                                            float type2yPos = 3.3F;
                                            instance.transform.position = new Vector3(int.Parse(x), type2yPos, int.Parse(z));
                                        }

                                        instance.transform.Rotate(new Vector3(0, int.Parse(rotation), 0));

                                        if (status.Contains("4"))
                                        {
                                            fire.SetActive(true);
                                            nameText.color = Color.red;
                                        }
                                        else if (status.Contains("3"))
                                        {
                                            flare.SetActive(false);
                                            fire.SetActive(false);
                                            smoke.SetActive(true);

                                            Color color = new Color();
                                            ColorUtility.TryParseHtmlString("#FF8F18FF", out color);
                                            nameText.color = color;
                                        }
                                        else if (status.Contains("2"))
                                        {
                                            fire.SetActive(false);
                                            smoke.SetActive(false);

                                            flare.SetActive(true);
                                            nameText.color = Color.yellow;
                                        }
                                        else if (status.Contains("1"))
                                        {
                                            flare.SetActive(false);
                                            fire.SetActive(false);
                                            smoke.SetActive(false);
                                            nameText.color = Color.blue;
                                        }
                                        else if (status.Contains("0"))
                                        {
                                            flare.SetActive(false);
                                            fire.SetActive(false);
                                            smoke.SetActive(false);
                                            nameText.color = Color.green;
                                        }
                                        GameObject canvas = instance.transform.Find("Canvas").gameObject;

                                        GameObject roleObject = canvas.transform.Find("Role").gameObject;
                                        roleText = roleObject.GetComponent<Text>();
                                        roleText.text = role;

                                        GameObject locationObject = canvas.transform.Find("Location").gameObject;
                                        locationText = locationObject.GetComponent<Text>();
                                        locationText.text = location;
                                        

                                        break;
                                    }
                                }

                            }
                            count++;
                        }

                    }
                }
                if (command == "SITE_1.0")
                {
                    string x = data[1];
                    string z = data[2];
                    string width = data[3];
                    string height = data[4];
                    string rotation = data[5];
                    string name = data[6];
                    string location = data[7];
                    string description = data[8];

                    SiteInstanceData siteInstanceData = new SiteInstanceData();
                    siteInstanceData.description = description;
                    siteInstanceData.name = name;

                    //Debug.LogFormat("Command {0} {1} {2} {3} {4} {5} {6} {7} {8}", command, name, x, z, width, height, rotation, status, description);

                    bool found = false;
                    
                    foreach (SiteInstanceData siteInstance in siteInstanceList)
                    {
                        if (siteInstance.name == name)
                        {
                            siteInstance.instance.transform.Rotate(new Vector3(0, int.Parse(rotation), 0));
                            siteInstance.instance.transform.localScale = new Vector3(float.Parse(width), 1, float.Parse(height));
                            siteInstance.instance.transform.position = new Vector3(int.Parse(x), yPos, int.Parse(z));


                            Transform[] allChildren = siteInstance.canvas.GetComponentsInChildren<Transform>();
                            foreach (Transform child in allChildren)
                            {
                                if (child.name == "Name")
                                {
                                    Text textname = child.GetComponent<Text>();
                                    textname.text = name;
                                    textname.color = Color.white;

                                }
                                if (child.name == "Location")
                                {
                                    Text textname = child.GetComponent<Text>();
                                    textname.text = location;
                                    textname.color = Color.white;
                                }
                            }
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        siteInstanceData.instance = (GameObject)Instantiate(sitePrefab, new Vector3(int.Parse(x), yPos, int.Parse(z)), new Quaternion(0, 0, 0, 0));
                        siteInstanceData.instance.transform.Rotate(new Vector3(0, int.Parse(rotation), 0));
                        siteInstanceData.instance.transform.localScale = new Vector3(float.Parse(width), 1, float.Parse(height));

                        Renderer renderer = siteInstanceData.instance.GetComponent<Renderer>();

                        siteInstanceData.canvas = (GameObject)Instantiate(canvasPrefab, siteInstanceData.instance.transform.position, new Quaternion(0, 0, 0, 0));
                        //Renderer canvasRen = siteInstanceData.canvas.GetComponent<Renderer>();

                        siteInstanceData.canvas.transform.SetParent(siteInstanceData.instance.transform, true);
                        siteInstanceData.canvas.transform.position = new Vector3(renderer.bounds.center.x - renderer.bounds.size.x / 3f, renderer.bounds.center.y, renderer.bounds.center.z);
                        siteInstanceList.Add(siteInstanceData);

                        Transform[] allChildren = siteInstanceData.canvas.GetComponentsInChildren<Transform>();
                        foreach (Transform child in allChildren)
                        {
                            if (child.name == "Name")
                            {
                                Text textname = child.GetComponent<Text>();
                                textname.text = name;
                                textname.color = Color.white;

                            }
                            if (child.name == "Location")
                            {
                                Text textname = child.GetComponent<Text>();
                                textname.text = location;
                                textname.color = Color.white;
                            }
                        }

                    }
                }
            }
        }
		if (commands.Count > 0)
		{
			commands.Clear();
		}
	}


    private bool CompareVectors(Vector3 a, Vector3 b)
    {
        return Vector3.SqrMagnitude(a - b) < 1.1F;
    }

    private void OnGUI()
    {

        if (mainController != null)
        {
            if (displayInfo)
            {
                mainController.infoRect = GUI.Window(1, mainController.infoRect, InfoWindow, "", GUIStyle.none);
            }

            if (GUI.Button(new Rect(Screen.width - 70, 0, 70, 30), "Info"))
            {
                displayInfo = !(displayInfo);
            }
        }
    }
    private void InfoWindow(int id)
    {
        GUIStyle style = new GUIStyle();
        style.wordWrap = false;
        style.stretchWidth = false;
        style.stretchHeight = false;
        style.alignment = TextAnchor.UpperLeft;
        int numElements = 5;

        GUI.contentColor = Color.white;

        // Calc height of window
        if (mainController != null)
        {
            style.fontSize = 14;
            float height = style.lineHeight * numElements;
            mainController.infoRect.height = height;
            mainController.infoRect.y = Screen.height - height;
        }
        GUILayout.BeginVertical(style);
        style.normal.textColor = Color.white;
        foreach (InstanceData message in messageList)
        {
            switch (message.status)
            {
                case "0":
                    style.normal.textColor = Color.green;
                    break;
                case "1":
                    style.normal.textColor = Color.white;
                    break;
                case "2":
                    style.normal.textColor = Color.yellow;
                    break;
                case "3":
                    Color color = new Color();
                    ColorUtility.TryParseHtmlString("#FF8F18FF", out color);
                    style.normal.textColor = color;
                    break;
                case "4":
                    style.normal.textColor = Color.red;
                    break;
            }

            GUILayout.Label(message.message, style);
        }
        GUILayout.EndVertical();

        //Save width in rect
        if (mainController != null)
        {
            int width = 550;
            mainController.infoRect.width = width;
            mainController.infoRect.x = Screen.width / 2 - width / 2;
        }

        GUI.DragWindow(new Rect(0, 0, Screen.width, Screen.height));
    }
}
