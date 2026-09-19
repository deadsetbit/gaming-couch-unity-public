#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace DSB.GC.Dev
{
    internal interface IGCLocalProjectRootResolver
    {
        string ResolveProjectRootPath();
        string ResolveProjectName();
    }

    internal sealed class GCUnityLocalProjectRootResolver : IGCLocalProjectRootResolver
    {
        internal static string NormalizeProjectRootPath(string value)
        {
            var fullPath = Path.GetFullPath(value);
            var normalizedSlashes = fullPath.Replace("\\", "/");
            var trimmedPath = normalizedSlashes.TrimEnd('/');
            if (string.IsNullOrEmpty(trimmedPath))
            {
                trimmedPath = "/";
            }

            if (IsWindowsPath(trimmedPath) || trimmedPath.StartsWith("//", StringComparison.Ordinal))
            {
                return trimmedPath.ToLowerInvariant();
            }

            return trimmedPath;
        }

        public string ResolveProjectRootPath()
        {
            var projectRootPath = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRootPath))
            {
                return NormalizeProjectRootPath(Application.dataPath);
            }

            return NormalizeProjectRootPath(projectRootPath);
        }

        public string ResolveProjectName()
        {
            if (!string.IsNullOrWhiteSpace(Application.productName))
            {
                return Application.productName.Trim();
            }

            return new DirectoryInfo(ResolveProjectRootPath()).Name;
        }

        private static bool IsWindowsPath(string value)
        {
            return value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && value[2] == '/';
        }
    }
}
#endif
