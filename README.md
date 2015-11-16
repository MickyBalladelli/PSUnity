# PSUnity

With this project we will look into integrating Unity with Powershell for creating a multi-purpose monitoring system. This system can be used with SCOM to display information about servers and their status such as Powershell DSC configuration drifts, status of Active Directory domain controllers, replication, status of various clusters, clouds etc.


## So, What's Unity?

Unity is super cool! It’s a 3D engine and an integrated development environment for building games. It supports Javascript and C# and can be used for creating applications for a variety of platforms such as Windows, Unix, Mac OS, Android, IOS, and a bunch of consoles.

It’s quite easy to learn Unity, and the on-line documentation, forums, and Youtube is full of tutorials and examples. One of the tutorials shows you how to create a full asteroids game in less than 2 hours. Which is pretty amazing.

Unity provides a number of namespaces for C#, these can be used to manipulate objects such as providing behavior, animations, effects. Its user interface is quite complete, and object manipulation is quite intuitive.

Unity does a lot for us, it hides [most] of the horrendous math required for implementing physics, and animations, and allows us to focus more on what we want to achieve. The asset store is great, and a lot of the reusable components are free, or have a free version. This means that we can build something quite fast without having to reinvent the wheel all the time.

I wrote PSUnity with just free components from the Asset store, from various 3D Models on the Web, as well as models found on the NASA website.

## Why integrate Powershell with Unity?

Unity provides a visually compelling interface, it allows displaying amazing graphics and lets you decide what objects you display and how you display them, you can define how objects interact with other objects, their characteristics, their physics. Pretty much anything goes and the only limitation is really your imagination.

Powershell can interface with pretty much anything and can retrieve all sort of information.

Driving Unity from Powershell makes sense as it provides a lot of flexibility in how the data is retrieved and updated in the PSUnity environment.

For example, we could use Powershell to connect to SCOM and retrieve the status of monitored systems. These monitored systems can be of any type, it’s then up to you to decide what and how you represent your findings within Unity. As we will see I chose a number of scenarios to display different kinds of objects.

It’s true that since Unity supports C#, I could have used .NET and C# to retrieve information about servers and display it. But then only Devs would have used PSUnity, and I really want IT pros to use it. IT pros use Powershell, not C#.

## Structure of PSUnity

The following image depicts how PSUnity is structured:

![image2](https://balladelli.com/wp-content/uploads/2015/09/PSUnity.png)

The PSUnity server can be accessed by Powershell cmdlets for creating and updating objects that will be displayed in PSUnity. Powershell scripts can interface to a variety of remote solutions to retrieve information such as, SCOM, Powershell DSC, ADSIPS, and others such as SSH (I use it to connect to a remote EMC Isilon NAS cluster and retrieve status information).

The PSUnity client connects to a PSUnity server via TCP/IP and pulls the latest information on a regular basis, and Unity does the rest.

## PSUnity cmdlets

PSUnity comes with a number of cmdlets which are used for updating the PSUnity server. This part is still work in progress, and new cmdlets are created for new needs., so watch this space on GitHub.


*New-PSUnitySession: creates a new PSUnity session. Once executed, Powershell is connected to PSUnity.
*Remove-PSUnitySession: closes the PSUnity session
*Set-PSUnityServer: creates or modifies a server object in PSUnity. Attributes are name, location, role, status (value between 0-4), description, position (x, y in 2D coordinates), rotation. If the server object does not exist, it is created. If the object exists it is updated. Objects in PSUnity are uniquely identified by their name attribute. The status defines the behavior of the server object. If the value of status is 2, there are sparks on the keyboard. 3 means smoke comes out of the server, and if it’s 4, the server is on fire. The color of the name of the server reflects the status too: Green, Blue, Yellow, Orange, Red. Servers are visible only in the Servers view.
*Set-PSUnitySite: creates or modifies a platform that can be used to group servers. Sites are visible in the Servers view. A Site has a label that displays its name and location.
*Set-PSUnityCity: Cities are predefined in PSUnity and are all disabled by default. This cmdlet enables a city (i.e. make it visible) in the Globe view. A city can receive an alternate name, this can be useful if you are using a naming convention for locations, such as location code or data centre name. Cities also have a status and description. The status defines the color of the object representing the city, Green, Blue, Yellow, Orange and Red.
*Set-PSUnityDomain: these are representing Active Directory domains.
*Set-PSUnityCloud: defines a VM in a cloud (cloud view).
*Set-PSUnityCloudInfo: defines a datacentre managing a cloud (cloud view).

See ./Powershell/TestPSUnity.ps1 for an example of using the cmdlets

## The PSUnity server

The PSUnity server is actually two servers in one. It accepts commands received by PSUnity cmdlets and maintains and updates the list of monitored objects such as servers, cities, sites, domains etc., it also acts as a policy server for the Unity Web client, without which it wouldn’t be able to connect to a port to retrieve object information. This is a security feature of Unity for preventing malicious games to send information to a third party while you are playing.

The PSUnity Server can be found under its own folder which contains the entire C# solution: ./PSUnityServer

Once started the server will wait for clients and provide data according to requests.

To connect to the PSUnity Server using Powershell you must use the following command:
```
$Session = New-PSUnitySession -ComputerName localhost -Port 7777
```
and to disconnect:
```
Remove-PSUnitySession -Session $Session
```
##PSUnity views

### Server View

When the PSUnity client starts, it displays the Server view, disconnected.

![image3](https://balladelli.com/wp-content/uploads/2015/10/newpsunity.png)

The view shows a lonely R2D2 and on the left hand-side some text boxes used to connect to a PSUnity server and define a refresh frequency.

Since we do have a PSUnity server up and running and a Powershell script was able to send some information, let’s hit Update (maybe I should change that to Connect).

![image3](https://balladelli.com/wp-content/uploads/2015/11/servers.png)

Let’s understand what we are looking at here. The Update button transformed into a Stop button with a countdown. When the countdown is at 0, a refresh happens.

Clicking on Console showed a window. R2D2 is going around to check each server and retrieve the Description attribute, that value will display in the console. R2D2 figures out where each server is, walks to it, spends a couple of seconds on each server, then moves to the next.

Hitting the “Tab” Key makes the camera follow R2D2. I added this because there might be servers placed outside the view, R2D2 will go to them, and we want to follow him doing so.

Then we see servers of different kinds and something that looks like a platform.

The platform is a PSUnity Site added with this line:

```
Set-PSUnitySite -Session $Session -name Site3 -X 30 -Y 20 -Width 7 -Height 7 -Location MainDC -Description "Domain controllers"
```

Note that it has a label above with the name and location.

Servers are of type 0, 1 and 2, type 0 being the default and most common one.  Type 1 is the cluster you see on the left representing an EMC Isilon cluster (NAS1 in the pic).

The color codes are defined by the status of the server.

If your Powershell scripts retrieves new values, for example a server becomes critically unavailable then you might want to update what you see.

```
Set-PSUnityServer -Session $Session -X -30 -Y 5 -Name Server1 -Role FIM -Location NY -Status 0 -Description "Server1 is down" 
 
Set-PSUnityServer -Session $Session -X -20 -Y 5 -Name Server3 -Role IIS -Location Madrid -Status 0 -Description "Server2 has its CPU exceed 90% threshold" 
```


### Globe View

The Globe view, is the Earth with a bunch of cities highlighted as seen in the first image in this article.

All the cities are invisible, unless you update them with the following command:
```
Set-PSUnityCity -Session $Session -Name Stockholm -Status 3
```
You can add or update a description by calling
```
Set-PSUnityCity -Session $Session -Name Chicago -Status 4 -Description "DC does not respond"
```
The descripting string could come from SCOM or other sources.

Here is an example:
![image5](https://balladelli.com/wp-content/uploads/2015/11/globe.png)


On the right hand-side we can see the domains and their statuses, they were added with these commands:

```
Set-PSUnityDomain -Session $Session -Name ad.local -Status 4 -Description "no ping from the domain"
Set-PSUnityDomain -Session $Session -Name emea.ad.local -Status 3 -Description "2 DCs drifted their DSC configuration"
Set-PSUnityDomain -Session $Session -Name asia.ad.local -Status 2 -Description "domain responds to ping"
```

And yes, there are some satellites flying by in low orbit. Courtesy of NASA free 3D models available on their website.

### Cloud view

The cloud view represents two data centers, a road linking them, and two clouds containing VMs rotating on top of each site.
Each Data Center has a truck. The goal of the truck is to move a VM, should it need to restart on the other site
Now, why would they want to take a truck to be moved? Why not, it felt funnier than the usual network link.

![image6](https://balladelli.com/wp-content/uploads/2015/10/Cloud.png)

To add a VM do:
```
Set-PSUnityCloud -Session $Session -Site 1 -Name "VM1" -VMHost "host name" -Cluster "cluster name" -Status 0 
```
Status is important, 0-4 are color codes (0 being green, and 4 being Red), 5 is a code for a starting VM and 6 is the code for a VM shutting down. Status 5 and 6 trigger animations.

To update the Data Centers 

```
Set-PSUnityCloudInfo -Session $Session -Site 1 -Name "Data Center 1" -Role "HyperV" -Location "Main Data Center" -Status 1 -Description "123"
Set-PSUnityCloudInfo -Session $Session -Site 2 -Name "Data Center 2" -Role "Standby" -Location "Backup Data Center" -Status 4 -Description "No issues found"
```

## GitHub
Please ensure you read the LICENSE file, all my code is free under the MIT license, however some of the components while being free, might have a different license.

Check the releases tab as I will be providing new releases as the project makes progress. 

Enjoy,


Micky Balladelli

micky@balladelli.com

Twitter: @mickyballadelli
