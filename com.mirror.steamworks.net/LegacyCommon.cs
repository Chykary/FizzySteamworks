#if !DISABLESTEAMWORKS
using Steamworks;
using System;
using System.Collections;
using UnityEngine;

namespace Mirror.FizzySteam
{
    public abstract class LegacyCommon
    {
        private EP2PSend[] channels;
        private int internal_ch => channels.Length;

        protected enum InternalMessages : byte
        {
            CONNECT,
            ACCEPT_CONNECT,
            DISCONNECT
        }

        private Callback<P2PSessionRequest_t> callback_OnNewConnection = null;
        private Callback<P2PSessionConnectFail_t> callback_OnConnectFail = null;

        protected readonly FizzySteamworks transport;

        protected LegacyCommon(FizzySteamworks transport)
        {
            channels = transport.Channels;

            callback_OnNewConnection = Callback<P2PSessionRequest_t>.Create(OnNewConnection);
            callback_OnConnectFail = Callback<P2PSessionConnectFail_t>.Create(OnConnectFail);

            this.transport = transport;
        }

        protected void Dispose()
        {
            if (callback_OnNewConnection != null)
            {
                callback_OnNewConnection.Dispose();
                callback_OnNewConnection = null;
            }

            if (callback_OnConnectFail != null)
            {
                callback_OnConnectFail.Dispose();
                callback_OnConnectFail = null;
            }
        }

        protected abstract void OnNewConnection(P2PSessionRequest_t result);

        private void OnConnectFail(P2PSessionConnectFail_t result)
        {
            OnConnectionFailed(result.m_steamIDRemote);
            CloseP2PSessionWithUser(result.m_steamIDRemote);

            switch (result.m_eP2PSessionError)
            {
                case 1:
                    Debug.LogError("Connection failed: The target user is not running the same game.");
                    break;
                case 2:
                    Debug.LogError("Connection failed: The local user doesn't own the app that is running.");
                    break;
                case 3:
                    Debug.LogError("Connection failed: Target user isn't connected to Steam.");
                    break;
                case 4:
                    Debug.LogError("Connection failed: The connection timed out because the target user didn't respond.");
                    break;
                default:
                    Debug.LogError("Connection failed: Unknown error.");
                    break;
            }
        }

        protected void SendInternal(CSteamID target, InternalMessages type)
        {
#if UNITY_SERVER
            SteamGameServerNetworking.SendP2PPacket(target, new byte[] { (byte)type }, 1, EP2PSend.k_EP2PSendReliable, internal_ch);
#else
            SteamNetworking.SendP2PPacket(target, new byte[] { (byte)type }, 1, EP2PSend.k_EP2PSendReliable, internal_ch);
#endif
        }
        protected void Send(CSteamID host, byte[] msgBuffer, int channel)
        {
#if UNITY_SERVER
            SteamGameServerNetworking.SendP2PPacket(host, msgBuffer, (uint)msgBuffer.Length, channels[Mathf.Min(channel, channels.Length - 1)], channel);
#else
            SteamNetworking.SendP2PPacket(host, msgBuffer, (uint)msgBuffer.Length, channels[Mathf.Min(channel, channels.Length - 1)], channel);
#endif
        }

        private bool Receive(out CSteamID clientSteamID, out byte[] receiveBuffer, int channel)
        {
#if UNITY_SERVER
            if (SteamGameServerNetworking.IsP2PPacketAvailable(out uint packetSize, channel))
            {
                receiveBuffer = new byte[packetSize];
                return SteamGameServerNetworking.ReadP2PPacket(receiveBuffer, packetSize, out _, out clientSteamID, channel);
            }
#else
            if (SteamNetworking.IsP2PPacketAvailable(out uint packetSize, channel))
            {
                receiveBuffer = new byte[packetSize];
                return SteamNetworking.ReadP2PPacket(receiveBuffer, packetSize, out _, out clientSteamID, channel);
            }
#endif

            receiveBuffer = null;
            clientSteamID = CSteamID.Nil;
            return false;
        }

        protected void CloseP2PSessionWithUser(CSteamID clientSteamID)
        {
#if UNITY_SERVER
            SteamGameServerNetworking.CloseP2PSessionWithUser(clientSteamID);
#else
            SteamNetworking.CloseP2PSessionWithUser(clientSteamID);
#endif
        }

        protected void WaitForClose(CSteamID cSteamID)
        {
            if (transport.enabled)
            {
                transport.StartCoroutine(DelayedClose(cSteamID));
            }
            else
            {
                CloseP2PSessionWithUser(cSteamID);
            }
        }

        private IEnumerator DelayedClose(CSteamID cSteamID)
        {
            yield return null;
            CloseP2PSessionWithUser(cSteamID);
        }

        public void ReceiveData()
        {
            try
            {
                while (transport.enabled && Receive(out CSteamID clientSteamID, out byte[] internalMessage, internal_ch))
                {
                    if (internalMessage.Length == 1)
                    {
                        OnReceiveInternalData((InternalMessages)internalMessage[0], clientSteamID);
                        return; // Wait one frame
                    }
                    else
                    {
                        Debug.Log("Incorrect package length on internal channel.");
                    }
                }

                for (int chNum = 0; chNum < channels.Length; chNum++)
                {
                    while (transport.enabled && Receive(out CSteamID clientSteamID, out byte[] receiveBuffer, chNum))
                    {
                        OnReceiveData(receiveBuffer, clientSteamID, chNum);
                    }
                }

            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        protected abstract void OnReceiveInternalData(InternalMessages type, CSteamID clientSteamID);
        protected abstract void OnReceiveData(byte[] data, CSteamID clientSteamID, int channel);
        protected abstract void OnConnectionFailed(CSteamID remoteId);
    }
}
#endif // !DISABLESTEAMWORKS