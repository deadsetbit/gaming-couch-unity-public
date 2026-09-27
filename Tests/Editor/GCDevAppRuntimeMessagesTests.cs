using DSB.GC;
using DSB.GC.Dev;
using NUnit.Framework;
using UnityEngine;

public sealed class GCDevAppRuntimeMessagesTests
{
    [TearDown]
    public void TearDown()
    {
        GCDevAppRuntimeOutputSettings.ResetForTests();
    }

    [Test]
    public void RuntimeRegisterMessageNormalizesPackageIdentityForWireFields()
    {
        var packageIdentity = GCEditorPackageIdentity.Resolve();
        var message = GCDevAppRuntimeMessages.BuildRuntimeRegisterMessage(
            123456789L,
            new TestProjectRootResolver("/tmp/gaming-couch-test", "Test Game")
        );
        var json = JsonUtility.ToJson(message);

        Assert.That(message.type, Is.EqualTo("runtime_register"));
        Assert.That(message.timestamp, Is.EqualTo(123456789L));
        Assert.That(message.runtimeKind, Is.EqualTo("unity_editor"));
        Assert.That(message.projectRootPath, Is.EqualTo("/tmp/gaming-couch-test"));
        Assert.That(message.projectName, Is.EqualTo("Test Game"));
        Assert.That(message.platform, Is.EqualTo(packageIdentity.platform));
        Assert.That(message.gameProtocolVersion, Is.EqualTo(packageIdentity.gameProtocolVersion));
        Assert.That(message.integrationName, Is.EqualTo(packageIdentity.packageName));
        Assert.That(message.integrationVersion, Is.EqualTo(packageIdentity.packageVersion));
        Assert.That(message.rendererMode, Is.EqualTo("external"));
        Assert.That(message.displayName, Is.EqualTo("Unity Editor"));
        Assert.That(json, Does.Contain("\"type\":\"runtime_register\""));
        Assert.That(json, Does.Contain("\"runtimeKind\":\"unity_editor\""));
        Assert.That(json, Does.Contain("\"projectRootPath\":\"/tmp/gaming-couch-test\""));
        Assert.That(json, Does.Contain("\"projectName\":\"Test Game\""));
        Assert.That(json, Does.Contain("\"platform\":\"" + packageIdentity.platform + "\""));
        Assert.That(json, Does.Contain("\"gameProtocolVersion\":" + packageIdentity.gameProtocolVersion));
        Assert.That(json, Does.Contain("\"integrationName\":\"" + packageIdentity.packageName + "\""));
        Assert.That(json, Does.Contain("\"integrationVersion\":\"" + packageIdentity.packageVersion + "\""));
        Assert.That(json, Does.Not.Contain("\"packageName\""));
        Assert.That(json, Does.Not.Contain("\"packageVersion\""));
        Assert.That(json, Does.Contain("\"rendererMode\":\"external\""));
        Assert.That(json, Does.Contain("\"displayName\":\"Unity Editor\""));
    }

    [Test]
    public void StoppedRuntimeSnapshotHasNoRunIdOrSeatsAndKeepsPauseAndTimescale()
    {
        var state = GCDevAppRuntimeMessages.BuildRuntimeSnapshotState(
            "run-that-should-not-leak",
            false,
            CreateSeatIdentities(),
            true,
            0.25f
        );
        var message = GCDevAppRuntimeMessages.BuildRuntimeSnapshotMessage(222L, state);

        Assert.That(state.runId, Is.Null);
        Assert.That(state.isRunning, Is.False);
        Assert.That(state.seats, Is.Empty);
        AssertCapabilities(state.capabilities);
        Assert.That(state.paused, Is.True);
        Assert.That(state.timescale, Is.EqualTo(0.25f));
        Assert.That(message.type, Is.EqualTo("runtime_snapshot"));
        Assert.That(message.timestamp, Is.EqualTo(222L));
        Assert.That(message.runId, Is.Null);
        Assert.That(message.isRunning, Is.False);
        Assert.That(message.seats, Is.Empty);
        Assert.That(message.paused, Is.True);
        Assert.That(message.timescale, Is.EqualTo(0.25f));
    }

    [Test]
    public void RunningRuntimeSnapshotIncludesRunIdCapabilitiesPauseTimescaleAndSeats()
    {
        var state = GCDevAppRuntimeMessages.BuildRuntimeSnapshotState(
            "run-123",
            true,
            CreateSeatIdentities(),
            false,
            1.5f
        );
        var message = GCDevAppRuntimeMessages.BuildRuntimeSnapshotMessage(333L, state);

        Assert.That(message.type, Is.EqualTo("runtime_snapshot"));
        Assert.That(message.timestamp, Is.EqualTo(333L));
        Assert.That(message.runId, Is.EqualTo("run-123"));
        Assert.That(message.isRunning, Is.True);
        AssertCapabilities(message.capabilities);
        Assert.That(message.paused, Is.False);
        Assert.That(message.timescale, Is.EqualTo(1.5f));
        Assert.That(message.seats, Has.Length.EqualTo(3));
        Assert.That(message.seats[0].playerIndex, Is.EqualTo(0));
        Assert.That(message.seats[1].playerIndex, Is.EqualTo(1));
        Assert.That(message.seats[2].playerIndex, Is.EqualTo(2));
    }

    [Test]
    public void RuntimeSeatsPreserveSourceIndexFallbackLabelFallbackAndPlayerTypes()
    {
        var state = GCDevAppRuntimeMessages.BuildRuntimeSnapshotState(
            "run-123",
            true,
            CreateSeatIdentities(),
            false,
            1.0f
        );

        Assert.That(state.seats, Has.Length.EqualTo(3));
        Assert.That(state.seats[0].playerIndex, Is.EqualTo(0));
        Assert.That(state.seats[0].seatIndex, Is.EqualTo(4));
        Assert.That(state.seats[0].label, Is.EqualTo("Seat 4"));
        Assert.That(state.seats[0].type, Is.EqualTo("bot"));
        Assert.That(state.seats[1].playerIndex, Is.EqualTo(1));
        Assert.That(state.seats[1].seatIndex, Is.EqualTo(2));
        Assert.That(state.seats[1].label, Is.EqualTo("Seat 2"));
        Assert.That(state.seats[1].type, Is.EqualTo("player"));
        Assert.That(state.seats[2].playerIndex, Is.EqualTo(2));
        Assert.That(state.seats[2].seatIndex, Is.EqualTo(8));
        Assert.That(state.seats[2].label, Is.EqualTo("Custom Seat"));
        Assert.That(state.seats[2].type, Is.EqualTo("player"));
    }

    [Test]
    public void SnapshotSignatureIsStableUntilRuntimeStateChanges()
    {
        var state = GCDevAppRuntimeMessages.BuildRuntimeSnapshotState(
            "run-123",
            true,
            CreateSeatIdentities(),
            false,
            1.0f
        );
        var unchangedState = GCDevAppRuntimeMessages.BuildRuntimeSnapshotState(
            "run-123",
            true,
            CreateSeatIdentities(),
            false,
            1.0f
        );
        var changedState = GCDevAppRuntimeMessages.BuildRuntimeSnapshotState(
            "run-123",
            true,
            CreateSeatIdentities(),
            true,
            1.0f
        );

        Assert.That(
            GCDevAppRuntimeMessages.BuildRuntimeSnapshotSignature(unchangedState),
            Is.EqualTo(GCDevAppRuntimeMessages.BuildRuntimeSnapshotSignature(state))
        );
        Assert.That(
            GCDevAppRuntimeMessages.BuildRuntimeSnapshotSignature(changedState),
            Is.Not.EqualTo(GCDevAppRuntimeMessages.BuildRuntimeSnapshotSignature(state))
        );
    }

    [Test]
    public void SnapshotJsonKeepsWireFieldNames()
    {
        var state = GCDevAppRuntimeMessages.BuildRuntimeSnapshotState(
            "run-123",
            true,
            CreateSeatIdentities(),
            true,
            0.5f
        );
        var json = JsonUtility.ToJson(GCDevAppRuntimeMessages.BuildRuntimeSnapshotMessage(444L, state));

        Assert.That(json, Does.Contain("\"type\":\"runtime_snapshot\""));
        Assert.That(json, Does.Contain("\"runId\":\"run-123\""));
        Assert.That(json, Does.Contain("\"isRunning\":true"));
        Assert.That(json, Does.Contain("\"capabilities\""));
        Assert.That(json, Does.Contain("\"seats\""));
        Assert.That(json, Does.Contain("\"playerIndex\":0"));
        Assert.That(json, Does.Contain("\"seatIndex\":4"));
        Assert.That(json, Does.Contain("\"label\":\"Seat 4\""));
        Assert.That(json, Does.Contain("\"type\":\"bot\""));
        Assert.That(json, Does.Not.Contain("playerId"));
        Assert.That(json, Does.Not.Contain("platformPlayerId"));
        Assert.That(json, Does.Not.Contain("stableKey"));
        Assert.That(json, Does.Contain("\"paused\":true"));
        Assert.That(json, Does.Contain("\"timescale\":0.5"));
    }

    [Test]
    public void RuntimeOutputSettingsPreserveExplicitRuntimeLogCaptureWithoutDevAppOverride()
    {
        var options = new GCRuntimeOutputOptions
        {
            runtimeLogCapture = GCRuntimeUnityLogCaptureMode.Full,
        };

        var appliedOptions = GCDevAppRuntimeOutputSettings.Apply(options);

        Assert.That(appliedOptions.runtimeLogCapture, Is.EqualTo(GCRuntimeUnityLogCaptureMode.Full));
    }

    [Test]
    public void RuntimeOutputSettingsApplyDevAppRuntimeLogCaptureOverride()
    {
        var options = new GCRuntimeOutputOptions
        {
            runtimeLogCapture = GCRuntimeUnityLogCaptureMode.Full,
        };
        GCDevAppRuntimeOutputSettings.SetRuntimeLogCaptureMode(GCRuntimeUnityLogCaptureMode.WarningAndError);

        var appliedOptions = GCDevAppRuntimeOutputSettings.Apply(options);

        Assert.That(appliedOptions.runtimeLogCapture, Is.EqualTo(GCRuntimeUnityLogCaptureMode.WarningAndError));
    }

    private static GCSeatIdentity[] CreateSeatIdentities()
    {
        return new[]
        {
            new GCSeatIdentity
            {
                sourceSeatIndex = 4,
                label = null,
                playerType = GCPlayerType.bot,
            },
            new GCSeatIdentity
            {
                sourceSeatIndex = 0,
                label = string.Empty,
                playerType = GCPlayerType.player,
            },
            new GCSeatIdentity
            {
                sourceSeatIndex = 8,
                label = "Custom Seat",
                playerType = GCPlayerType.player,
            },
        };
    }

    private static void AssertCapabilities(RuntimeCapabilitiesMessage capabilities)
    {
        Assert.That(capabilities, Is.Not.Null);
        Assert.That(capabilities.restart, Is.True);
        Assert.That(capabilities.pause, Is.True);
        Assert.That(capabilities.timescale, Is.True);
    }

    private sealed class TestProjectRootResolver : IGCLocalProjectRootResolver
    {
        private readonly string projectRootPath;
        private readonly string projectName;

        internal TestProjectRootResolver(string projectRootPath, string projectName)
        {
            this.projectRootPath = projectRootPath;
            this.projectName = projectName;
        }

        public string ResolveProjectRootPath()
        {
            return projectRootPath;
        }

        public string ResolveProjectName()
        {
            return projectName;
        }
    }
}
