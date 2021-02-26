using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ValheimMapDataSync
{
    public static class SharedExplore // Note: this is pretty much entirely stolen from ValheimPlus!
    {
        private static ConfigEntry<bool> cfg_enabled;
        private static ConfigEntry<float> cfg_radius;
        private static ConfigEntry<bool> cfg_sendExploredAreas;
        private static ConfigEntry<bool> cfg_receiveExploredAreas;
        [FeatureConfig]
        private static void InitConfig(ConfigFile cfg)
        {
            cfg_enabled = cfg.Bind("SharedExplore", "Enabled", true, "Receive exploration updates from other players?");
            cfg_radius = cfg.Bind("SharedExplore", "Radius", 100.0f, "Radius around other players that will be explored");
            cfg_sendExploredAreas = cfg.Bind("SharedExplore", "Send", false, "Send explored areas to other players?");
            cfg_receiveExploredAreas = cfg.Bind("SharedExplore", "Receive", false, "Receive explored areas from other players?");
        }

        private static FieldInfo s_explored = typeof(Minimap).GetField("m_explored", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo s_hasGenerated = typeof(Minimap).GetField("m_hasGenerated", BindingFlags.NonPublic | BindingFlags.Instance);
        private static bool[] GetExplored(this Minimap instance)
        {
            return instance != null && s_explored != null ? (bool[])s_explored.GetValue(instance) : null;
        }
        public static bool GetHasGenerated(this Minimap instance)
        {
            return instance != null && s_hasGenerated != null && (bool)s_hasGenerated.GetValue(instance);
        }

        private static ZPackage SerializeExploration()
        {
            bool[] explored = GetExplored(Minimap.instance);
            if (explored == null)
            {
                return null;
            }
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            for (int y = 0; y < Minimap.instance.m_textureSize; ++y)
            {
                for (int x = 0; x < Minimap.instance.m_textureSize; ++x)
                {
                    int idx = y * Minimap.instance.m_textureSize + x;
                    if (explored[idx])
                    {
                        minX = UnityEngine.Mathf.Min(minX, x);
                        minY = UnityEngine.Mathf.Min(minY, y);
                        maxX = UnityEngine.Mathf.Max(maxX, x);
                        maxY = UnityEngine.Mathf.Max(maxY, y);
                    }
                }
            }
            if (minX == int.MaxValue ||
                minY == int.MaxValue ||
                maxX == int.MinValue ||
                maxY == int.MinValue)
            {
                return null;
            }
            
            ZPackage zPackage = new ZPackage();

            zPackage.Write(minX);
            zPackage.Write(minY);
            zPackage.Write(maxX);
            zPackage.Write(maxY);

            int w = maxX - minX + 1;
            int h = maxY - minY + 1;
            BitArray bits = new BitArray(w * h, false);
            for(int y = minY; y <= maxY; ++y)
            {
                for (int x = minX; x <= maxX; ++x)
                {
                    int idx = y * Minimap.instance.m_textureSize + x;
                    int localX = x - minX;
                    int localY = y - minY;
                    int localIdx = y * w + x;
                    bits[localIdx] = explored[idx];
                }
            }
            byte[] bytes = new byte[bits.Length / 8 + (bits.Length % 8 == 0 ? 0 : 1)];
            zPackage.Write(bytes);

            return zPackage;
        }

        private static void DeserializeExploration(ZPackage zPackage)
        {
            bool[] explored = GetExplored(Minimap.instance);
            if (explored == null)
            {
                return;
            }
            int minX = zPackage.ReadInt();
            int maxX = zPackage.ReadInt();
            int minY = zPackage.ReadInt();
            int maxY = zPackage.ReadInt();
            int nBytes = zPackage.ReadInt();
            int w = maxX - minX + 1;
            int h = maxY - minY + 1;
            byte[] bytes = zPackage.ReadByteArray();
            if (bytes.Length < (w * h) / 8)
            {
                UnityEngine.Debug.LogWarning("Received exploration data is too small!");
                return;
            }
            BitArray bits = new BitArray(bytes);
            for (int y = minY; y <= maxY; ++y)
            {
                for (int x = minX; x <= maxX; ++x)
                {
                    int idx = y * Minimap.instance.m_textureSize + x;
                    int localX = x - minX;
                    int localY = y - minY;
                    int localIdx = y * w + x;
                    explored[idx] |= bits[localIdx];
                }
            }
        }

        private static void RPC_Explore(long sender, ZPackage zPackage)
        {
            if (cfg_receiveExploredAreas != null && cfg_receiveExploredAreas.Value)
            {
                if (ZNet.instance == null || ZNet.instance.GetUID() == sender)
                {
                    return;
                }
                UnityEngine.Debug.LogWarning("RPC_Explore");
                DeserializeExploration(zPackage);
            }
        }
        private static void Call_Explore(ZPackage zPackage)
        {
            UnityEngine.Debug.LogWarning("Call_Explore");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SharedExploration_Explore", new object[] { zPackage });
        }

        [HarmonyPatch(typeof(Minimap))]
        public static class Minimap_Hooks
        {
            [HarmonyReversePatch]
            [HarmonyPatch(typeof(Minimap), "Explore", new Type[] { typeof(Vector3), typeof(float) })]
            public static void call_Explore(object instance, Vector3 p, float radius) => throw new NotImplementedException();
        }
        [HarmonyPatch(typeof(Game), "Start")]
        public static class Game_Start
        {
            private static void Postfix(ref Game __instance)
            {
                ZRoutedRpc.instance.Register<ZPackage>("SharedExploration_Explore", new Action<long, ZPackage>(RPC_Explore));
            }
        }
        [HarmonyPatch(typeof(ZNet))]
        public static class ZNet_Hooks
        {
            [HarmonyReversePatch]
            [HarmonyPatch(typeof(ZNet), "GetOtherPublicPlayers", new Type[] { typeof(List<ZNet.PlayerInfo>) })]
            public static void GetOtherPublicPlayers(object instance, List<ZNet.PlayerInfo> playerList) => throw new NotImplementedException();
        }
        [HarmonyPatch(typeof(Minimap), "UpdateExplore")]
        public static class Minimap_UpdateExplore
        {
            private static void Prefix(ref float dt, ref Player player, ref Minimap __instance, ref float ___m_exploreTimer, ref float ___m_exploreInterval, ref List<ZNet.PlayerInfo> ___m_tempPlayerInfo) // Set after awake function
            {
                if (cfg_enabled == null || cfg_enabled.Value == false || cfg_radius == null)
                {
                    return;
                }

                float explorerTime = ___m_exploreTimer;
                explorerTime += Time.deltaTime;
                if (explorerTime > ___m_exploreInterval)
                {
                    ___m_tempPlayerInfo.Clear();
                    ZNet_Hooks.GetOtherPublicPlayers(ZNet.instance, ___m_tempPlayerInfo); // inconsistent returns but works

                    if (___m_tempPlayerInfo.Count > 0)
                    {
                        foreach (ZNet.PlayerInfo m_Player in ___m_tempPlayerInfo)
                        {
                            Minimap_Hooks.call_Explore(__instance, m_Player.m_position, cfg_radius.Value);
                        }
                    }
                }

                Minimap_Hooks.call_Explore(__instance, player.transform.position, cfg_radius.Value);
            }
        }
        [HarmonyPatch(typeof(ZRoutedRpc))]
        public static class ZRoutedRpc_Hooks
        {
            [HarmonyReversePatch]
            [HarmonyPatch(typeof(ZRoutedRpc), "GetServerPeerID")]
            public static long GetServerPeerID(object instance) => throw new NotImplementedException();
        }
        [HarmonyPatch(typeof(ZRoutedRpc), "AddPeer", typeof(ZNetPeer))]
        public static class ZRoutedRpc_AddPeer
        {
            private static void Postfix(ref ZRoutedRpc __instance, ZNetPeer peer)
            {
                if (cfg_sendExploredAreas != null && cfg_sendExploredAreas.Value)
                {
                    long serverPeerId = ZRoutedRpc_Hooks.GetServerPeerID(__instance);
                    UnityEngine.Debug.LogWarning($"Add Peer: {peer.m_uid}: {peer.m_playerName} (self: {ZNet.instance.GetUID()}; server: {serverPeerId})");

                    if (peer.m_uid != serverPeerId)
                    {
                        if (GetHasGenerated(Minimap.instance))
                        {
                            ZPackage zPackage = SerializeExploration();
                            if (zPackage != null)
                            {
                                Call_Explore(zPackage);
                            }
                        }
                    }
                }
            }
        }
        [HarmonyPatch(typeof(Minimap), "LoadMapData")]
        public static class Minimap_LoadMapData
        {
            private static void Postfix(ref Minimap __instance)
            {
                if (cfg_sendExploredAreas != null && cfg_sendExploredAreas.Value)
                {
                    ZPackage zPackage = SerializeExploration();
                    if (zPackage != null)
                    {
                        Call_Explore(zPackage);
                    }
                }
            }
        }
    }
}
