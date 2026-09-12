using UnityEditor;

public static class GamingCouchWebGLBuildMenu
{
        [MenuItem("GamingCouch/WebGL Build/Preview release build settings (slow build)", false, GamingCouchMenuPriorities.WebGLBuild + 2)]
        public static void PreviewReleaseBuildSettings()
        {
                GamingCouchWebGLBuildSettingsPreviewWindow.OpenReleaseProfile();
        }


        [MenuItem("GamingCouch/WebGL Build/Preview dev build settings (fast build)", false, GamingCouchMenuPriorities.WebGLBuild + 1)]
        public static void PreviewDevBuildSettings()
        {
                GamingCouchWebGLBuildSettingsPreviewWindow.OpenDevProfile();
        }

        [MenuItem("GamingCouch/WebGL Build/Preview web export settings", false, GamingCouchMenuPriorities.WebGLBuild)]
        public static void PreviewWebGLExportSettings()
        {
                GamingCouchWebGLBuildSettingsPreviewWindow.OpenWebGLExportSettings(null);
        }
}
