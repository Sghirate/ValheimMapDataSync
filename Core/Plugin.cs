using BepInEx;
using HarmonyLib;

namespace ValheimMapDataSync
{
    [BepInPlugin("com.sghirate.valheim.mapdatasync", "MapDataSync", "0.1.0.0")]
    public class MapSyncDataPlugin : BaseUnityPlugin
    {
        void Awake()
        {
            FeatureConfigs.Init(Config);

            Harmony harmony = new Harmony("mod.mapdatasync");
            harmony.PatchAll();

            Logger.LogMessage("MapDataSync loaded!");
        }
    }
}
