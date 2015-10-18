using System;
using System.IO;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Net.Security;
using System.Security.Principal;
using System.Security.Authentication;

public class StateObject
{
    public int bufferSize;
    public NetworkStream stream;
    public byte[] buffer;
    public StringBuilder sb = new StringBuilder();
}


public class MainController : MonoBehaviour
{
    public GUISkin skin;
    public List<string> commands = new List<string>();
    public string message = "";
    public DateTime lastUpdate = new DateTime();
    public string serverName = "127.0.0.1", port = "7777",frequency = "10";
    private Rect windowRect = new Rect(300, 0, 650, 530);
    private bool updating = false;
    IPAddress ipAddress = null;
    Timer updateTimer;
    public DateTime countdown;
    public Rect domainsRect;
    public Rect infoRect;
    public Rect vMrect;
    public Rect sitesRect;
    public Rect sitesRect2;
    private bool updatingDisplay = false;

    private bool displayAbout = false;
    Thread update;


    private static MainController instance;
    public static MainController Instance
    {
        get
        {
            return instance;
        }
    }
    public void DisplayIsUpdating(bool b)
    {
        updatingDisplay = b;
    }
    void Awake()
    {
        DontDestroyOnLoad(transform.gameObject);
        if (instance)
            Destroy(gameObject);
        instance = this;
        domainsRect = new Rect(Screen.width - 250, Screen.height - 200, 250, 500);
        sitesRect = new Rect(0, Screen.height/2 - 200, 250, 500);

        Application.LoadLevel(1);
    }
    void Start()
    {
        DontDestroyOnLoad(gameObject);
    }
    private void OnGUI()
    {
        GUI.skin = skin;

        if (updating == false)
        {
            GUILayout.Label("Server Name");
            serverName = GUILayout.TextField(serverName);
            GUILayout.Label("Port");
            port = GUILayout.TextField(port);
            GUILayout.Label("Frequency");
            frequency = GUILayout.TextField(frequency);

            if (GUILayout.Button("Update"))
            {

                IPAddress[] addresses = Dns.GetHostAddresses(serverName);

                foreach (IPAddress address in addresses)
                {
                    if (IPAddress.TryParse(address.ToString(), out ipAddress) == true)
                        break;
                }

                TimerCallback timerCallback = timerUpdate;
                DateTime now = DateTime.UtcNow;
                countdown = now.AddSeconds(int.Parse(frequency)+1);

                updateTimer = new Timer(new TimerCallback(timerCallback), null, 0, int.Parse(frequency)*1000);
            }
        }
        else
        {
            TimeSpan remainingTime = countdown - DateTime.UtcNow;

            if (remainingTime.Seconds < 0)
            {
                DateTime now = DateTime.UtcNow;
                countdown = now.AddSeconds(int.Parse(frequency)+1);
            }

            if (GUILayout.Button("Stop "+((int)remainingTime.TotalSeconds).ToString()))
            {

                //update.Abort();
                updateTimer.Dispose();
                updating = false;
            }
        }

        if (displayAbout)
        {
            windowRect = GUI.Window(0, windowRect, AboutWindow, "About PSUnity");
        }

        if (GUILayout.Button("About"))
        {
            displayAbout = !(displayAbout);
        }

        if (GUILayout.Button("View"))
        {
            int displayScene = Application.loadedLevel;
            displayScene++;
            if (displayScene >= Application.levelCount)
                displayScene = 1;

            
            Application.LoadLevel(displayScene);
        }
    }

    private static bool AllCommands(String s)
    {
        return true;
    }

    public void timerUpdate(System.Object stateInfo)
    {
        if (updatingDisplay)
            return;

        commands.RemoveAll(AllCommands);

        if (ipAddress != null )
        {
            TcpClient tcpClient = new TcpClient();

            tcpClient.Connect(ipAddress, int.Parse(port));

            StateObject state = new StateObject();
            state.stream = tcpClient.GetStream();

            StreamWriter writer = new StreamWriter(state.stream);
            StreamReader reader = new StreamReader(state.stream);

            writer.WriteLine("GETALL");
            writer.Flush();
            state.bufferSize = tcpClient.ReceiveBufferSize;
            state.buffer = new byte[state.bufferSize];

            Thread.Sleep(300);

            String content = String.Empty;

            while(true)
            {
                content = reader.ReadLine();
                if (content == null)
                    break;

                if (content == "<END>")
                    break;
                commands.Add(content);
            } 
            reader.Close();
            reader.Dispose();
            writer.Dispose();
            state.stream.Close();
            tcpClient.Close();

            lastUpdate = DateTime.Now;
            DateTime now = DateTime.UtcNow;
            countdown = now.AddSeconds(int.Parse(frequency) + 1);

            /*UpdateThread updateThread = new UpdateThread();
            updateThread.port = port;
            updateThread.ipAddress = ipAddress;
            updateThread.mainController = this;


            update = new Thread(new ThreadStart(updateThread.Update));
            update.Start();*/
            updating = true;
        }
    }
    private void AboutWindow(int id)
    {
        GUIStyle style = new GUIStyle();
        style.wordWrap = false;
        style.stretchWidth = false;
        style.stretchHeight = false;
        style.alignment = TextAnchor.UpperLeft;
        string message;
        GUI.contentColor = Color.white;

        GUILayout.BeginVertical(style);
        style.normal.textColor = Color.white;
        GUILayout.Label("PSUnity", style);
        GUILayout.Label("Copyright (c) 2015 Micky Balladelli", style);
        GUILayout.Label("https://balladelli.com", style);
        message = "Made with Unity and powered by PowerShell.";
        GUILayout.Label(message, style);
        message = "";
        GUILayout.Label(message, style);
        GUILayout.Label("Standard MIT OpenSource License applies (http://opensource.org/licenses/MIT)", style);
        message = "";
        GUILayout.Label(message, style);

        message = "Permission is hereby granted, free of charge, to any person obtaining a copy";
        GUILayout.Label(message, style);
        message = "of this software and associated documentation files(the \"Software\"), to deal";
        GUILayout.Label(message, style);
        message = "in the Software without restriction, including without limitation the rights";
        GUILayout.Label(message, style);
        message = "to use, copy, modify, merge, publish, distribute, sublicense, and/ or sell";
        GUILayout.Label(message, style);
        message = "copies of the Software, and to permit persons to whom the Software is";
        GUILayout.Label(message, style);
        message = "furnished to do so, subject to the following conditions:";
        GUILayout.Label(message, style);

        message = "The above copyright notice and this permission notice shall be included in";
        GUILayout.Label(message, style);
        message = "all copies or substantial portions of the Software.";
        GUILayout.Label(message, style);

        message = "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR";
        GUILayout.Label(message, style);
        message = "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,";
        GUILayout.Label(message, style);
        message = "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE";
        GUILayout.Label(message, style);
        message = "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER";
        GUILayout.Label(message, style);
        message = "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,";
        GUILayout.Label(message, style);
        message = "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN";
        GUILayout.Label(message, style);
        message = "THE SOFTWARE.";
        GUILayout.Label(message, style);
        message = "";
        GUILayout.Label(message, style);
        message = "Individual license restrictions for the components below may apply:";
        GUILayout.Label(message, style);
        message = "";
        GUILayout.Label(message, style);
        message = "Server rack by flevasgr (Meletis): http://tf3dm.com/3d-model/server-rack-76768.html";
        GUILayout.Label(message, style);
        message = "Super computer by tharidu (TDG): http://tf3dm.com/3d-model/super-camputer-ibm-75503.html";
        GUILayout.Label(message, style);
        message = "Personal computer by rsmedia: http://tf3dm.com/3d-model/pc-18210.html";
        GUILayout.Label(message, style);
        message = "R2D2 by asbrock (Degardin Arnaud): http://tf3dm.com/3d-model/r2d2-by-abrock-46556.html";
        GUILayout.Label(message, style);
        message = "Earth by Glenn Campbell: http://tf3dm.com/3d-model/world-globe-94593.html";
        GUILayout.Label(message, style);
        message = "All satellites by NASA: http://nasa3d.arc.nasa.gov/models";
        GUILayout.Label(message, style);
        message = "Galaxy code modified from: https://github.com/beltoforion/Galaxy-Renderer";
        GUILayout.Label(message, style);
        message = "Trucks by MarcosStyLL: http://tf3dm.com/3d-model/dump-truck-79822.html";
        GUILayout.Label(message, style);
        
        GUILayout.EndVertical();

        GUI.DragWindow(new Rect(0, 0, Screen.width, Screen.height));
    }
}

class UpdateThread
{
    public MainController mainController;
    public string port;
    public IPAddress ipAddress;
    TcpClient tcpClient = new TcpClient();

    public void Update()
    {

        tcpClient = new TcpClient();

        tcpClient.Connect(ipAddress, int.Parse(port));

        StateObject state = new StateObject();
        state.stream = tcpClient.GetStream();

        // NegotiateStream is not implemented in Unity :(
        //try
        //{
        /*            NegotiateStream nStream = new NegotiateStream(state.stream);

                    nStream.AuthenticateAsClient((NetworkCredential)CredentialCache.DefaultCredentials, "PSUNITY\\Unity",
                                                 ProtectionLevel.EncryptAndSign,
                                                 TokenImpersonationLevel.Identification);
        */
        //}
        //catch (Exception ex)
        //{
        //    Debug.LogFormat("{0}", ex.Message);
        //}

        StreamWriter writer = new StreamWriter(state.stream);
        StreamReader reader = new StreamReader(state.stream);

        writer.WriteLine("GETALL");
        writer.Flush();
        state.bufferSize = tcpClient.ReceiveBufferSize;
        state.buffer = new byte[state.bufferSize];



        Thread.Sleep(300);

        String content = String.Empty;

        do
        {
             
            try
            {
                content = reader.ReadLine();
                if (content == null)
                    break;

                //string[] data = (content).Split('\n');

                mainController.commands.Add(content);

            }
            catch (Exception e)
            {
                Debug.Log("*********************************************");
                Debug.Log(e);
                break;
            }


        } while (content != null);

        reader.Dispose();
        writer.Dispose();
        state.stream.Close();
        tcpClient.Close();

        mainController.lastUpdate = DateTime.Now;
        DateTime now = DateTime.UtcNow;
        mainController.countdown = now.AddSeconds(int.Parse(mainController.frequency) + 1);
    }
}
