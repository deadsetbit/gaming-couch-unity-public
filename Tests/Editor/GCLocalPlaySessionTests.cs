using System;
using System.Text.RegularExpressions;
using DSB.GC;
using DSB.GC.Dev;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GCLocalPlaySessionTests
{
    [Test]
    public void ValidCaptureIsCachedAndReusedForSetupAndPlayAccess()
    {
        var provider = new FakeLocalPlaySessionProvider
        {
            CaptureHandler = () => CreateSuccessfulCapture("duel", 12345, 1, 3),
        };

        using (GCLocalPlaySession.OverrideForTests(provider, null, null))
        {
            Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.True);
            Assert.That(provider.CaptureCount, Is.EqualTo(1));

            Assert.That(
                GCLocalPlaySession.TryRequireCapturedSetupOptions("Test setup", out var setupOptions),
                Is.True
            );
            Assert.That(setupOptions.mode, Is.EqualTo(GCMode.Development));
            Assert.That(setupOptions.isServer, Is.True);
            Assert.That(setupOptions.gameModeId, Is.EqualTo("duel"));

            Assert.That(
                GCLocalPlaySession.TryRequireCapturedPlayOptions("Test play", out var playOptions, out var seatIdentities),
                Is.True
            );
            Assert.That(playOptions.seed, Is.EqualTo(12345));
            Assert.That(playOptions.players, Has.Length.EqualTo(2));
            Assert.That(seatIdentities, Has.Length.EqualTo(2));
            Assert.That(playOptions.players[0].playerIndex, Is.EqualTo(0));
            Assert.That(playOptions.players[1].playerIndex, Is.EqualTo(1));
            Assert.That(seatIdentities[0].sourceSeatIndex, Is.EqualTo(1));
            Assert.That(seatIdentities[0].stableKey, Is.EqualTo("1"));
            Assert.That(seatIdentities[1].sourceSeatIndex, Is.EqualTo(3));
            Assert.That(seatIdentities[1].stableKey, Is.EqualTo("3"));
            var playOptionsJson = JsonUtility.ToJson(playOptions);
            Assert.That(playOptionsJson, Does.Contain("\"playerIndex\":0"));
            Assert.That(playOptionsJson, Does.Contain("\"playerIndex\":1"));
            Assert.That(playOptionsJson, Does.Not.Contain("playerId"));
            Assert.That(playOptionsJson, Does.Not.Contain("platformPlayerId"));
            Assert.That(playOptionsJson, Does.Not.Contain("sourceSeatIndex"));
            Assert.That(playOptionsJson, Does.Not.Contain("stableKey"));
            Assert.That(provider.CaptureCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void FailedCaptureBlocksSetupAndPlayAccessWithValidationDetails()
    {
        var provider = new FakeLocalPlaySessionProvider
        {
            CaptureHandler = () => CreateFailedCapture(
                "/tmp/gc.dev.json",
                "Valid platform data gate failed because enabled seat count exceeds the selected entry maximum."
            ),
        };

        using (GCLocalPlaySession.OverrideForTests(provider, null, null))
        {
            LogAssert.Expect(LogType.Error, new Regex("enabled seat count exceeds"));
            Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.False);

            var activeCapture = GCLocalPlaySession.GetActiveCaptureForTests();
            Assert.That(activeCapture, Is.Not.Null);
            Assert.That(activeCapture.success, Is.False);
            Assert.That(FindIssue(activeCapture.validation, "enabled seat count exceeds"), Is.Not.Null);

            LogAssert.Expect(LogType.Error, new Regex("Test setup blocked because root gc\\.dev\\.json"));
            Assert.That(
                GCLocalPlaySession.TryRequireCapturedSetupOptions("Test setup", out _),
                Is.False
            );

            LogAssert.Expect(LogType.Error, new Regex("Test play blocked because root gc\\.dev\\.json"));
            Assert.That(
                GCLocalPlaySession.TryRequireCapturedPlayOptions("Test play", out _, out _),
                Is.False
            );
        }
    }

    [Test]
    public void RestartPreflightFailureDoesNotClearOrReplaceActiveCapture()
    {
        var provider = new FakeLocalPlaySessionProvider
        {
            CaptureHandler = () => CreateSuccessfulCapture("duel", 111, 1),
        };

        using (GCLocalPlaySession.OverrideForTests(
            provider,
            _ => GCLocalPlaySessionPreflightResult.Failed(
                "Gaming Couch restart blocked by test.",
                "/tmp/gc.dev.json",
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    "test failure",
                    "/tmp/gc.dev.json"
                ))
            ),
            null
        ))
        {
            Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.True);
            var activeBeforePreflight = GCLocalPlaySession.GetActiveCaptureForTests();

            var preflightResult = GCLocalPlaySession.RunPreflight(GCLocalPlaySessionBoundary.GamingCouchRestart);

            Assert.That(preflightResult.success, Is.False);
            Assert.That(GCLocalPlaySession.GetActiveCaptureForTests(), Is.SameAs(activeBeforePreflight));
            Assert.That(GCLocalPlaySession.GetActiveCaptureForTests().playOptions.seed, Is.EqualTo(111));
        }
    }

    [Test]
    public void RootValidationStillRunsAfterRegisteredPreflightSucceeds()
    {
        var provider = new FakeLocalPlaySessionProvider
        {
            ValidateHandler = _ => GCLocalPlaySessionPreflightResult.Failed(
                "Unity Play Mode entry blocked by root validation.",
                "/tmp/gc.dev.json",
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    "Valid platform data gate failed because enabled seat count exceeds the selected entry maximum.",
                    "/tmp/gc.dev.json"
                ))
            ),
        };

        var preflightCount = 0;
        using (GCLocalPlaySession.OverrideForTests(
            provider,
            _ =>
            {
                preflightCount++;
                return GCLocalPlaySessionPreflightResult.Succeeded();
            },
            null
        ))
        {
            var result = GCLocalPlaySession.RunPreflight(GCLocalPlaySessionBoundary.UnityPlayModeEntry);

            Assert.That(preflightCount, Is.EqualTo(1));
            Assert.That(provider.ValidateCount, Is.EqualTo(1));
            Assert.That(result.success, Is.False);
            Assert.That(FindIssue(result.validation, "enabled seat count exceeds"), Is.Not.Null);
        }
    }

    [Test]
    public void SuccessfulRestartPreflightFollowedByRecaptureReadsLatestLocalPlaySettings()
    {
        var seed = 111;
        var enabledSeats = new[] { 1 };
        var provider = new FakeLocalPlaySessionProvider
        {
            CaptureHandler = () => CreateSuccessfulCapture("duel", seed, enabledSeats),
            ValidateHandler = _ => GCLocalPlaySessionPreflightResult.Succeeded(),
        };

        using (GCLocalPlaySession.OverrideForTests(provider, null, null))
        {
            Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.True);
            Assert.That(GCLocalPlaySession.GetActiveCaptureForTests().playOptions.seed, Is.EqualTo(111));

            seed = 222;
            enabledSeats = new[] { 1, 2 };
            var preflightResult = GCLocalPlaySession.RunPreflight(GCLocalPlaySessionBoundary.GamingCouchRestart);

            Assert.That(preflightResult.success, Is.True);
            Assert.That(GCLocalPlaySession.GetActiveCaptureForTests().playOptions.seed, Is.EqualTo(111));
            Assert.That(GCLocalPlaySession.CaptureForRestart(), Is.True);
            Assert.That(GCLocalPlaySession.GetActiveCaptureForTests().playOptions.seed, Is.EqualTo(222));
            Assert.That(GCLocalPlaySession.GetActiveCaptureForTests().playOptions.players, Has.Length.EqualTo(2));
        }
    }

    [Test]
    public void CaptureSuccessNotificationFiresOnlyAfterSuccessfulCapture()
    {
        var shouldFail = true;
        var provider = new FakeLocalPlaySessionProvider
        {
            CaptureHandler = () => shouldFail
                ? CreateFailedCapture(
                    "/tmp/gc.dev.json",
                    "Valid platform data gate failed because enabled seat count exceeds the selected entry maximum."
                )
                : CreateSuccessfulCapture("duel", 12345, 1),
        };

        var captureSucceededCount = 0;
        using (GCLocalPlaySession.OverrideForTests(provider, null, () => captureSucceededCount++))
        {
            LogAssert.Expect(LogType.Error, new Regex("enabled seat count exceeds"));
            Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.False);
            Assert.That(captureSucceededCount, Is.EqualTo(0));

            shouldFail = false;
            Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.True);
            Assert.That(captureSucceededCount, Is.EqualTo(1));
        }
    }

    private static GCLocalPlaySessionIssue FindIssue(
        GCLocalPlaySessionValidationResult validation,
        string messageFragment
    )
    {
        if (validation == null || validation.issues == null)
        {
            return null;
        }

        for (var index = 0; index < validation.issues.Length; index++)
        {
            var issue = validation.issues[index];
            if (issue != null && issue.message != null && issue.message.Contains(messageFragment))
            {
                return issue;
            }
        }

        return null;
    }

    private static GCLocalPlaySessionCaptureResult CreateSuccessfulCapture(
        string entryKey,
        int seed,
        params int[] enabledSeats
    )
    {
        enabledSeats = enabledSeats ?? Array.Empty<int>();
        var setupOptions = new GCSetupOptions
        {
            isServer = true,
            gameModeId = entryKey,
            mode = GCMode.Development,
        };
        var playOptions = new GCPlayOptions
        {
            players = new GCPlayerOptions[enabledSeats.Length],
            seed = seed,
        };
        var seatIdentities = new GCSeatIdentity[enabledSeats.Length];

        for (var index = 0; index < enabledSeats.Length; index++)
        {
            var seatIndex = enabledSeats[index];
            playOptions.players[index] = new GCPlayerOptions
            {
                playerIndex = index,
                type = GCPlayerType.player.ToString(),
                color = GCPlayerColor.blue.ToString(),
            };
            seatIdentities[index] = new GCSeatIdentity
            {
                sourceSeatIndex = seatIndex,
                stableKey = seatIndex.ToString(),
                label = "Seat " + seatIndex,
                playerType = GCPlayerType.player,
                playerColor = GCPlayerColor.blue,
            };
        }

        return GCLocalPlaySessionCaptureResult.Succeeded(
            setupOptions,
            playOptions,
            seatIdentities,
            GCLocalPlaySessionValidationResult.Valid(),
            "/tmp/gc.dev.json"
        );
    }

    private static GCLocalPlaySessionCaptureResult CreateFailedCapture(string path, string message)
    {
        return GCLocalPlaySessionCaptureResult.Failed(
            path,
            GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(message, path))
        );
    }

    private sealed class FakeLocalPlaySessionProvider : IGCLocalPlaySessionProvider
    {
        internal Func<GCLocalPlaySessionCaptureResult> CaptureHandler { private get; set; }
        internal Func<GCLocalPlaySessionBoundary, GCLocalPlaySessionPreflightResult> ValidateHandler { private get; set; }
        internal int CaptureCount { get; private set; }
        internal int ValidateCount { get; private set; }

        public GCLocalPlaySessionCaptureResult Capture()
        {
            CaptureCount++;
            return CaptureHandler != null
                ? CaptureHandler()
                : CreateSuccessfulCapture("duel", 12345, 1);
        }

        public GCLocalPlaySessionPreflightResult Validate(GCLocalPlaySessionBoundary context)
        {
            ValidateCount++;
            return ValidateHandler != null
                ? ValidateHandler(context)
                : GCLocalPlaySessionPreflightResult.Succeeded();
        }
    }
}
