using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class EventController : MonoBehaviour {
	private static List<City> cities = new List<City>();
	private List<string> commands = new List<string>();     // Local copy of commands received, this list is emptied once treated.
	private GameObject[] citiesObjects;
	public float rotationSpeed = 8.0F;
	public float angle = 90F;
	MainController mainController;
	private DateTime lastUpdate = new DateTime();
	private static List<Event> events = new List<Event>();
	private static List<Event> lastEvents = new List<Event>();
	private static List<ByServer> byserver = new List<ByServer>();
	private static List<ByAccount> byaccount = new List<ByAccount>();
	private bool displayEvents = true;

	// Use this for initialization
	void Start () {
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
	void Update()
	{
		if (mainController != null)
		{

			// Check if new commands arrived 
			if (lastUpdate != mainController.lastUpdate)
			{
				commands.Clear();
				try
				{
					foreach (string c in mainController.commands)
					{
						commands.Add(c);
					}
					lastUpdate = mainController.lastUpdate;
				}
				catch
				{}
			}
		}
		// Deal with received commands
		foreach (string elem in commands)
		{
			try
			{
			string[] data = elem.Split(';');
			
				if (data.Length > 1)
				{
					string command = data[0];
					if (command == "EVENT_1.0")
					{
						string name = data[1];
						string status = data[2];
						string description = data[3];

						Event e = new Event();
						e.name = name;
						e.status = status;
						e.description = description;

						events.Add(e);
					}
					if (command == "EVENT_BYSERVER1.0")
					{
						ByServer s = new ByServer();
						s.server = data[1];
						s.number = data[2];

						byserver.Add(s);
						if (byserver.Count > 10)
							byserver.RemoveAt(0);
					}
					if (command == "EVENT_BYACCOUNT1.0")
					{
						ByAccount s = new ByAccount();
						s.account = data[1];
						s.number = data[2];

						byaccount.Add(s);
						if (byaccount.Count > 10)
							byaccount.RemoveAt(0);
					}
				}
			}
			catch{}

		}

		if (commands.Count > 0)
		{
			commands.Clear();
		}
	}

	void LateUpdate () 
	{
		if (events.Count > 0)
		{
			Event e = events[0];

			foreach (GameObject cityObject in citiesObjects)
			{
				if (cityObject.name == e.name)
				{
					foreach (GameObject city in citiesObjects)
					{
						city.SetActive(false);
					}
					cityObject.SetActive(true);
					Renderer renderer = cityObject.GetComponent<Renderer>();
					GameObject radiation = cityObject.transform.Find("Radiation").gameObject;
					ParticleSystem radRen = radiation.gameObject.GetComponent<ParticleSystem>();

					switch (e.status)
					{
					default:
						renderer.material.color = Color.cyan;
						radRen.startColor = Color.cyan;
						break;
					case "0":
						renderer.material.color = Color.green;
						radRen.startColor = Color.green;
						break;
					case "1":
						renderer.material.color = Color.cyan;
						radRen.startColor = Color.cyan;
						break;
					case "2":
						renderer.material.color = Color.yellow;
						radRen.startColor = Color.yellow;
						break;
					case "3":
						Color color = new Color();
						ColorUtility.TryParseHtmlString("#FF8F18FF", out color);
						renderer.material.color = color;
						radRen.startColor = color;
						break;
					case "4":
						renderer.material.color = Color.red;
						radRen.startColor = Color.red;
						break;

					}
					radiation.SetActive(true);

					//Vector3 direction = (cityObject.transform.position - transform.position).normalized;

					// convert the hit point into local coordinates
					Vector3 localPos = transform.InverseTransformPoint(cityObject.transform.position);
					Vector3 longDir = localPos;

					// zero y to project the vector to the x-z-plane (i.e. the equator)
					longDir.y = 0;

					//calculate the angle between our reference and the city
					float longitude = Vector3.Angle(-Vector3.forward,longDir);

					// if our point is on the western hemisphere negate the angle
					if (longDir.x<0)
						longitude = -longitude;

					// calculate the latitude in degree
					float latitude = Mathf.Asin(localPos.normalized.y)*Mathf.Rad2Deg;

					// build our rotation from the two angles
					var rotation = Quaternion.AngleAxis(-latitude,Vector3.right)*Quaternion.AngleAxis(longitude,Vector3.up);

					// Rotate on y only so it feels like the poles don't move
					rotation.x = 0.0F;
					rotation.z = 0.0F;

					// Slerp towards the desired new rotation
					transform.localRotation = Quaternion.Slerp (transform.localRotation, rotation, rotationSpeed * Time.deltaTime);
					var r1 = Math.Round(transform.localRotation.eulerAngles.y, 2);
					var r2 = Math.Round(rotation.eulerAngles.y, 2);

					// Are done?
					if (r1 == r2 )
					{
						lastEvents.Add(e);
						if (lastEvents.Count > 10)
							lastEvents.RemoveAt(0);
						mainController.singleEventRect.x = 0; // Reset for next event window
						events.RemoveAt(0);

					}

					break;
				}
			}
		}
	}

	private void OnGUI()
	{

		if (mainController != null)
		{
			if (displayEvents)
			{
				mainController.eventsRect = GUI.Window(0, mainController.eventsRect, EventsWindow, "Last 10 events");
			}
			if (events.Count > 0)
			{
				mainController.singleEventRect = GUI.Window(1, mainController.singleEventRect, SingleEventWindow, "Event");
			}
			if (byserver.Count > 0)
			{
				mainController.byServerRect = GUI.Window(2, mainController.byServerRect, ByServerWindow, "Events by Server");
			}
			if (byaccount.Count > 0)
			{
				mainController.byAccountRect = GUI.Window(3, mainController.byAccountRect, ByAccountWindow, "Events by Account");
			}

			if (GUI.Button(new Rect(Screen.width - 70, 0, 70, 30), "Events"))
			{
				displayEvents = !(displayEvents);
			}
		}
	}
	private void EventsWindow(int id)
	{
		if (lastEvents.Count > 0)
		{
			GUIStyle style = new GUIStyle();
			style.wordWrap = false;
			style.alignment = TextAnchor.LowerLeft;

			GUILayout.BeginVertical();
			GUI.contentColor = Color.white;
			style.fontSize = 14;
			float height = style.lineHeight * (lastEvents.Count + 2);

			if (mainController != null)
			{
				mainController.eventsRect.y = Screen.height - height;
			}
			if (mainController != null)
			{
				mainController.eventsRect.height = height;
			}
			float width = 0f;

			foreach (Event e in lastEvents)
			{
				switch (e.status)
				{
				default:
					style.normal.textColor = Color.cyan;
					break;
				case "0":
					style.normal.textColor = Color.green;
					break;
				case "1":
					style.normal.textColor = Color.cyan;
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
				GUILayout.Label(e.name+ ": "+e.description, style);
				Vector2 v = style.CalcSize(new GUIContent(e.name+ ": "+e.description + " "));
				v.x += 30;
				if (v.x > width)
					width = v.x;

			}
			//Save width in rect
			if (mainController != null )
			{
				mainController.eventsRect.width = width;
				mainController.eventsRect.x = Screen.width - width - 2; // don't forget a little margin
			}
			GUILayout.EndVertical();

			GUI.DragWindow(new Rect(0, 0, Screen.width, Screen.height));
		}
	}

	private void SingleEventWindow(int id)
	{
		GUIStyle style = new GUIStyle();
		style.wordWrap = true;
		style.alignment = TextAnchor.UpperLeft;
		style.fontSize = 16;

		GUILayout.BeginVertical();
		GUILayout.ExpandHeight(true);
		GUI.contentColor = Color.white;

		// Calc height of window
		if (mainController != null && mainController.singleEventRect.x == 0)
		{
			style.fontSize = 10;
			float height = style.lineHeight * 3;

			mainController.singleEventRect.y = UnityEngine.Random.Range(100,400);
			mainController.singleEventRect.x = UnityEngine.Random.Range(100,400);

			mainController.singleEventRect.height = height;
			mainController.singleEventRect.width = 300;

		}

		Event e = events[0];
		foreach (GameObject cityObject in citiesObjects)
		{
			if (cityObject.name == e.name)
			{

				switch (e.status)
				{
					default:
						style.normal.textColor = Color.cyan;
						break;
					case "0":
						style.normal.textColor = Color.green;
						break;
					case "1":
						style.normal.textColor = Color.cyan;
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
				GUILayout.Label(e.name+ ": "+e.description, style);
				Vector2 v = style.CalcSize(new GUIContent(e.name+ ": "+e.description + " "));
				mainController.singleEventRect.height = style.CalcHeight(new GUIContent(e.name+ ": "+e.description + " "), 300)+(style.lineHeight*2);

			}
		}
		GUILayout.EndVertical();

		GUI.DragWindow(new Rect(0, 0, Screen.width, Screen.height));
	}

	private void ByServerWindow(int id)
	{
		GUIStyle style = new GUIStyle();
		style.wordWrap = false;
		style.alignment = TextAnchor.LowerLeft;
		style.normal.textColor = Color.white;
		style.fontSize = 16;
		float height = style.lineHeight * (byserver.Count + 1);

		GUILayout.BeginVertical();
		GUI.contentColor = Color.white;

		// Calc height of window
		if (mainController != null && mainController.byServerRect.y == 0)
		{			
			mainController.byServerRect.y = Screen.height - height;
		}
		if (mainController != null)
		{
			mainController.byServerRect.height = height;
		}
		float width = 0f;

		foreach (ByServer e in byserver)
		{
			GUILayout.Label(e.server+ ": \t"+e.number, style);
			Vector2 v = style.CalcSize(new GUIContent(e.server+ ": \t"+e.number + " "));
			v.x += 40;
			if (v.x > width)
				width = v.x;

		}
		if (mainController != null)
		{
			mainController.byServerRect.width = width;
		}
		if (mainController != null && mainController.byServerRect.x == 0 )
		{
			mainController.byServerRect.x = 1;
		}
		GUILayout.EndVertical();

		GUI.DragWindow(new Rect(0, 0, Screen.width, Screen.height));
	}
	private void ByAccountWindow(int id)
	{
		GUIStyle style = new GUIStyle();
		style.wordWrap = false;
		style.alignment = TextAnchor.LowerLeft;
		style.normal.textColor = Color.white;
		style.fontSize = 16;
		float height = style.lineHeight * (byaccount.Count + 1);

		GUILayout.BeginVertical();
		GUI.contentColor = Color.white;

		// Calc height of window
		if (mainController != null)
		{
			mainController.byAccountRect.height = height;
		}
		if (mainController != null && mainController.byAccountRect.y == 0)
		{
			mainController.byAccountRect.y = Screen.height - height;
		}
		float width = 0f;

		foreach (ByAccount e in byaccount)
		{
			GUILayout.Label(e.account+ ": \t"+e.number, style);
			Vector2 v = style.CalcSize(new GUIContent(e.account+ ": \t"+e.number + " "));
			v.x += 40;
			if (v.x > width)
				width = v.x;

		}
		//Save width in rect
		if (mainController != null)
		{
			mainController.byAccountRect.width = width;
		}
		if (mainController != null && mainController.byAccountRect.x == 0)
		{
			mainController.byAccountRect.x = Screen.width/4; 
		}
		GUILayout.EndVertical();

		GUI.DragWindow(new Rect(0, 0, Screen.width, Screen.height));
	}

}
	

public class Event
{
	public string name;  // city name
	public string status;
	public string description;
}

public class ByServer
{
	public string server;
	public string number;
}

public class ByAccount
{
	public string account;
	public string number;
}

