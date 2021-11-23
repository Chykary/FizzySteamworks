#if !DISABLESTEAMWORKS
using Steamworks;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Mirror.FizzySteam
{
    public abstract class NextCommon
    {
        protected const int MAX_MESSAGES = 256;

        protected EResult SendSocket(HSteamNetConnection conn, byte[] data, int channelId)
        {
            Array.Resize(ref data, data.Length + 1);
            data[data.Length - 1] = (byte)channelId;

            GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr pData = pinnedArray.AddrOfPinnedObject();
            int sendFlag = channelId == Channels.Unreliable ? Constants.k_nSteamNetworkingSend_Unreliable : Constants.k_nSteamNetworkingSend_Reliable;
            EResult res = SteamNetworkingSockets.SendMessageToConnection(conn, pData, (uint)data.Length, sendFlag, out long _);
            if (res != EResult.k_EResultOK)
            {
                Debug.LogWarning($"Send issue: {res}");
            }

            pinnedArray.Free();
            return res;
        }

        protected (byte[], int) ProcessMessage(IntPtr ptrs)
        {
            throw new NotImplementedException("It is not possible to implament the ProcessMessage function based on the current release version of Steamworks.NET.\nWorkarounds do exist, please see the comments section in the NextCommon.cs for more information.");

            //HACK: If you have choosen to implament the latest change set from Steamworks.NET as documented here: https://github.com/rlabrecque/Steamworks.NET/issues/424 then you can safely uncomment the following code
            
            //NOTE: Heathen's Steamworks Foundation and Steamworks Complete already has 424 implamented and so you can safely use the following code as is

            //SteamNetworkingMessage_t data = Marshal.PtrToStructure<SteamNetworkingMessage_t>(ptrs);
            //byte[] managedArray = new byte[data.m_cbSize];
            //Marshal.Copy(data.m_pData, managedArray, 0, data.m_cbSize);
            //SteamNetworkingMessage_t.Release(ptrs);

            //int channel = managedArray[managedArray.Length - 1];
            //Array.Resize(ref managedArray, managedArray.Length - 1);
            //return (managedArray, channel);
        }
    }
}
#endif // !DISABLESTEAMWORKS