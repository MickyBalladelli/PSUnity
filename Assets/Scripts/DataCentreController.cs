using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Threading;

public class InstanceVMData
{
    public string VMname;
    public string VMhost;
    public string site;
    public string status;
    public string cluster;
    public string description;
    public GameObject instance;
}
public class MessageData
{
    public string message;
    public string status;
}

public class DataCentreController : MonoBehaviour 
{
    public TruckController truck1;
    public TruckController truck2;
    public GameObject cluster1;
    public GameObject cluster2;
    public GameObject sphere1;
    public GameObject sphere2;
    public GameObject VMprefab;

    private List<string> commands = new List<string>();     // Local copy of commands received, this list is emptied once treated.

    private string message = "";
    private List<MessageData> messageList = new List<MessageData>();                     // List of messages displayed 
    private List<InstanceVMData> instanceList1 = new List<InstanceVMData>();             // VM data array
    private List<InstanceVMData> instanceList2 = new List<InstanceVMData>();             // VM data array
    private List<InstanceVMData> truckInstanceList1 = new List<InstanceVMData>();             // VM data array
    private List<InstanceVMData> truckInstanceList2 = new List<InstanceVMData>();             // VM data array

    public DateTime lastUpdate = new DateTime();
    public float speed = 10.5F;
    float countdown = 2F;
    bool displayVMinfo = true;
    float messageCountdown = 0F;
    string nameSite1 = "Site 1";
    string nameSite2 = "Site 2";


    bool movingTruck1 = false;
    bool movingTruck2 = false;

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
        // See if trucks have something to move
        if (!movingTruck1)
        {
            for (int i = 0; i < truckInstanceList1.Count; i++)
            {
                GameObject spotlight = truck1.transform.Find("Spotlight").gameObject;
                truckInstanceList1[i].instance.transform.position = Vector3.Lerp(truckInstanceList1[i].instance.transform.position, spotlight.transform.position, Time.deltaTime * speed);
            }
        }
        if (truckInstanceList1.Count > 0 && CompareVectors(truckInstanceList1[0].instance.transform.position, truck1.transform.Find("Spotlight").gameObject.transform.position))
        { 
            countdown -= Time.deltaTime;

            SmoothFollow sf = Camera.main.GetComponent<SmoothFollow>();
            if (!movingTruck1)
            {
                truck1.GoToTarget();
                sf.SetDistance(70);
                sf.SetTarget(truck1.transform);
            }
            movingTruck1 = true;
            if (countdown <= 0)
            {

                if (truck1.GetComponent<NavMeshAgent>().remainingDistance <= 0)
                {
                    countdown = 2F;
                    sf.SetTarget(null);

                    if (messageList.Count == 0)
                        mainController.DisplayIsUpdating(false);

                    movingTruck1 = false;
                    truck1.GoToSource();

                    for (int i = 0; i < truckInstanceList1.Count; i++)
                    {
                        truckInstanceList1[i].instance.transform.SetParent(sphere2.transform, true);
                        instanceList2.Add(truckInstanceList1[i]);

                        Renderer renderer = sphere2.GetComponent<Renderer>();
                        float radius = renderer.bounds.extents.magnitude;
                        Vector3 pos = sphere2.transform.position + UnityEngine.Random.insideUnitSphere * radius / 2;
                        
                        truckInstanceList1[i].instance.transform.position = pos;
                        truckInstanceList1.Remove(truckInstanceList1[i]);
                        i--;

                    }
                }
            }
        }

        if (!movingTruck2)
        {
            for (int i = 0; i < truckInstanceList2.Count; i++)
            {
                GameObject spotlight = truck2.transform.Find("Spotlight").gameObject;
                truckInstanceList2[i].instance.transform.position = Vector3.Lerp(truckInstanceList2[i].instance.transform.position, spotlight.transform.position, Time.deltaTime * speed);
            }
        }
        if (truckInstanceList2.Count > 0 && CompareVectors(truckInstanceList2[0].instance.transform.position, truck2.transform.Find("Spotlight").gameObject.transform.position))
        {
            countdown -= Time.deltaTime;
            SmoothFollow sf = Camera.main.GetComponent<SmoothFollow>();
            if (!movingTruck2)
            {
                truck2.GoToTarget();
                sf.SetDistance(70);
                sf.SetTarget(truck2.transform);
            }

            movingTruck2 = true;
            if (countdown <= 0)
            {

                if (truck2.GetComponent<NavMeshAgent>().remainingDistance <= 0)
                {
                    countdown = 2F;
                    sf.SetTarget(null);
                    truck2.GoToSource();
                    movingTruck2 = false;
                    if (messageList.Count == 0)
                        mainController.DisplayIsUpdating(false);

                    for (int i = 0; i < truckInstanceList2.Count; i++)
                    {
                        truckInstanceList2[i].instance.transform.SetParent(sphere1.transform, true);
                        instanceList1.Add(truckInstanceList2[i]);
                        Renderer renderer = sphere1.GetComponent<Renderer>();
                        float radius = renderer.bounds.extents.magnitude;
                        Vector3 pos = sphere1.transform.position + UnityEngine.Random.insideUnitSphere * radius / 2;

                        truckInstanceList2[i].instance.transform.position = pos;
                        truckInstanceList2.Remove(truckInstanceList2[i]);
                        i--;
                    }
                }
            }
        }

        // Check if new commands arrived
        if (mainController && lastUpdate != mainController.lastUpdate)
        {
            commands.RemoveAll(AllCommands);

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

                if (command == "CLOUD_1.0")
                {
                    string VMname = data[1];
                    string VMhost = data[2];
                    string cluster = data[3];
                    string site = data[4];
                    string status = data[5];
                    string description = data[6];

                    // tell the main controller that we're busy updating all the commands
                    mainController.DisplayIsUpdating(true);


                    InstanceVMData instanceData = new InstanceVMData();
                    instanceData.VMname = VMname;
                    instanceData.site = site;
                    instanceData.status = status;
                    instanceData.cluster = cluster;
                    instanceData.description = description;

                    //Debug.LogFormat("Command {0} {1} {2} {3} {4} {5} {6} {7} {8}", command, name, x, z, width, height, rotation, status, description);

                    bool found = false;

                    foreach (InstanceVMData instance in truckInstanceList1)
                    {
                        if (instance.VMname == VMname)
                        {
                            UpdateVM(instance.instance, status);

                            found = true;
                            break;
                        }
                    }
                    foreach (InstanceVMData instance in truckInstanceList2)
                    {
                        if (instance.VMname == VMname)
                        {
                            UpdateVM(instance.instance, status);
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        for (int i = 0; i < instanceList1.Count; i++)
                        {
                            if (instanceList1[i].VMname == VMname)
                            {
                                
                                if (site == "2")
                                {
                                    string now = DateTime.Now.ToString("H:mm:ss");
                                    MessageData m = new MessageData();
                                    m.message = now + " " + "Moving VM: " + VMname + " to site " + nameSite2;
                                    m.status = "1"; // white
                                    messageList.Add(m);
                                    // instance for multiple trucks approach, to be implemented
                                    //truck1 = (GameObject)Instantiate(truck1prefab, new Vector3(-12.2F, 11.28F, 324.7F), new Quaternion(-14.25281F, -8.512268F, 0, 0));
                                    instanceList1[i].instance.transform.SetParent(truck1.transform, true);
                                    truckInstanceList1.Add(instanceList1[i]);
                                    countdown = 2F;
                                    instanceList1.Remove(instanceList1[i]);
                                    //i--;
                                }
                                else
                                {
                                    if (instanceList1[i].status != status || instanceList1[i].description != description)
                                    {
                                        UpdateVM(instanceList1[i].instance, status);

                                        instanceList1[i].status = status;
                                        instanceList1[i].description = description;

                                        string now = DateTime.Now.ToString("H:mm:ss");
                                        MessageData m = new MessageData();
                                        m.message = now + " " + "Update VM: " + VMname + " status " + description;
                                        m.status = status;
                                        messageList.Add(m);
                                    }
                                }
                                found = true;
                                break;
                            }
                        }
                    }
                    if (!found)
                    {
                        for (int i = 0; i < instanceList2.Count; i++)
                        {
                            if (instanceList2[i].VMname == VMname)
                            {

                                if (site == "1")
                                {
                                    string now = DateTime.Now.ToString("H:mm:ss");
                                    MessageData m = new MessageData();
                                    m.message = now + " " + "Moving VM: " + VMname + " to site " +nameSite1;
                                    m.status = "1"; // white
                                    messageList.Add(m);

                                    instanceList2[i].instance.transform.SetParent(truck2.transform, true);
                                    truckInstanceList2.Add(instanceList2[i]);
                                    countdown = 2F;
                                    instanceList2.Remove(instanceList2[i]);
                                    //i--;

                                }
                                else
                                {
                                    if (instanceList2[i].status != status || instanceList2[i].description != description)
                                    {
                                        UpdateVM(instanceList2[i].instance, status);

                                        instanceList2[i].status = status;
                                        instanceList2[i].description = description;
                                        string now = DateTime.Now.ToString("H:mm:ss");
                                        MessageData m = new MessageData();
                                        m.message = now + " " + "Update VM: " + VMname + " status " + description;
                                        m.status = status;
                                        messageList.Add(m);
                                    }                                
                                }
                                found = true;
                                break;
                            }
                        }
                    }
                    if (!found)
                    {
                        GameObject sphere;
                        MessageData m = new MessageData();
                        string now = DateTime.Now.ToString("H:mm:ss");

                        //m.message = now + " " + "New VM: " + VMname + " Host: " + VMhost + " Cluster: " + cluster + " Info: " + description;
                        //m.status = status;
                        // messageList.Add(m);

                        if (site == "1")
                            sphere = sphere1;
                        else
                            sphere = sphere2;

                        Renderer renderer = sphere.GetComponent<Renderer>();
                        float radius = renderer.bounds.extents.magnitude;
                        Vector3 pos = sphere.transform.position + UnityEngine.Random.insideUnitSphere * radius/2;
                        instanceData.instance = (GameObject)Instantiate(VMprefab, pos, new Quaternion(0, 0, 0, 0));
                        instanceData.instance.transform.SetParent(sphere.transform, true);

                        UpdateVM(instanceData.instance, status);
                        if (site == "1")
                            instanceList1.Add(instanceData);
                        else
                            instanceList2.Add(instanceData);
                    }
                }
                if (command == "CLOUDINFO_1.0")
                {
                    string name = data[1];
                    string role = data[2];
                    string location = data[3];
                    string site = data[4];
                    string status = data[5];
                    string description = data[6];

                    GameObject cluster;
                    if (site == "1")
                    {
                        nameSite1 = name;
                        cluster = cluster1;
                    }
                    else
                    {
                        nameSite2 = name;
                        cluster = cluster2;
                    }

                    GameObject canvas = cluster.transform.Find("Canvas").gameObject;

                    GameObject nameObject = canvas.transform.Find("Name").gameObject;
                    Text nameText = nameObject.GetComponent<Text>();
                    nameText.text = name;

                    GameObject roleObject = canvas.transform.Find("Role").gameObject;
                    Text roleText = roleObject.GetComponent<Text>();
                    roleText.text = role;

                    GameObject locationObject = canvas.transform.Find("Location").gameObject;
                    Text locationText = locationObject.GetComponent<Text>();
                    locationText.text = location;


                }
            }
        }
		if (commands.Count > 0)
		{
            commands.RemoveAll(AllCommands);
        }
    }
    private void UpdateVM(GameObject instance, string status)
    {
        GameObject radiation = instance.transform.Find("Radiation").gameObject;
        ParticleSystem particles = radiation.gameObject.GetComponent<ParticleSystem>();

        if (status == "0")
            particles.startColor = Color.green;
        else if (status == "1")
            particles.startColor = Color.blue;
        else if (status == "2")
            particles.startColor = Color.yellow;
        else if (status == "3")
        {
            Color color = new Color();
            ColorUtility.TryParseHtmlString("#FF8F18FF", out color);
            particles.startColor = color;
        }
        else if (status == "4")
            particles.startColor = Color.red;

    }

    private static bool AllCommands(String s)
    {
        return true;
    }

    private bool CompareVectors(Vector3 a, Vector3 b)
    {
        return Vector3.SqrMagnitude(a - b) < 1.1F;
    }

    private void OnGUI()
    {

        if (mainController != null)
        {
            if (displayVMinfo)
            {
                mainController.vMrect = GUI.Window(1, mainController.vMrect, VMWindow, "", GUIStyle.none);
            }

            if (GUI.Button(new Rect(Screen.width - 70, 0, 70, 30), "VM Info"))
            {
                displayVMinfo = !(displayVMinfo);
            }
        }
    }
    private void VMWindow(int id)
    {
        GUIStyle style = new GUIStyle();
        style.wordWrap = false;
        style.stretchWidth = false;
        style.stretchHeight = false;
        style.alignment = TextAnchor.UpperLeft;
        int numElements = 8;
     //   GUILayout.BeginVertical();
        GUI.contentColor = Color.white;

        // Calc height of window
        if (mainController != null)
        {
            style.fontSize = 14;
            float height = style.lineHeight * numElements;
            mainController.vMrect.height = height;
            mainController.vMrect.y = Screen.height - height;
        }
        int critical = 0, error = 0, warning = 0, info = 0, normal = 0;
        foreach (InstanceVMData e in instanceList1)
        {
            switch (e.status)
            {
                case "0":
                    normal++;
                    break;
                case "1":
                    info++;
                    break;
                case "2":
                    warning++;
                    break;
                case "3":
                    error++;
                    break;
                case "4":
                    critical++;
                    break;
            }
        }
        GUILayout.BeginHorizontal(style);
        style.normal.textColor = Color.white;
        GUILayout.Label("VMs in " + nameSite1 + " : ", style);
        if (normal > 0)
        {
            style.normal.textColor = Color.green;
            GUILayout.Label(normal.ToString(), style);
        }
        if (info > 0)
        {
            style.normal.textColor = Color.blue;
            GUILayout.Label(info.ToString(), style);
        }
        if (warning > 0)
        {
            style.normal.textColor = Color.yellow;
            GUILayout.Label(warning.ToString(), style);
        }
        if (error > 0)
        {
            Color color = new Color();
            ColorUtility.TryParseHtmlString("#FF8F18FF", out color);
            style.normal.textColor = color;

            GUILayout.Label(error.ToString(), style);
        }
        if (critical > 0)
        {
            style.normal.textColor = Color.red;
            GUILayout.Label(critical.ToString(), style);
        }
        GUILayout.EndHorizontal();

        critical = 0; error = 0; warning = 0; info = 0; normal = 0;
        foreach (InstanceVMData e in instanceList2)
        {
            switch (e.status)
            {
                case "0":
                    normal++;
                    break;
                case "1":
                    info++;
                    break;
                case "2":
                    warning++;
                    break;
                case "3":
                    error++;
                    break;
                case "4":
                    critical++;
                    break;
            }
        }
        GUILayout.BeginHorizontal(style);
        style.normal.textColor = Color.white;
        GUILayout.Label("VMs in " + nameSite2 + " : ", style);
        if (normal > 0)
        {
            style.normal.textColor = Color.green;
            GUILayout.Label(normal.ToString(), style);
        }
        if (info > 0)
        {
            style.normal.textColor = Color.blue;
            GUILayout.Label(info.ToString(), style);
        }
        if (warning > 0)
        {
            style.normal.textColor = Color.yellow;
            GUILayout.Label(warning.ToString(), style);
        }
        if (error > 0)
        {
            Color color = new Color();
            ColorUtility.TryParseHtmlString("#FF8F18FF", out color);
            style.normal.textColor = color;

            GUILayout.Label(error.ToString(), style);
        }
        if (critical > 0)
        {
            style.normal.textColor = Color.red;
            GUILayout.Label(critical.ToString(), style);
        }
        GUILayout.EndHorizontal();

        style.normal.textColor = Color.white;

        if (movingTruck1)
            GUILayout.Label("Moving to " + nameSite2 + ": " + truckInstanceList1.Count, style);
        if (movingTruck2)
            GUILayout.Label("Moving to " + nameSite1 + ": " + truckInstanceList2.Count, style);


        float width = 0f;
        messageCountdown -= Time.deltaTime;

        if (messageCountdown <= 0)
        {
            if (messageList.Count > 0)
                messageList.RemoveAt(0);

            if (messageList.Count > 10)
                messageCountdown = 0F;
            else if (messageList.Count > 8)
                messageCountdown = 0.5F;
            else
            {
                messageCountdown = 1F;

                if (movingTruck1 == false && movingTruck2 == false)
                    mainController.DisplayIsUpdating(false);
            }

        }

        for (int i = 0; i < messageList.Count; i++)
        {

            switch (messageList[i].status)
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
            
            GUILayout.Label(messageList[i].message, style);
            Vector2 v = style.CalcSize(new GUIContent(messageList[i].message));
            if (v.x > width)
                width = v.x;

            

        }

        //Save width in rect
        if (mainController != null)
        {
            width = 350;
            mainController.vMrect.width = width;
            mainController.vMrect.x = Screen.width/2 - width/2; 
        }
//        GUILayout.EndVertical();

        GUI.DragWindow(new Rect(0, 0, Screen.width, Screen.height));
    }
}
