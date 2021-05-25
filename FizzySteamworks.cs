using Steamworks;
using System;
using System.IO;
using UnityEngine;

namespace Mirror.FizzySteam
{
  [HelpURL("https://github.com/Chykary/FizzySteamworks")]
  public class FizzySteamworks : Transport
  {
    private const string STEAM_SCHEME = "steam";

    private static IClient client;
    private static IServer server;

    [SerializeField]
    public EP2PSend[] Channels = new EP2PSend[2] { EP2PSend.k_EP2PSendReliable, EP2PSend.k_EP2PSendUnreliableNoDelay };

    [Tooltip("Timeout for connecting in seconds.")]
    public int Timeout = 25;
    [Tooltip("The Steam ID for your application.")]
    public string SteamAppID = "480";
    [Tooltip("Allow or disallow P2P connections to fall back to being relayed through the Steam servers if a direct connection or NAT-traversal cannot be established.")]
    public bool AllowSteamRelay = true;

    [Tooltip("Use SteamSockets instead of the (deprecated) SteamNetworking. This will always use Relay.")]
    public bool UseNextGenSteamNetworking = true;

    [Header("Info")]
    [Tooltip("This will display your Steam User ID when you start or connect to a server.")]
    public ulong SteamUserID;

    private void Awake()
    {
      const string fileName = "steam_appid.txt";
      if (File.Exists(fileName))
      {
        string content = File.ReadAllText(fileName);
        if (content != SteamAppID)
        {
          File.WriteAllText(fileName, SteamAppID.ToString());
          Debug.Log($"Updating {fileName}. Previous: {content}, new SteamAppID {SteamAppID}");
        }
      }
      else
      {
        File.WriteAllText(fileName, SteamAppID.ToString());
        Debug.Log($"New {fileName} written with SteamAppID {SteamAppID}");
      }
    }

    private void OnEnable()
    {
      Debug.Assert(Channels != null && Channels.Length > 0, "No channel configured for FizzySteamworks.");
      Invoke(nameof(FetchSteamID), 1f);
    }

    public override void ClientEarlyUpdate()
    {
      if(enabled)
      {
        client?.ReceiveData();
      }
    }

    public override void ServerEarlyUpdate()
    {
      if(enabled)
      {
        server?.ReceiveData();
      }
    }

    public override void ClientLateUpdate()
    {
      if (enabled)
      {
        client?.FlushData();
      }
    }

    public override void ServerLateUpdate()
    {
      if (enabled)
      {
        server?.FlushData();
      }
    }

    public override bool ClientConnected() => ClientActive() && client.Connected;
    public override void ClientConnect(string address)
    {
      if (!SteamManager.Initialized)
      {
        Debug.LogError("SteamWorks not initialized. Client could not be started.");
        OnClientDisconnected.Invoke();
        return;
      }

      FetchSteamID();

      if (ServerActive())
      {
        Debug.LogError("Transport already running as server!");
        return;
      }

      if (!ClientActive() || client.Error)
      {
        if (UseNextGenSteamNetworking)
        {
          Debug.Log($"Starting client [SteamSockets], target address {address}.");
          client = NextClient.CreateClient(this, address);
        }
        else
        {
          Debug.Log($"Starting client [DEPRECATED SteamNetworking], target address {address}. Relay enabled: {AllowSteamRelay}");
          SteamNetworking.AllowP2PPacketRelay(AllowSteamRelay);
          client = LegacyClient.CreateClient(this, address);
        }
      }
      else
      {
        Debug.LogError("Client already running!");
      }
    }

    public override void ClientConnect(Uri uri)
    {
      if (uri.Scheme != STEAM_SCHEME)
        throw new ArgumentException($"Invalid url {uri}, use {STEAM_SCHEME}://SteamID instead", nameof(uri));

      ClientConnect(uri.Host);
    }

    public override void ClientSend(int channelId, ArraySegment<byte> segment)
    {
      byte[] data = new byte[segment.Count];
      Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);
      client.Send(data, channelId);
    }

    public override void ClientDisconnect()
    {
      if (ClientActive())
      {
        Shutdown();
      }
    }
    public bool ClientActive() => client != null;


    public override bool ServerActive() => server != null;
    public override void ServerStart()
    {
      if (!SteamManager.Initialized)
      {
        Debug.LogError("SteamWorks not initialized. Server could not be started.");
        return;
      }

      FetchSteamID();

      if (ClientActive())
      {
        Debug.LogError("Transport already running as client!");
        return;
      }

      if (!ServerActive())
      {
        if (UseNextGenSteamNetworking)
        {
          Debug.Log($"Starting server [SteamSockets].");
          server = NextServer.CreateServer(this, NetworkManager.singleton.maxConnections);
        }
        else
        {
          Debug.Log($"Starting server [DEPRECATED SteamNetworking]. Relay enabled: {AllowSteamRelay}");
          SteamNetworking.AllowP2PPacketRelay(AllowSteamRelay);
          server = LegacyServer.CreateServer(this, NetworkManager.singleton.maxConnections);
        }
      }
      else
      {
        Debug.LogError("Server already started!");
      }
    }

    public override Uri ServerUri()
    {
      var steamBuilder = new UriBuilder
      {
        Scheme = STEAM_SCHEME,
        Host = SteamUser.GetSteamID().m_SteamID.ToString()
      };

      return steamBuilder.Uri;
    }

    public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
    {
      if (ServerActive())
      {
        byte[] data = new byte[segment.Count];
        Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);
        server.Send(connectionId, data, channelId);
      }
    }
    public override bool ServerDisconnect(int connectionId)
    {
      if (ServerActive())
      {
        server.Disconnect(connectionId);
      }

      return true;
    }
    public override string ServerGetClientAddress(int connectionId) => ServerActive() ? server.ServerGetClientAddress(connectionId) : string.Empty;
    public override void ServerStop()
    {
      if (ServerActive())
      {
        Shutdown();
      }
    }

    public override void Shutdown()
    {
      if (server != null)
      {
        server.Shutdown();
        server = null;
        Debug.Log("Transport shut down - was server.");
      }

      if (client != null)
      {
        client.Disconnect();
        client = null;
        Debug.Log("Transport shut down - was client.");
      }
    }

    public override int GetMaxPacketSize(int channelId)
    {
      if (channelId >= Channels.Length)
      {
        Debug.LogError("Channel Id exceeded configured channels! Please configure more channels.");
        return 1200;
      }

      switch (Channels[channelId])
      {
        case EP2PSend.k_EP2PSendUnreliable:
        case EP2PSend.k_EP2PSendUnreliableNoDelay:
          return 1200;
        case EP2PSend.k_EP2PSendReliable:
        case EP2PSend.k_EP2PSendReliableWithBuffering:
          return 1048576;
        default:
          throw new NotSupportedException();
      }
    }

    public override bool Available()
    {
      try
      {
        return SteamManager.Initialized;
      }
      catch
      {
        return false;
      }
    }

    private void FetchSteamID()
    {
      if (SteamManager.Initialized)
      {
        if (UseNextGenSteamNetworking)
        {
          SteamNetworkingUtils.InitRelayNetworkAccess();
        }

        SteamUserID = SteamUser.GetSteamID().m_SteamID;
      }
    }

    private void OnDestroy()
    {
      Shutdown();
    }
  }
}