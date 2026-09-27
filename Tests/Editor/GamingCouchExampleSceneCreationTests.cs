using System;
using NUnit.Framework;
using UnityEditor;

// These tests intentionally exercise only the pure result-mapping seam that wires example-scene
// creation into the Start Screen. The scene-creation flow itself (NewScene/SaveScene) is not unit
// tested: creating and closing scenes inside the shared EditMode Test Runner session breaks the
// framework's own scene-setup restore, so that behavior is verified manually in the Editor.
public sealed class GamingCouchExampleSceneCreationTests
{
    [Test]
    public void CreatedResultMapsToInfoAndCopiesMessageAndDetails()
    {
        var creation = new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Created,
            false,
            "Assets/GamingCouch/GCExample/GCExampleScene.unity",
            "Created the example scene.",
            new[] { "Detail A", "Detail B" },
            Array.Empty<string>()
        );

        var actionResult = GamingCouchStartScreenSetupActions.FromExampleSceneCreationResult(creation);

        Assert.That(actionResult.messageType, Is.EqualTo(MessageType.Info));
        Assert.That(actionResult.message, Is.EqualTo("Created the example scene."));
        Assert.That(actionResult.details, Is.EqualTo(new[] { "Detail A", "Detail B" }));
        Assert.That(actionResult.shouldRefreshAndRepaint, Is.True);
    }

    [Test]
    public void PendingCompilationCreatedResultMapsToWarning()
    {
        var creation = new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Created,
            true,
            "Assets/GamingCouch/GCExample/GCExampleScene.unity",
            "Created; setup will finish after compilation.",
            null,
            Array.Empty<string>()
        );

        var actionResult = GamingCouchStartScreenSetupActions.FromExampleSceneCreationResult(creation);

        Assert.That(actionResult.messageType, Is.EqualTo(MessageType.Warning));
    }

    [Test]
    public void CancelledResultMapsToWarningAndBlockedResultMapsToError()
    {
        var cancelled = new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Cancelled,
            false,
            null,
            "Create New Example Scene was cancelled.",
            null,
            Array.Empty<string>()
        );
        var blocked = new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Blocked,
            false,
            null,
            "Exit Play Mode before creating a new example scene.",
            null,
            Array.Empty<string>()
        );

        Assert.That(
            GamingCouchStartScreenSetupActions.FromExampleSceneCreationResult(cancelled).messageType,
            Is.EqualTo(MessageType.Warning)
        );
        Assert.That(
            GamingCouchStartScreenSetupActions.FromExampleSceneCreationResult(blocked).messageType,
            Is.EqualTo(MessageType.Error)
        );
    }

    [Test]
    public void NullResultMapsToError()
    {
        var actionResult = GamingCouchStartScreenSetupActions.FromExampleSceneCreationResult(null);

        Assert.That(actionResult.messageType, Is.EqualTo(MessageType.Error));
    }

    [Test]
    public void SceneInfoTextPointsToExampleFolder()
    {
        var text = GamingCouchExampleSceneCreation.BuildSceneInfoText();

        Assert.That(text, Does.Contain("GamingCouch example scene"));
        Assert.That(text, Does.Contain("Assets/GamingCouch/GCExample"));
    }

    [Test]
    public void GeneratedFilesGuidanceListsExampleAssetsAndHeadline()
    {
        var guidance = GamingCouchExampleSceneCreation.BuildGeneratedFilesGuidance("Created it.");

        Assert.That(guidance, Does.Contain("Created it."));
        Assert.That(guidance, Does.Contain("Assets/GamingCouch/GCExample/GCExampleTemplate.cs"));
        Assert.That(guidance, Does.Contain("Assets/GamingCouch/GCExample/GCPlayer.prefab"));
    }
}
