using System;
using System.Collections.Generic;
using System.Reflection;
using DSB.GC;
using DSB.GC.Game;
using DSB.GC.Log;
using DSB.GC.RuntimeMessages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GCPlayerStateModelTests
{
    private readonly List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        GCRuntimeMessageOutput.ResetForTests(() => 0);
        GCLog.logLevel = LogLevel.None;
        GamingCouchEditorTestSupport.ClearGamingCouchInstance();
    }

    [TearDown]
    public void TearDown()
    {
        GamingCouchEditorTestSupport.DestroyTrackedObjects(objectsToDestroy);
        GCRuntimeMessageOutput.ResetForTests(null);
        GCLog.logLevel = LogLevel.None;
        GamingCouchEditorTestSupport.ClearGamingCouchInstance();
    }

    [Test]
    public void EliminationTransitionsExposeStateBooleansTimestampsAndEventArgs()
    {
        var player = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 2);
        var longRevokableReason = new string('e', 300);
        var events = new List<GCPlayerEliminationStateChangedEventArgs>();
        player.OnEliminationStateChanged += events.Add;

        player.SetEliminatedRevokable(longRevokableReason);

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.Revokable));
        Assert.That(player.IsEliminated, Is.True);
        Assert.That(player.IsEliminatedRevokable, Is.True);
        Assert.That(player.IsEliminatedPermanent, Is.False);
        Assert.That(player.LastSetEliminatedRevokableGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(player.LastSetEliminatedGameTime, Is.EqualTo(player.LastSetEliminatedRevokableGameTime));
        Assert.That(events, Has.Count.EqualTo(1));
        AssertEliminationEvent(
            events[0],
            2,
            GCPlayerEliminationState.None,
            GCPlayerEliminationState.Revokable,
            longRevokableReason,
            player.LastSetEliminatedRevokableGameTime
        );

        player.SetEliminatedPermanent("final hazard");

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.Permanent));
        Assert.That(player.IsEliminatedPermanent, Is.True);
        Assert.That(player.IsEliminatedRevokable, Is.False);
        Assert.That(player.LastSetEliminatedPermanentGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(player.LastSetEliminatedGameTime, Is.EqualTo(player.LastSetEliminatedPermanentGameTime));
        Assert.That(events, Has.Count.EqualTo(2));
        AssertEliminationEvent(
            events[1],
            2,
            GCPlayerEliminationState.Revokable,
            GCPlayerEliminationState.Permanent,
            "final hazard",
            player.LastSetEliminatedPermanentGameTime
        );
    }

    [Test]
    public void FinishTransitionsExposeStateBooleansTimestampsAndEventArgs()
    {
        var player = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 1);
        var longRevokableReason = new string('f', 300);
        var events = new List<GCPlayerFinishStateChangedEventArgs>();
        player.OnFinishStateChanged += events.Add;

        player.SetFinishedRevokable(longRevokableReason);

        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.Revokable));
        Assert.That(player.IsFinished, Is.True);
        Assert.That(player.IsFinishedRevokable, Is.True);
        Assert.That(player.IsFinishedPermanent, Is.False);
        Assert.That(player.LastSetFinishedRevokableGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(player.LastSetFinishedGameTime, Is.EqualTo(player.LastSetFinishedRevokableGameTime));
        Assert.That(events, Has.Count.EqualTo(1));
        AssertFinishEvent(
            events[0],
            1,
            GCPlayerFinishState.None,
            GCPlayerFinishState.Revokable,
            longRevokableReason,
            player.LastSetFinishedRevokableGameTime
        );

        player.SetFinishedPermanent("finish line");

        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.Permanent));
        Assert.That(player.IsFinishedPermanent, Is.True);
        Assert.That(player.IsFinishedRevokable, Is.False);
        Assert.That(player.LastSetFinishedPermanentGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(player.LastSetFinishedGameTime, Is.EqualTo(player.LastSetFinishedPermanentGameTime));
        Assert.That(events, Has.Count.EqualTo(2));
        AssertFinishEvent(
            events[1],
            1,
            GCPlayerFinishState.Revokable,
            GCPlayerFinishState.Permanent,
            "finish line",
            player.LastSetFinishedPermanentGameTime
        );
    }

    [Test]
    public void RevokingRevokableStatesClearsOnlyTheMatchingState()
    {
        var player = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 3);
        var eliminationEvents = new List<GCPlayerEliminationStateChangedEventArgs>();
        var finishEvents = new List<GCPlayerFinishStateChangedEventArgs>();
        player.OnEliminationStateChanged += eliminationEvents.Add;
        player.OnFinishStateChanged += finishEvents.Add;

        player.SetEliminatedRevokable("temporary");
        player.SetFinishedRevokable("checkpoint");

        player.SetRevokeEliminated("respawn");

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.None));
        Assert.That(player.IsEliminated, Is.False);
        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.Revokable));
        Assert.That(player.IsFinished, Is.True);
        Assert.That(player.LastSetRevokeEliminatedGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(player.LastSetRevokeGameTime, Is.EqualTo(player.LastSetRevokeEliminatedGameTime));
        Assert.That(eliminationEvents, Has.Count.EqualTo(2));
        AssertEliminationEvent(
            eliminationEvents[1],
            3,
            GCPlayerEliminationState.Revokable,
            GCPlayerEliminationState.None,
            "respawn",
            player.LastSetRevokeEliminatedGameTime
        );
        Assert.That(finishEvents, Has.Count.EqualTo(1));

        player.SetRevokeFinished("rollback");

        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.None));
        Assert.That(player.IsFinished, Is.False);
        Assert.That(player.LastSetRevokeFinishedGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(player.LastSetRevokeGameTime, Is.EqualTo(player.LastSetRevokeFinishedGameTime));
        Assert.That(finishEvents, Has.Count.EqualTo(2));
        AssertFinishEvent(
            finishEvents[1],
            3,
            GCPlayerFinishState.Revokable,
            GCPlayerFinishState.None,
            "rollback",
            player.LastSetRevokeFinishedGameTime
        );
        Assert.That(eliminationEvents, Has.Count.EqualTo(2));
    }

    [Test]
    public void DuplicateAndInvalidStateTransitionsDiagnoseAndNoOp()
    {
        var player = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 4);
        player.SetEliminatedPermanent("final");
        player.SetFinishedPermanent("final");
        var emittedDiagnostics = new List<string>();
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += emittedDiagnostics.Add;
        var eliminationEventCount = 0;
        var finishEventCount = 0;
        player.OnEliminationStateChanged += args => eliminationEventCount++;
        player.OnFinishStateChanged += args => finishEventCount++;

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.duplicate_elimination: Player is already permanently eliminated.");
        player.SetEliminatedPermanent("duplicate");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.invalid_transition: Permanent elimination cannot transition back to revokable elimination.");
        player.SetEliminatedRevokable("invalid");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.invalid_revoke: Only revokable elimination can be revoked.");
        player.SetRevokeEliminated("invalid revoke");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.duplicate_finish: Player is already permanently finished.");
        player.SetFinishedPermanent("duplicate");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.invalid_transition: Permanent finish cannot transition back to revokable finish.");
        player.SetFinishedRevokable("invalid");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.invalid_revoke: Only revokable finish can be revoked.");
        player.SetRevokeFinished("invalid revoke");

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.Permanent));
        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.Permanent));
        Assert.That(eliminationEventCount, Is.EqualTo(0));
        Assert.That(finishEventCount, Is.EqualTo(0));
        Assert.That(emittedDiagnostics, Has.Count.EqualTo(6));
        AssertStateDiagnosticContext(
            emittedDiagnostics[0],
            "gc.state.duplicate_elimination",
            4,
            "SetEliminatedPermanent",
            "Permanent",
            "Permanent"
        );
        AssertStateDiagnosticContext(
            emittedDiagnostics[1],
            "gc.state.invalid_transition",
            4,
            "SetEliminatedRevokable",
            "Permanent",
            "Revokable"
        );
        AssertStateDiagnosticContext(
            emittedDiagnostics[2],
            "gc.state.invalid_revoke",
            4,
            "SetRevokeEliminated",
            "Permanent",
            "None"
        );
        AssertStateDiagnosticContext(
            emittedDiagnostics[3],
            "gc.state.duplicate_finish",
            4,
            "SetFinishedPermanent",
            "Permanent",
            "Permanent"
        );
        AssertStateDiagnosticContext(
            emittedDiagnostics[4],
            "gc.state.invalid_transition",
            4,
            "SetFinishedRevokable",
            "Permanent",
            "Revokable"
        );
        AssertStateDiagnosticContext(
            emittedDiagnostics[5],
            "gc.state.invalid_revoke",
            4,
            "SetRevokeFinished",
            "Permanent",
            "None"
        );
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void DuplicateRevokableStatesAndRevokeFromNoneDiagnoseAndNoOp()
    {
        var player = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 8);
        var eliminationEventCount = 0;
        var finishEventCount = 0;
        player.OnEliminationStateChanged += args => eliminationEventCount++;
        player.OnFinishStateChanged += args => finishEventCount++;

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.invalid_revoke: Only revokable elimination can be revoked.");
        player.SetRevokeEliminated("none");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.invalid_revoke: Only revokable finish can be revoked.");
        player.SetRevokeFinished("none");

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.None));
        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.None));

        player.SetEliminatedRevokable("temporary");
        player.SetFinishedRevokable("checkpoint");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.duplicate_elimination: Player is already revokably eliminated.");
        player.SetEliminatedRevokable("duplicate");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.duplicate_finish: Player is already revokably finished.");
        player.SetFinishedRevokable("duplicate");

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.Revokable));
        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.Revokable));
        Assert.That(eliminationEventCount, Is.EqualTo(1));
        Assert.That(finishEventCount, Is.EqualTo(1));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void FinishAndEliminationCanCoexist()
    {
        var player = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 5);

        player.SetEliminatedPermanent("out");
        player.SetFinishedRevokable("checkpoint");

        Assert.That(player.IsEliminatedPermanent, Is.True);
        Assert.That(player.IsFinishedRevokable, Is.True);

        player.SetRevokeFinished("rollback");

        Assert.That(player.IsEliminatedPermanent, Is.True);
        Assert.That(player.IsFinished, Is.False);
    }

    [Test]
    public void EliminationAndFinishTransitionModelsRepresentAcceptedNoOpAndRejectedResults()
    {
        AssertAcceptedTransition(
            GCPlayerTransitions.SetEliminatedRevokable(3, GCPlayerEliminationState.None, "temporary"),
            GCPlayerTransitionKind.PlayerEliminationStateChanged,
            3,
            GCPlayerEliminationState.None,
            GCPlayerEliminationState.Revokable,
            "temporary"
        );
        AssertAcceptedTransition(
            GCPlayerTransitions.SetEliminatedPermanent(3, GCPlayerEliminationState.Revokable, "promotion"),
            GCPlayerTransitionKind.PlayerEliminationStateChanged,
            3,
            GCPlayerEliminationState.Revokable,
            GCPlayerEliminationState.Permanent,
            "promotion"
        );
        AssertAcceptedTransition(
            GCPlayerTransitions.SetRevokeEliminated(3, GCPlayerEliminationState.Revokable, "respawn"),
            GCPlayerTransitionKind.PlayerEliminationStateChanged,
            3,
            GCPlayerEliminationState.Revokable,
            GCPlayerEliminationState.None,
            "respawn"
        );
        AssertInactiveTransition(
            GCPlayerTransitions.SetEliminatedPermanent(3, GCPlayerEliminationState.Permanent, "duplicate"),
            GCPlayerTransitionOutcome.NoOp,
            GCPlayerTransitionKind.PlayerEliminationStateChanged,
            3,
            GCPlayerEliminationState.Permanent,
            GCPlayerEliminationState.Permanent,
            "duplicate",
            GCPlayerTransitionRejectionReason.DuplicateValue
        );
        AssertInactiveTransition(
            GCPlayerTransitions.SetEliminatedRevokable(3, GCPlayerEliminationState.Permanent, "invalid"),
            GCPlayerTransitionOutcome.Rejected,
            GCPlayerTransitionKind.PlayerEliminationStateChanged,
            3,
            GCPlayerEliminationState.Permanent,
            GCPlayerEliminationState.Revokable,
            "invalid",
            GCPlayerTransitionRejectionReason.InvalidTransition
        );
        AssertInactiveTransition(
            GCPlayerTransitions.SetRevokeEliminated(3, GCPlayerEliminationState.None, "invalid revoke"),
            GCPlayerTransitionOutcome.Rejected,
            GCPlayerTransitionKind.PlayerEliminationStateChanged,
            3,
            GCPlayerEliminationState.None,
            GCPlayerEliminationState.None,
            "invalid revoke",
            GCPlayerTransitionRejectionReason.InvalidRevoke
        );

        AssertAcceptedTransition(
            GCPlayerTransitions.SetFinishedRevokable(4, GCPlayerFinishState.None, "checkpoint"),
            GCPlayerTransitionKind.PlayerFinishStateChanged,
            4,
            GCPlayerFinishState.None,
            GCPlayerFinishState.Revokable,
            "checkpoint"
        );
        AssertAcceptedTransition(
            GCPlayerTransitions.SetFinishedPermanent(4, GCPlayerFinishState.Revokable, "finish"),
            GCPlayerTransitionKind.PlayerFinishStateChanged,
            4,
            GCPlayerFinishState.Revokable,
            GCPlayerFinishState.Permanent,
            "finish"
        );
        AssertAcceptedTransition(
            GCPlayerTransitions.SetRevokeFinished(4, GCPlayerFinishState.Revokable, "rollback"),
            GCPlayerTransitionKind.PlayerFinishStateChanged,
            4,
            GCPlayerFinishState.Revokable,
            GCPlayerFinishState.None,
            "rollback"
        );
        AssertInactiveTransition(
            GCPlayerTransitions.SetFinishedPermanent(4, GCPlayerFinishState.Permanent, "duplicate"),
            GCPlayerTransitionOutcome.NoOp,
            GCPlayerTransitionKind.PlayerFinishStateChanged,
            4,
            GCPlayerFinishState.Permanent,
            GCPlayerFinishState.Permanent,
            "duplicate",
            GCPlayerTransitionRejectionReason.DuplicateValue
        );
        AssertInactiveTransition(
            GCPlayerTransitions.SetFinishedRevokable(4, GCPlayerFinishState.Permanent, "invalid"),
            GCPlayerTransitionOutcome.Rejected,
            GCPlayerTransitionKind.PlayerFinishStateChanged,
            4,
            GCPlayerFinishState.Permanent,
            GCPlayerFinishState.Revokable,
            "invalid",
            GCPlayerTransitionRejectionReason.InvalidTransition
        );
        AssertInactiveTransition(
            GCPlayerTransitions.SetRevokeFinished(4, GCPlayerFinishState.None, "invalid revoke"),
            GCPlayerTransitionOutcome.Rejected,
            GCPlayerTransitionKind.PlayerFinishStateChanged,
            4,
            GCPlayerFinishState.None,
            GCPlayerFinishState.None,
            "invalid revoke",
            GCPlayerTransitionRejectionReason.InvalidRevoke
        );
    }

    [Test]
    public void StatusTransitionsPreservePublicCallbackAndNoOpBehavior()
    {
        var player = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 9);
        var longReason = new string('r', 300);
        var events = new List<(GCPlayerStatus status, string statusText, string reason)>();
        var transitionEvents = new List<(
            GCPlayerStatus oldStatus,
            string oldStatusText,
            GCPlayerStatus status,
            string statusText,
            string reason
        )>();
        player.OnStatusChanged += (status, statusText, reason) => events.Add((status, statusText, reason));
        player.OnStatusTransitionChanged += (oldStatus, oldStatusText, status, statusText, reason) =>
            transitionEvents.Add((oldStatus, oldStatusText, status, statusText, reason));

        player.SetStatus(GCPlayerStatus.Success, "Finished lap", longReason);

        Assert.That(player.Status, Is.EqualTo(GCPlayerStatus.Success));
        Assert.That(player.StatusText, Is.EqualTo("Finished lap"));
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].status, Is.EqualTo(GCPlayerStatus.Success));
        Assert.That(events[0].statusText, Is.EqualTo("Finished lap"));
        Assert.That(events[0].reason, Is.EqualTo(longReason));
        Assert.That(transitionEvents, Has.Count.EqualTo(1));
        Assert.That(transitionEvents[0].oldStatus, Is.EqualTo(GCPlayerStatus.Neutral));
        Assert.That(transitionEvents[0].oldStatusText, Is.EqualTo(""));
        Assert.That(transitionEvents[0].status, Is.EqualTo(GCPlayerStatus.Success));
        Assert.That(transitionEvents[0].statusText, Is.EqualTo("Finished lap"));
        Assert.That(transitionEvents[0].reason, Is.EqualTo(longReason));

        player.SetStatus(GCPlayerStatus.Success, "Finished lap", "duplicate status");

        Assert.That(player.Status, Is.EqualTo(GCPlayerStatus.Success));
        Assert.That(player.StatusText, Is.EqualTo("Finished lap"));
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(transitionEvents, Has.Count.EqualTo(1));
    }

    [Test]
    public void StatusTransitionModelRepresentsAcceptedNoOpAndRejectedResults()
    {
        var accepted = GCPlayerTransitions.SetStatus(
            2,
            GCPlayerStatus.Neutral,
            "",
            GCPlayerStatus.Warning,
            "low fuel",
            "status reason"
        );

        Assert.That(accepted.Accepted, Is.True);
        Assert.That(accepted.Outcome, Is.EqualTo(GCPlayerTransitionOutcome.Accepted));
        Assert.That(accepted.IsNoOp, Is.False);
        Assert.That(accepted.IsRejected, Is.False);
        Assert.That(accepted.Kind, Is.EqualTo(GCPlayerTransitionKind.PlayerStatusChanged));
        Assert.That(accepted.PlayerIndex, Is.EqualTo(2));
        Assert.That(accepted.PreviousValue.Status, Is.EqualTo(GCPlayerStatus.Neutral));
        Assert.That(accepted.PreviousValue.StatusText, Is.EqualTo(""));
        Assert.That(accepted.Value.Status, Is.EqualTo(GCPlayerStatus.Warning));
        Assert.That(accepted.Value.StatusText, Is.EqualTo("low fuel"));
        Assert.That(accepted.ReasonText, Is.EqualTo("status reason"));
        Assert.That(accepted.ChangedAtGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(accepted.EmitsSemanticTransition, Is.True);
        Assert.That(accepted.LatestStateDirtyFlags, Is.EqualTo(
            GCPlayerLatestStateDirtyFlags.RuntimeStateSnapshot | GCPlayerLatestStateDirtyFlags.PlayersHud
        ));
        Assert.That(accepted.MarksLatestStateDirty, Is.True);
        Assert.That(accepted.MarksRuntimeStateSnapshotDirty, Is.True);
        Assert.That(accepted.MarksPlayersHudDirty, Is.True);
        Assert.That(accepted.WasClamped, Is.False);
        Assert.That(accepted.RejectionReason, Is.EqualTo(GCPlayerTransitionRejectionReason.None));

        var noOp = GCPlayerTransitions.SetStatus(
            2,
            GCPlayerStatus.Warning,
            "",
            GCPlayerStatus.Warning,
            null,
            "duplicate reason"
        );

        Assert.That(noOp.Accepted, Is.False);
        Assert.That(noOp.Outcome, Is.EqualTo(GCPlayerTransitionOutcome.NoOp));
        Assert.That(noOp.IsNoOp, Is.True);
        Assert.That(noOp.IsRejected, Is.False);
        Assert.That(noOp.Kind, Is.EqualTo(GCPlayerTransitionKind.PlayerStatusChanged));
        Assert.That(noOp.PlayerIndex, Is.EqualTo(2));
        Assert.That(noOp.PreviousValue.Status, Is.EqualTo(GCPlayerStatus.Warning));
        Assert.That(noOp.PreviousValue.StatusText, Is.EqualTo(""));
        Assert.That(noOp.Value.Status, Is.EqualTo(GCPlayerStatus.Warning));
        Assert.That(noOp.Value.StatusText, Is.EqualTo(""));
        Assert.That(noOp.ReasonText, Is.EqualTo("duplicate reason"));
        Assert.That(noOp.ChangedAtGameTime, Is.EqualTo(-1f));
        Assert.That(noOp.EmitsSemanticTransition, Is.False);
        Assert.That(noOp.LatestStateDirtyFlags, Is.EqualTo(GCPlayerLatestStateDirtyFlags.None));
        Assert.That(noOp.MarksLatestStateDirty, Is.False);
        Assert.That(noOp.MarksRuntimeStateSnapshotDirty, Is.False);
        Assert.That(noOp.MarksPlayersHudDirty, Is.False);
        Assert.That(noOp.WasClamped, Is.False);
        Assert.That(noOp.RejectionReason, Is.EqualTo(GCPlayerTransitionRejectionReason.DuplicateValue));

        var rejected = GCPlayerTransitionResult<GCPlayerStatusValue>.Reject(
            GCPlayerTransitionKind.PlayerStatusChanged,
            2,
            new GCPlayerStatusValue(GCPlayerStatus.Failure, "done"),
            new GCPlayerStatusValue(GCPlayerStatus.Pending, "retry"),
            "invalid reason",
            GCPlayerTransitionRejectionReason.InvalidTransition
        );

        Assert.That(rejected.Accepted, Is.False);
        Assert.That(rejected.Outcome, Is.EqualTo(GCPlayerTransitionOutcome.Rejected));
        Assert.That(rejected.IsNoOp, Is.False);
        Assert.That(rejected.IsRejected, Is.True);
        Assert.That(rejected.Kind, Is.EqualTo(GCPlayerTransitionKind.PlayerStatusChanged));
        Assert.That(rejected.PlayerIndex, Is.EqualTo(2));
        Assert.That(rejected.PreviousValue.Status, Is.EqualTo(GCPlayerStatus.Failure));
        Assert.That(rejected.PreviousValue.StatusText, Is.EqualTo("done"));
        Assert.That(rejected.Value.Status, Is.EqualTo(GCPlayerStatus.Pending));
        Assert.That(rejected.Value.StatusText, Is.EqualTo("retry"));
        Assert.That(rejected.ReasonText, Is.EqualTo("invalid reason"));
        Assert.That(rejected.ChangedAtGameTime, Is.EqualTo(-1f));
        Assert.That(rejected.EmitsSemanticTransition, Is.False);
        Assert.That(rejected.LatestStateDirtyFlags, Is.EqualTo(GCPlayerLatestStateDirtyFlags.None));
        Assert.That(rejected.MarksLatestStateDirty, Is.False);
        Assert.That(rejected.WasClamped, Is.False);
        Assert.That(rejected.RejectionReason, Is.EqualTo(GCPlayerTransitionRejectionReason.InvalidTransition));
    }

    [Test]
    public void ScalarTransitionsPreservePublicCallbacksNoOpsAndClampDiagnostics()
    {
        var player = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 10);
        var longReason = new string('s', 300);
        var scoreEvents = new List<(int oldValue, int value, string reason)>();
        var livesEvents = new List<(int oldValue, int value, string reason)>();
        var meterEvents = new List<(int oldValue, int value, string reason)>();
        player.OnScoreChanged += (oldValue, value, reason) => scoreEvents.Add((oldValue, value, reason));
        player.OnLivesChanged += (oldValue, value, reason) => livesEvents.Add((oldValue, value, reason));
        player.OnMeterChanged += (oldValue, value, reason) => meterEvents.Add((oldValue, value, reason));

        player.SetScore(10, longReason);
        player.SetScore(10, "duplicate score");
        player.AddScore(5, "score bonus");
        player.SubtractScore(3, "score penalty");

        player.SetLives(3, "lives");
        player.SetLives(3, "duplicate lives");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.clamped_value: Lives were clamped.");
        player.SetLives(-2, "clamped lives");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.clamped_value: Lives were clamped.");
        player.SetLives(-1, "clamped duplicate lives");

        player.SetMeter(50, "meter");
        player.SetMeter(50, "duplicate meter");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.clamped_value: Meter was clamped.");
        player.SetMeter(150, "clamped high meter");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.clamped_value: Meter was clamped.");
        player.SetMeter(120, "clamped high duplicate meter");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.clamped_value: Meter was clamped.");
        player.SetMeter(-5, "clamped low meter");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.clamped_value: Meter was clamped.");
        player.SetMeter(-2, "clamped low duplicate meter");

        Assert.That(player.Score, Is.EqualTo(12));
        Assert.That(scoreEvents, Has.Count.EqualTo(3));
        Assert.That(scoreEvents[0], Is.EqualTo((0, 10, longReason)));
        Assert.That(scoreEvents[1], Is.EqualTo((10, 15, "score bonus")));
        Assert.That(scoreEvents[2], Is.EqualTo((15, 12, "score penalty")));

        Assert.That(player.Lives, Is.EqualTo(0));
        Assert.That(livesEvents, Has.Count.EqualTo(2));
        Assert.That(livesEvents[0], Is.EqualTo((0, 3, "lives")));
        Assert.That(livesEvents[1], Is.EqualTo((3, 0, "clamped lives")));

        Assert.That(player.Meter, Is.EqualTo(-1));
        Assert.That(meterEvents, Has.Count.EqualTo(3));
        Assert.That(meterEvents[0], Is.EqualTo((-1, 50, "meter")));
        Assert.That(meterEvents[1], Is.EqualTo((50, 100, "clamped high meter")));
        Assert.That(meterEvents[2], Is.EqualTo((100, -1, "clamped low meter")));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void PublicPlayerMutatorsAndCallbackShapesStayGameFacingCompatible()
    {
        var player = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 11);
        var eliminationEvents = new List<GCPlayerEliminationStateChangedEventArgs>();
        var finishEvents = new List<GCPlayerFinishStateChangedEventArgs>();
        var scoreEvents = new List<(int oldValue, int value, string reason)>();
        var livesEvents = new List<(int oldValue, int value, string reason)>();
        var meterEvents = new List<(int oldValue, int value, string reason)>();
        var statusEvents = new List<(GCPlayerStatus status, string statusText, string reason)>();

        AssertPublicCallbackField(
            "OnEliminationStateChanged",
            typeof(Action<GCPlayerEliminationStateChangedEventArgs>)
        );
        AssertPublicCallbackField(
            "OnFinishStateChanged",
            typeof(Action<GCPlayerFinishStateChangedEventArgs>)
        );
        AssertPublicCallbackField("OnScoreChanged", typeof(Action<int, int, string>));
        AssertPublicCallbackField("OnLivesChanged", typeof(Action<int, int, string>));
        AssertPublicCallbackField("OnMeterChanged", typeof(Action<int, int, string>));
        AssertPublicCallbackField("OnStatusChanged", typeof(Action<GCPlayerStatus, string, string>));

        Action<string> setEliminatedPermanent = player.SetEliminatedPermanent;
        Action<string> setEliminatedRevokable = player.SetEliminatedRevokable;
        Action<string> setRevokeEliminated = player.SetRevokeEliminated;
        Action<string> setFinishedPermanent = player.SetFinishedPermanent;
        Action<string> setFinishedRevokable = player.SetFinishedRevokable;
        Action<string> setRevokeFinished = player.SetRevokeFinished;
        Action<int, string> setScore = player.SetScore;
        Action<int, string> addScore = player.AddScore;
        Action<int, string> subtractScore = player.SubtractScore;
        Action<int, string> setLives = player.SetLives;
        Action<int, string> addLives = player.AddLives;
        Action<int, string> subtractLives = player.SubtractLives;
        Action<GCPlayerStatus, string, string> setStatus = player.SetStatus;
        Action<int, string> setMeter = player.SetMeter;

        player.OnEliminationStateChanged += eliminationEvents.Add;
        player.OnFinishStateChanged += finishEvents.Add;
        player.OnScoreChanged += (oldValue, value, reason) => scoreEvents.Add((oldValue, value, reason));
        player.OnLivesChanged += (oldValue, value, reason) => livesEvents.Add((oldValue, value, reason));
        player.OnMeterChanged += (oldValue, value, reason) => meterEvents.Add((oldValue, value, reason));
        player.OnStatusChanged += (status, statusText, reason) => statusEvents.Add((status, statusText, reason));

        setEliminatedRevokable("temporary elimination");
        setRevokeEliminated("respawn");
        setEliminatedPermanent("final elimination");
        setFinishedRevokable("temporary finish");
        setRevokeFinished("rollback");
        setFinishedPermanent("final finish");
        setScore(10, "score");
        addScore(5, "bonus");
        subtractScore(2, "penalty");
        setLives(3, "lives");
        addLives(2, "extra life");
        subtractLives(4, "damage");
        setStatus(GCPlayerStatus.Warning, "Low health", "status");
        setMeter(42, "meter");

        Assert.That(eliminationEvents, Has.Count.EqualTo(3));
        AssertEliminationEvent(
            eliminationEvents[0],
            11,
            GCPlayerEliminationState.None,
            GCPlayerEliminationState.Revokable,
            "temporary elimination",
            player.LastSetEliminatedRevokableGameTime
        );
        AssertEliminationEvent(
            eliminationEvents[1],
            11,
            GCPlayerEliminationState.Revokable,
            GCPlayerEliminationState.None,
            "respawn",
            player.LastSetRevokeEliminatedGameTime
        );
        AssertEliminationEvent(
            eliminationEvents[2],
            11,
            GCPlayerEliminationState.None,
            GCPlayerEliminationState.Permanent,
            "final elimination",
            player.LastSetEliminatedPermanentGameTime
        );

        Assert.That(finishEvents, Has.Count.EqualTo(3));
        AssertFinishEvent(
            finishEvents[0],
            11,
            GCPlayerFinishState.None,
            GCPlayerFinishState.Revokable,
            "temporary finish",
            player.LastSetFinishedRevokableGameTime
        );
        AssertFinishEvent(
            finishEvents[1],
            11,
            GCPlayerFinishState.Revokable,
            GCPlayerFinishState.None,
            "rollback",
            player.LastSetRevokeFinishedGameTime
        );
        AssertFinishEvent(
            finishEvents[2],
            11,
            GCPlayerFinishState.None,
            GCPlayerFinishState.Permanent,
            "final finish",
            player.LastSetFinishedPermanentGameTime
        );

        Assert.That(scoreEvents, Is.EqualTo(new[]
        {
            (0, 10, "score"),
            (10, 15, "bonus"),
            (15, 13, "penalty"),
        }));
        Assert.That(livesEvents, Is.EqualTo(new[]
        {
            (0, 3, "lives"),
            (3, 5, "extra life"),
            (5, 1, "damage"),
        }));
        Assert.That(statusEvents, Is.EqualTo(new[]
        {
            (GCPlayerStatus.Warning, "Low health", "status"),
        }));
        Assert.That(meterEvents, Is.EqualTo(new[]
        {
            (-1, 42, "meter"),
        }));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ScalarTransitionModelsRepresentAcceptedNoOpAndClampedResults()
    {
        AssertAcceptedTransition(
            GCPlayerTransitions.SetScore(2, 0, 10, "score"),
            GCPlayerTransitionKind.PlayerScoreChanged,
            2,
            0,
            10,
            "score"
        );
        AssertInactiveTransition(
            GCPlayerTransitions.SetScore(2, 10, 10, "duplicate score"),
            GCPlayerTransitionOutcome.NoOp,
            GCPlayerTransitionKind.PlayerScoreChanged,
            2,
            10,
            10,
            "duplicate score",
            GCPlayerTransitionRejectionReason.DuplicateValue
        );
        AssertAcceptedTransition(
            GCPlayerTransitions.SetLives(3, 2, -5, "clamped lives"),
            GCPlayerTransitionKind.PlayerLivesChanged,
            3,
            2,
            0,
            "clamped lives",
            wasClamped: true
        );
        AssertInactiveTransition(
            GCPlayerTransitions.SetLives(3, 0, -1, "clamped duplicate lives"),
            GCPlayerTransitionOutcome.NoOp,
            GCPlayerTransitionKind.PlayerLivesChanged,
            3,
            0,
            0,
            "clamped duplicate lives",
            GCPlayerTransitionRejectionReason.DuplicateValue,
            wasClamped: true
        );
        AssertAcceptedTransition(
            GCPlayerTransitions.SetMeter(4, -1, 75, "meter"),
            GCPlayerTransitionKind.PlayerMeterChanged,
            4,
            -1,
            75,
            "meter"
        );
        AssertAcceptedTransition(
            GCPlayerTransitions.SetMeter(4, 75, 150, "clamped high meter"),
            GCPlayerTransitionKind.PlayerMeterChanged,
            4,
            75,
            100,
            "clamped high meter",
            wasClamped: true
        );
        AssertAcceptedTransition(
            GCPlayerTransitions.SetMeter(4, 25, -5, "clamped low meter"),
            GCPlayerTransitionKind.PlayerMeterChanged,
            4,
            25,
            -1,
            "clamped low meter",
            wasClamped: true
        );
        AssertInactiveTransition(
            GCPlayerTransitions.SetMeter(4, -1, -2, "clamped duplicate meter"),
            GCPlayerTransitionOutcome.NoOp,
            GCPlayerTransitionKind.PlayerMeterChanged,
            4,
            -1,
            -1,
            "clamped duplicate meter",
            GCPlayerTransitionRejectionReason.DuplicateValue,
            wasClamped: true
        );
    }

    [Test]
    public void StoreBroadEliminationListsTrackNoneVersusAnyEliminatedState()
    {
        var player = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 7);
        var store = new GCPlayerStore<GCPlayer>();
        store.AddPlayer(player);

        Assert.That(store.PlayersUneliminated.Count, Is.EqualTo(1));
        Assert.That(store.PlayersEliminated.Count, Is.EqualTo(0));

        player.SetEliminatedRevokable("temporary");

        Assert.That(store.PlayersUneliminated.Count, Is.EqualTo(0));
        Assert.That(store.PlayersEliminated.Count, Is.EqualTo(1));
        Assert.That(store.PlayersEliminatedRevokable, Is.EqualTo(new[] { player }));
        Assert.That(store.PlayersEliminatedPermanent, Is.Empty);

        player.SetEliminatedPermanent("promotion");

        Assert.That(store.PlayersUneliminated.Count, Is.EqualTo(0));
        Assert.That(store.PlayersEliminated.Count, Is.EqualTo(1));
        Assert.That(store.PlayersEliminatedRevokable, Is.Empty);
        Assert.That(store.PlayersEliminatedPermanent, Is.EqualTo(new[] { player }));
    }

    [Test]
    public void StoreStateCollectionsExposePlayersPrefixAndBotNonBotSymmetry()
    {
        var livePlayer = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 0);
        var eliminatedBot = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 1, GCPlayerType.bot);
        var eliminatedNonBot = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 2);
        var finishedBot = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 3, GCPlayerType.bot);
        var finishedNonBot = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 4);

        eliminatedBot.SetEliminatedRevokable("temporary");
        eliminatedNonBot.SetEliminatedPermanent("final");
        finishedBot.SetFinishedRevokable("checkpoint");
        finishedNonBot.SetFinishedPermanent("done");

        var store = new GCPlayerStore<GCPlayer>();
        store.AddPlayer(livePlayer);
        store.AddPlayer(eliminatedBot);
        store.AddPlayer(eliminatedNonBot);
        store.AddPlayer(finishedBot);
        store.AddPlayer(finishedNonBot);

        Assert.That(store.Players, Is.EqualTo(new[] { livePlayer, eliminatedBot, eliminatedNonBot, finishedBot, finishedNonBot }));
        Assert.That(store.PlayersBot, Is.EqualTo(new[] { eliminatedBot, finishedBot }));
        Assert.That(store.PlayersNonBot, Is.EqualTo(new[] { livePlayer, eliminatedNonBot, finishedNonBot }));

        Assert.That(store.PlayersUneliminated, Is.EqualTo(new[] { livePlayer, finishedBot, finishedNonBot }));
        Assert.That(store.PlayersUneliminatedBot, Is.EqualTo(new[] { finishedBot }));
        Assert.That(store.PlayersUneliminatedNonBot, Is.EqualTo(new[] { livePlayer, finishedNonBot }));

        Assert.That(store.PlayersEliminated, Is.EqualTo(new[] { eliminatedBot, eliminatedNonBot }));
        Assert.That(store.PlayersEliminatedBot, Is.EqualTo(new[] { eliminatedBot }));
        Assert.That(store.PlayersEliminatedNonBot, Is.EqualTo(new[] { eliminatedNonBot }));
        Assert.That(store.PlayersEliminatedRevokable, Is.EqualTo(new[] { eliminatedBot }));
        Assert.That(store.PlayersEliminatedRevokableBot, Is.EqualTo(new[] { eliminatedBot }));
        Assert.That(store.PlayersEliminatedRevokableNonBot, Is.Empty);
        Assert.That(store.PlayersEliminatedPermanent, Is.EqualTo(new[] { eliminatedNonBot }));
        Assert.That(store.PlayersEliminatedPermanentBot, Is.Empty);
        Assert.That(store.PlayersEliminatedPermanentNonBot, Is.EqualTo(new[] { eliminatedNonBot }));

        Assert.That(store.PlayersFinished, Is.EqualTo(new[] { finishedBot, finishedNonBot }));
        Assert.That(store.PlayersFinishedBot, Is.EqualTo(new[] { finishedBot }));
        Assert.That(store.PlayersFinishedNonBot, Is.EqualTo(new[] { finishedNonBot }));
        Assert.That(store.PlayersFinishedRevokable, Is.EqualTo(new[] { finishedBot }));
        Assert.That(store.PlayersFinishedRevokableBot, Is.EqualTo(new[] { finishedBot }));
        Assert.That(store.PlayersFinishedRevokableNonBot, Is.Empty);
        Assert.That(store.PlayersFinishedPermanent, Is.EqualTo(new[] { finishedNonBot }));
        Assert.That(store.PlayersFinishedPermanentBot, Is.Empty);
        Assert.That(store.PlayersFinishedPermanentNonBot, Is.EqualTo(new[] { finishedNonBot }));
    }

    [Test]
    public void StoreRejectsDuplicatePlayersAndPlayerIndices()
    {
        var player = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 0);
        var duplicateIndexPlayer = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 0);
        var store = new GCPlayerStore<GCPlayer>();
        store.AddPlayer(player);

        Assert.Throws<InvalidOperationException>(() => store.AddPlayer(player));
        Assert.Throws<InvalidOperationException>(() => store.AddPlayer(duplicateIndexPlayer));
        Assert.That(store.Players, Is.EqualTo(new[] { player }));
        Assert.That(store.PlayersUneliminated, Is.EqualTo(new[] { player }));
    }

    [Test]
    public void BroadPlacementCriteriaTreatPermanentAndRevokableStatesAsCurrent()
    {
        var eliminatedRevokable = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 0);
        var eliminatedPermanent = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 1);
        var livePlayer = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 2);
        eliminatedRevokable.SetEliminatedRevokable("temporary");
        eliminatedPermanent.SetEliminatedPermanent("final");

        var finishedRevokable = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 3);
        var finishedPermanent = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 4);
        var unfinishedPlayer = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 5);
        finishedRevokable.SetFinishedRevokable("checkpoint");
        finishedPermanent.SetFinishedPermanent("done");

        var eliminatedGame = CreateGameWithPlacementCriteria(GCPlacementSortCriteria.Eliminated);
        var eliminatedOrder = eliminatedGame.GetPlayersInPlacementOrder(new[] { eliminatedRevokable, eliminatedPermanent, livePlayer });
        Assert.That(eliminatedOrder, Is.EqualTo(new[] { eliminatedRevokable, eliminatedPermanent, livePlayer }));

        var finishedGame = CreateGameWithPlacementCriteria(GCPlacementSortCriteria.Finished);
        var finishedOrder = finishedGame.GetPlayersInPlacementOrder(new[] { finishedRevokable, finishedPermanent, unfinishedPlayer });
        Assert.That(finishedOrder, Is.EqualTo(new[] { finishedRevokable, finishedPermanent, unfinishedPlayer }));
    }

    [Test]
    public void RuntimeStateSnapshotProjectsCanonicalDynamicPlayerState()
    {
        var leadingPlayer = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 0);
        var trailingPlayer = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 1, GCPlayerType.bot);
        leadingPlayer.SetScore(10, "score");
        leadingPlayer.SetLives(2, "lives");
        leadingPlayer.SetStatus(GCPlayerStatus.Success, "Finished lap", "status");
        leadingPlayer.SetMeter(74, "meter");
        leadingPlayer.SetFinishedRevokable("checkpoint");
        trailingPlayer.SetScore(5, "score");
        trailingPlayer.SetEliminatedPermanent("out");

        var store = new GCPlayerStore<GCPlayer>();
        store.AddPlayer(leadingPlayer);
        store.AddPlayer(trailingPlayer);
        var game = new GCGame(null, store, new GCGameSetupOptions
        {
            placementCriteria = new[] { GCPlacementSortCriteria.ScoreDescending },
        });

        var payload = game.BuildRuntimeStateSnapshotPayload(GCStatus.Playing);
        var json = payload.ToJson();

        Assert.That(payload.game.status, Is.EqualTo("playing"));
        Assert.That(payload.players, Has.Length.EqualTo(2));
        GamingCouchEditorTestSupport.AssertSnapshotPlayer(
            payload.players[0],
            playerIndex: 0,
            score: 10,
            lives: 2,
            status: "Success",
            statusText: "Finished lap",
            meter: 74,
            placement: 1,
            eliminationState: "None",
            finishState: "Revokable"
        );
        GamingCouchEditorTestSupport.AssertSnapshotPlayer(
            payload.players[1],
            playerIndex: 1,
            score: 5,
            lives: 0,
            status: "Neutral",
            statusText: "",
            meter: -1,
            placement: 2,
            eliminationState: "Permanent",
            finishState: "None"
        );
        Assert.That(json, Does.Contain("\"game\":{\"status\":\"playing\"}"));
        Assert.That(json, Does.Contain("\"playerIndex\":0"));
        Assert.That(json, Does.Contain("\"placement\":1"));
        Assert.That(json, Does.Contain("\"elimination\":\"Permanent\""));
        Assert.That(json, Does.Not.Contain("\"type\""));
        Assert.That(json, Does.Not.Contain("\"color\""));
    }

    [Test]
    public void RuntimeStateSnapshotRequiresCanonicalPlayerIndicesExactlyOnce()
    {
        var player = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 0);
        var duplicateIndexPlayer = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 0);
        var outOfRangePlayer = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 2);

        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { player, duplicateIndexPlayer },
            new[] { player, duplicateIndexPlayer }
        ));
        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { player, outOfRangePlayer },
            new[] { player, outOfRangePlayer }
        ));
    }

    [Test]
    public void RuntimeStateSnapshotValidatesPlayerAndPlacementInputs()
    {
        var firstPlayer = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 0);
        var secondPlayer = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 1);
        var outsidePlayer = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 2);

        Assert.Throws<ArgumentNullException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            null,
            new[] { firstPlayer }
        ));
        Assert.Throws<ArgumentNullException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { firstPlayer },
            null
        ));
        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new GCPlayer[] { firstPlayer, null },
            new[] { firstPlayer, secondPlayer }
        ));
        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { firstPlayer },
            new GCPlayer[] { null }
        ));
        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { firstPlayer, secondPlayer },
            new[] { firstPlayer, firstPlayer }
        ));
        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { firstPlayer, secondPlayer },
            new[] { firstPlayer }
        ));
        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { firstPlayer, secondPlayer },
            new[] { firstPlayer, outsidePlayer }
        ));
    }

    [Test]
    public void RuntimeStateSnapshotUsesExactNormalizedGameStatusAndOneBasedPlacements()
    {
        var firstPlayer = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 0);
        var secondPlayer = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 1);

        Assert.That(
            GCRuntimeStateSnapshotBuilder.BuildPayload(GCStatus.PendingSetup, new[] { firstPlayer }, new[] { firstPlayer }).game.status,
            Is.EqualTo("pending_setup")
        );
        Assert.That(
            GCRuntimeStateSnapshotBuilder.BuildPayload(GCStatus.SetupDone, new[] { firstPlayer }, new[] { firstPlayer }).game.status,
            Is.EqualTo("setup_done")
        );
        Assert.That(
            GCRuntimeStateSnapshotBuilder.BuildPayload(GCStatus.Playing, new[] { firstPlayer }, new[] { firstPlayer }).game.status,
            Is.EqualTo("playing")
        );
        Assert.That(
            GCRuntimeStateSnapshotBuilder.BuildPayload(GCStatus.GameOver, new[] { firstPlayer }, new[] { firstPlayer }).game.status,
            Is.EqualTo("game_over")
        );

        var reversedPayload = GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { firstPlayer, secondPlayer },
            new[] { secondPlayer, firstPlayer }
        );
        Assert.That(reversedPayload.players[0].placement, Is.EqualTo(2));
        Assert.That(reversedPayload.players[1].placement, Is.EqualTo(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            (GCStatus)999,
            new[] { firstPlayer },
            new[] { firstPlayer }
        ));
    }

    [Test]
    public void RemovedStateApisFailAtSourceWithMigrationGuidance()
    {
        AssertObsoleteError(
            typeof(GCPlayer).GetMethod("SetEliminated"),
            "SetEliminatedPermanent"
        );
        AssertObsoleteError(
            typeof(GCPlayer).GetMethod("SetEliminated"),
            "SetEliminatedRevokable"
        );
        AssertObsoleteError(
            typeof(GCPlayer).GetMethod("SetUneliminated"),
            "SetRevokeEliminated"
        );
        AssertObsoleteError(
            typeof(GCPlayer).GetMethod("SetFinished"),
            "SetFinishedPermanent"
        );
        AssertObsoleteError(
            typeof(GCPlayer).GetMethod("SetFinished"),
            "SetFinishedRevokable"
        );

        Assert.That(typeof(GCPlayer).GetField("OnEliminated"), Is.Null);
        Assert.That(typeof(GCPlayer).GetField("OnUneliminated"), Is.Null);
        Assert.That(typeof(GCPlayer).GetField("OnFinished"), Is.Null);
    }

    [Test]
    public void RemovedStoreCollectionsFailAtSourceWithMigrationGuidance()
    {
        var storeType = typeof(GCPlayerStore<GCPlayer>);

        AssertObsoleteError(storeType.GetProperty("PlayersEnumerable"), "Players");
        AssertObsoleteError(storeType.GetProperty("PlayerCount"), "Players.Count");
        AssertObsoleteError(storeType.GetProperty("UneliminatedPlayers"), "PlayersUneliminated");
        AssertObsoleteError(storeType.GetProperty("UneliminatedPlayersEnumerable"), "PlayersUneliminated");
        AssertObsoleteError(storeType.GetProperty("UneliminatedBotPlayers"), "PlayersUneliminatedBot");
        AssertObsoleteError(storeType.GetProperty("UneliminatedNonBotPlayers"), "PlayersUneliminatedNonBot");
        AssertObsoleteError(storeType.GetProperty("UneliminatedPlayerCount"), "PlayersUneliminated.Count");
        AssertObsoleteError(storeType.GetProperty("EliminatedPlayers"), "PlayersEliminated");
        AssertObsoleteError(storeType.GetProperty("EliminatedPlayersEnumerable"), "PlayersEliminated");
        AssertObsoleteError(storeType.GetProperty("EliminatedBotPlayers"), "PlayersEliminatedBot");
        AssertObsoleteError(storeType.GetProperty("EliminatedNonBotPlayers"), "PlayersEliminatedNonBot");
        AssertObsoleteError(storeType.GetProperty("EliminatedPlayerCount"), "PlayersEliminated.Count");
    }

    [Test]
    public void PostGameOverPlayerMutationsDiagnoseAndNoOp()
    {
        CreateGameOverGamingCouch();
        var player = GamingCouchEditorTestSupport.CreatePlayer(objectsToDestroy, 6);

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.post_game_over_mutation: Player mutation after game over was ignored.");
        player.SetEliminatedPermanent("too late");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.post_game_over_mutation: Player mutation after game over was ignored.");
        player.SetFinishedPermanent("too late");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.post_game_over_mutation: Player mutation after game over was ignored.");
        player.SetScore(10, "too late");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.post_game_over_mutation: Player mutation after game over was ignored.");
        player.SetLives(3, "too late");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.post_game_over_mutation: Player mutation after game over was ignored.");
        player.SetStatus(GCPlayerStatus.Success, "done", "too late");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.post_game_over_mutation: Player mutation after game over was ignored.");
        player.SetMeter(50, "too late");

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.None));
        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.None));
        Assert.That(player.Score, Is.EqualTo(0));
        Assert.That(player.Lives, Is.EqualTo(0));
        Assert.That(player.Status, Is.EqualTo(GCPlayerStatus.Neutral));
        Assert.That(player.StatusText, Is.EqualTo(""));
        Assert.That(player.Meter, Is.EqualTo(-1));
        LogAssert.NoUnexpectedReceived();
    }

    private static GCGame CreateGameWithPlacementCriteria(params GCPlacementSortCriteria[] criteria)
    {
        return new GCGame(null, new GCPlayerStore<GCPlayer>(), new GCGameSetupOptions
        {
            placementCriteria = criteria,
        });
    }

    private void CreateGameOverGamingCouch()
    {
        var gameObject = new GameObject("Gaming Couch");
        objectsToDestroy.Add(gameObject);
        gameObject.SetActive(false);
        var gamingCouch = gameObject.AddComponent<GamingCouch>();
        gamingCouch.LogLevel = LogLevel.None;
        gameObject.SetActive(true);
        typeof(GamingCouch)
            .GetField("status", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(gamingCouch, GCStatus.GameOver);
    }

    private static void AssertObsoleteError(MemberInfo member, string expectedGuidance)
    {
        Assert.That(member, Is.Not.Null);

        var obsolete = member.GetCustomAttribute<ObsoleteAttribute>();
        Assert.That(obsolete, Is.Not.Null);
        Assert.That(obsolete.IsError, Is.True);
        Assert.That(obsolete.Message, Does.Contain(expectedGuidance));
    }

    private static void AssertPublicCallbackField(string fieldName, Type fieldType)
    {
        var field = typeof(GCPlayer).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);

        Assert.That(field, Is.Not.Null);
        Assert.That(field.FieldType, Is.EqualTo(fieldType));
    }

    private static void AssertAcceptedTransition<TValue>(
        GCPlayerTransitionResult<TValue> transition,
        GCPlayerTransitionKind kind,
        int playerIndex,
        TValue previousValue,
        TValue value,
        string reason,
        bool wasClamped = false
    )
    {
        Assert.That(transition.Accepted, Is.True);
        Assert.That(transition.Outcome, Is.EqualTo(GCPlayerTransitionOutcome.Accepted));
        Assert.That(transition.IsNoOp, Is.False);
        Assert.That(transition.IsRejected, Is.False);
        Assert.That(transition.Kind, Is.EqualTo(kind));
        Assert.That(transition.PlayerIndex, Is.EqualTo(playerIndex));
        Assert.That(transition.PreviousValue, Is.EqualTo(previousValue));
        Assert.That(transition.Value, Is.EqualTo(value));
        Assert.That(transition.ReasonText, Is.EqualTo(reason));
        Assert.That(transition.ChangedAtGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(transition.EmitsSemanticTransition, Is.True);
        Assert.That(transition.LatestStateDirtyFlags, Is.EqualTo(
            GCPlayerLatestStateDirtyFlags.RuntimeStateSnapshot | GCPlayerLatestStateDirtyFlags.PlayersHud
        ));
        Assert.That(transition.MarksLatestStateDirty, Is.True);
        Assert.That(transition.MarksRuntimeStateSnapshotDirty, Is.True);
        Assert.That(transition.MarksPlayersHudDirty, Is.True);
        Assert.That(transition.WasClamped, Is.EqualTo(wasClamped));
        Assert.That(transition.RejectionReason, Is.EqualTo(GCPlayerTransitionRejectionReason.None));
    }

    private static void AssertInactiveTransition<TValue>(
        GCPlayerTransitionResult<TValue> transition,
        GCPlayerTransitionOutcome outcome,
        GCPlayerTransitionKind kind,
        int playerIndex,
        TValue previousValue,
        TValue value,
        string reason,
        GCPlayerTransitionRejectionReason rejectionReason,
        bool wasClamped = false
    )
    {
        Assert.That(transition.Accepted, Is.False);
        Assert.That(transition.Outcome, Is.EqualTo(outcome));
        Assert.That(transition.IsNoOp, Is.EqualTo(outcome == GCPlayerTransitionOutcome.NoOp));
        Assert.That(transition.IsRejected, Is.EqualTo(outcome == GCPlayerTransitionOutcome.Rejected));
        Assert.That(transition.Kind, Is.EqualTo(kind));
        Assert.That(transition.PlayerIndex, Is.EqualTo(playerIndex));
        Assert.That(transition.PreviousValue, Is.EqualTo(previousValue));
        Assert.That(transition.Value, Is.EqualTo(value));
        Assert.That(transition.ReasonText, Is.EqualTo(reason));
        Assert.That(transition.ChangedAtGameTime, Is.EqualTo(-1f));
        Assert.That(transition.EmitsSemanticTransition, Is.False);
        Assert.That(transition.LatestStateDirtyFlags, Is.EqualTo(GCPlayerLatestStateDirtyFlags.None));
        Assert.That(transition.MarksLatestStateDirty, Is.False);
        Assert.That(transition.MarksRuntimeStateSnapshotDirty, Is.False);
        Assert.That(transition.MarksPlayersHudDirty, Is.False);
        Assert.That(transition.WasClamped, Is.EqualTo(wasClamped));
        Assert.That(transition.RejectionReason, Is.EqualTo(rejectionReason));
    }

    private static void AssertEliminationEvent(
        GCPlayerEliminationStateChangedEventArgs args,
        int playerIndex,
        GCPlayerEliminationState oldState,
        GCPlayerEliminationState newState,
        string reason,
        float changedAtGameTime
    )
    {
        Assert.That(args.playerIndex, Is.EqualTo(playerIndex));
        Assert.That(args.oldState, Is.EqualTo(oldState));
        Assert.That(args.newState, Is.EqualTo(newState));
        Assert.That(args.reason, Is.EqualTo(reason));
        Assert.That(args.changedAtGameTime, Is.EqualTo(changedAtGameTime));
    }

    private static void AssertFinishEvent(
        GCPlayerFinishStateChangedEventArgs args,
        int playerIndex,
        GCPlayerFinishState oldState,
        GCPlayerFinishState newState,
        string reason,
        float changedAtGameTime
    )
    {
        Assert.That(args.playerIndex, Is.EqualTo(playerIndex));
        Assert.That(args.oldState, Is.EqualTo(oldState));
        Assert.That(args.newState, Is.EqualTo(newState));
        Assert.That(args.reason, Is.EqualTo(reason));
        Assert.That(args.changedAtGameTime, Is.EqualTo(changedAtGameTime));
    }

    private static void AssertStateDiagnosticContext(
        string json,
        string code,
        int playerIndex,
        string mutator,
        string oldState,
        string requestedState
    )
    {
        Assert.That(json, Does.Contain("\"type\":\"gc.diagnostic\""));
        Assert.That(json, Does.Contain("\"name\":\"" + code + "\""));
        Assert.That(json, Does.Contain("\"sourceArea\":\"state\""));
        Assert.That(json, Does.Contain("\"playerIndex\":" + playerIndex));
        Assert.That(json, Does.Contain("\"mutator\":\"" + mutator + "\""));
        Assert.That(json, Does.Contain("\"oldState\":\"" + oldState + "\""));
        Assert.That(json, Does.Contain("\"requestedState\":\"" + requestedState + "\""));
    }
}
