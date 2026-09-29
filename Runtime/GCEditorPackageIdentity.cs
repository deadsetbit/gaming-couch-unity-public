#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor.PackageManager;
using UnityEngine;

namespace DSB.GC
{
    [Serializable]
    internal sealed class GCPackageIdentity
    {
        public string platform;
        public string packageName;
        public string packageVersion;
        public int gameProtocolVersion;

        internal GCRuntimeInfo ToRuntimeInfo()
        {
            return new GCRuntimeInfo
            {
                platform = platform,
                packageName = packageName,
                packageVersion = packageVersion,
                gameProtocolVersion = gameProtocolVersion,
            };
        }
    }

    internal static class GCEditorPackageIdentity
    {
        internal const string PackageManifestFileName = "package.json";
        internal const string Platform = "unity";
        internal const int GameProtocolVersion = 1;

        internal static GCPackageIdentity Resolve()
        {
            return ResolveForAssembly(typeof(GamingCouch).Assembly);
        }

        internal static GCPackageIdentity ResolveForAssembly(Assembly packageAssembly)
        {
            if (packageAssembly == null)
            {
                throw new ArgumentNullException(nameof(packageAssembly));
            }

            var packageInfo = PackageInfo.FindForAssembly(packageAssembly);
            if (packageInfo != null && !string.IsNullOrWhiteSpace(packageInfo.resolvedPath))
            {
                return ResolveFromPackageRoot(packageInfo.resolvedPath);
            }

            if (packageInfo != null)
            {
                return ResolveFromPackageMetadata(packageInfo.name, packageInfo.version);
            }

            throw new InvalidOperationException(
                "Could not resolve Gaming Couch package identity from Unity package metadata."
            );
        }

        internal static GCPackageIdentity ResolveFromPackageRoot(string packageRootPath)
        {
            if (string.IsNullOrWhiteSpace(packageRootPath))
            {
                throw new InvalidOperationException("Gaming Couch package root path is empty.");
            }

            return ResolveFromPackageManifest(Path.Combine(packageRootPath, PackageManifestFileName));
        }

        internal static GCPackageIdentity ResolveFromPackageManifest(string packageManifestPath)
        {
            var manifest = ReadPackageManifest(packageManifestPath);
            return CreateIdentity(manifest.name, manifest.version, "package manifest " + packageManifestPath);
        }

        internal static GCPackageIdentity ResolveFromPackageMetadata(string packageName, string packageVersion)
        {
            // Unity PackageInfo exposes manifest-derived name/version when no resolved package root is available.
            return CreateIdentity(packageName, packageVersion, "Unity package metadata");
        }

        private static GCPackageIdentity CreateIdentity(string packageName, string packageVersion, string source)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new InvalidOperationException("Gaming Couch package identity from " + source + " is missing name.");
            }

            if (string.IsNullOrWhiteSpace(packageVersion))
            {
                throw new InvalidOperationException("Gaming Couch package identity from " + source + " is missing version.");
            }

            return new GCPackageIdentity
            {
                platform = Platform,
                packageName = packageName,
                packageVersion = packageVersion,
                gameProtocolVersion = GameProtocolVersion,
            };
        }

        private static PackageManifest ReadPackageManifest(string packageManifestPath)
        {
            if (string.IsNullOrWhiteSpace(packageManifestPath))
            {
                throw new InvalidOperationException("Gaming Couch package manifest path is empty.");
            }

            if (!File.Exists(packageManifestPath))
            {
                throw new InvalidOperationException("Gaming Couch package manifest was not found: " + packageManifestPath);
            }

            var manifestJson = File.ReadAllText(packageManifestPath);

            try
            {
                var manifest = JsonUtility.FromJson<PackageManifest>(manifestJson);
                if (manifest == null)
                {
                    throw new InvalidOperationException(
                        "Gaming Couch package manifest root is empty: " + packageManifestPath
                    );
                }

                return manifest;
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    "Gaming Couch package manifest is not valid JSON: " + packageManifestPath,
                    exception
                );
            }
        }

        [Serializable]
        private sealed class PackageManifest
        {
            public string name;
            public string version;
        }
    }
}
#endif
