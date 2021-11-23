# FizzySteamworks

This is a community maintained repo forked from **[RayStorm](https://github.com/Raystorms/FizzySteamyMirror)**. 

Mirror **[docs](https://mirror-networking.com/docs/Transports/Fizzy.html)** and the official community **[Discord](https://discord.gg/N9QVxbM)**.

FizzySteamworks brings together **[Steam](https://store.steampowered.com)** and **[Mirror](https://github.com/vis2k/Mirror)** . It supports both the old SteamNetworking and the new SteamSockets. 

## Dependencies
If you are using Heathen's Steamworks Foundation or Steamworks Complete you do not need to install Steamworks.NET as they already have it included.

You must have Mirror installed and working before you can use this transport.
**[Mirror](https://github.com/vis2k/Mirror)** FizzySteamworks is also obviously dependant on Mirror which is a streamline, bug fixed, maintained version of UNET for Unity.

You must have Steamworks.NET installed and working before you can use this transport.
**[Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET)** FizzySteamworks relies on Steamworks.NET to communicate with the **[Steamworks API](https://partner.steamgames.com/doc/sdk)**. **Requires .Net 4.x**  

## Installation
### Unity Package Manager

Unity Package Manager support is still fairly new but you can use it like so:

1. Open the Package Manager
2. Click the "+" (plus) button located in the upper left of the window
3. Select the "Add package from git URL..." option
4. Enter the following URL:
    `https://github.com/Chykary/FizzySteamworks.git?path=/com.mirror.steamworks.net`
5. Click the "Add" button and wait several seconds for the system to download and install the Steamworks.NET package from GitHub.

### Manual

Fewer steps but more error prone and subject to being out of date with the latest changes:

1. Download the latest [unitypackage](https://github.com/Chykary/FizzySteamworks/releases) from the release section.
2. Import the package into Unity.


## Setting Up

1. Install Steamworks.NET instructions can be found [here](https://github.com/rlabrecque/Steamworks.NET).
2. Install Mirror **(Requires Mirror 35.0+)** from the Unity asset store **[Download Mirror](https://assetstore.unity.com/packages/tools/network/mirror-129321)**.
3. Install FizzySteamworks from package manager as discribed in the above Install step.
3. In your **"NetworkManager"** object replace **"KCP"** with **"FizzySteamworks"**.

## Host
To be able to have your game working you need to make sure you have Steam running in the background and that the Steam API initalized correctly. You can then call StartHost and use Mirror as you normally would.

## Client
To connect a client to a host or server you need the CSteamID of the target you wish to connect to this is used in place of IP/Port. If your creating a Peer to Peer architecture then you would use the CSteamID of the host, this is Steam user ID as a ulong value. If you are creating a Client Server architecture then you will be using the CSteamID issued to the Steam Game Server when it logs the Steam API on. This is an advanced use case supported by Heathen's Steamworks but requires additional custom code if your using Steamworks.NET directly.

1. Send the game to your buddy.
2. Your buddy needs the host or game server **steamID64** to be able to connect.
3. Place the **steamID64** into **"localhost"** then click **"Client"**
5. Then they will be connected to your server be that your machine as a P2P connection or yoru Steam Game Server as a Client Server connection.

## Testing your game locally
You cant connect to yourself locally while using **FizzySteamworks** since it's using Steams Networking which runs over Steam Client and addresses its connection based on the unique CSteamID of each actor. If you want to test your game locally you'll have to use **"Telepathy Transport"** instead of **"FizzySteamworks"**.
