using BepInEx.Configuration;
using HarmonyLib;

namespace ValheimMapDataSync
{
    public static class RememberIP
    {
        private static ConfigEntry<string> cfg_ip;
        [FeatureConfig]
        private static void InitConfig(ConfigFile cfg)
        {
            cfg_ip = cfg.Bind("RememberIP", "IP", string.Empty, "IP of the last joined server");
        }

        [HarmonyPatch(typeof(FejdStartup), "OnJoinIPOpen")]
        public static class FejdStartup_OnJoinIPOpen
        {
            private static void Postfix(ref FejdStartup __instance)
            {
                __instance.m_joinIPAddress.text = cfg_ip?.Value;
            }
        }

        [HarmonyPatch(typeof(FejdStartup), "OnJoinIPConnect")]
        public static class FejdStartup_OnJoinIPConnect
        {
            private static void Postfix(ref FejdStartup __instance)
            {
                if (cfg_ip != null)
                {
                    cfg_ip.Value = __instance.m_joinIPAddress.text;
                }
            }
        }
    }
}
