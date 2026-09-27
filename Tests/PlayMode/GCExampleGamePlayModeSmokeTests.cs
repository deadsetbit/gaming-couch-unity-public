#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DSB.GC;
using DSB.GC.Dev;
using DSB.GC.ExampleCanonical;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Behavioral smoke test for the canonical example game (the master the generator copies into the
// user's project as GCExampleGame + GCExamplePlayer). It drives the real editor-play lifecycle —
// setup -> play -> input -> game-over — and asserts the platform calls fired in order, expressed via
// observable state: the example spawns its players (setup + play ran), primary input scores a player
// (input ran while Playing), and the round ends at GCStatus.GameOver (game-over ran). This is the
// only PlayMode test; all others are EditMode. See plan §6.4 / ADR 0017.
//
// Editor-only, guarded with #if UNITY_EDITOR: it drives the editor-play capture lifecycle
// (GCLocalPlaySession et al., all #if UNITY_EDITOR), so a standalone build strips those types. We
// guard the *file* rather than pin the asmdef to the Editor platform on purpose — an Editor-only
// includePlatforms reclassifies this assembly as EditMode and empties the PlayMode/Player tabs. With
// the guard, it stays a PlayMode test in the Editor, and the "Player" run compiles it to nothing
// instead of failing on the stripped types. Do NOT remove the guard.
public sealed class GCExampleGamePlayModeSmokeTests
{
    private GamingCouch gamingCouch;
    private GameObject listenerObject;
    private GameObject playerPrefab;
    private IDisposable sessionOverride;

    [UnityTest]
    public IEnumerator ExampleGameDrivesSetupPlayInputAndGameOver()
    {
        // 1. Fake a valid editor-play capture (2 players) so the runtime's SetupDone -> _EditorPlay ->
        //    Play chain runs without a gc.dev.json file on disk.
        var provider = new FakeLocalPlaySessionProvider(() => CreateSuccessfulCapture("smoke", 123, 1, 2));
        sessionOverride = GCLocalPlaySession.OverrideForTests(provider, null, null);
        Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.True, "test capture did not succeed");

        // 2. Player prefab: a GameObject whose root has GCExamplePlayerSource (: GCPlayer). It also
        //    carries the component, so SpawnedPlayers() filters it out of the spawned count.
        playerPrefab = new GameObject("GCExamplePlayer", typeof(GCExamplePlayerSource));

        // 3. Listener with the full example game; shorten the round so game-over arrives quickly while
        //    leaving a comfortable window to exercise input.
        listenerObject = new GameObject("Game");
        var game = listenerObject.AddComponent<GCExampleGameSource>();
        SetPrivateField(game, "roundSeconds", 2.0f);

        // 4. GamingCouch created inactive so listener/playerPrefab are wired before Awake (avoids its
        //    "listener not set" error log), then activated.
        var gcObject = new GameObject("GamingCouch");
        gcObject.SetActive(false);
        gamingCouch = gcObject.AddComponent<GamingCouch>();
        SetPrivateField(gamingCouch, "listener", listenerObject);
        SetPrivateField(gamingCouch, "playerPrefab", playerPrefab);
        gcObject.SetActive(true);
        Assert.That(GamingCouch.Instance, Is.SameAs(gamingCouch));

        // 5. The runtime drives the whole contract itself: GamingCouch.Start() reads the captured
        //    options and SendMessages "GamingCouchSetup" to the listener, which then runs
        //    SetupGameVersus -> SetupDone -> _EditorPlay -> Play -> "GamingCouchPlay". We only pump
        //    frames and observe; triggering setup ourselves would double-run it ("Game already set").

        // 6. Setup + play ran: the example spawned its two players and the platform is Playing.
        yield return WaitUntil(() => SpawnedPlayers().Length == 2, timeoutSeconds: 8f);
        Assert.That(SpawnedPlayers().Length, Is.EqualTo(2), "example game did not spawn its two players");
        Assert.That(gamingCouch.Status, Is.EqualTo(GCStatus.Playing), "platform did not reach Playing");

        // 7. Input phase: hold "primary" for player 0 while Playing; the game's Update scores it.
        var player0 = SpawnedPlayers().Single(player => player.Index == 0);
        var scoreBefore = player0.Score;
        yield return WaitUntil(
            () =>
            {
                gamingCouch.ApplyExternalPlayerInput(0, new GCControllerInputsData { b0 = 1 }, "test_input");
                return player0.Score > scoreBefore;
            },
            timeoutSeconds: 8f
        );
        Assert.That(player0.Score, Is.GreaterThan(scoreBefore), "primary input did not score the player");

        // 8. Game-over: the round coroutine finishes and calls GameOver().
        yield return WaitUntil(() => gamingCouch.Status == GCStatus.GameOver, timeoutSeconds: 8f);
        Assert.That(gamingCouch.Status, Is.EqualTo(GCStatus.GameOver), "platform did not reach GameOver");
    }

    [TearDown]
    public void TearDown()
    {
        // GamingCouch is DontDestroyOnLoad, so PlayMode scene teardown will not collect it; destroy it
        // explicitly or the next test sees a stale singleton.
        if (gamingCouch != null)
        {
            UnityEngine.Object.DestroyImmediate(gamingCouch.gameObject);
        }
        if (listenerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(listenerObject);
        }
        if (playerPrefab != null)
        {
            UnityEngine.Object.DestroyImmediate(playerPrefab);
        }

        foreach (var stray in SpawnedPlayers())
        {
            UnityEngine.Object.DestroyImmediate(stray.gameObject);
        }

        sessionOverride?.Dispose();
        sessionOverride = null;
    }

    // Players the example spawned at runtime — excludes the prefab source GameObject, which also has
    // the component but is not a managed player.
    private GCExamplePlayerSource[] SpawnedPlayers()
    {
        return UnityEngine.Object
            .FindObjectsByType<GCExamplePlayerSource>(FindObjectsSortMode.None)
            .Where(player => player != null && player.gameObject != playerPrefab)
            .ToArray();
    }

    private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
    {
        var start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeoutSeconds)
        {
            yield return null;
        }
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "missing private field " + name + " on " + target.GetType().Name);
        field.SetValue(target, value);
    }

    // Mirrors GCLocalPlaySessionTests.CreateSuccessfulCapture.
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

    private sealed class FakeLocalPlaySessionProvider : IGCLocalPlaySessionProvider
    {
        private readonly Func<GCLocalPlaySessionCaptureResult> captureHandler;

        internal FakeLocalPlaySessionProvider(Func<GCLocalPlaySessionCaptureResult> captureHandler)
        {
            this.captureHandler = captureHandler;
        }

        public GCLocalPlaySessionCaptureResult Capture()
        {
            return captureHandler();
        }

        public GCLocalPlaySessionPreflightResult Validate(GCLocalPlaySessionBoundary context)
        {
            return GCLocalPlaySessionPreflightResult.Succeeded();
        }
    }
}
#endif
