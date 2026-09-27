using DSB.GC;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal enum GamingCouchSceneWiringStatus
{
    Succeeded,
    Unchanged,
    Blocked,
}

internal enum GamingCouchObjectReferenceState
{
    MissingSlot,
    Empty,
    Assigned,
    Missing,
}

internal sealed class GamingCouchSceneWiringResult
{
    internal readonly GamingCouchSceneWiringStatus status;
    internal readonly GamingCouch gamingCouch;
    internal readonly bool changed;
    internal readonly string message;

    internal GamingCouchSceneWiringResult(
        GamingCouchSceneWiringStatus status,
        GamingCouch gamingCouch,
        bool changed,
        string message
    )
    {
        this.status = status;
        this.gamingCouch = gamingCouch;
        this.changed = changed;
        this.message = message;
    }

    internal bool IsBlocked
    {
        get { return status == GamingCouchSceneWiringStatus.Blocked; }
    }

    internal static GamingCouchSceneWiringResult SucceededResult(GamingCouch gamingCouch, string message)
    {
        return new GamingCouchSceneWiringResult(GamingCouchSceneWiringStatus.Succeeded, gamingCouch, true, message);
    }

    internal static GamingCouchSceneWiringResult UnchangedResult(GamingCouch gamingCouch, string message)
    {
        return new GamingCouchSceneWiringResult(GamingCouchSceneWiringStatus.Unchanged, gamingCouch, false, message);
    }

    internal static GamingCouchSceneWiringResult BlockedResult(GamingCouch gamingCouch, string message)
    {
        return new GamingCouchSceneWiringResult(GamingCouchSceneWiringStatus.Blocked, gamingCouch, false, message);
    }
}

internal static class GamingCouchSceneWiring
{
    internal const string ListenerPropertyName = "listener";
    internal const string PlayerPrefabPropertyName = "playerPrefab";

    private const string CreateGamingCouchUndoName = "Create GamingCouch";
    private const string AssignListenerUndoName = "Assign GamingCouch Listener";
    private const string AssignPlayerPrefabUndoName = "Assign GamingCouch Player Prefab";

    internal static GamingCouchSceneWiringResult EnsureActiveSceneGamingCouch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return GamingCouchSceneWiringResult.BlockedResult(
                null,
                "Exit Play Mode before creating a GamingCouch object."
            );
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return GamingCouchSceneWiringResult.BlockedResult(
                null,
                "No loaded active scene is available for GamingCouch setup."
            );
        }

        var gamingCouches = FindGamingCouchesInScene(scene);
        if (gamingCouches.Length > 1)
        {
            return GamingCouchSceneWiringResult.BlockedResult(
                null,
                "The active scene contains multiple GamingCouch components. Remove duplicates manually before running setup."
            );
        }

        if (gamingCouches.Length == 1)
        {
            return GamingCouchSceneWiringResult.UnchangedResult(
                gamingCouches[0],
                "The active scene already contains a GamingCouch object."
            );
        }

        var gameObject = new GameObject("GamingCouch");
        Undo.RegisterCreatedObjectUndo(gameObject, CreateGamingCouchUndoName);

        var gamingCouch = gameObject.AddComponent<GamingCouch>();
        if (gameObject.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(gameObject, scene);
        }

        return GamingCouchSceneWiringResult.SucceededResult(
            gamingCouch,
            "Created a GamingCouch object in the active scene."
        );
    }

    internal static GamingCouchSceneWiringResult AssignListenerIfMissing(GamingCouch gamingCouch, GameObject listener)
    {
        return AssignObjectReferenceIfMissing(
            gamingCouch,
            ListenerPropertyName,
            listener,
            AssignListenerUndoName,
            "listener"
        );
    }

    internal static GamingCouchSceneWiringResult AssignPlayerPrefabIfMissing(GamingCouch gamingCouch, GameObject playerPrefab)
    {
        return AssignObjectReferenceIfMissing(
            gamingCouch,
            PlayerPrefabPropertyName,
            playerPrefab,
            AssignPlayerPrefabUndoName,
            "player prefab",
            false
        );
    }

    internal static GamingCouchSceneWiringResult ReplacePlayerPrefab(GamingCouch gamingCouch, GameObject playerPrefab)
    {
        return AssignObjectReferenceIfMissing(
            gamingCouch,
            PlayerPrefabPropertyName,
            playerPrefab,
            AssignPlayerPrefabUndoName,
            "player prefab",
            true
        );
    }

    internal static GamingCouch[] FindActiveSceneGamingCouches()
    {
        return FindGamingCouchesInScene(SceneManager.GetActiveScene());
    }

    // What the Start Screen inspects, as opposed to what setup may edit. Entering Play Mode runs
    // GamingCouch.Awake, which marks the object DontDestroyOnLoad and so moves it into a scene
    // SceneManager does not enumerate. Setup paths keep using FindActiveSceneGamingCouches,
    // because an edit made to a moved object cannot outlive the play session.
    //
    // The predicate is isPlaying rather than the isPlayingOrWillChangePlaymode the setup guards
    // use, and the difference is deliberate. Awake returns before DontDestroyOnLoad while
    // Application.isPlaying is false, so until play is actually running the object is still in the
    // active scene's roots and the strict finder is the correct answer. A setup guard has to fire
    // earlier, because the imminent transition would discard the edit it is about to make.
    internal static GamingCouch[] FindActiveSceneGamingCouchesForInspection()
    {
        var activeSceneGamingCouches = FindGamingCouchesInScene(SceneManager.GetActiveScene());
        if (!EditorApplication.isPlaying)
        {
            return activeSceneGamingCouches;
        }

        return SelectGamingCouchesForInspection(
            activeSceneGamingCouches,
            FindGamingCouchesOutsidePreviewScenes(),
            GetEnumeratedScenes()
        );
    }

    // Counts, on top of the active scene's own, every candidate alive outside all the scenes
    // SceneManager enumerates. That is wider than "came from the active scene", which cannot be
    // asked once the object has moved: DontDestroyOnLoad does not record where the object was.
    // It is the right signal while a scene plays anyway, because GamingCouch.Awake destroys every
    // duplicate, so at most one such object is ever alive and it is the one the session runs on.
    internal static GamingCouch[] SelectGamingCouchesForInspection(
        GamingCouch[] activeSceneGamingCouches,
        GamingCouch[] candidates,
        Scene[] enumeratedScenes
    )
    {
        var selected = new List<GamingCouch>(activeSceneGamingCouches);
        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            if (candidate == null || selected.Contains(candidate))
            {
                continue;
            }

            if (!IsEnumeratedScene(candidate.gameObject.scene, enumeratedScenes))
            {
                selected.Add(candidate);
            }
        }

        return selected.ToArray();
    }

    private static bool IsEnumeratedScene(Scene scene, Scene[] enumeratedScenes)
    {
        for (var index = 0; enumeratedScenes != null && index < enumeratedScenes.Length; index++)
        {
            if (enumeratedScenes[index].handle == scene.handle)
            {
                return true;
            }
        }

        return false;
    }

    // A prefab stage also lives in a scene SceneManager does not enumerate. Its objects are being
    // edited rather than played, so they are never Play Mode survivors.
    private static GamingCouch[] FindGamingCouchesOutsidePreviewScenes()
    {
        var found = UnityEngine.Object.FindObjectsByType<GamingCouch>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        var candidates = new List<GamingCouch>(found.Length);
        for (var index = 0; index < found.Length; index++)
        {
            var candidate = found[index];
            if (candidate != null && !EditorSceneManager.IsPreviewScene(candidate.gameObject.scene))
            {
                candidates.Add(candidate);
            }
        }

        return candidates.ToArray();
    }

    private static Scene[] GetEnumeratedScenes()
    {
        var scenes = new Scene[SceneManager.sceneCount];
        for (var index = 0; index < scenes.Length; index++)
        {
            scenes[index] = SceneManager.GetSceneAt(index);
        }

        return scenes;
    }

    internal static GamingCouch[] FindGamingCouchesInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return new GamingCouch[0];
        }

        var gamingCouches = new List<GamingCouch>();
        var roots = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var root = roots[rootIndex];
            if (root == null)
            {
                continue;
            }

            var components = root.GetComponentsInChildren<GamingCouch>(true);
            for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                var component = components[componentIndex];
                if (component != null)
                {
                    gamingCouches.Add(component);
                }
            }
        }

        return gamingCouches.ToArray();
    }

    internal static UnityEngine.Object ReadObjectReference(GamingCouch gamingCouch, string propertyName)
    {
        if (gamingCouch == null)
        {
            return null;
        }

        var serializedObject = new SerializedObject(gamingCouch);
        serializedObject.Update();
        var property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
        {
            return null;
        }

        return property.objectReferenceValue;
    }

    internal static bool HasObjectReference(GamingCouch gamingCouch, string propertyName)
    {
        var state = GetObjectReferenceState(gamingCouch, propertyName);
        return state == GamingCouchObjectReferenceState.Assigned ||
               state == GamingCouchObjectReferenceState.Missing;
    }

    internal static bool HasAssignedObjectReference(GamingCouch gamingCouch, string propertyName)
    {
        return GetObjectReferenceState(gamingCouch, propertyName) == GamingCouchObjectReferenceState.Assigned;
    }

    internal static bool HasMissingObjectReference(GamingCouch gamingCouch, string propertyName)
    {
        return GetObjectReferenceState(gamingCouch, propertyName) == GamingCouchObjectReferenceState.Missing;
    }

    internal static GamingCouchObjectReferenceState GetObjectReferenceState(GamingCouch gamingCouch, string propertyName)
    {
        var property = FindObjectReferenceProperty(gamingCouch, propertyName);
        if (property == null)
        {
            return GamingCouchObjectReferenceState.MissingSlot;
        }

        if (property.objectReferenceValue != null)
        {
            return GamingCouchObjectReferenceState.Assigned;
        }

#if UNITY_6000_4_OR_NEWER
        return property.objectReferenceEntityIdValue.IsValid()
            ? GamingCouchObjectReferenceState.Missing
            : GamingCouchObjectReferenceState.Empty;
#else
        return property.objectReferenceInstanceIDValue != 0
            ? GamingCouchObjectReferenceState.Missing
            : GamingCouchObjectReferenceState.Empty;
#endif
    }

    internal static bool HasObjectReferenceSlot(GamingCouch gamingCouch, string propertyName)
    {
        return FindObjectReferenceProperty(gamingCouch, propertyName) != null;
    }

    internal static void MarkSceneDirty(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return;
        }

        var scene = gameObject.scene;
        if (scene.IsValid() && scene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static GamingCouchSceneWiringResult AssignObjectReferenceIfMissing(
        GamingCouch gamingCouch,
        string propertyName,
        GameObject reference,
        string undoName,
        string displayName,
        bool replaceAssigned = false
    )
    {
        if (gamingCouch == null)
        {
            return GamingCouchSceneWiringResult.BlockedResult(
                null,
                "A GamingCouch object is required before assigning the " + displayName + " reference."
            );
        }

        if (reference == null)
        {
            return GamingCouchSceneWiringResult.BlockedResult(
                gamingCouch,
                "A " + displayName + " object is required before assigning the GamingCouch reference."
            );
        }

        var serializedObject = new SerializedObject(gamingCouch);
        serializedObject.Update();
        var property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
        {
            return GamingCouchSceneWiringResult.BlockedResult(
                gamingCouch,
                "The GamingCouch " + displayName + " serialized field could not be found."
            );
        }

        var referenceState = GetSerializedObjectReferenceState(property);
        if (referenceState == GamingCouchObjectReferenceState.Assigned && !replaceAssigned)
        {
            return GamingCouchSceneWiringResult.UnchangedResult(
                gamingCouch,
                "The GamingCouch " + displayName + " reference already contains a serialized reference."
            );
        }

        if (referenceState == GamingCouchObjectReferenceState.Missing &&
            propertyName != ListenerPropertyName)
        {
            return GamingCouchSceneWiringResult.UnchangedResult(
                gamingCouch,
                "The GamingCouch " + displayName + " reference points to a missing object and was left unchanged."
            );
        }

        Undo.RecordObject(gamingCouch, undoName);
        property.objectReferenceValue = reference;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gamingCouch);
        MarkSceneDirty(gamingCouch.gameObject);

        return GamingCouchSceneWiringResult.SucceededResult(
            gamingCouch,
            referenceState == GamingCouchObjectReferenceState.Assigned
                ? "Replaced the GamingCouch " + displayName + " reference."
                : referenceState == GamingCouchObjectReferenceState.Missing
                ? "Replaced the missing GamingCouch " + displayName + " reference."
                : "Assigned the GamingCouch " + displayName + " reference."
        );
    }

    private static SerializedProperty FindObjectReferenceProperty(GamingCouch gamingCouch, string propertyName)
    {
        if (gamingCouch == null)
        {
            return null;
        }

        var serializedObject = new SerializedObject(gamingCouch);
        serializedObject.Update();
        var property = serializedObject.FindProperty(propertyName);
        return property != null && property.propertyType == SerializedPropertyType.ObjectReference
            ? property
            : null;
    }

    private static GamingCouchObjectReferenceState GetSerializedObjectReferenceState(SerializedProperty property)
    {
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
        {
            return GamingCouchObjectReferenceState.MissingSlot;
        }

        if (property.objectReferenceValue != null)
        {
            return GamingCouchObjectReferenceState.Assigned;
        }

#if UNITY_6000_4_OR_NEWER
        return property.objectReferenceEntityIdValue.IsValid()
            ? GamingCouchObjectReferenceState.Missing
            : GamingCouchObjectReferenceState.Empty;
#else
        return property.objectReferenceInstanceIDValue != 0
            ? GamingCouchObjectReferenceState.Missing
            : GamingCouchObjectReferenceState.Empty;
#endif
    }
}
