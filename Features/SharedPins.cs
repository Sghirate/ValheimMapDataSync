using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ValheimMapDataSync
{
    public static class SharedPins
    {
        private static ConfigEntry<bool> cfg_enabled;
        [FeatureConfig]
        private static void InitConfig(ConfigFile cfg)
        {
            cfg_enabled = cfg.Bind("SharedPins", "Enabled", true, "Share pins with other players?");
        }
        
        private static Dictionary<long, List<Minimap.PinData>> s_syncedPins = new Dictionary<long, List<Minimap.PinData>>();
        private static List<Tuple<long, Vector3, Minimap.PinType, string>> s_pendingPins = new List<Tuple<long, Vector3, Minimap.PinType, string>>();

        private static FieldInfo s_pins = typeof(Minimap).GetField("m_pins", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo s_hasGenerated = typeof(Minimap).GetField("m_hasGenerated", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo s_players = typeof(ZNet).GetField("m_players", BindingFlags.NonPublic | BindingFlags.Instance);
        public static List<Minimap.PinData> GetPins(this Minimap instance)
        {
            return instance != null && s_pins != null ? (List<Minimap.PinData>)s_pins.GetValue(instance) : null;
        }
        public static bool GetHasGenerated(this Minimap instance)
        {
            return instance != null && s_hasGenerated != null && (bool)s_hasGenerated.GetValue(instance);
        }
        public static List<ZNet.PlayerInfo> GetPlayers(this ZNet instance)
        {
            return instance != null && s_players != null ? (List<ZNet.PlayerInfo>)s_players.GetValue(instance) : null;
        }

        public static void AddPinToMinimap(long sender, Vector3 pos, Minimap.PinType pinType, string name)
        {
            if (!GetHasGenerated(Minimap.instance))
            {
                UnityEngine.Debug.LogWarning("queue pin");
                s_pendingPins.Add(new Tuple<long, Vector3, Minimap.PinType, string>(sender, pos, pinType, name));
            }
            else
            {
                UnityEngine.Debug.LogWarning("append pin");
                List<Minimap.PinData> pins;
                if (!s_syncedPins.TryGetValue(sender, out pins))
                {
                    pins = new List<Minimap.PinData>();
                    s_syncedPins[sender] = pins;
                }
                for (int i = 0; i < pins.Count; ++i)
                {
                    Minimap.PinData pin = pins[i];
                    if (pin.m_type == pinType && Utils.DistanceXZ(pos, pin.m_pos) < 1.0f)
                    {
                        pin.m_name = name;
                        return;
                    }
                }

                UnityEngine.Debug.LogWarning("create minimap pin");
                Minimap.PinData newPin = Minimap.instance.AddPin(pos, pinType, name, false, false);

                pins.Add(newPin);
            }
        }
        public static void RemovePinFromMinimap(long sender, Vector3 pos, Minimap.PinType pinType)
        {
            if (!GetHasGenerated(Minimap.instance))
            {
                for (int i = s_pendingPins.Count - 1; i >= 0; --i)
                {
                    if (s_pendingPins[i].Item1 == sender && s_pendingPins[i].Item3 == pinType && Utils.DistanceXZ(s_pendingPins[i].Item2, pos) < 1f)
                    {
                        s_pendingPins.RemoveAt(i);
                    }
                }
            }
            else
            {
                if (s_syncedPins.TryGetValue(sender, out List<Minimap.PinData> pins))
                {
                    for (int i = pins.Count - 1; i >= 0; --i)
                    {
                        if (pins[i].m_type == pinType && Utils.DistanceXZ(pins[i].m_pos, pos) < 1f)
                        {
                            Minimap.instance.RemovePin(pins[i]);
                            pins.RemoveAt(i);
                        }
                    }
                }
            }
        }
        public static void RemoveAllPinsFromMinimap(long sender)
        {
            if (!GetHasGenerated(Minimap.instance))
            {
                for (int i = s_pendingPins.Count - 1; i >= 0; --i)
                {
                    if (s_pendingPins[i].Item1 == sender)
                    {
                        s_pendingPins.RemoveAt(i);
                    }
                }
            }
            else
            {
                if (s_syncedPins.TryGetValue(sender, out List<Minimap.PinData> pins))
                {
                    for(int i = 0; i < pins.Count; ++i)
                    {
                        Minimap.instance.RemovePin(pins[i]);
                    }
                    pins = null;
                    s_syncedPins.Remove(sender);
                }
            }
        }
        public static void RenamePinOnMinimap(long sender, Vector3 pos, Minimap.PinType pinType, string name)
        {
            if (!GetHasGenerated(Minimap.instance))
            {
                for (int i = s_pendingPins.Count - 1; i >= 0; --i)
                {
                    if (s_pendingPins[i].Item1 == sender && s_pendingPins[i].Item3 == pinType && Utils.DistanceXZ(s_pendingPins[i].Item2, pos) < 1f)
                    {
                        s_pendingPins[i] = new Tuple<long, Vector3, Minimap.PinType, string>(
                            sender,
                            pos,
                            pinType,
                            name);
                    }
                }
            }
            else
            {
                if (s_syncedPins.TryGetValue(sender, out List<Minimap.PinData> pins))
                {
                    for (int i = pins.Count - 1; i >= 0; --i)
                    {
                        if (pins[i].m_type == pinType && Utils.DistanceXZ(pins[i].m_pos, pos) < 1f)
                        {
                            pins[i].m_name = name;
                        }
                    }
                }
            }
        }
       
        public static void RPC_AddPin(long sender, Vector3 pos, int pinType, string name)
        {
            if (ZNet.instance == null || ZNet.instance.GetUID() == sender)
            {
                return;
            }
            UnityEngine.Debug.LogWarning($"RPC_AddPin: {sender}, {pinType}, {name}, {pos}");
            AddPinToMinimap(sender, pos, (Minimap.PinType)pinType, name);
        }
        public static void Call_AddPin(Vector3 pos, int pinType, string name)
        {
            UnityEngine.Debug.LogWarning("Call_AddPin");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SharedPins_AddPin", new object[]{
                pos,
                pinType,
                name
            });
        }
        public static void Call_AddPin(long receiver, Vector3 pos, int pinType, string name)
        {
            UnityEngine.Debug.LogWarning("Call_AddPin");
            ZRoutedRpc.instance.InvokeRoutedRPC(receiver, "SharedPins_AddPin", new object[]{
                pos,
                pinType,
                name
            });
        }
        public static void RPC_RemovePin(long sender, Vector3 pos, int pinType)
        {
            if (ZNet.instance == null || ZNet.instance.GetUID() == sender)
            {
                return;
            }
            UnityEngine.Debug.LogWarning("RPC_RemovePin");
            RemovePinFromMinimap(sender, pos, (Minimap.PinType)pinType);
        }
        public static void Call_RemovePin(Vector3 pos, int pinType, string name)
        {
            UnityEngine.Debug.LogWarning("Call_RemovePin");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SharedPins_RemovePin", new object[]{
                pos,
                pinType,
                name
            });
        }
        public static void RPC_RemoveAllPins(long sender)
        {
            if (ZNet.instance == null || ZNet.instance.GetUID() == sender)
            {
                return;
            }
            UnityEngine.Debug.LogWarning("RPC_RemoveAllPins");
            RemoveAllPinsFromMinimap(sender);
        }
        public static void Call_RemoveAllPins()
        {
            UnityEngine.Debug.LogWarning("Call_RemoveAllPins");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SharedPins_RemoveAllPins", new object[]{ });
        }
        public static void RPC_RenamePin(long sender, Vector3 pos, int pinType, string name)
        {
            if (ZNet.instance == null || ZNet.instance.GetUID() == sender)
            {
                return;
            }
            UnityEngine.Debug.LogWarning("RPC_RenamePin");
            RenamePinOnMinimap(sender, pos, (Minimap.PinType)pinType, name);
        }
        public static void Call_RenamePin(Vector3 pos, int pinType, string name)
        {
            UnityEngine.Debug.LogWarning("Call_RenamePin");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SharedPins_RenamePin", new object[] {
                pos,
                pinType,
                name
            });
        }

        [HarmonyPatch(typeof(ZRoutedRpc))]
        public static class ZRoutedRpc_Hooks
        {
            [HarmonyReversePatch]
            [HarmonyPatch(typeof(ZRoutedRpc), "GetServerPeerID")]
            public static long GetServerPeerID(object instance) => throw new NotImplementedException();
        }
        [HarmonyPatch(typeof(Game), "Start")]
        public static class Game_Start
        {
            private static void Postfix(ref Game __instance)
            {
                if (cfg_enabled == null || !cfg_enabled.Value)
                {
                    return;
                }
                ZRoutedRpc.instance.Register<Vector3, int, string>("SharedPins_AddPin", new System.Action<long, Vector3, int, string>(RPC_AddPin));
                ZRoutedRpc.instance.Register<Vector3, int>("SharedPins_RemovePin", new System.Action<long, Vector3, int>(RPC_RemovePin));
                ZRoutedRpc.instance.Register("SharedPins_RemoveAllPins", new System.Action<long>(RPC_RemoveAllPins));
                ZRoutedRpc.instance.Register<Vector3, int, string>("SharedPins_RenamePin", new Action<long, Vector3, int, string>(RPC_RenamePin));
            }
        }

        [HarmonyPatch(typeof(Minimap), "AddPin", typeof(Vector3), typeof(Minimap.PinType), typeof(string), typeof(bool), typeof(bool))]
        public static class Minimap_AddPin
        {
            private static void Postfix(ref Minimap __instance, Vector3 pos, Minimap.PinType type, string name, bool save, bool isChecked)
            {
                if (cfg_enabled == null || !cfg_enabled.Value)
                {
                    return;
                }
                if (save)
                {
                    Call_AddPin(pos, (int)type, name);
                }
            }
        }
        [HarmonyPatch(typeof(Minimap), "RemovePin", typeof(Minimap.PinData))]
        public static class Minimap_RemovePin
        {
            private static void Postfix(ref Minimap __instance, Minimap.PinData pin)
            {
                if (cfg_enabled == null || !cfg_enabled.Value)
                {
                    return;
                }
                if (pin.m_save)
                {
                    Call_RemovePin(pin.m_pos, (int)pin.m_type, pin.m_name);
                }
            }
        }
        [HarmonyPatch(typeof(Minimap), "LoadMapData")]
        public static class Minimap_LoadMapData
        {
            private static void Postfix(ref Minimap __instance)
            {
                if (cfg_enabled == null || !cfg_enabled.Value)
                {
                    return;
                }
                List<Minimap.PinData> pins = GetPins(__instance);
                if (pins != null)
                {
                    for (int i = 0; i < pins.Count; ++i)
                    {
                        if (pins[i].m_save)
                        {
                            Call_AddPin(pins[i].m_pos, (int)pins[i].m_type, pins[i].m_name);
                        }
                    }
                }

                for (int i = 0; i < s_pendingPins.Count; ++i)
                {
                    AddPinToMinimap(s_pendingPins[i].Item1, s_pendingPins[i].Item2, s_pendingPins[i].Item3, s_pendingPins[i].Item4);
                }
                s_pendingPins.Clear();
            }
        }
        [HarmonyPatch(typeof(Minimap), "OnDestroy")]
        public static class Minimap_OnDestroy
        {
            private static void Postfix(ref Minimap __instance)
            {
                if (cfg_enabled == null || !cfg_enabled.Value)
                {
                    return;
                }
                s_syncedPins.Clear();
            }
        }
        [HarmonyPatch(typeof(Minimap), "UpdateNameInput")]
        public static class Minimap_UpdateNameInput
        {
            private static void Prefix(ref Minimap __instance, ref Minimap.PinData __state, ref Minimap.PinData ___m_namePin)
            {
                if (cfg_enabled == null || !cfg_enabled.Value)
                {
                    return;
                }
                __state = GetHasGenerated(Minimap.instance) ? ___m_namePin : null;
            }

            private static void Postfix(ref Minimap __instance, Minimap.PinData __state, ref Minimap.PinData ___m_namePin)
            {
                if (cfg_enabled == null || !cfg_enabled.Value)
                {
                    return;
                }
                if (__state != null && __state.m_save && ___m_namePin == null)
                {
                    Call_RenamePin(__state.m_pos, (int)__state.m_type, __state.m_name);
                }
            }
        }
        [HarmonyPatch(typeof(ZRoutedRpc), "AddPeer", typeof(ZNetPeer))]
        public static class ZRoutedRpc_AddPeer
        {
            private static void Postfix(ref ZRoutedRpc __instance, ZNetPeer peer)
            {
                if (cfg_enabled == null || !cfg_enabled.Value)
                {
                    return;
                }

                long serverPeerId = ZRoutedRpc_Hooks.GetServerPeerID(__instance);
                UnityEngine.Debug.LogWarning($"Add Peer: {peer.m_uid}: {peer.m_playerName} (self: {ZNet.instance.GetUID()}; server: {serverPeerId})");
                if (peer.m_uid != serverPeerId)
                {
                    if (GetHasGenerated(Minimap.instance))
                    {
                        List<Minimap.PinData> pins = GetPins(Minimap.instance);
                        if (pins != null)
                        {
                            for (int i = 0; i < pins.Count; ++i)
                            {
                                if (pins[i].m_save)
                                {
                                    Call_AddPin(peer.m_uid, pins[i].m_pos, (int)pins[i].m_type, pins[i].m_name);
                                }
                            }
                        }
                    }
                }
            }
        }
        [HarmonyPatch(typeof(ZRoutedRpc), "RemovePeer", typeof(ZNetPeer))]
        public static class ZRoutedRpc_RemovePeer
        {
            private static void Postfix(ref ZRoutedRpc __instance, ZNetPeer peer)
            {
                if (cfg_enabled == null || !cfg_enabled.Value)
                {
                    return;
                }

                UnityEngine.Debug.LogWarning($"Remove Peer: {peer.m_uid}: {peer.m_playerName}");
                RemoveAllPinsFromMinimap(peer.m_uid);
            }
        }
    }
}
