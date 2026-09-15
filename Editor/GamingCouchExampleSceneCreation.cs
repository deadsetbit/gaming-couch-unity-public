using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if GC_HAS_UGUI
using UnityEngine.UI;
#endif

internal enum GCExampleSceneCreationStatus
{
    Created,
    Cancelled,
    Blocked,
}

internal sealed class GCExampleSceneCreationResult
{
    internal readonly GCExampleSceneCreationStatus status;
    internal readonly bool isPendingCompilation;
    internal readonly string scenePath;
    internal readonly string message;
    internal readonly string[] details;
    internal readonly string[] existingScenePaths;

    internal GCExampleSceneCreationResult(
        GCExampleSceneCreationStatus status,
        bool isPendingCompilation,
        string scenePath,
        string message,
        string[] details,
        string[] existingScenePaths
    )
    {
        this.status = status;
        this.isPendingCompilation = isPendingCompilation;
        this.scenePath = scenePath;
        this.message = message;
        this.details = details ?? new string[0];
        this.existingScenePaths = existingScenePaths ?? new string[0];
    }

    internal bool IsCreated { get { return status == GCExampleSceneCreationStatus.Created; } }
    internal bool IsCancelled { get { return status == GCExampleSceneCreationStatus.Cancelled; } }
    internal bool IsBlocked { get { return status == GCExampleSceneCreationStatus.Blocked; } }
    internal bool IsPendingCompilation { get { return isPendingCompilation; } }
}

// Resets the GamingCouch example to a single clean scene (unlike Active Scene Setup, which wires the
// scene the user already has open). It saves modified scenes, opens a fresh scene with the default
// camera + light, moves any previous example scenes and leftover blocking folders to the Trash, then
// reuses Active Scene Setup to wire the GamingCouch object and generate/link the example Game
// listener and player prefab. Existing example scripts and the player prefab are reused when valid,
// so a reset never deletes a compiled script only to immediately regenerate it (which would trip the
// generator's "a compiled type already exists" guard before the domain reloads). Making the new
// scene the first Build Settings scene is intentionally left to the existing "Set up missing pieces"
// action so resetting the example never silently changes which scene a build boots into.
internal static class GamingCouchExampleSceneCreation
{
    internal const string ExampleSceneBaseName = "GCExampleScene";
    internal const string ExampleSceneAssetPath =
        GamingCouchActiveSceneSetup.ExampleFolderAssetPath + "/" + ExampleSceneBaseName + ".unity";

    internal static GCExampleSceneCreationResult CreateExampleScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return Blocked(null, "Exit Play Mode before creating a new example scene.", null, null);
        }

        if (GamingCouchActiveSceneSetup.HasPendingSetup())
        {
            return Blocked(
                null,
                "Active Scene Setup is still finishing after generating example scripts. Wait for it to complete, then create the new example scene.",
                null,
                null
            );
        }

        var existingScenePaths = FindExistingExampleScenePaths();
        var blockingFolders = GamingCouchActiveSceneSetup.FindBlockingExampleAssetFolders();
        var hasSomethingToReplace = existingScenePaths.Length > 0 || blockingFolders.Length > 0;
        if (hasSomethingToReplace && !Application.isBatchMode)
        {
            var confirmed = EditorUtility.DisplayDialog(
                "Create New Example Scene",
                "This replaces the current GamingCouch example under " +
                    GamingCouchActiveSceneSetup.ExampleFolderAssetPath + " with one clean scene.\n\n" +
                    DescribeResetActions(existingScenePaths, blockingFolders) +
                    "\n\nRemoved items are moved to the Trash (recoverable). Existing example scripts " +
                    "and the player prefab are reused when valid.",
                "Create Clean Scene",
                "Cancel"
            );
            if (!confirmed)
            {
                return Cancelled(existingScenePaths);
            }
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return Cancelled(existingScenePaths);
        }

        // Switch to a fresh scene BEFORE removing the old example scenes, so we never delete the
        // scene that is currently open. In Single mode this closes any open example scene.
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Remove the previous example scenes and any leftover blocking folders (e.g. a directory
        // literally named "GCExampleGame.cs"). Valid example scripts and the player prefab are
        // intentionally kept and reused: deleting a compiled script here only to regenerate it in the
        // same pass would trip the generator's "a compiled type already exists" guard before Unity
        // reloads the domain.
        var removalReasons = new List<string>();
        var removedScenes = RemoveExampleScenes(existingScenePaths, removalReasons);
        var cleanup = GamingCouchActiveSceneSetup.RemoveBlockingExampleAssetFolders();
        AppendRange(removalReasons, cleanup.blockedReasons);
        if (removalReasons.Count > 0 ||
            GamingCouchActiveSceneSetup.FindBlockingExampleAssetFolders().Length > 0)
        {
            return Blocked(
                null,
                "Could not remove the previous example before creating a clean one. Remove the listed items manually, then try again.",
                removalReasons.ToArray(),
                existingScenePaths
            );
        }

        var blockedReasons = new List<string>();
        if (!GamingCouchActiveSceneSetup.EnsureProjectFolderRecursive(
                GamingCouchActiveSceneSetup.ExampleFolderAssetPath,
                blockedReasons
            ))
        {
            return Blocked(
                null,
                "Could not create the example folder for the new scene.",
                blockedReasons.ToArray(),
                existingScenePaths
            );
        }

        // The canonical scene path is normally free now that the old scenes are trashed; fall back to
        // a unique name only if something still occupies it, rather than overwriting.
        var scenePath = AssetExistsAtPath(ExampleSceneAssetPath)
            ? AssetDatabase.GenerateUniqueAssetPath(ExampleSceneAssetPath)
            : ExampleSceneAssetPath;
        if (!EditorSceneManager.SaveScene(newScene, scenePath))
        {
            return Blocked(scenePath, "Could not save the new example scene to " + scenePath + ".", null, existingScenePaths);
        }

        var setupResult = GamingCouchActiveSceneSetup.EnsureActiveSceneSetup(false);

        // Add an on-screen label so the otherwise-empty Game View (players only spawn at runtime)
        // points the user at the generated example scripts.
        CreateSceneInfoOverlay(newScene);

        // Persist the GamingCouch object (and any synchronously wired references) so they survive
        // the domain reload that first-run example-script generation triggers. The listener and
        // player-prefab references are wired by the post-compilation continuation and left for the
        // user to save, matching how Active Scene Setup already behaves.
        EditorSceneManager.SaveScene(newScene);

        var details = new List<string>();
        AddRemovedExampleDetail(details, removedScenes, cleanup.removedAssetPaths);
        AppendRange(details, setupResult != null ? setupResult.details : null);

        if (setupResult != null && setupResult.IsBlocked)
        {
            return new GCExampleSceneCreationResult(
                GCExampleSceneCreationStatus.Blocked,
                false,
                scenePath,
                "Created " + scenePath + ", but Active Scene Setup is blocked.",
                details.ToArray(),
                existingScenePaths
            );
        }

        var pending = setupResult != null && setupResult.IsPendingCompilation;
        var message = pending
            ? "Created a clean " + scenePath + ". Active Scene Setup will finish wiring it after Unity compiles the generated example scripts."
            : "Created a clean " + scenePath + " and completed Active Scene Setup.";

        Debug.Log(
            BuildGeneratedFilesGuidance(message),
            AssetDatabase.LoadMainAssetAtPath(GamingCouchActiveSceneSetup.ExampleTemplateScriptAssetPath)
        );

        return new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Created,
            pending,
            scenePath,
            message,
            details.ToArray(),
            existingScenePaths
        );
    }

    // Moves the previous example scenes to the Trash (recoverable). Returns the scenes actually
    // removed; any failure is recorded in blockedReasons so the caller can surface it.
    private static string[] RemoveExampleScenes(string[] scenePaths, List<string> blockedReasons)
    {
        var removed = new List<string>();
        for (var i = 0; i < scenePaths.Length; i++)
        {
            var path = scenePaths[i];
            if (!AssetExistsAtPath(path))
            {
                continue;
            }

            if (AssetDatabase.MoveAssetToTrash(path))
            {
                removed.Add(path);
            }
            else
            {
                blockedReasons.Add("Could not remove the existing example scene " + path + ".");
            }
        }

        return removed.ToArray();
    }

    private static bool AssetExistsAtPath(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath) &&
               AssetDatabase.LoadMainAssetAtPath(assetPath) != null;
    }

    internal static string[] FindExistingExampleScenePaths()
    {
        var folder = GamingCouchActiveSceneSetup.ExampleFolderAssetPath;
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return new string[0];
        }

        var guids = AssetDatabase.FindAssets("t:Scene", new[] { folder });
        var paths = new List<string>();
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".unity", StringComparison.Ordinal))
            {
                paths.Add(path);
            }
        }

        paths.Sort(StringComparer.Ordinal);
        return paths.ToArray();
    }

    // internal: "Wire example game" clears the same blocking folders and confirms with the same
    // wording.
    internal static string DescribeResetActions(string[] existingScenePaths, string[] blockingFolders)
    {
        var lines = new List<string>();
        AppendDescribedPaths(lines, "Remove " + existingScenePaths.Length + " existing example scene(s):", existingScenePaths);
        AppendDescribedPaths(lines, "Remove " + blockingFolders.Length + " leftover folder(s) blocking the example scripts:", blockingFolders);
        return lines.Count > 0
            ? string.Join("\n", lines)
            : "A fresh example scene will be created.";
    }

    private static void AppendDescribedPaths(List<string> lines, string heading, string[] paths)
    {
        if (paths == null || paths.Length == 0)
        {
            return;
        }

        lines.Add(heading);
        for (var i = 0; i < paths.Length; i++)
        {
            lines.Add("  - " + paths[i]);
        }
    }

    private static void AddRemovedExampleDetail(List<string> details, string[] removedScenes, string[] removedFolders)
    {
        if (removedScenes != null && removedScenes.Length > 0)
        {
            details.Add("Moved " + removedScenes.Length + " previous example scene(s) to the Trash.");
        }

        if (removedFolders != null && removedFolders.Length > 0)
        {
            details.Add("Moved " + removedFolders.Length + " leftover blocking folder(s) to the Trash.");
        }
    }

    private static void AppendRange(List<string> target, string[] source)
    {
        if (source == null)
        {
            return;
        }

        for (var i = 0; i < source.Length; i++)
        {
            target.Add(source[i]);
        }
    }

    private static void CreateSceneInfoOverlay(Scene scene)
    {
#if GC_HAS_UGUI
        var root = new GameObject("GamingCouch Example Info", typeof(Canvas), typeof(CanvasScaler));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("Panel", typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.6f);
        panelImage.raycastTarget = false;
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -24f);
        panelRect.sizeDelta = new Vector2(1040f, 210f);

        var textObject = new GameObject("Text", typeof(Text));
        textObject.transform.SetParent(panel.transform, false);
        var text = textObject.GetComponent<Text>();
        text.font = GetBuiltinFont();
        text.text = BuildSceneInfoText();
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 28;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 16f);
        textRect.offsetMax = new Vector2(-24f, -16f);

        if (root.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(root, scene);
        }
#endif
    }

#if GC_HAS_UGUI
    private static Font GetBuiltinFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
#endif

    internal static string BuildSceneInfoText()
    {
        return
            "GamingCouch example scene\n" +
            "Example scripts: " + GamingCouchActiveSceneSetup.ExampleFolderAssetPath + "\n" +
            "GCExampleTemplate.cs (barebones template — copy it to start your own game)\n" +
            "Players spawn at runtime on the Gaming Couch platform.\n" +
            "(You can delete this label.)";
    }

    internal static string BuildGeneratedFilesGuidance(string headline)
    {
        return
            "GamingCouch: " + headline + "\n" +
            "Browse the generated example files and grow them into your game:\n" +
            "  - " + GamingCouchActiveSceneSetup.ExampleTemplateScriptAssetPath + "  (barebones game listener)\n" +
            "  - " + GamingCouchActiveSceneSetup.StockPlayerPrefabAssetPath + "  (stock player prefab)\n" +
            "Run \"Wire example game\" to swap in the full example game (GCExampleGame + GCExamplePlayer).";
    }

    private static GCExampleSceneCreationResult Cancelled(string[] existingScenePaths)
    {
        return new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Cancelled,
            false,
            null,
            "Create New Example Scene was cancelled.",
            null,
            existingScenePaths
        );
    }

    private static GCExampleSceneCreationResult Blocked(
        string scenePath,
        string message,
        string[] details,
        string[] existingScenePaths
    )
    {
        return new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Blocked,
            false,
            scenePath,
            message,
            details,
            existingScenePaths
        );
    }
}
