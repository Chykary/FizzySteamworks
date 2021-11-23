#if !DISABLESTEAMWORKS
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
        [Tooltip("Allow or disallow P2P connections to fall back to being relayed through the Steam servers if a direct connection or NAT-traversal cannot be established.")]
        public bool AllowSteamRelay = true;

        [Tooltip("Use SteamSockets instead of the (deprecated) SteamNetworking. This will always use Relay.")]
        public bool UseNextGenSteamNetworking = true;

        private void OnEnable()
        {
            Debug.Assert(Channels != null && Channels.Length > 0, "No channel configured for FizzySteamworks.");
            Invoke(nameof(InitRelayNetworkAccess), 1f);
        }

        public override void ClientEarlyUpdate()
        {
            if (enabled)
            {
                client?.ReceiveData();
            }
        }

        public override void ServerEarlyUpdate()
        {
            if (enabled)
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
            try
            {
#if UNITY_SERVER
                SteamGameServerNetworkingUtils.InitRelayNetworkAccess();
#else
                SteamNetworkingUtils.InitRelayNetworkAccess();
#endif

                InitRelayNetworkAccess();

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
            catch (Exception ex)
            {
                Debug.LogError("Exception: " + ex.Message + ". Client could not be started.");
                OnClientDisconnected.Invoke();
            }
        }

        public override void ClientConnect(Uri uri)
        {
            if (uri.Scheme != STEAM_SCHEME)
                throw new ArgumentException($"Invalid url {uri}, use {STEAM_SCHEME}://SteamID instead", nameof(uri));

            ClientConnect(uri.Host);
        }

        public override void ClientSend(ArraySegment<byte> segment, int channelId)
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
            try
            {
#if UNITY_SERVER
                SteamGameServerNetworkingUtils.InitRelayNetworkAccess();
#else
                SteamNetworkingUtils.InitRelayNetworkAccess();
#endif


                InitRelayNetworkAccess();

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
#if UNITY_SERVER
                        SteamGameServerNetworking.AllowP2PPacketRelay(AllowSteamRelay);
#else

                        SteamNetworking.AllowP2PPacketRelay(AllowSteamRelay);
#endif
                        server = LegacyServer.CreateServer(this, NetworkManager.singleton.maxConnections);
                    }
                }
                else
                {
                    Debug.LogError("Server already started!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return;
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

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            if (ServerActive())
            {
                byte[] data = new byte[segment.Count];
                Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);
                server.Send(connectionId, data, channelId);
            }
        }
        public override void ServerDisconnect(int connectionId)
        {
            if (ServerActive())
            {
                server.Disconnect(connectionId);
            }
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
            if (UseNextGenSteamNetworking)
            {
                return Constants.k_cbMaxSteamNetworkingSocketsMessageSizeSend;
            }
            else
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
        }

        public override bool Available()
        {
            try
            {
#if UNITY_SERVER
                SteamGameServerNetworkingUtils.InitRelayNetworkAccess();
#else
                SteamNetworkingUtils.InitRelayNetworkAccess();
#endif
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void InitRelayNetworkAccess()
        {
            try
            {
                if (UseNextGenSteamNetworking)
                {
#if UNITY_SERVER
                    SteamGameServerNetworkingUtils.InitRelayNetworkAccess();
#else
                    SteamNetworkingUtils.InitRelayNetworkAccess();
#endif
                }
            }
            catch { }
        }

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}
#endif // !DISABLESTEAMWORKS