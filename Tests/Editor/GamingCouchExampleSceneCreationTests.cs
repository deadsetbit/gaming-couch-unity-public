using System;
using System.Linq;
using DSB.GC.Dev;
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
            "Assets/GamingCouch/GCExample/GCTemplateScene.unity",
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
            "Assets/GamingCouch/GCExample/GCTemplateScene.unity",
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
            "Create Example Scene was cancelled.",
            null,
            Array.Empty<string>()
        );
        var blocked = new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Blocked,
            false,
            null,
            "Exit Play Mode before creating a new scene.",
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
    public void EachSceneKindOwnsItsOwnScenePath()
    {
        Assert.That(
            GamingCouchExampleSceneCreation.GetSpec(GCExampleSceneKind.Template).sceneAssetPath,
            Is.EqualTo("Assets/GamingCouch/GCExample/GCTemplateScene.unity")
        );
        Assert.That(
            GamingCouchExampleSceneCreation.GetSpec(GCExampleSceneKind.ExampleGame).sceneAssetPath,
            Is.EqualTo("Assets/GamingCouch/GCExample/GCExampleGameScene.unity")
        );
    }

    [Test]
    public void ExampleGameSceneWiresTheGameFlavorDirectly()
    {
        Assert.That(
            GamingCouchExampleSceneCreation.GetSpec(GCExampleSceneKind.ExampleGame).setupAction,
            Is.EqualTo(GCActiveSceneSetupAction.ActiveSceneExampleGame)
        );
        Assert.That(
            GamingCouchExampleSceneCreation.GetSpec(GCExampleSceneKind.Template).setupAction,
            Is.EqualTo(GCActiveSceneSetupAction.ActiveSceneMissingPieces)
        );
    }

    [Test]
    public void EachSceneKindOwnsOnlyItsOwnGeneratedFiles()
    {
        var template = GamingCouchExampleSceneCreation.GetOwnedAssetPaths(
            GamingCouchActiveSceneSetup.GetScriptSetupSpec(
                GamingCouchExampleSceneCreation.GetSpec(GCExampleSceneKind.Template).setupAction
            )
        );
        var game = GamingCouchExampleSceneCreation.GetOwnedAssetPaths(
            GamingCouchActiveSceneSetup.GetScriptSetupSpec(
                GamingCouchExampleSceneCreation.GetSpec(GCExampleSceneKind.ExampleGame).setupAction
            )
        );

        Assert.That(template, Is.EqualTo(new[]
        {
            "Assets/GamingCouch/GCExample/GCExampleTemplate.cs",
            "Assets/GamingCouch/GCExample/GCPlayer.prefab",
        }));
        Assert.That(game, Is.EqualTo(new[]
        {
            "Assets/GamingCouch/GCExample/GCExampleGame.cs",
            "Assets/GamingCouch/GCExample/GCExamplePlayer.cs",
            "Assets/GamingCouch/GCExample/GCExampleRing.cs",
            "Assets/GamingCouch/GCExample/GCExampleBot.cs",
            "Assets/GamingCouch/GCExample/GCExamplePlayer.prefab",
        }));
    }

    [Test]
    public void ExampleGameRosterIsSeatOneHumanAndSevenBots()
    {
        var current = new GCDevJsonFile("balance", "843", Enumerable.Range(1, 8)
            .Select(seat => new GCDevJsonSeat("Seat " + seat, seat <= 2, false))
            .ToArray());

        var roster = GamingCouchExampleSceneCreation.BuildExampleGameRoster(current, 8);

        Assert.That(roster.entryKey, Is.EqualTo("balance"));
        Assert.That(roster.seed, Is.EqualTo("843"));
        Assert.That(roster.seats.Select(seat => seat.name), Is.EqualTo(current.seats.Select(seat => seat.name)), "seat names are kept");
        Assert.That(roster.seats.All(seat => seat.enabled), Is.True);
        Assert.That(roster.seats[0].isBot, Is.False, "seat 1, the blue seat, is the developer");
        Assert.That(roster.seats.Skip(1).All(seat => seat.isBot), Is.True);
    }

    [Test]
    public void ExampleGameRosterStopsAtTheEntrysMaxPlayers()
    {
        var current = new GCDevJsonFile("balance", "random", Enumerable.Range(1, 8)
            .Select(seat => new GCDevJsonSeat("Seat " + seat, true, false))
            .ToArray());

        var roster = GamingCouchExampleSceneCreation.BuildExampleGameRoster(current, 4);

        Assert.That(roster.EnabledSeatCount, Is.EqualTo(4));
        Assert.That(roster.seats.Take(4).All(seat => seat.enabled), Is.True);
        Assert.That(roster.seats[0].isBot, Is.False);
    }

    [Test]
    public void ReadyMessageTellsTheDeveloperToPressPlay()
    {
        Assert.That(
            GamingCouchExampleSceneCreation.BuildReadyMessage("GCExampleGameScene"),
            Is.EqualTo("Created GCExampleGameScene. Press Play to run it.")
        );
    }

    [Test]
    public void SceneInfoTextNamesTheTemplateAndExampleFolder()
    {
        var template = GamingCouchExampleSceneCreation.BuildSceneInfoText(GamingCouchExampleSceneCreation.GetSpec(GCExampleSceneKind.Template));

        Assert.That(template, Does.Contain("GamingCouch template scene"));
        Assert.That(template, Does.Contain("GCExampleTemplate.cs"));
        Assert.That(template, Does.Contain("Assets/GamingCouch/GCExample"));
    }

    [Test]
    public void ExampleGameSceneHasNoOnScreenNoteOverTheArena()
    {
        Assert.That(GamingCouchExampleSceneCreation.GetSpec(GCExampleSceneKind.ExampleGame).overlayTitle, Is.Null);
    }

    [Test]
    public void GeneratedFilesGuidanceListsEachKindsAssets()
    {
        var template = GamingCouchExampleSceneCreation.BuildGeneratedFilesGuidance("Created it.", GamingCouchExampleSceneCreation.GetSpec(GCExampleSceneKind.Template));
        var game = GamingCouchExampleSceneCreation.BuildGeneratedFilesGuidance("Created it.", GamingCouchExampleSceneCreation.GetSpec(GCExampleSceneKind.ExampleGame));

        Assert.That(template, Does.Contain("Created it."));
        Assert.That(template, Does.Contain("Assets/GamingCouch/GCExample/GCExampleTemplate.cs"));
        Assert.That(template, Does.Contain("Assets/GamingCouch/GCExample/GCPlayer.prefab"));
        Assert.That(game, Does.Contain("Assets/GamingCouch/GCExample/GCExampleGame.cs"));
        Assert.That(game, Does.Contain("Assets/GamingCouch/GCExample/GCExamplePlayer.cs"));
        Assert.That(game, Does.Contain("Assets/GamingCouch/GCExample/GCExampleRing.cs"));
        Assert.That(game, Does.Contain("Assets/GamingCouch/GCExample/GCExampleBot.cs"));
        Assert.That(game, Does.Contain("Assets/GamingCouch/GCExample/GCExamplePlayer.prefab"));
    }
}
