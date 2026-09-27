using System.Runtime.InteropServices;
using UnityEngine;

namespace DSB.GC
{
    internal static class GCWebGLRuntimeInfoBootstrap
    {
        internal const string RuntimeInfoResourceName = "GamingCouchRuntimeInfo";
        internal const string UnityBuildInfoResourceName = "GamingCouchUnityBuildInfo";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void GamingCouchRegisterRuntimeInfo(string runtimeInfoJson);

        [DllImport("__Internal")]
        private static extern void GamingCouchRegisterUnityBuildInfo(string unityBuildInfoJson);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void RegisterRuntimeInfoBeforeSplashScreen()
        {
            var runtimeInfoJson = LoadBakedRuntimeInfoJson();
            if (string.IsNullOrWhiteSpace(runtimeInfoJson))
            {
                Debug.LogError("Gaming Couch runtime info payload is missing.");
                return;
            }

            GamingCouchRegisterRuntimeInfo(runtimeInfoJson);

            var unityBuildInfoJson = LoadBakedUnityBuildInfoJson();
            if (!string.IsNullOrWhiteSpace(unityBuildInfoJson))
            {
                GamingCouchRegisterUnityBuildInfo(unityBuildInfoJson);
            }
        }
#endif

        internal static string LoadBakedRuntimeInfoJson()
        {
            return LoadBakedResourceJson(RuntimeInfoResourceName);
        }

        internal static string LoadBakedUnityBuildInfoJson()
        {
            return LoadBakedResourceJson(UnityBuildInfoResourceName);
        }

        private static string LoadBakedResourceJson(string resourceName)
        {
            var resource = Resources.Load<TextAsset>(resourceName);
            if (resource == null)
            {
                return null;
            }

            return resource.text.TrimEnd('\r', '\n');
        }
    }
}
