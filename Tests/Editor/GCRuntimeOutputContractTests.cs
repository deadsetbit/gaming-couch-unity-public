using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using DSB.GC;
using DSB.GC.Dev;
using DSB.GC.Game;
using DSB.GC.Hud;
using DSB.GC.Log;
using DSB.GC.RuntimeMessages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GCRuntimeOutputContractTests
{
    private readonly List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();
    private double nowSeconds;
    private float previousTimeScale;

    [SetUp]
    public void SetUp()
    {
        previousTimeScale = Time.timeScale;
        nowSeconds = 1.0;
        GCRuntimeOutput.ResetForTests(() => nowSeconds);
        GCRuntimeOutput.BeginActiveRun();
        GCLog.logLevel = LogLevel.None;
        GamingCouchEditorTestSupport.ClearGamingCouchInstance();
    }

    [TearDown]
    public void TearDown()
    {
        GamingCouchEditorTestSupport.DestroyTrackedObjects(objectsToDestroy);
        Time.timeScale = previousTimeScale;
        GCRuntimeOutput.ResetForTests(null);
        GCLog.logLevel = LogLevel.None;
        GamingCouchEditorTestSupport.ClearGamingCouchInstance();
    }

    [Test]
    public void PlayerStateChangeEmitsTransitionBeforeOneCoalescedSnapshot()
    {
        var context = CreateRuntimeGame(2);
        var emitted = new List<string>();
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;
        nowSeconds = 1.125;

        context.players[0].SetScore(10, "score reason");
        context.players[0].SetLives(2, "lives reason");
        context.gamingCouch.FlushRuntimeOutput();

        Assert.That(emitted, Has.Count.EqualTo(1));
        var json = emitted[0];
        Assert.That(json, Does.Contain("\"type\":\"runtime_messages\""));
        Assert.That(json, Does.Contain("\"v\":1"));
        Assert.That(json, Does.Contain("\"type\":\"gc.player\",\"name\":\"score_changed\""));
        Assert.That(json, Does.Contain("\"type\":\"gc.player\",\"name\":\"score_changed\",\"seq\":1,\"ms\":125,\"playerIndex\":0,\"data\":{\"from\":0"));
        Assert.That(json, Does.Contain("\"from\":0"));
        Assert.That(json, Does.Contain("\"to\":10"));
        Assert.That(json, Does.Contain("\"reason\":\"score reason\""));
        Assert.That(json, Does.Contain("\"type\":\"gc.player\",\"name\":\"lives_changed\""));
        Assert.That(json, Does.Contain("\"type\":\"gc.state\",\"name\":\"snapshot\""));
        Assert.That(json.IndexOf("\"type\":\"gc.player\",\"name\":\"score_changed\""), Is.LessThan(json.IndexOf("\"type\":\"gc.state\",\"name\":\"snapshot\"")));
        Assert.That(CountOccurrences(json, "\"type\":\"gc.state\",\"name\":\"snapshot\""), Is.EqualTo(1));
        Assert.That(json, Does.Contain("\"seq\":1"));
        Assert.That(json, Does.Contain("\"seq\":2"));
        Assert.That(json, Does.Contain("\"seq\":3"));
        Assert.That(json, Does.Contain("\"ms\":125"));
    }

    [Test]
    public void RuntimeMessageTimestampsFollowUnscaledRealTimeWhileGameTimestampsFollowTheScaledClock()
    {
        const double realWaitSeconds = 0.05;
        var context = CreateRuntimeGame(1);
        var emitted = new List<string>();

        // Real clock, not an injected one: a runtime clock reading scaled time would stamp about
        // eight times the wall-clock wait here.
        Time.timeScale = 8f;
        GCRuntimeOutput.ResetForTests(null);
        GCRuntimeOutput.BeginActiveRun();
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;
        var runStartRealtimeSeconds = Time.realtimeSinceStartupAsDouble;
        var gameTimeBeforeWait = Time.time;

        while (Time.realtimeSinceStartupAsDouble - runStartRealtimeSeconds < realWaitSeconds)
        {
        }

        context.players[0].SetEliminatedRevokable("pit");
        context.gamingCouch.FlushRuntimeOutput();
        var elapsedRealtimeMs = (Time.realtimeSinceStartupAsDouble - runStartRealtimeSeconds) * 1000.0;

        Assert.That(emitted, Has.Count.EqualTo(1));
        var runtimeTimeMs = ReadFirstRuntimeTimeMs(emitted[0]);
        Assert.That(runtimeTimeMs, Is.GreaterThanOrEqualTo(45));
        Assert.That(runtimeTimeMs, Is.LessThanOrEqualTo(elapsedRealtimeMs + 25.0));
        Assert.That(context.players[0].LastSetEliminatedGameTime, Is.EqualTo(Time.time));
        Assert.That(Time.time, Is.EqualTo(gameTimeBeforeWait));
    }

    [Test]
    public void EliminateThenRespawnBeforeFlushEmitsOrderedTransitionsAndLatestProjection()
    {
        var context = CreateRuntimeGame(2);
        var emitted = new List<string>();
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;

        context.players[0].SetEliminatedRevokable("pit");
        context.players[0].SetRevokeEliminated("respawn");
        context.gamingCouch.FlushRuntimeOutput();

        Assert.That(emitted, Has.Count.EqualTo(1));
        var json = emitted[0];
        Assert.That(CountOccurrences(json, "\"type\":\"gc.player\",\"name\":\"elimination_changed\""), Is.EqualTo(2));
        Assert.That(CountOccurrences(json, "\"type\":\"gc.state\",\"name\":\"snapshot\""), Is.EqualTo(1));
        AssertMessageOrder(
            json,
            "\"type\":\"gc.player\",\"name\":\"elimination_changed\",\"seq\":1",
            "\"type\":\"gc.player\",\"name\":\"elimination_changed\",\"seq\":2",
            "\"type\":\"gc.state\",\"name\":\"snapshot\",\"seq\":3"
        );
        Assert.That(
            json,
            Does.Contain(
                "\"data\":{\"game\":{\"status\":\"playing\"},\"players\":[{\"playerIndex\":0,\"score\":0,\"lives\":0,\"status\":\"Neutral\",\"text\":\"\",\"meter\":-1,\"placement\":1,\"elimination\":\"None\",\"finish\":\"None\"}"
            )
        );
        AssertMessageOrder(
            json,
            "\"from\":\"None\",\"to\":\"Revokable\",\"reason\":\"pit\"",
            "\"from\":\"Revokable\",\"to\":\"None\",\"reason\":\"respawn\"",
            "\"type\":\"gc.state\",\"name\":\"snapshot\""
        );

        var snapshot = context.gamingCouch.BuildRuntimeStateSnapshotPayload();
        GamingCouchEditorTestSupport.AssertSnapshotPlayer(
            snapshot.players[0],
            playerIndex: 0,
            score: 0,
            lives: 0,
            status: "Neutral",
            statusText: "",
            meter: -1,
            placement: 1,
            eliminationState: "None",
            finishState: "None"
        );
    }

    [Test]
    public void MeterChangesBeforeFlushEmitOrderedTransitionsAndOneLatestProjection()
    {
        var context = CreateRuntimeGame(1);
        var emitted = new List<string>();
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;

        context.players[0].SetMeter(10, "charge");
        context.players[0].SetMeter(25, "boost");
        context.players[0].SetMeter(80, "finish");
        context.gamingCouch.FlushRuntimeOutput();

        Assert.That(emitted, Has.Count.EqualTo(1));
        var json = emitted[0];
        Assert.That(CountOccurrences(json, "\"type\":\"gc.player\",\"name\":\"meter_changed\""), Is.EqualTo(3));
        Assert.That(CountOccurrences(json, "\"type\":\"gc.state\",\"name\":\"snapshot\""), Is.EqualTo(1));
        AssertMessageOrder(
            json,
            "\"type\":\"gc.player\",\"name\":\"meter_changed\",\"seq\":1",
            "\"type\":\"gc.player\",\"name\":\"meter_changed\",\"seq\":2",
            "\"type\":\"gc.player\",\"name\":\"meter_changed\",\"seq\":3",
            "\"type\":\"gc.state\",\"name\":\"snapshot\",\"seq\":4"
        );
        Assert.That(
            json,
            Does.Contain(
                "\"data\":{\"game\":{\"status\":\"playing\"},\"players\":[{\"playerIndex\":0,\"score\":0,\"lives\":0,\"status\":\"Neutral\",\"text\":\"\",\"meter\":80,\"placement\":1,\"elimination\":\"None\",\"finish\":\"None\"}]}"
            )
        );
        AssertMessageOrder(
            json,
            "\"from\":-1,\"to\":10,\"reason\":\"charge\"",
            "\"from\":10,\"to\":25,\"reason\":\"boost\"",
            "\"from\":25,\"to\":80,\"reason\":\"finish\"",
            "\"type\":\"gc.state\",\"name\":\"snapshot\""
        );

        var snapshot = context.gamingCouch.BuildRuntimeStateSnapshotPayload();
        GamingCouchEditorTestSupport.AssertSnapshotPlayer(
            snapshot.players[0],
            playerIndex: 0,
            score: 0,
            lives: 0,
            status: "Neutral",
            statusText: "",
            meter: 80,
            placement: 1,
            eliminationState: "None",
            finishState: "None"
        );
    }

    [Test]
    public void DuplicateScalarAndMeterHotPathCallsDoNotQueueRuntimeOutput()
    {
        var context = CreateRuntimeGame(1);
        var emitted = new List<string>();
        var callbackCount = 0;
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;
        context.players[0].OnScoreChanged += (oldValue, value, reason) => callbackCount++;
        context.players[0].OnLivesChanged += (oldValue, value, reason) => callbackCount++;
        context.players[0].OnMeterChanged += (oldValue, value, reason) => callbackCount++;

        context.gamingCouch.FlushRuntimeOutput();
        emitted.Clear();

        for (var index = 0; index < 200; index++)
        {
            context.players[0].SetScore(0, "duplicate score");
            context.players[0].SetLives(0, "duplicate lives");
            context.players[0].SetMeter(-1, "duplicate meter");
        }

        context.gamingCouch.FlushRuntimeOutput();

        Assert.That(callbackCount, Is.EqualTo(0));
        Assert.That(emitted, Is.Empty);
    }

    [Test]
    public void HighFrequencyStatTransitionsFlushInBoundedBatchesAndKeepLatestSnapshotCoalesced()
    {
        var context = CreateRuntimeGame(1);
        var emitted = new List<string>();
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;
        var transitionCount = GCRuntimeMessageOutput.MaxPendingMessagesPerBatch + 5;

        for (var value = 1; value <= transitionCount; value++)
        {
            context.players[0].SetScore(value, "score " + value);
        }

        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(
            CountOccurrences(emitted[0], "\"type\":\"gc.player\",\"name\":\"score_changed\""),
            Is.EqualTo(GCRuntimeMessageOutput.MaxPendingMessagesPerBatch)
        );
        Assert.That(emitted[0], Does.Not.Contain("\"type\":\"gc.state\",\"name\":\"snapshot\""));
        AssertMessageOrder(
            emitted[0],
            "\"type\":\"gc.player\",\"name\":\"score_changed\",\"seq\":1",
            "\"type\":\"gc.player\",\"name\":\"score_changed\",\"seq\":" +
                GCRuntimeMessageOutput.MaxPendingMessagesPerBatch
        );

        context.gamingCouch.FlushRuntimeOutput();

        Assert.That(emitted, Has.Count.EqualTo(2));
        Assert.That(
            CountOccurrences(emitted[1], "\"type\":\"gc.player\",\"name\":\"score_changed\""),
            Is.EqualTo(5)
        );
        Assert.That(CountOccurrences(emitted[1], "\"type\":\"gc.state\",\"name\":\"snapshot\""), Is.EqualTo(1));
        AssertMessageOrder(
            emitted[1],
            "\"type\":\"gc.player\",\"name\":\"score_changed\",\"seq\":" +
                (GCRuntimeMessageOutput.MaxPendingMessagesPerBatch + 1),
            "\"type\":\"gc.player\",\"name\":\"score_changed\",\"seq\":" + transitionCount,
            "\"type\":\"gc.state\",\"name\":\"snapshot\",\"seq\":" + (transitionCount + 1)
        );
        Assert.That(
            emitted[1],
            Does.Contain(
                "\"data\":{\"game\":{\"status\":\"playing\"},\"players\":[{\"playerIndex\":0,\"score\":" +
                    transitionCount
            )
        );
    }

    [Test]
    public void GameOverAfterBoundedTransitionFlushKeepsFinalSnapshotBeforeGameOver()
    {
        var context = CreateRuntimeGame(2);
        var emitted = new List<string>();
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;
        var transitionCount = GCRuntimeMessageOutput.MaxPendingMessagesPerBatch + 1;

        for (var value = 1; value <= transitionCount; value++)
        {
            context.players[0].SetScore(value, "score " + value);
        }

        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(
            CountOccurrences(emitted[0], "\"type\":\"gc.player\",\"name\":\"score_changed\""),
            Is.EqualTo(GCRuntimeMessageOutput.MaxPendingMessagesPerBatch)
        );
        Assert.That(emitted[0], Does.Not.Contain("\"type\":\"gc.state\",\"name\":\"snapshot\""));
        Assert.That(emitted[0], Does.Not.Contain("\"type\":\"gc.game\",\"name\":\"game_over\""));

        Assert.That(context.gamingCouch.TrySubmitGameOverPlacement(new[] { 0, 1 }, out var gameOverEnvelope), Is.True);

        Assert.That(emitted, Has.Count.EqualTo(2));
        Assert.That(gameOverEnvelope, Is.EqualTo(emitted[1]));
        Assert.That(CountOccurrences(gameOverEnvelope, "\"type\":\"gc.player\",\"name\":\"score_changed\""), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameOverEnvelope, "\"type\":\"gc.state\",\"name\":\"snapshot\""), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameOverEnvelope, "\"type\":\"gc.game\",\"name\":\"game_over\""), Is.EqualTo(1));
        AssertMessageOrder(
            gameOverEnvelope,
            "\"type\":\"gc.player\",\"name\":\"score_changed\",\"seq\":" + transitionCount,
            "\"type\":\"gc.state\",\"name\":\"snapshot\",\"seq\":" + (transitionCount + 1),
            "\"type\":\"gc.game\",\"name\":\"game_over\",\"seq\":" + (transitionCount + 2)
        );
        Assert.That(
            gameOverEnvelope,
            Does.Contain(
                "\"data\":{\"game\":{\"status\":\"game_over\"},\"players\":[{\"playerIndex\":0,\"score\":" +
                    transitionCount
            )
        );
    }

    [Test]
    public void RuntimeTransitionReasonTextIsBoundedWithoutChangingPublicReasonText()
    {
        var context = CreateRuntimeGame(1);
        var emitted = new List<string>();
        var publicReasons = new List<string>();
        var boundedReason = new string('r', GCRuntimePayloadBounds.MaxReasonTextLength);
        var longReason = boundedReason + "overflow";
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;
        context.players[0].OnMeterChanged += (oldValue, value, reason) => publicReasons.Add(reason);

        context.players[0].SetMeter(50, longReason);
        context.gamingCouch.FlushRuntimeOutput();

        Assert.That(publicReasons, Is.EqualTo(new[] { longReason }));
        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(emitted[0], Does.Contain("\"type\":\"gc.player\",\"name\":\"meter_changed\""));
        Assert.That(emitted[0], Does.Contain("\"reason\":\"" + boundedReason + "\""));
        Assert.That(emitted[0], Does.Not.Contain("overflow"));
    }

    [Test]
    public void DiagnosticFlushesQueuedRuntimeMessagesInSequenceOrder()
    {
        var context = CreateRuntimeGame(1);
        var emitted = new List<string>();
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;
        nowSeconds = 1.125;

        context.players[0].SetScore(10, "score reason");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.clamped_value: Lives were clamped.");
        context.players[0].SetLives(-1, "invalid lives");

        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(emitted[0], Does.Contain("\"type\":\"gc.player\",\"name\":\"score_changed\""));
        Assert.That(emitted[0], Does.Contain("\"type\":\"gc.diagnostic\""));
        Assert.That(
            emitted[0].IndexOf("\"type\":\"gc.player\",\"name\":\"score_changed\""),
            Is.LessThan(emitted[0].IndexOf("\"type\":\"gc.diagnostic\""))
        );
        Assert.That(emitted[0], Does.Contain("\"seq\":1"));
        Assert.That(emitted[0], Does.Contain("\"seq\":2"));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void SnapshotBootToggleLeavesTransitionsEnabled()
    {
        GCRuntimeOutput.BeginActiveRun(new GCRuntimeOutputOptions
        {
            stateSnapshots = false,
            screenSpace = true,
        });
        var context = CreateRuntimeGame(1);
        var emitted = new List<string>();
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;

        context.players[0].SetMeter(50, "meter reason");
        context.gamingCouch.FlushRuntimeOutput();

        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(emitted[0], Does.Contain("\"type\":\"gc.player\",\"name\":\"meter_changed\""));
        Assert.That(emitted[0], Does.Not.Contain("\"type\":\"gc.state\",\"name\":\"snapshot\""));
    }

    [Test]
    public void GameOverPlacementSubmitsObjectPayloadAfterPendingSnapshotAndAcceptsOnlyFirst()
    {
        var context = CreateRuntimeGame(2);
        var emitted = new List<string>();
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;

        Assert.That(context.gamingCouch.TrySubmitGameOverPlacement(new[] { 0, 1 }, out var firstEnvelope), Is.True);

        Assert.That(firstEnvelope, Is.EqualTo(emitted[0]));
        Assert.That(firstEnvelope, Does.Contain("\"type\":\"gc.state\",\"name\":\"snapshot\""));
        Assert.That(firstEnvelope, Does.Contain("\"type\":\"gc.game\",\"name\":\"game_over\""));
        Assert.That(firstEnvelope, Does.Contain("\"data\":{\"playersByPlacement\":[0,1]}"));
        Assert.That(firstEnvelope.IndexOf("\"type\":\"gc.state\",\"name\":\"snapshot\""), Is.LessThan(firstEnvelope.IndexOf("\"type\":\"gc.game\",\"name\":\"game_over\"")));

        LogAssert.Expect(LogType.Error, "[GC] Diagnostic gc.runtime.invalid_game_over_placement: Game-over placement was already accepted for this active run.");
        Assert.That(context.gamingCouch.TrySubmitGameOverPlacement(new[] { 0, 1 }, out var secondEnvelope), Is.False);
        Assert.That(secondEnvelope, Is.Null);
        Assert.That(emitted, Has.Count.EqualTo(2));
        Assert.That(emitted[1], Does.Contain("\"type\":\"gc.diagnostic\""));
        Assert.That(emitted[1], Does.Contain("\"name\":\"gc.runtime.invalid_game_over_placement\""));
        Assert.That(emitted[1], Does.Not.Contain("\"type\":\"gc.game\",\"name\":\"game_over\""));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void GameOverPlacementFlushesPendingTransitionsAndFinalSnapshotBeforeGameOver()
    {
        var context = CreateRuntimeGame(2);
        var emitted = new List<string>();
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;

        context.players[0].SetEliminatedRevokable("pit");
        context.players[0].SetRevokeEliminated("respawn");
        context.players[1].SetMeter(90, "finish charge");

        Assert.That(context.gamingCouch.TrySubmitGameOverPlacement(new[] { 0, 1 }, out var gameOverEnvelope), Is.True);

        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(gameOverEnvelope, Is.EqualTo(emitted[0]));
        Assert.That(CountOccurrences(gameOverEnvelope, "\"type\":\"gc.player\",\"name\":\"elimination_changed\""), Is.EqualTo(2));
        Assert.That(CountOccurrences(gameOverEnvelope, "\"type\":\"gc.player\",\"name\":\"meter_changed\""), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameOverEnvelope, "\"type\":\"gc.state\",\"name\":\"snapshot\""), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameOverEnvelope, "\"type\":\"gc.game\",\"name\":\"game_over\""), Is.EqualTo(1));
        AssertMessageOrder(
            gameOverEnvelope,
            "\"type\":\"gc.player\",\"name\":\"elimination_changed\",\"seq\":1",
            "\"type\":\"gc.player\",\"name\":\"elimination_changed\",\"seq\":2",
            "\"type\":\"gc.player\",\"name\":\"meter_changed\",\"seq\":3",
            "\"type\":\"gc.state\",\"name\":\"snapshot\",\"seq\":4",
            "\"type\":\"gc.game\",\"name\":\"game_over\",\"seq\":5"
        );
        AssertMessageOrder(
            gameOverEnvelope,
            "\"from\":\"None\",\"to\":\"Revokable\",\"reason\":\"pit\"",
            "\"from\":\"Revokable\",\"to\":\"None\",\"reason\":\"respawn\"",
            "\"from\":-1,\"to\":90,\"reason\":\"finish charge\"",
            "\"data\":{\"game\":{\"status\":\"game_over\"},\"players\":[{\"playerIndex\":0,\"score\":0,\"lives\":0,\"status\":\"Neutral\",\"text\":\"\",\"meter\":-1,\"placement\":1,\"elimination\":\"None\",\"finish\":\"None\"},{\"playerIndex\":1,\"score\":0,\"lives\":0,\"status\":\"Neutral\",\"text\":\"\",\"meter\":90,\"placement\":2,\"elimination\":\"None\",\"finish\":\"None\"}]}",
            "\"data\":{\"playersByPlacement\":[0,1]}"
        );
        Assert.That(gameOverEnvelope, Does.Not.Contain("\"data\":[0,1]"));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void GameOverPlacementRejectsReentrantSubmissionBeforePublishingSecondResult()
    {
        var context = CreateRuntimeGame(2);
        var emitted = new List<string>();
        var reentered = false;
        GCRuntimeOutput.RuntimeMessagesEmitted += json =>
        {
            emitted.Add(json);
            if (reentered || !json.Contains("\"type\":\"gc.game\",\"name\":\"game_over\""))
            {
                return;
            }

            reentered = true;
            LogAssert.Expect(LogType.Error, "[GC] Diagnostic gc.runtime.invalid_game_over_placement: Game-over placement was already accepted for this active run.");
            Assert.That(context.gamingCouch.TrySubmitGameOverPlacement(new[] { 1, 0 }, out var reentrantEnvelope), Is.False);
            Assert.That(reentrantEnvelope, Is.Null);
        };

        Assert.That(context.gamingCouch.TrySubmitGameOverPlacement(new[] { 0, 1 }, out var firstEnvelope), Is.True);

        Assert.That(firstEnvelope, Is.EqualTo(emitted[0]));
        Assert.That(emitted, Has.Count.EqualTo(2));
        Assert.That(emitted[0], Does.Contain("\"data\":{\"playersByPlacement\":[0,1]}"));
        Assert.That(emitted[1], Does.Contain("\"name\":\"gc.runtime.invalid_game_over_placement\""));
        Assert.That(emitted[1], Does.Not.Contain("\"data\":{\"playersByPlacement\":[1,0]}"));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void RuntimeOutputFacadeOwnsGameOverPlacementOrderingAndFirstAcceptedWins()
    {
        var emitted = new List<string>();
        var acceptedCallbackCount = 0;
        var reentered = false;
        GCRuntimeOutput.RuntimeMessagesEmitted += json =>
        {
            emitted.Add(json);
            if (reentered || !json.Contains("\"type\":\"gc.game\",\"name\":\"game_over\""))
            {
                return;
            }

            reentered = true;
            LogAssert.Expect(LogType.Error, "[GC] Diagnostic gc.runtime.invalid_game_over_placement: Game-over placement was already accepted for this active run.");
            Assert.That(
                GCRuntimeOutput.TrySubmitGameOverPlacement(
                    new[] { 1, 0 },
                    2,
                    _ => true,
                    () => acceptedCallbackCount++,
                    () => "{\"game\":{\"status\":\"game_over\"},\"players\":[]}",
                    out var reentrantEnvelope
                ),
                Is.False
            );
            Assert.That(reentrantEnvelope, Is.Null);
        };

        GCRuntimeOutput.QueueStateSnapshot();
        Assert.That(
            GCRuntimeOutput.TrySubmitGameOverPlacement(
                new[] { 0, 1 },
                2,
                _ => true,
                () => acceptedCallbackCount++,
                () => "{\"game\":{\"status\":\"game_over\"},\"players\":[]}",
                out var firstEnvelope
            ),
            Is.True
        );

        Assert.That(firstEnvelope, Is.EqualTo(emitted[0]));
        Assert.That(acceptedCallbackCount, Is.EqualTo(1));
        Assert.That(emitted, Has.Count.EqualTo(2));
        Assert.That(emitted[0], Does.Contain("\"type\":\"gc.state\",\"name\":\"snapshot\""));
        Assert.That(emitted[0], Does.Contain("\"type\":\"gc.game\",\"name\":\"game_over\""));
        Assert.That(emitted[0].IndexOf("\"type\":\"gc.state\",\"name\":\"snapshot\""), Is.LessThan(emitted[0].IndexOf("\"type\":\"gc.game\",\"name\":\"game_over\"")));
        Assert.That(emitted[1], Does.Contain("\"name\":\"gc.runtime.invalid_game_over_placement\""));
        Assert.That(emitted[1], Does.Not.Contain("\"data\":{\"playersByPlacement\":[1,0]}"));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void RuntimeOutputFacadeRejectsReentrantGameOverPlacementDuringAcceptCallback()
    {
        var emitted = new List<string>();
        var acceptedCallbackCount = 0;
        string reentrantEnvelope = null;
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;

        LogAssert.Expect(LogType.Error, "[GC] Diagnostic gc.runtime.invalid_game_over_placement: Game-over placement was already accepted for this active run.");
        Assert.That(
            GCRuntimeOutput.TrySubmitGameOverPlacement(
                new[] { 0, 1 },
                2,
                _ => true,
                () =>
                {
                    acceptedCallbackCount++;
                    Assert.That(
                        GCRuntimeOutput.TrySubmitGameOverPlacement(
                            new[] { 1, 0 },
                            2,
                            _ => true,
                            () => acceptedCallbackCount++,
                            () => "{\"game\":{\"status\":\"game_over\"},\"players\":[]}",
                            out reentrantEnvelope
                        ),
                        Is.False
                    );
                },
                () => "{\"game\":{\"status\":\"game_over\"},\"players\":[]}",
                out var firstEnvelope
            ),
            Is.True
        );

        Assert.That(reentrantEnvelope, Is.Null);
        Assert.That(acceptedCallbackCount, Is.EqualTo(1));
        Assert.That(emitted, Has.Count.EqualTo(2));
        Assert.That(emitted[0], Does.Contain("\"name\":\"gc.runtime.invalid_game_over_placement\""));
        Assert.That(emitted[0], Does.Not.Contain("\"data\":{\"playersByPlacement\":[1,0]}"));
        Assert.That(firstEnvelope, Is.EqualTo(emitted[1]));
        Assert.That(firstEnvelope, Does.Contain("\"data\":{\"playersByPlacement\":[0,1]}"));
        Assert.That(firstEnvelope, Does.Not.Contain("\"data\":{\"playersByPlacement\":[1,0]}"));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void RuntimeOutputFacadeRejectsReentrantGameOverPlacementDuringValidationCallback()
    {
        var emitted = new List<string>();
        var acceptedCallbackCount = 0;
        string reentrantEnvelope = null;
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;

        LogAssert.Expect(LogType.Error, "[GC] Diagnostic gc.runtime.invalid_game_over_placement: Game-over placement was already accepted for this active run.");
        Assert.That(
            GCRuntimeOutput.TrySubmitGameOverPlacement(
                new[] { 0, 1 },
                2,
                _ =>
                {
                    Assert.That(
                        GCRuntimeOutput.TrySubmitGameOverPlacement(
                            new[] { 1, 0 },
                            2,
                            __ => true,
                            () => acceptedCallbackCount++,
                            () => "{\"game\":{\"status\":\"game_over\"},\"players\":[]}",
                            out reentrantEnvelope
                        ),
                        Is.False
                    );
                    return true;
                },
                () => acceptedCallbackCount++,
                () => "{\"game\":{\"status\":\"game_over\"},\"players\":[]}",
                out var firstEnvelope
            ),
            Is.True
        );

        Assert.That(reentrantEnvelope, Is.Null);
        Assert.That(acceptedCallbackCount, Is.EqualTo(1));
        Assert.That(emitted, Has.Count.EqualTo(2));
        Assert.That(emitted[0], Does.Contain("\"name\":\"gc.runtime.invalid_game_over_placement\""));
        Assert.That(emitted[0], Does.Not.Contain("\"data\":{\"playersByPlacement\":[1,0]}"));
        Assert.That(firstEnvelope, Is.EqualTo(emitted[1]));
        Assert.That(firstEnvelope, Does.Contain("\"data\":{\"playersByPlacement\":[0,1]}"));
        Assert.That(firstEnvelope, Does.Not.Contain("\"data\":{\"playersByPlacement\":[1,0]}"));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void GameOverPlacementPayloadRejectsMissingDuplicateAndOutOfRangeIndices()
    {
        Assert.Throws<System.ArgumentException>(() => GCRuntimeGameOverPlacementPayload.BuildJson(new[] { 0 }, 2));
        Assert.Throws<System.ArgumentException>(() => GCRuntimeGameOverPlacementPayload.BuildJson(new[] { 0, 0 }, 2));
        Assert.Throws<System.ArgumentException>(() => GCRuntimeGameOverPlacementPayload.BuildJson(new[] { 0, 2 }, 2));
        Assert.That(
            GCRuntimeGameOverPlacementPayload.BuildJson(new[] { 0, 1 }, 2),
            Is.EqualTo("{\"playersByPlacement\":[0,1]}")
        );
    }

    [Test]
    public void ScreenSpaceEmitsValidatedV1AnchorsAndKeepsNameTagsOutOfContract()
    {
        CreateRuntimeGame(1);
        var emitted = new List<string>();
        GCRuntimeOutput.ScreenSpaceEmitted += emitted.Add;
        var hud = new GCHud();

        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "playerOverhead",
            playerIndex = 0,
            x = 1.2f,
            y = -0.25f,
            isOffScreen = true,
        });
        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "playerPosition",
            playerIndex = 0,
            x = 0.4f,
            y = 0.5f,
            isOffScreen = false,
        });
        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "name",
            playerIndex = 0,
            x = 0.4f,
            y = 0.5f,
            isOffScreen = false,
        });

        hud.HandleQueue();

        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(emitted[0], Does.Contain("\"type\":\"screen_space\""));
        Assert.That(emitted[0], Does.Contain("\"v\":1"));
        Assert.That(emitted[0], Does.Contain("\"frame\":"));
        Assert.That(emitted[0], Does.Contain("\"ms\":0"));
        Assert.That(emitted[0], Does.Contain("\"type\":\"playerOverhead\""));
        Assert.That(emitted[0], Does.Contain("\"type\":\"playerPosition\""));
        Assert.That(emitted[0], Does.Contain("\"playerIndex\":0"));
        Assert.That(emitted[0], Does.Contain("\"x\":1"));
        Assert.That(emitted[0], Does.Contain("\"y\":0"));
        Assert.That(emitted[0], Does.Contain("\"offscreen\":true"));
        Assert.That(emitted[0], Does.Not.Contain("\"type\":\"name\""));
    }

    [Test]
    public void WebGLJslibExportsCanonicalScreenSpaceBridge()
    {
        var bridgePath = Path.Combine(GamingCouchEditorTestSupport.FindPackageRootPath(), "Plugins", "GamingCouch.jslib");
        var bridge = File.ReadAllText(bridgePath);

        Assert.That(bridge, Does.Contain("GamingCouchScreenSpace: function (screenSpaceJsonString)"));
        Assert.That(bridge, Does.Contain("window.gamingCouchScreenSpace"));
        Assert.That(bridge, Does.Contain("JSON.parse(UTF8ToString(screenSpaceJsonString))"));
        Assert.That(bridge, Does.Contain("window.gamingCouchScreenSpace(screenSpace);"));
    }

    [Test]
    public void CurrentWebGLRuntimeSourcesDoNotEmitLegacyHudOrGameOverBridges()
    {
        var packageRootPath = GamingCouchEditorTestSupport.FindPackageRootPath();
        var bridgeSource = File.ReadAllText(Path.Combine(packageRootPath, "Plugins", "GamingCouch.jslib"));
        var hudSource = File.ReadAllText(Path.Combine(packageRootPath, "Runtime", "Hud", "GCHud.cs"));
        var nameTagSource = File.ReadAllText(Path.Combine(packageRootPath, "Runtime", "Hud", "GCNameTag.cs"));
        var runtimeSource = File.ReadAllText(Path.Combine(packageRootPath, "Runtime", "GamingCouch.cs"));

        Assert.That(bridgeSource, Does.Not.Contain("GamingCouchUpdatePlayersHud"));
        Assert.That(bridgeSource, Does.Not.Contain("GamingCouchUpdateScreenPointHud"));
        Assert.That(bridgeSource, Does.Not.Contain("GamingCouchGameEnd"));
        Assert.That(hudSource, Does.Not.Contain("GamingCouchUpdatePlayersHud"));
        Assert.That(hudSource, Does.Not.Contain("GamingCouchUpdateScreenPointHud"));
        Assert.That(nameTagSource, Does.Not.Contain("type = \"name\""));
        Assert.That(nameTagSource, Does.Contain("type = \"playerOverhead\""));
        Assert.That(runtimeSource, Does.Not.Contain("GamingCouchGameEnd"));
    }

    [Test]
    public void LegacyHudUpdateApisAreCompileTimeErrorsWithMigrationMessages()
    {
        var updatePlayersObsolete = typeof(GCHud)
            .GetMethod("UpdatePlayers")
            .GetCustomAttribute<ObsoleteAttribute>();
        var updateScreenPointHudObsolete = typeof(GCHud)
            .GetMethod("UpdateScreenPointHud")
            .GetCustomAttribute<ObsoleteAttribute>();

        Assert.That(updatePlayersObsolete, Is.Not.Null);
        Assert.That(updatePlayersObsolete.IsError, Is.True);
        Assert.That(updatePlayersObsolete.Message, Does.Contain("Use GCPlayer score/lives/status/meter APIs"));
        Assert.That(updatePlayersObsolete.Message, Does.Contain("runtime_messages state snapshots"));

        Assert.That(updateScreenPointHudObsolete, Is.Not.Null);
        Assert.That(updateScreenPointHudObsolete.IsError, Is.True);
        Assert.That(updateScreenPointHudObsolete.Message, Does.Contain("Use QueuePointData"));
    }

    [Test]
    public void ScreenSpaceRejectsDuplicateAnchorPairs()
    {
        CreateRuntimeGame(1);
        var hud = new GCHud();
        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "playerOverhead",
            playerIndex = 0,
            x = 0.1f,
            y = 0.2f,
            isOffScreen = false,
        });
        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "playerOverhead",
            playerIndex = 0,
            x = 0.3f,
            y = 0.4f,
            isOffScreen = false,
        });

        LogAssert.Expect(
            LogType.Warning,
            new Regex(@"\[GC\] Diagnostic gc\.runtime\.malformed_screen_space: Malformed screen-space output was rejected\.")
        );
        hud.HandleQueue();
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ScreenSpaceRejectsOutOfRangePlayerIndexBeforeQueueingAnchor()
    {
        CreateRuntimeGame(1);
        var emitted = new List<string>();
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;
        GCRuntimeOutput.ScreenSpaceEmitted += emitted.Add;
        var hud = new GCHud();

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.mapping.invalid_player_index: Player index is outside the active mapping.");
        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "playerOverhead",
            playerIndex = 1,
            x = 0.5f,
            y = 0.5f,
            isOffScreen = false,
        });
        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "playerOverhead",
            playerIndex = 0,
            x = 0.5f,
            y = 0.5f,
            isOffScreen = false,
        });
        hud.HandleQueue();

        Assert.That(emitted, Has.Count.EqualTo(2));
        Assert.That(emitted[0], Does.Contain("\"name\":\"gc.mapping.invalid_player_index\""));
        Assert.That(emitted[1], Does.Contain("\"type\":\"screen_space\""));
        Assert.That(emitted[1], Does.Contain("\"playerIndex\":0"));
        Assert.That(emitted[1], Does.Not.Contain("\"playerIndex\":1"));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void HandleQueueEmitsScreenSpaceOnlyWhenQueueHasAnchors()
    {
        CreateRuntimeGame(1);
        var emitted = new List<string>();
        GCRuntimeOutput.ScreenSpaceEmitted += emitted.Add;
        var hud = new GCHud();

        // An empty queue must not emit a per-frame heartbeat envelope.
        hud.HandleQueue();
        Assert.That(emitted, Is.Empty);

        // A non-empty queue still emits (guards against over-suppression).
        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "playerOverhead",
            playerIndex = 0,
            x = 0.5f,
            y = 0.5f,
            isOffScreen = false,
        });
        hud.HandleQueue();

        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(emitted[0], Does.Contain("\"type\":\"screen_space\""));
    }

    [Test]
    public void SetStatusWithNullTextDoesNotEmitDuplicateTransitionAfterEmptyText()
    {
        var context = CreateRuntimeGame(1);
        var emitted = new List<string>();
        var publicCallbackCount = 0;
        context.players[0].OnStatusChanged += (status, statusText, reason) => publicCallbackCount++;
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;

        context.players[0].SetStatus(GCPlayerStatus.Neutral, null, "same status");
        context.gamingCouch.FlushRuntimeOutput();

        Assert.That(publicCallbackCount, Is.EqualTo(0));
        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(emitted[0], Does.Contain("\"type\":\"gc.state\",\"name\":\"snapshot\""));
        Assert.That(emitted[0], Does.Not.Contain("\"type\":\"gc.player\",\"name\":\"status_changed\""));
    }

    [Test]
    public void PlayersHudAutoUpdateQueuesCanonicalStateSnapshot()
    {
        var context = CreateRuntimeGame(1);
        var emitted = new List<string>();
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;
        context.gamingCouch.FlushRuntimeOutput();
        emitted.Clear();

        GamingCouchEditorTestSupport.SetPrivateField(context.game, "isPlayersHudAutoUpdateEnabled", true);
        GamingCouchEditorTestSupport.SetPrivateField(context.game, "isPlayersHudAutoUpdatePending", true);

        context.game.HandlePlayersHudAutoUpdate();
        context.gamingCouch.FlushRuntimeOutput();

        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(emitted[0], Does.Contain("\"type\":\"gc.state\",\"name\":\"snapshot\""));
        Assert.That(emitted[0], Does.Contain("\"data\":{\"game\":{\"status\":\"playing\"}"));
    }

    [Test]
    public void RuntimeStateSnapshotProjectsEveryPlayerStateFieldWithPlacements()
    {
        var context = CreateRuntimeGame(2);
        context.players[0].SetScore(12, "score");
        context.players[0].SetLives(3, "lives");
        context.players[0].SetStatus(GCPlayerStatus.Warning, "low fuel", "status");
        context.players[0].SetMeter(44, "meter");
        context.players[1].SetScore(7, "score");
        context.players[1].SetEliminatedPermanent("out");
        context.players[1].SetFinishedRevokable("finish");

        var snapshot = context.gamingCouch.BuildRuntimeStateSnapshotPayload();

        Assert.That(snapshot.game.status, Is.EqualTo("playing"));
        Assert.That(snapshot.players, Has.Length.EqualTo(2));
        GamingCouchEditorTestSupport.AssertSnapshotPlayer(
            snapshot.players[0],
            playerIndex: 0,
            score: 12,
            lives: 3,
            status: "Warning",
            statusText: "low fuel",
            meter: 44,
            placement: 1,
            eliminationState: "None",
            finishState: "None"
        );
        GamingCouchEditorTestSupport.AssertSnapshotPlayer(
            snapshot.players[1],
            playerIndex: 1,
            score: 7,
            lives: 0,
            status: "Neutral",
            statusText: "",
            meter: -1,
            placement: 2,
            eliminationState: "Permanent",
            finishState: "Revokable"
        );
    }

    [Test]
    public void PackageInternalConsumersUseAcceptedTransitionsWhenPublicCallbacksAreCleared()
    {
        var context = CreateRuntimeGame(2);
        context.gamingCouch.FlushRuntimeOutput();
        ClearLegacyPlayerCallbacks(context.players[0]);
        ClearLegacyPlayerCallbacks(context.players[1]);

        var emitted = new List<string>();
        GCRuntimeOutput.RuntimeMessagesEmitted += emitted.Add;

        context.players[0].SetScore(10, "score");
        context.players[0].SetLives(2, "lives");
        context.players[0].SetStatus(GCPlayerStatus.Success, "ready", "status");
        context.players[0].SetMeter(50, "meter");
        context.players[1].SetEliminatedPermanent("out");
        context.players[1].SetFinishedRevokable("finish");
        context.gamingCouch.FlushRuntimeOutput();

        Assert.That(emitted, Has.Count.EqualTo(1));
        var json = emitted[0];
        AssertMessageOrder(
            json,
            "\"type\":\"gc.player\",\"name\":\"score_changed\"",
            "\"type\":\"gc.player\",\"name\":\"lives_changed\"",
            "\"type\":\"gc.player\",\"name\":\"status_changed\"",
            "\"type\":\"gc.player\",\"name\":\"meter_changed\"",
            "\"type\":\"gc.player\",\"name\":\"elimination_changed\"",
            "\"type\":\"gc.player\",\"name\":\"finish_changed\"",
            "\"type\":\"gc.state\",\"name\":\"snapshot\""
        );
        Assert.That(
            json,
            Does.Contain("\"data\":{\"from\":{\"status\":\"Neutral\",\"text\":\"\"},\"to\":{\"status\":\"Success\",\"text\":\"ready\"},\"reason\":\"status\"}")
        );
        Assert.That(CountOccurrences(json, "\"type\":\"gc.state\",\"name\":\"snapshot\""), Is.EqualTo(1));

        var store = context.gamingCouch.InternalPlayerStore;
        Assert.That(store.PlayersEliminated, Is.EqualTo(new[] { context.players[1] }));
        Assert.That(store.PlayersEliminatedPermanent, Is.EqualTo(new[] { context.players[1] }));
        Assert.That(store.PlayersFinished, Is.EqualTo(new[] { context.players[1] }));
        Assert.That(store.PlayersFinishedRevokable, Is.EqualTo(new[] { context.players[1] }));

        var snapshot = context.gamingCouch.BuildRuntimeStateSnapshotPayload();
        GamingCouchEditorTestSupport.AssertSnapshotPlayer(
            snapshot.players[0],
            playerIndex: 0,
            score: 10,
            lives: 2,
            status: "Success",
            statusText: "ready",
            meter: 50,
            placement: 1,
            eliminationState: "None",
            finishState: "None"
        );
        GamingCouchEditorTestSupport.AssertSnapshotPlayer(
            snapshot.players[1],
            playerIndex: 1,
            score: 0,
            lives: 0,
            status: "Neutral",
            statusText: "",
            meter: -1,
            placement: 2,
            eliminationState: "Permanent",
            finishState: "Revokable"
        );

        Assert.That(context.gamingCouch.TrySubmitGameOverPlacement(new[] { 0, 1 }, out var gameOverEnvelope), Is.True);
        Assert.That(gameOverEnvelope, Does.Contain("\"type\":\"gc.state\",\"name\":\"snapshot\""));
        Assert.That(gameOverEnvelope, Does.Contain("\"type\":\"gc.game\",\"name\":\"game_over\""));
        Assert.That(gameOverEnvelope, Does.Contain("\"status\":\"game_over\""));
        Assert.That(gameOverEnvelope, Does.Contain("\"playersByPlacement\":[0,1]"));
        AssertMessageOrder(
            gameOverEnvelope,
            "\"type\":\"gc.state\",\"name\":\"snapshot\"",
            "\"type\":\"gc.game\",\"name\":\"game_over\""
        );
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
        GamingCouchEditorTestSupport.SetPrivateField(gamingCouch, "game", game);
        GamingCouchEditorTestSupport.SetPrivateField(gamingCouch, "status", GCStatus.Playing);
        GamingCouchEditorTestSupport.SetPrivateField(gamingCouch, "playerIndexMapping", CreatePlayerIndexMapping(playerCount));

        var players = new GCPlayer[playerCount];
        for (var index = 0; index < playerCount; index++)
        {
            players[index] = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, index);
            game.SetupPlayer(players[index]);
            store.AddPlayer(players[index]);
        }

        gamingCouch.QueueRuntimeStateSnapshot();
        return new RuntimeGameContext(gamingCouch, game, players);
    }

    private static void ClearLegacyPlayerCallbacks(GCPlayer player)
    {
        player.OnEliminationStateChanged = null;
        player.OnFinishStateChanged = null;
        player.OnScoreChanged = null;
        player.OnLivesChanged = null;
        player.OnMeterChanged = null;
        player.OnStatusChanged = null;
        player.OnStatusTransitionChanged = null;
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

    private static long ReadFirstRuntimeTimeMs(string runtimeMessagesJson)
    {
        var match = Regex.Match(runtimeMessagesJson, "\"ms\":(\\d+)");
        Assert.That(match.Success, Is.True, "Expected a runtime message timestamp in " + runtimeMessagesJson);
        return long.Parse(match.Groups[1].Value);
    }

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static void AssertMessageOrder(string value, params string[] needles)
    {
        var previousIndex = -1;

        foreach (var needle in needles)
        {
            var index = value.IndexOf(needle, System.StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Expected output to contain " + needle);
            Assert.That(index, Is.GreaterThan(previousIndex), "Expected " + needle + " to appear in order.");
            previousIndex = index;
        }
    }

    private readonly struct RuntimeGameContext
    {
        internal readonly GamingCouch gamingCouch;
        internal readonly GCGame game;
        internal readonly GCPlayer[] players;

        internal RuntimeGameContext(GamingCouch gamingCouch, GCGame game, GCPlayer[] players)
        {
            this.gamingCouch = gamingCouch;
            this.game = game;
            this.players = players;
        }
    }
}
