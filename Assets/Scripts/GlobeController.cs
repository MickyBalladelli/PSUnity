using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;


public class GlobeController : MonoBehaviour
{
    private DateTime lastUpdate = new DateTime();
    private List<string> commands = new List<string>();     // Local copy of commands received, this list is emptied once treated.
    private GameObject[] citiesObjects;
    private bool displayDomains = true;
    private bool displaySites = true;
    MainController mainController;
    private static List<Domain> domains = new List<Domain>();
    private static List<City> cities = new List<City>();

    private void Start()
    {
        mainController = MainController.Instance;
        citiesObjects = GameObject.FindGameObjectsWithTag("City");

        foreach (GameObject city in citiesObjects)
        {
            city.SetActive(false);
        }
        if (mainController != null)
        {
            mainController.DisplayIsUpdating(false);

            foreach (string c in mainController.commands)
            {
                commands.Add(c);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (mainController != null)
        {

            // Check if new commands arrived 
            if (lastUpdate != mainController.lastUpdate)
            {
                commands.RemoveAll(AllCommands);

                foreach (string c in mainController.commands)
                {
                    commands.Add(c);
                }
                lastUpdate = mainController.lastUpdate;
            }
        }
        // Deal with received commands
        foreach (string elem in commands)
        {
            string[] data = elem.Split(';');

            if (data.Length > 1)
            {
                string command = data[0];
                if (command == "DOMAIN_1.0")
                {
                    string name = data[1];
                    string status = data[2];
                    string description = data[3];

                    Domain domain = new Domain();
                    domain.name = name;
                    domain.status = status;
                    domain.description = description;

                    bool found = false;

                    foreach (Domain d in domains)
                    {
                        if (d.name == name)
                        {
                            domains.Remove(d);
                            domains.Add(domain);
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        domains.Add(domain);
                    }
                }

                if (command == "CITY_1.0")
                {
                    string name = data[1];
                    string status = data[2];
                    string altname = data[3];
                    string description = data[4];

                    City city = new City();
                    city.name = name;
                    city.altname = altname;
                    city.status = status;
                    city.description = description;

                    bool found = false;

                    foreach (City c in cities)
                    {
                        if (c.name == name)
                        {
                            cities.Remove(c);
                            cities.Add(city);
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        cities.Add(city);
                    }


                    foreach (GameObject cityObject in citiesObjects)
                    {
                        if (cityObject.name == name || cityObject.name == altname)
                        {
                            cityObject.SetActive(true);
                            Renderer renderer = cityObject.GetComponent<Renderer>();
                            GameObject radiation = cityObject.transform.Find("Radiation").gameObject;
                            ParticleSystem radRen = radiation.gameObject.GetComponent<ParticleSystem>();
                            if (altname != "")
                            {

                                Transform[] allChildren = cityObject.GetComponentsInChildren<Transform>();
                                foreach (Transform child in allChildren)
                                {
                                    if (child.name == "Name")
                                    {
                                        Text nameText = child.GetComponent<Text>();
                                        nameText.text = altname;
                                        break;
                                    }
                                }
                            }

                            if (status.Contains("4"))
                            {
                                renderer.material.color = Color.red;
                                radRen.startColor = Color.red;
                                radiation.SetActive(true);
                            }
                            else if (status.Contains("3"))
                            {
                                Color color = new Color();
                                ColorUtility.TryParseHtmlString("#FF8F18FF", out color);
                                renderer.material.color = color;
                                radRen.startColor = color;

                                radiation.SetActive(true);
                            }
                            else if (status.Contains("2"))
                            {
                                renderer.material.color = Color.yellow;
                                radRen.startColor = Color.yellow;

                                radiation.SetActive(true);
                            }
                            else if (status.Contains("1"))
                            {
                                renderer.material.color = Color.blue;
                                Color color = new Color();
                                ColorUtility.TryParseHtmlString("#79D8FFD1", out color);
                                radRen.startColor = color;
                                radiation.SetActive(true);
                            }
                            else if (status.Contains("0"))
                            {
                                renderer.material.color = Color.green;
                                radRen.startColor = Color.green;
                                radiation.SetActive(true);
                            }
                        }
                    }
                }
            }
        }
        if (commands.Count > 0)
        {
            commands.RemoveAll(AllCommands);
        }
    }
    private static bool AllCommands(String s)
    {
        return true;
    }

    private void OnGUI()
    {
        if (mainController != null)
        {
            if (displayDomains)
            {
                mainController.domainsRect = GUI.Window(1, mainController.domainsRect, DomainWindow, "", GUIStyle.none);
            }

            if (GUI.Button(new Rect(Screen.width - 70, 0, 70, 30), "Domains"))
            {
                displayDomains = !(displayDomains);
            }

            if (displaySites)
            {
                mainController.sitesRect = GUI.Window(2, mainController.sitesRect, SitesWindow, "", GUIStyle.none);
                //mainController.sitesRect2 = GUI.Window(3, mainController.sitesRect2, SitesWindow2, "", GUIStyle.none);
            }

            if (GUI.Button(new Rect(Screen.width - 70, 30, 70, 30), "Sites"))
            {
                displaySites = !(displaySites);
            }

        }
    }
    private void DomainWindow(int id)
    {
        GUIStyle style = new GUIStyle();
        style.wordWrap = false;
        style.alignment = TextAnchor.LowerRight;
        //Font[] fonts = Resources.FindObjectsOfTypeAll(typeof(Font)) as Font[];
        //style.font = fonts[2];

        GUILayout.BeginVertical();
        GUI.contentColor = Color.white;

        // Calc height of window
        if (mainController != null)
        {
            style.fontSize = 14;
            float height = style.lineHeight * domains.Count;
            style.fontSize = 30;
            height += style.lineHeight * domains.Count;
            mainController.domainsRect.height = height;
            mainController.domainsRect.y = Screen.height - height;
        }
        float width = 0f;

        foreach (Domain domain in domains)
        {
            switch (domain.status)
            {
                case "0":
                    style.normal.textColor = Color.green;
                    break;
                case "1":
                    style.normal.textColor = Color.blue;
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
            style.fontSize = 30;
            GUILayout.Label(domain.name, style);
            Vector2 v = style.CalcSize(new GUIContent(domain.name));
            if (v.x > width)
                width = v.x;
            style.fontSize = 14;
            GUILayout.Label(domain.description, style);
            v = style.CalcSize(new GUIContent(domain.description));
            if (v.x > width)
                width = v.x;

        }
        //Save width in rect
        if (mainController != null)
        {
            mainController.domainsRect.width = width;
            mainController.domainsRect.x = Screen.width - width - 2; // don't forget a little margin
        }
        GUILayout.EndVertical();

        GUI.DragWindow(new Rect(0, 0, Screen.width, Screen.height));
    }

    private Rect DrawWindow(string status, GUIStyle style, float width)
    {
        int sizeName = 18;
        int sizeDesc = 12;
        style.wordWrap = false;
        style.alignment = TextAnchor.LowerLeft;
        GUI.contentColor = Color.white;


        Rect rect = new Rect();

        float height = 0f;

        foreach (City city in cities)
        {
            if (city.status == status)
            {
                string name;
                if (city.altname != "")
                    name = city.altname;
                else
                    name = city.name;

                switch (city.status)
                {
                    default:
                    case "0":
                        style.normal.textColor = Color.green;
                        break;
                    case "1":
                        style.normal.textColor = Color.blue;
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
                style.fontSize = sizeName;
                height += style.lineHeight;
                GUILayout.Label(name, style);
                Vector2 v = style.CalcSize(new GUIContent(name));
                if (v.x > width)
                    width = v.x;

                if (city.description != "\r" && city.status != "0")
                {
                    style.fontSize = sizeDesc;
                    GUILayout.Label(city.description, style);
                    v = style.CalcSize(new GUIContent(city.description));
                    if (v.x > width)
                        width = v.x;
                    style.fontSize = sizeDesc;
                    height += style.lineHeight;
                }
            }
        }
        rect.width = width;
        rect.x = 200;
        float y = height;
        if (y < 0)
            y = 0;

        rect.y = y;
        rect.height = height;

        return rect;
    }
    private void SitesWindow(int id)
    {
        GUILayout.BeginVertical();
        GUIStyle style = new GUIStyle();

        GUILayout.BeginVertical();
        Rect rect;
        float width = 0f, height;

        rect = DrawWindow("3", style, width);
        width = rect.width;
        height = rect.height;
        
        rect = DrawWindow("4", style, width);
        width = rect.width;
        height += rect.height;


        //Save size and position of window rect
        if (mainController != null)
        {
            mainController.sitesRect.width = width;
            mainController.sitesRect.x = 0;
            //y = Screen.height - y;
            //if (y < 0)
            //    y = 0;

            mainController.sitesRect.y = Screen.height - height;
            mainController.sitesRect.height = height;
        }
        GUILayout.EndVertical();
        //GUI.DragWindow(new Rect(0, 0, Screen.width, Screen.height));
    }
    private void SitesWindow2(int id)
    {

        GUILayout.BeginVertical();
        GUIStyle style = new GUIStyle();
        GUILayout.BeginVertical();

        Rect rect;
        float width = 0f, height;
        rect = DrawWindow("2", style, width);
        width = rect.width; height = rect.height;
        
        rect = DrawWindow("1", style, width);
        width = rect.width;
        height += rect.height;

        rect = DrawWindow("0", style, width);
        width = rect.width;
        height += rect.height;


        //Save size and position of window rect
        if (mainController != null)
        {
            mainController.sitesRect2.width = width;
            mainController.sitesRect2.x = 200;

            mainController.sitesRect2.y = Screen.height - height;
            mainController.sitesRect2.height = height;
        }
        GUILayout.EndVertical();
    }

}

public class Domain
{
    public string name;
    public string status;
    public string description;
}
public class City
{
    public string name;
    public string altname;
    public string status;
    public string description;
}
