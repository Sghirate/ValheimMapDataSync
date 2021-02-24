using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimMapDataSync
{
    public static class SharedExplore // Note: this is pretty much entirely stolen from ValheimPlus!
    {
        private static ConfigEntry<bool> cfg_enabled;
        private static ConfigEntry<float> cfg_radius;
        [FeatureConfig]
        private static void InitConfig(ConfigFile cfg)
        {
            cfg_enabled = cfg.Bind("SharedExplore", "Enabled", true, "Receive exploration updates from other players?");
            cfg_radius = cfg.Bind("SharedExplore", "Radius", 100.0f, "Radius around other players that will be explored");
        }

        // TODO: finish once motivation for networking stuff is there
        //private static FieldInfo s_explored = typeof(Minimap).GetField("m_explored", BindingFlags.NonPublic | BindingFlags.Instance);
        //private static bool[] GetExplored(this Minimap instance)
        //{
        //    return instance != null && s_explored != null ? (bool[])s_explored.GetValue(instance) : null;
        //}
        //private static ZPackage GetExploredRegion()
        //{
        //    bool[] explored = GetExplored(Minimap.instance);
        //    if (explored == null)
        //    {
        //        return null;
        //    }
        //    int minX = int.MaxValue;
        //    int minY = int.MaxValue;
        //    int maxX = int.MinValue;
        //    int maxY = int.MinValue;
        //    for (int y = 0; y < Minimap.instance.m_textureSize; ++y)
        //    {
        //        for (int x = 0; x < Minimap.instance.m_textureSize; ++x)
        //        {
        //            int idx = y * Minimap.instance.m_textureSize + x;
        //            if(explored[idx])
        //            {
        //                minX = UnityEngine.Mathf.Min(minX, x);
        //                minY = UnityEngine.Mathf.Min(minY, y);
        //                maxX = UnityEngine.Mathf.Max(maxX, x);
        //                maxY = UnityEngine.Mathf.Max(maxY, y);
        //            }
        //        }
        //    }
        //    if (minX == int.MaxValue ||
        //        minY == int.MaxValue ||
        //        maxX == int.MinValue ||
        //        maxY == int.MinValue)
        //    {
        //        return null;
        //    }
        //    ZPackage zPackage = new ZPackage();
        //    zPackage.Write(minX);
        //    zPackage.Write(minY);
        //    zPackage.Write(maxX);
        //    zPackage.Write(maxY);
        //    int n = (maxX - minX + 1) * (maxY - minY + 1);
        //    int i = 0;
        //    BitArray arr = new BitArray(n);
        //    for (int y = 0; y < Minimap.instance.m_textureSize; ++y)
        //    {
        //        for (int x = 0; x < Minimap.instance.m_textureSize; ++x)
        //        {
        //            int idx = y * Minimap.instance.m_textureSize + x;
        //            arr[i++] = explored[idx];
        //        }
        //    }
        //}

        [HarmonyPatch(typeof(Minimap))]
        public static class Minimap_Hooks
        {
            [HarmonyReversePatch]
            [HarmonyPatch(typeof(Minimap), "Explore", new Type[] { typeof(Vector3), typeof(float) })]
            public static void call_Explore(object instance, Vector3 p, float radius) => throw new NotImplementedException();
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
    }
}
