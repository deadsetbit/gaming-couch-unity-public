using System.Collections.Generic;
using System.Reflection;
using DSB.GC;
using DSB.GC.Dev;
using DSB.GC.Game;
using DSB.GC.Log;
using DSB.GC.RuntimeMessages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// GamingCouch.Clear() must NOT drop the run-scoped
// player-index mapping. The legacy round-reset contract is Clear() followed by
// GetCurrentPlayPlayerOptions()/SetupPlayers WITHOUT a fresh Play(); if Clear() nulled the mapping,
// every subsequent GamingCouchInputs message would be silently dropped (TryValidatePlayerIndex
// returns false) and GameOver() placement would always be rejected (TryValidateGameOverPlacement
// returns false), soft-locking the platform.
public sealed class GamingCouchClearMappingTests
{
    private const string InvalidPlayerIndexWarning =
        "[GC] Diagnostic gc.mapping.invalid_player_index: Player index is outside the active mapping.";

    private readonly List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        GCRuntimeOutput.ResetForTests(() => 1.0);
        GCRuntimeOutput.BeginActiveRun();
        GCLog.logLevel = LogLevel.None;
        ClearGamingCouchInstance();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var unityObject in objectsToDestroy)
        {
            if (unityObject)
            {
                UnityEngine.Object.DestroyImmediate(unityObject);
            }
        }

        objectsToDestroy.Clear();
        GCRuntimeOutput.ResetForTests(null);
        GCLog.logLevel = LogLevel.None;
        ClearGamingCouchInstance();
    }

    [Test]
    public void PlayerInputsStillValidateAndFlowAfterClearWithoutNewPlay()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch clear-mapping input test");
        objectsToDestroy.Add(gamingCouch.gameObject);
        SetPrivateField(
            gamingCouch,
            "playerIndexMapping",
            GCActiveRunProjection.Create(CreatePlayOptions(123, GCPlayerType.player, GCPlayerType.player)).PlayerIndexMapping
        );

        gamingCouch.Clear();

        // The mapping survives Clear(), so platform inputs keep validating for the mapped indices...
        Assert.That(gamingCouch.TryValidatePlayerIndex(0, "test_input", out _), Is.True);
        Assert.That(gamingCouch.TryValidatePlayerIndex(1, "test_input", out _), Is.True);

        // ...while still bounding out-of-range indices (the mapping is genuinely alive, not a stub).
        LogAssert.Expect(LogType.Warning, InvalidPlayerIndexWarning);
        Assert.That(gamingCouch.TryValidatePlayerIndex(9, "test_input", out _), Is.False);

        gamingCouch.ApplyExternalPlayerInput(0, new GCControllerInputsData { a0 = 0.75f, b0 = 1 }, "test_input");

        var inputs = gamingCouch.GetInputsByPlayerIndex(0);
        Assert.That(inputs, Is.Not.Null);
        Assert.That(inputs.RawData.a0, Is.EqualTo(0.75f));
        Assert.That(inputs.RawData.b0, Is.EqualTo(1));
    }

    [Test]
    public void GameOverPlacementStillValidatesAfterClearWithoutNewPlay()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch clear-mapping placement test");
        objectsToDestroy.Add(gamingCouch.gameObject);
        SetPrivateField(
            gamingCouch,
            "playerIndexMapping",
            GCActiveRunProjection.Create(CreatePlayOptions(123, GCPlayerType.player, GCPlayerType.player)).PlayerIndexMapping
        );

        gamingCouch.Clear();

        // Placement validation over the surviving mapping still accepts a complete, unique roster...
        Assert.That(gamingCouch.TryValidateGameOverPlacement(new[] { 0, 1 }, "game_over"), Is.True);

        // ...and still rejects a malformed placement (here: wrong length).
        LogAssert.Expect(LogType.Warning, InvalidPlayerIndexWarning);
        Assert.That(gamingCouch.TryValidateGameOverPlacement(new[] { 0 }, "game_over"), Is.False);
    }

    [Test]
    public void GameOverSubmissionSucceedsAfterClearAndRoundResetWithoutNewPlay()
    {
        var context = CreateRuntimeGame(2);

        // Legacy round-reset: Clear() drops the round's players and inputs (but keeps the run-scoped
        // mapping and the game), then the game re-instantiates its roster via
        // GetCurrentPlayPlayerOptions()/SetupPlayers -- never calling Play() again.
        context.gamingCouch.Clear();
        Assert.That(context.gamingCouch.InternalPlayerStore.Players.Count, Is.EqualTo(0));
        ReAddPlayers(context, 2);

        Assert.That(
            context.gamingCouch.TrySubmitGameOverPlacement(new[] { 0, 1 }, out var gameOverEnvelope),
            Is.True
        );
        Assert.That(gameOverEnvelope, Does.Contain("\"type\":\"gc.game\",\"name\":\"game_over\""));
        Assert.That(gameOverEnvelope, Does.Contain("\"playersByPlacement\":[0,1]"));
        Assert.That(context.gamingCouch.Status, Is.EqualTo(GCStatus.GameOver));
    }

    private RuntimeGameContext CreateRuntimeGame(int playerCount)
    {
        var gameObject = new GameObject("Gaming Couch");
        objectsToDestroy.Add(gameObject);
        gameObject.SetActive(false);
        var gamingCouch = gameObject.AddComponent<GamingCouch>();
        gamingCouch.LogLevel = LogLevel.None;
        gameObject.SetActive(true);

        var store = gamingCouch.InternalPlayerStore;
        var game = new GCGame(gamingCouch, store, new GCGameSetupOptions
        {
            placementCriteria = new[] { GCPlacementSortCriteria.ScoreDescending },
        });
        SetPrivateField(gamingCouch, "game", game);
        SetPrivateField(gamingCouch, "status", GCStatus.Playing);
        SetPrivateField(gamingCouch, "playerIndexMapping", CreatePlayerIndexMapping(playerCount));

        for (var index = 0; index < playerCount; index++)
        {
            var player = CreatePlayer(index);
            game.SetupPlayer(player);
            store.AddPlayer(player);
        }

        return new RuntimeGameContext(gamingCouch, game);
    }

    private void ReAddPlayers(RuntimeGameContext context, int playerCount)
    {
        var store = context.gamingCouch.InternalPlayerStore;
        for (var index = 0; index < playerCount; index++)
        {
            var player = CreatePlayer(index);
            context.game.SetupPlayer(player);
            store.AddPlayer(player);
        }
    }

    private GCPlayer CreatePlayer(int playerIndex)
    {
        var gameObject = new GameObject("Player " + playerIndex);
        objectsToDestroy.Add(gameObject);
        var player = gameObject.AddComponent<GCPlayer>();
        player._InternalGamingCouchSetup(new GCPlayerSetupOptions
        {
            playerIndex = playerIndex,
            type = GCPlayerType.player,
            colorEnum = GCPlayerColor.blue,
            colorName = "blue",
        });
        return player;
    }

    private static GCPlayerIndexMapping CreatePlayerIndexMapping(int playerCount)
    {
        var players = new GCPlayerOptions[playerCount];
        var seats = new GCSeatIdentity[playerCount];
        for (var index = 0; index < playerCount; index++)
        {
            players[index] = new GCPlayerOptions
            {
                playerIndex = index,
                type = "player",
                color = "blue",
            };
            seats[index] = new GCSeatIdentity
            {
                sourceSeatIndex = index + 1,
                stableKey = (index + 1).ToString(),
                playerType = GCPlayerType.player,
                playerColor = GCPlayerColor.blue,
            };
        }

        return GCPlayerIndexMapping.Create(new GCPlayOptions
        {
            players = players,
            seed = 123,
            usesProvidedPlayerIndexMapping = true,
        }, seats);
    }

    private static GCPlayOptions CreatePlayOptions(int seed, params GCPlayerType[] playerTypes)
    {
        var players = new GCPlayerOptions[playerTypes.Length];
        for (var index = 0; index < playerTypes.Length; index++)
        {
            players[index] = new GCPlayerOptions
            {
                playerIndex = index,
                type = playerTypes[index].ToString(),
                color = GCPlayerColor.blue.ToString(),
            };
        }

        return new GCPlayOptions
        {
            players = players,
            seed = seed,
        };
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target
            .GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static void ClearGamingCouchInstance()
    {
        typeof(GamingCouch)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, null);
    }

    private sealed class RuntimeGameContext
    {
        internal readonly GamingCouch gamingCouch;
        internal readonly GCGame game;

        internal RuntimeGameContext(GamingCouch gamingCouch, GCGame game)
        {
            this.gamingCouch = gamingCouch;
            this.game = game;
        }
    }
}
