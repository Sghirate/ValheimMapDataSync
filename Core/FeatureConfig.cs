using BepInEx.Configuration;
using System;
using System.Reflection;

namespace ValheimMapDataSync
{
    [AttributeUsage(AttributeTargets.Method)]
    public class FeatureConfigAttribute : Attribute { }

    public static class FeatureConfigs
    {
        public static void Init(ConfigFile cfg)
        {
            UnityEngine.Debug.Log("  Init feature configs");
            foreach (Type t in typeof(FeatureConfigs).Assembly.GetTypes())
            {
                if (t.IsClass)
                {
                    foreach (MethodInfo m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        object[] attrs = m.GetCustomAttributes(typeof(FeatureConfigAttribute), false);
                        if (attrs != null && attrs.Length > 0)
                        {
                            UnityEngine.Debug.Log("    Init: " + t.FullName);
                            m.Invoke(null, new object[] { cfg });
                        }
                    }
                }
            }
        }
    }
}
