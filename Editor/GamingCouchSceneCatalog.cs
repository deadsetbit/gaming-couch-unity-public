using System;
using System.Collections.Generic;
using System.IO;
using DSB.GC;
using UnityEditor;
using UnityEngine;

// One project scene that hosts a GamingCouch component. The catalog lists these so the Start
// Screen can offer pre-existing scenes as starting points alongside "Create New Example Scene".
internal sealed class GCGamingCouchSceneEntry
{
    internal readonly string name;
    internal readonly string path;

    internal GCGamingCouchSceneEntry(string name, string path)
    {
        this.name = name;
        this.path = path;
    }
}

// Finds the project scenes that contain a GamingCouch component without opening them. Scanning the
// serialized scene YAML for the GamingCouch script GUID keeps this non-disruptive (loading each
// scene additively would fire scene events and thrash the editor) and reliable across Unity
// versions (AssetDatabase.GetDependencies script coverage has shifted between releases). Only
// scenes under Assets are considered; package scenes are read-only and never a user starting point.
internal static class GamingCouchSceneCatalog
{
    private const string AssetsPathPrefix = "Assets/";
    private const string SceneAssetExtension = ".unity";
    // Unity writes crash-recovery scene backups under an "_Recovery" folder; they are not real
    // starting points, so keep them out of the list.
    private const string RecoveryFolderSegment = "/_Recovery/";

    internal static GCGamingCouchSceneEntry[] FindGamingCouchScenes()
    {
        var scriptGuid = ResolveGamingCouchScriptGuid();
        if (string.IsNullOrEmpty(scriptGuid))
        {
            return new GCGamingCouchSceneEntry[0];
        }

        // A MonoBehaviour reference in a .unity file looks like:
        //   m_Script: {fileID: 11500000, guid: <32 hex chars>, type: 3}
        // Matching "guid: <guid>" avoids false positives from the GUID appearing in another field.
        var guidToken = "guid: " + scriptGuid;

        var entries = new List<GCGamingCouchSceneEntry>();
        var sceneGuids = AssetDatabase.FindAssets("t:Scene");
        for (var i = 0; i < sceneGuids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            if (string.IsNullOrEmpty(path) ||
                !path.StartsWith(AssetsPathPrefix, StringComparison.Ordinal) ||
                !path.EndsWith(SceneAssetExtension, StringComparison.Ordinal) ||
                path.IndexOf(RecoveryFolderSegment, StringComparison.Ordinal) >= 0)
            {
                continue;
            }

            if (!SceneFileReferencesScript(path, guidToken))
            {
                continue;
            }

            entries.Add(new GCGamingCouchSceneEntry(Path.GetFileNameWithoutExtension(path), path));
        }

        entries.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.Ordinal));
        return entries.ToArray();
    }

    private static bool SceneFileReferencesScript(string scenePath, string guidToken)
    {
        var fullPath = ToAbsolutePath(scenePath);
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            using (var reader = new StreamReader(fullPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.IndexOf(guidToken, StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }
            }
        }
        catch (IOException)
        {
            return false;
        }

        return false;
    }

    private static string ToAbsolutePath(string assetPath)
    {
        // Asset paths are project-relative ("Assets/..."). Application.dataPath points at
        // "<project>/Assets", so its parent is the project root the asset path hangs off of.
        var projectRoot = Directory.GetParent(Application.dataPath);
        if (projectRoot == null)
        {
            return null;
        }

        return Path.Combine(projectRoot.FullName, assetPath);
    }

    private static string ResolveGamingCouchScriptGuid()
    {
        // The name term narrows the search to a handful of scripts; GetClass is authoritative and
        // rejects the same-named editor scripts, leaving only the runtime GamingCouch component.
        var guids = AssetDatabase.FindAssets("GamingCouch t:MonoScript");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null && script.GetClass() == typeof(GamingCouch))
            {
                return AssetDatabase.AssetPathToGUID(path);
            }
        }

        return null;
    }
}
