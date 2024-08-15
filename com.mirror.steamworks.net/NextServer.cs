#if !DISABLESTEAMWORKS
using Steamworks;
using System;
using System.Linq;
using UnityEngine;

namespace Mirror.FizzySteam
{
    public class NextServer : NextCommon, IServer
    {
        private event Action<int,string> OnConnectedWithAddress;
        private event Action<int, byte[], int> OnReceivedData;
        private event Action<int> OnDisconnected;
        private event Action<int, TransportError, string> OnReceivedError;

        private BidirectionalDictionary<HSteamNetConnection, int> connToMirrorID;
        private BidirectionalDictionary<CSteamID, int> steamIDToMirrorID;
        private int maxConnections;
        private int nextConnectionID;

        private HSteamListenSocket listenSocket;

        private Callback<SteamNetConnectionStatusChangedCallback_t> c_onConnectionChange = null;

        private static NextServer server;
        private NextServer(int maxConnections)
        {
            this.maxConnections = maxConnections;
            connToMirrorID = new BidirectionalDictionary<HSteamNetConnection, int>();
            steamIDToMirrorID = new BidirectionalDictionary<CSteamID, int>();
            nextConnectionID = 1;
#if UNITY_SERVER
            c_onConnectionChange = Callback<SteamNetConnectionStatusChangedCallback_t>.CreateGameServer(OnConnectionStatusChanged);
#else
            c_onConnectionChange = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);
#endif
        }

        public static NextServer CreateServer(FizzySteamworks transport, int maxConnections)
        {
            server = new NextServer(maxConnections);

            server.OnConnectedWithAddress += (id,addres) => transport.OnServerConnectedWithAddress.Invoke(id,addres);
            server.OnDisconnected += (id) => transport.OnServerDisconnected.Invoke(id);
            server.OnReceivedData += (id, data, ch) => transport.OnServerDataReceived.Invoke(id, new ArraySegment<byte>(data), ch);
            server.OnReceivedError += (id, error, reason) => transport.OnServerError.Invoke(id, error, reason);

            try
            {
#if UNITY_SERVER
                SteamGameServerNetworkingUtils.InitRelayNetworkAccess();
#else
                SteamNetworkingUtils.InitRelayNetworkAccess();
#endif
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            server.Host();

            return server;
        }

        private void Host()
        {
            SteamNetworkingConfigValue_t[] options = new SteamNetworkingConfigValue_t[] { };
#if UNITY_SERVER
            listenSocket = SteamGameServerNetworkingSockets.CreateListenSocketP2P(0, options.Length, options);
#else
            listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, options.Length, options);
#endif
        }

        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t param)
        {
            ulong clientSteamID = param.m_info.m_identityRemote.GetSteamID64();
            if (param.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
            {
                if (connToMirrorID.Count >= maxConnections)
                {
                    Debug.Log($"Incoming connection {clientSteamID} would exceed max connection count. Rejecting.");
#if UNITY_SERVER
                    SteamGameServerNetworkingSockets.CloseConnection(param.m_hConn, 0, "Max Connection Count", false);
#else
                    SteamNetworkingSockets.CloseConnection(param.m_hConn, 0, "Max Connection Count", false);
#endif
                    return;
                }

                EResult res;

#if UNITY_SERVER
                if ((res = SteamGameServerNetworkingSockets.AcceptConnection(param.m_hConn)) == EResult.k_EResultOK)
#else
                if ((res = SteamNetworkingSockets.AcceptConnection(param.m_hConn)) == EResult.k_EResultOK)
#endif
                {
                    Debug.Log($"Accepting connection {clientSteamID}");
                }
                else
                {
                    Debug.Log($"Connection {clientSteamID} could not be accepted: {res}");
                }
            }
            else if (param.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                int connectionId = nextConnectionID++;
                connToMirrorID.Add(param.m_hConn, connectionId);
                steamIDToMirrorID.Add(param.m_info.m_identityRemote.GetSteamID(), connectionId);
                OnConnectedWithAddress?.Invoke(connectionId,server.ServerGetClientAddress(connectionId));
                Debug.Log($"Client with SteamID {clientSteamID} connected. Assigning connection id {connectionId}");
            }
            else if (param.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer || param.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
            {
                if (connToMirrorID.TryGetValue(param.m_hConn, out int connId))
                {
                    InternalDisconnect(connId, param.m_hConn);
                }
            }
            else
            {
                Debug.Log($"Connection {clientSteamID} state changed: {param.m_info.m_eState}");
            }
        }

        private void InternalDisconnect(int connId, HSteamNetConnection socket)
        {
            OnDisconnected?.Invoke(connId);
#if UNITY_SERVER
            SteamGameServerNetworkingSockets.CloseConnection(socket, 0, "Graceful disconnect", false);
#else
            SteamNetworkingSockets.CloseConnection(socket, 0, "Graceful disconnect", false);
#endif
            connToMirrorID.Remove(connId);
            steamIDToMirrorID.Remove(connId);
            Debug.Log($"Client with ConnectionID {connId} disconnected.");
        }

        public void Disconnect(int connectionId)
        {
            if (connToMirrorID.TryGetValue(connectionId, out HSteamNetConnection conn))
            {
                Debug.Log($"Connection id {connectionId} disconnected.");
#if UNITY_SERVER
                SteamGameServerNetworkingSockets.CloseConnection(conn, 0, "Disconnected by server", false);
#else
                SteamNetworkingSockets.CloseConnection(conn, 0, "Disconnected by server", false);
#endif
                steamIDToMirrorID.Remove(connectionId);
                connToMirrorID.Remove(connectionId);
                OnDisconnected?.Invoke(connectionId);
            }
            else
            {
                Debug.LogWarning("Trying to disconnect unknown connection id: " + connectionId);
            }
        }

        public void FlushData()
        {
            foreach (HSteamNetConnection conn in connToMirrorID.FirstTypes.ToList())
            {
#if UNITY_SERVER
                SteamGameServerNetworkingSockets.FlushMessagesOnConnection(conn);
#else
                SteamNetworkingSockets.FlushMessagesOnConnection(conn);
#endif
            }
        }

        public void ReceiveData()
        {
            foreach (HSteamNetConnection conn in connToMirrorID.FirstTypes.ToList())
            {
                if (connToMirrorID.TryGetValue(conn, out int connId))
                {
                    IntPtr[] ptrs = new IntPtr[MAX_MESSAGES];
                    int messageCount;

#if UNITY_SERVER
                    if ((messageCount = SteamGameServerNetworkingSockets.ReceiveMessagesOnConnection(conn, ptrs, MAX_MESSAGES)) > 0)
#else
                    if ((messageCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, ptrs, MAX_MESSAGES)) > 0)
#endif
                    {
                        for (int i = 0; i < messageCount; i++)
                        {
                            (byte[] data, int ch) = ProcessMessage(ptrs[i]);
                            OnReceivedData?.Invoke(connId, data, ch);
                        }
                    }
                }
            }
        }

        public void Send(int connectionId, byte[] data, int channelId)
        {
            if (connToMirrorID.TryGetValue(connectionId, out HSteamNetConnection conn))
            {
                EResult res = SendSocket(conn, data, channelId);

                if (res == EResult.k_EResultNoConnection || res == EResult.k_EResultInvalidParam)
                {
                    Debug.Log($"Connection to {connectionId} was lost.");
                    InternalDisconnect(connectionId, conn);
                }
                else if (res != EResult.k_EResultOK)
                {
                    Debug.LogError($"Could not send: {res}");
                }
            }
            else
            {
                Debug.LogError("Trying to send on an unknown connection: " + connectionId);
                OnReceivedError?.Invoke(connectionId, TransportError.Unexpected, "ERROR Unknown Connection");
            }
        }

        public string ServerGetClientAddress(int connectionId)
        {
            if (steamIDToMirrorID.TryGetValue(connectionId, out CSteamID steamId))
            {
                return steamId.ToString();
            }
            else
            {
                Debug.LogError("Trying to get info on an unknown connection: " + connectionId);
                OnReceivedError?.Invoke(connectionId, TransportError.Unexpected, "ERROR Unknown Connection");
                return string.Empty;
            }
        }

        public void Shutdown()
        {
#if UNITY_SERVER
            SteamGameServerNetworkingSockets.CloseListenSocket(listenSocket);
#else
            SteamNetworkingSockets.CloseListenSocket(listenSocket);
#endif

            c_onConnectionChange?.Dispose();
            c_onConnectionChange = null;
        }
    }
}
#endif // !DISABLESTEAMWORKS
