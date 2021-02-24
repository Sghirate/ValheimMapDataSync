using BepInEx.Configuration;
using HarmonyLib;

namespace ValheimMapDataSync
{
    public static class AutoVisible
    {
        private static ConfigEntry<bool> cfg_enabled;
        [FeatureConfig]
        private static void InitConfig(ConfigFile cfg)
        {
            cfg_enabled = cfg.Bind("AutoVisible", "Enabled", true, "Be visible to other players upon entering a server");
        }

        [HarmonyPatch(typeof(Minimap), "Start")]
        public static class Minimap_Start
        {
            private static void Postfix(ref Minimap __instance)
            {
                if (cfg_enabled != null && cfg_enabled.Value)
                {
                    ZNet.instance.SetPublicReferencePosition(true);
                }
            }
        }
    }
}
