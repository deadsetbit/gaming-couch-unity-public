using System;
using System.Collections.Generic;
using System.Reflection;
using DSB.GC;
using DSB.GC.RuntimeMessages;
using NUnit.Framework;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

internal static class GamingCouchEditorTestSupport
{
    internal static GamingCouch CreateGamingCouch(string name)
    {
        var gameObject = new GameObject(name);
        gameObject.SetActive(false);
        return gameObject.AddComponent<GamingCouch>();
    }

    internal static GameObject CreateCompatibleListener(string name)
    {
        var gameObject = new GameObject(name);
        gameObject.AddComponent<CompatibleGameScriptReceiver>();
        return gameObject;
    }

    internal static GameObject CreatePlayerPrefabObject(string name)
    {
        var gameObject = new GameObject(name);
        gameObject.AddComponent<GCPlayer>();
        return gameObject;
    }

    internal static GCPlayer CreatePlayer(
        List<UnityEngine.Object> objectsToDestroy,
        int playerIndex,
        GCPlayerType playerType = GCPlayerType.player
    )
    {
        var gameObject = new GameObject("Player " + playerIndex);
        objectsToDestroy.Add(gameObject);
        var player = gameObject.AddComponent<GCPlayer>();
        player._InternalGamingCouchSetup(new GCPlayerSetupOptions
        {
            playerIndex = playerIndex,
            type = playerType,
            colorEnum = GCPlayerColor.blue,
            colorName = "blue",
        });
        return player;
    }

    internal static void DestroyTrackedObjects(List<UnityEngine.Object> objectsToDestroy)
    {
        foreach (var unityObject in objectsToDestroy)
        {
            if (unityObject)
            {
                UnityEngine.Object.DestroyImmediate(unityObject);
            }
        }

        objectsToDestroy.Clear();
    }

    internal static string FindPackageRootPath()
    {
        var packageInfo = PackageInfo.FindForAssembly(typeof(GamingCouch).Assembly);
        if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
        {
            return packageInfo.resolvedPath;
        }

        throw new InvalidOperationException("Could not resolve Gaming Couch package root.");
    }

    internal static void SetPrivateField(object target, string fieldName, object value)
    {
        target
            .GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    internal static void ClearGamingCouchInstance()
    {
        typeof(GamingCouch)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, null);
    }

    internal static void AssertSnapshotPlayer(
        GCRuntimeStateSnapshotPlayer player,
        int playerIndex,
        int score,
        int lives,
        string status,
        string statusText,
        int meter,
        int placement,
        string eliminationState,
        string finishState
    )
    {
        Assert.That(player.playerIndex, Is.EqualTo(playerIndex));
        Assert.That(player.score, Is.EqualTo(score));
        Assert.That(player.lives, Is.EqualTo(lives));
        Assert.That(player.status, Is.EqualTo(status));
        Assert.That(player.statusText, Is.EqualTo(statusText));
        Assert.That(player.meter, Is.EqualTo(meter));
        Assert.That(player.placement, Is.EqualTo(placement));
        Assert.That(player.eliminationState, Is.EqualTo(eliminationState));
        Assert.That(player.finishState, Is.EqualTo(finishState));
    }
}

internal sealed class CompatibleGameScriptReceiver : MonoBehaviour
{
    private void GamingCouchSetup(GCSetupOptions options)
    {
    }

    private void GamingCouchPlay(GCPlayOptions options)
    {
    }
}

internal sealed class SetupOnlyGameScriptReceiver : MonoBehaviour
{
    private void GamingCouchSetup(GCSetupOptions options)
    {
    }
}

internal abstract class CompatibleGameScriptReceiverBase : MonoBehaviour
{
    public void GamingCouchSetup(GCSetupOptions options)
    {
    }

    public void GamingCouchPlay(GCPlayOptions options)
    {
    }
}

internal sealed class InheritedCompatibleGameScriptReceiver : CompatibleGameScriptReceiverBase
{
}

// Fixture stand-in for the generated example game type. It intentionally does NOT reuse the real
// generated type name (GCExampleGame): the generator's FindTypeByName guard matches by simple type
// name across every loaded assembly, so a fixture sharing that name would make the test assembly
// permanently block example-script generation in any project that loads these tests.
//
// GCExampleGameFixture stays in this editor test assembly because it is only ever added to an
// in-scene GameObject (the game listener), never serialized onto a saved prefab. The matching player
// fixtures (GCExamplePlayerFixture, ColorPlaceholderPrefabPlayer) instead live in the runtime
// GamingCouch.Tests.Fixtures assembly because they ARE baked into real prefabs, and an editor-assembly
// MonoBehaviour cannot be attached to a prefab.
internal sealed class GCExampleGameFixture : MonoBehaviour
{
    private void GamingCouchSetup(GCSetupOptions options)
    {
    }

    private void GamingCouchPlay(GCPlayOptions options)
    {
    }
}

internal sealed class WrongSignatureGameScriptReceiver : MonoBehaviour
{
    public void GamingCouchSetup()
    {
    }

    public void GamingCouchPlay(string options)
    {
    }
}
