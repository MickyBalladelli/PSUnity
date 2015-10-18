using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Principal;
using System.Security.Authentication;

namespace PSUnityServer
{
    class Program
    {
        static int Main(string[] args)
        {
            string param;
            if (args.Length == 0)
            {
                param = "--all";
            }
            else
            {
                param = args[0];
            }
            const string AllPolicy =

@"<?xml version='1.0'?>
<cross-domain-policy>
        <allow-access-from domain=""*"" to-ports=""*"" />
</cross-domain-policy>";

            const string LocalPolicy =

        @"<?xml version='1.0'?>
<cross-domain-policy>
	<allow-access-from domain=""*"" to-ports=""4500-4550"" />
</cross-domain-policy>";


            string policy = null;
            switch (param)
            {
                case "-h":
                case "--h":
                    Console.WriteLine("PSUnityServer.exe [--all | --range | --file policy]");
                    Console.WriteLine("\t--all	Allow access on every port)");
                    Console.WriteLine("\t--range	Allow access on portrange 4500-4550)");
                    Console.WriteLine("See http://docs.unity3d.com/Manual/SecuritySandbox.html for more information about Unity sandbox security.");
                    return 1;

                case "--all":
                    policy = AllPolicy;
                    break;
                case "--local":
                    policy = LocalPolicy;
                    break;
                case "--file":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Missing policy file name after '--file'.");
                        return 2;
                    }
                    string filename = args[1];
                    if (!File.Exists(filename))
                    {
                        Console.WriteLine("Could not find policy file '{0}'.", filename);
                        return 3;
                    }
                    using (StreamReader sr = new StreamReader(filename))
                    {
                        policy = sr.ReadToEnd();
                    }
                    break;
                default:
                    Console.WriteLine("Unknown option '{0}'.", args[0]);
                    return 4;
            }

            SocketPolicyServer server = new SocketPolicyServer(policy);
            int result = server.Start();
            if (result != 0)
                return result;

            // Start the PSUnity server
            threadedServer psUnityServer = new threadedServer();

            Console.WriteLine("Hit Return to stop the server.");
            Console.ReadLine();
            server.Stop();
            return 0;
        }
    }

    class threadedServer
    {
        private TcpListener client;

        public threadedServer()
        {
            client = new TcpListener(IPAddress.Any, 7777);
            client.Start();
            Console.WriteLine("PSUnity Server by Micky Balladelli");

            Console.WriteLine("Waiting for clients...");

            while (true)
            {
                while (!client.Pending())
                {
                    Thread.Sleep(1000);
                }
                ConnectionThread newconnection = new ConnectionThread();
                newconnection.threadListner = this.client;
                Thread newthread = new Thread(new ThreadStart(newconnection.HandleConnection));
                newthread.Start();
            }
        }
    }

    class ConnectionThread
    {
        public TcpListener threadListner;
        private static int connections = 0;

        private static List<ServerElement> servers = new List<ServerElement>();
        private static List<SiteElement> sites = new List<SiteElement>();
        private static List<CityElement> cities = new List<CityElement>();
        private static List<GalaxyElement> galaxies = new List<GalaxyElement>();
        private static List<DomainElement> domains = new List<DomainElement>();
        private static List<CloudElement> VMs = new List<CloudElement>();
        private static List<CloudSiteElement> cloudSites = new List<CloudSiteElement>();



        public void HandleConnection()
        {
            // int recv;
            TcpClient client = threadListner.AcceptTcpClient();

            byte[] data = new byte[client.ReceiveBufferSize];
            NetworkStream stream = client.GetStream();

            try
            {
                /* NegotiateStream nStream = new NegotiateStream(stream);

                nStream.AuthenticateAsServer((NetworkCredential)CredentialCache.DefaultCredentials,
                                             ProtectionLevel.EncryptAndSign,
                                             TokenImpersonationLevel.Identification);
                                             */
            }
            catch (IOException e)
            {
                Console.WriteLine(e);
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine(e);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }



            StreamReader reader = new StreamReader(stream);
            StreamWriter writer = new StreamWriter(stream);
            writer.AutoFlush = true;
            writer.NewLine = "\n";
            connections++;

            Console.WriteLine("New client accepted. Connected clients: {0}", connections);

            string line;
            string [] command;
            do
            {
                try
                {
                    line = reader.ReadLine();

                    if (line != null)
                    {
                        command = line.Split(';');

                        switch (command[0])
                        {
                            case "GETALL":
                                {
                                    Console.WriteLine("Received {0} command", command[0]);

                                    foreach (ServerElement s in servers)
                                    {
                                        writer.WriteLine(s.command);
                                    }
                                    
                                    Console.WriteLine("Sent {0} servers", servers.Count);

                                    foreach (SiteElement s in sites)
                                    {
                                        writer.WriteLine(s.command);
                                    }
                                    
                                    Console.WriteLine("Sent {0} sites", sites.Count);

                                    foreach (CityElement c in cities)
                                    {
                                        writer.WriteLine(c.command);
                                    }
                                    
                                    foreach (GalaxyElement g in galaxies)
                                    {
                                        writer.WriteLine(g.command);
                                    }
                                    
                                    Console.WriteLine("Sent {0} galaxies", galaxies.Count);
                                    foreach (DomainElement d in domains)
                                    {
                                        writer.WriteLine(d.command);
                                    }
                                    
                                    Console.WriteLine("Sent {0} domains", domains.Count);
                                    foreach (CloudElement v in VMs)
                                    {
                                        writer.WriteLine(v.command);
                                    }
                                    

                                    Console.WriteLine("Sent {0} VMs", VMs.Count);
                                    foreach (CloudSiteElement c in cloudSites)
                                    {
                                        writer.WriteLine(c.command);
                                    }
                                    Console.WriteLine("Sent {0} Cloud sites", cloudSites.Count);
                                    writer.WriteLine("<END>");

                                    break;
                                }
                            case "SERVER_1.0":
                                {
                                    Console.WriteLine("Received {0} command", command[0]);
                                    ServerElement server = new ServerElement();
                                    server.data.x = command[1];
                                    server.data.y = command[2];
                                    server.data.rotation = command[3];
                                    server.data.name = command[4];
                                    server.data.role = command[5];
                                    server.data.location = command[6];
                                    server.data.status = command[7];
                                    server.data.type = command[8];
                                    server.data.description = command[9];
                                    server.command = line;
                                    bool found = false;

                                    foreach (ServerElement s in servers)
                                    {
                                        if (s.data.name == server.data.name)
                                        {
                                            servers.Remove(s);
                                            servers.Add(server);
                                            found = true;
                                            break;
                                        }
                                    }
                                    if (!found)
                                    {
                                        servers.Add(server);
                                    }
                                    break;
                                }
                            case "SITE_1.0":
                                {
                                    Console.WriteLine("Received {0} command", command[0]);
                                    SiteElement site = new SiteElement();
                                    site.data.x = command[1];
                                    site.data.y = command[2];
                                    site.data.width = command[3];
                                    site.data.height = command[4];
                                    site.data.rotation = command[5];
                                    site.data.name = command[6];
                                    site.data.location = command[7];
                                    site.data.description = command[8];
                                    site.command = line;
                                    bool found = false;

                                    foreach (SiteElement s in sites)
                                    {
                                        if (s.data.name == site.data.name)
                                        {
                                            sites.Remove(s);
                                            sites.Add(site);
                                            found = true;
                                            break;
                                        }
                                    }
                                    if (!found)
                                    {
                                        sites.Add(site);
                                    }
                                    break;
                                }

                            case "CITY_1.0":
                                {
                                    Console.WriteLine("Received {0} command", command[0]);
                                    CityElement city = new CityElement();
                                    city.data.name = command[1];
                                    city.data.status = command[2];
                                    city.data.altname = command[3];
                                    city.data.description = command[4];
                                    city.command = line;
                                    bool found = false;

                                    foreach (CityElement c in cities)
                                    {
                                        if (c.data.name == city.data.name)
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
                                    break;
                                }

                            case "DOMAIN_1.0":
                                {
                                    Console.WriteLine("Received {0} command", command[0]);
                                    DomainElement domain = new DomainElement();
                                    domain.data.name = command[1];
                                    domain.data.status = command[2];
                                    domain.data.description = command[3];
                                    domain.command = line;
                                    bool found = false;

                                    foreach (DomainElement d in domains)
                                    {
                                        if (d.data.name == domain.data.name)
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
                                    break;
                                }
                            case "CLOUD_1.0":
                                {
                                    Console.WriteLine("Received {0} command", command[0]);
                                    CloudElement vm = new CloudElement();
                                    vm.data.name = command[1];
                                    vm.data.host = command[2];
                                    vm.data.cluster = command[3];
                                    vm.data.site = command[4];
                                    vm.data.status = command[5];
                                    vm.data.description = command[6];
                                    vm.command = line;
                                    bool found = false;

                                    foreach (CloudElement v in VMs)
                                    {
                                        if (v.data.name == vm.data.name)
                                        {
                                            VMs.Remove(v);
                                            VMs.Add(vm);
                                            found = true;
                                            break;
                                        }
                                    }
                                    if (!found)
                                    {
                                        VMs.Add(vm);
                                    }
                                    break;
                                }
                            case "CLOUDINFO_1.0":
                                {
                                    Console.WriteLine("Received {0} command", command[0]);
                                    CloudSiteElement site = new CloudSiteElement();
                                    site.data.name = command[1];
                                    site.data.role = command[2];
                                    site.data.location = command[3];
                                    site.data.site = command[4];
                                    site.data.status = command[5];
                                    site.data.description = command[6];
                                    site.command = line;
                                    bool found = false;

                                    foreach (CloudSiteElement c in cloudSites)
                                    {
                                        if (c.data.site == site.data.site)
                                        {
                                            cloudSites.Remove(c);
                                            cloudSites.Add(site);
                                            found = true;
                                            break;
                                        }
                                    }
                                    if (!found)
                                    {
                                        cloudSites.Add(site);
                                    }
                                    break;
                                }

                            case "GALAXY_1.0":
                                {
                                    Console.WriteLine("Received {0} command", command[0]);

                                    GalaxyElement galaxy = new GalaxyElement();
                                    galaxy.data.name = command[1];
                                    galaxy.data.stars = command[2];
                                    galaxy.data.dust = command[3];
                                    galaxy.data.description = command[4];
                                    galaxy.data.galaxyRadius = command[5];
                                    galaxy.data.coreRadius = command[6];
                                    galaxy.data.angularOffset = command[7];
                                    galaxy.data.coreExcentricity = command[8];
                                    galaxy.data.edgeExcentricity = command[9];

                                    galaxy.command = line;
                                    bool found = false;

                                    foreach (GalaxyElement g in galaxies)
                                    {
                                        if (g.data.name == galaxy.data.name)
                                        {
                                            galaxies.Remove(g);
                                            galaxies.Add(galaxy);
                                            found = true;
                                            break;
                                        }
                                    }
                                    if (!found)
                                    {
                                        galaxies.Add(galaxy);
                                    }
                                    break;
                                }


                            default:
                                break;
                        }
                    }
                }
                catch (SocketException e)
                {
                    Console.WriteLine(e);
                    break;
                }
                catch (IOException e)
                {
                    Console.WriteLine(e);
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    break;
                }

            } while (line != null);

            Thread.Sleep(300);

            reader.Dispose();
            writer.Dispose();
            stream.Close();
            client.Close();

            if (connections > 0)
                connections--;
            Console.WriteLine("Client disconnected. Connected clients: {0}", connections);
        }
    }

    public class ServerElement
    {
        public Server data = new Server();
        public string command;
        
    }
    public class Server
    {
        public string x;
        public string y;
        public string rotation;
        public string name;
        public string role;
        public string location;
        public string status;
        public string type;
        public string description;
    }
    public class SiteElement
    {
        public Site data = new Site();
        public string command;

    }
    public class Site
    {
        public string x;
        public string y;
        public string width;
        public string height;
        public string rotation;
        public string name;
        public string location;
        public string description;
    }
    public class CityElement
    {
        public City data = new City();
        public string command;

    }
    public class City
    {
        public string name;
        public string status;
        public string altname;
        public string description;
    }

    public class GalaxyElement
    {
        public Galaxy data = new Galaxy();
        public string command;

    }
    public class Galaxy
    {
        public string name;
        public string stars;
        public string dust;
        public string description;
        public string galaxyRadius;
        public string coreRadius;
        public string angularOffset;
        public string coreExcentricity;
        public string edgeExcentricity;
        //public string sigma;
        //public string coreOrbitalVelocity;
        //public string edgeOrbiralVelocity;            
    }
    public class DomainElement
    {
        public Domain data = new Domain();
        public string command;

    }
    public class Domain
    {
        public string name;
        public string status;
        public string description;
    }
    public class CloudElement
    {
        public Cloud data = new Cloud();
        public string command;

    }
    public class Cloud
    {
        public string name;
        public string host;
        public string cluster;
        public string site;
        public string status;
        public string description;
    }
    public class CloudSiteElement
    {
        public CloudSite data = new CloudSite();
        public string command;

    }
    public class CloudSite
    {
        public string name;
        public string role;
        public string location;
        public string site;
        public string status;
        public string description;
    }

    // Socket Policy Server (sockpol)
    //
    // Copyright (C) 2009 Novell, Inc (http://www.novell.com)
    //
    // Based on XSP source code (ApplicationServer.cs and XSPWebSource.cs)
    // Authors:
    //	Gonzalo Paniagua Javier (gonzalo@ximian.com)
    //	Lluis Sanchez Gual (lluis@ximian.com)
    //
    // Copyright (c) Copyright 2002-2007 Novell, Inc
    //
    // Permission is hereby granted, free of charge, to any person obtaining
    // a copy of this software and associated documentation files (the
    // "Software"), to deal in the Software without restriction, including
    // without limitation the rights to use, copy, modify, merge, publish,
    // distribute, sublicense, and/or sell copies of the Software, and to
    // permit persons to whom the Software is furnished to do so, subject to
    // the following conditions:
    //
    // The above copyright notice and this permission notice shall be
    // included in all copies or substantial portions of the Software.
    //
    // THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    // EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
    // MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
    // NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
    // LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
    // OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
    // WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
    //
    class SocketPolicyServer
    {

        const string PolicyFileRequest = "<policy-file-request/>";
        static byte[] request = Encoding.UTF8.GetBytes(PolicyFileRequest);
        private byte[] policy;

        private Socket listen_socket;
        private Thread runner;

        private AsyncCallback accept_cb;

        class Request
        {
            public Request(Socket s)
            {
                Socket = s;
                // the only answer to a single request (so it's always the same length)
                Buffer = new byte[request.Length];
                Length = 0;
            }

            public Socket Socket { get; private set; }
            public byte[] Buffer { get; set; }
            public int Length { get; set; }
        }

        public SocketPolicyServer(string xml)
        {
            // transform the policy to a byte array (a single time)
            policy = Encoding.UTF8.GetBytes(xml);
        }

        public int Start()
        {
            try
            {
                listen_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listen_socket.Bind(new IPEndPoint(IPAddress.Any, 843));
                listen_socket.Listen(500);
                listen_socket.Blocking = false;
            }
            catch (SocketException se)
            {
                // Most common mistake: port 843 is not user accessible on unix-like operating systems
                if (se.SocketErrorCode == SocketError.AccessDenied)
                {
                    Console.WriteLine("NOTE: must be run as root since the server listen to port 843");
                    return 5;
                }
                else
                {
                    Console.WriteLine(se);
                    return 6;
                }
            }

            runner = new Thread(new ThreadStart(RunServer));
            runner.Start();
            return 0;
        }

        void RunServer()
        {
            accept_cb = new AsyncCallback(OnAccept);
            listen_socket.BeginAccept(accept_cb, null);

            while (true) // Just sleep until we're aborted.
                Thread.Sleep(1000000);
        }

        void OnAccept(IAsyncResult ar)
        {
            Console.WriteLine("incoming connection");
            Socket accepted = null;
            try
            {
                accepted = listen_socket.EndAccept(ar);
            }
            catch
            {
            }
            finally
            {
                listen_socket.BeginAccept(accept_cb, null);
            }

            if (accepted == null)
                return;

            accepted.Blocking = true;

            Request request = new Request(accepted);
            accepted.BeginReceive(request.Buffer, 0, request.Length, SocketFlags.None, new AsyncCallback(OnReceive), request);
        }

        void OnReceive(IAsyncResult ar)
        {
            Request r = (ar.AsyncState as Request);
            Socket socket = r.Socket;
            try
            {
                r.Length += socket.EndReceive(ar);

                // compare incoming data with expected request
                for (int i = 0; i < r.Length; i++)
                {
                    if (r.Buffer[i] != request[i])
                    {
                        // invalid request, close socket
                        socket.Close();
                        return;
                    }
                }

                if (r.Length == request.Length)
                {
                    Console.WriteLine("got policy request, sending response");
                    // request complete, send policy
                    socket.BeginSend(policy, 0, policy.Length, SocketFlags.None, new AsyncCallback(OnSend), socket);
                }
                else
                {
                    // continue reading from socket
                    socket.BeginReceive(r.Buffer, r.Length, request.Length - r.Length, SocketFlags.None,
                        new AsyncCallback(OnReceive), ar.AsyncState);
                }
            }
            catch
            {
                // if anything goes wrong we stop our connection by closing the socket
                socket.Close();
            }
        }

        void OnSend(IAsyncResult ar)
        {
            Socket socket = (ar.AsyncState as Socket);
            try
            {
                socket.EndSend(ar);
            }
            catch
            {
                // whatever happens we close the socket
            }
            finally
            {
                socket.Close();
            }
        }

        public void Stop()
        {
            runner.Abort();
            listen_socket.Close();
        }

    }
}

