#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DSB.GC;
using DSB.GC.Dev;
using DSB.GC.ExampleCanonical;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Gameplay tests for the canonical example game, the master the generator copies into the user's
// project as GCExampleGame, GCExamplePlayer, GCExampleRing and GCExampleBot. The game tests drive the
// real editor-play lifecycle (setup, play, game over) through a faked capture and observe platform
// state. The ball and ring tests drive one component on its own. See ADR 0017.
//
// Editor-only, guarded with #if UNITY_EDITOR: it drives the editor-play capture lifecycle
// (GCLocalPlaySession et al., all #if UNITY_EDITOR), so a standalone build strips those types. We
// guard the *file* rather than pin the asmdef to the Editor platform on purpose — an Editor-only
// includePlatforms reclassifies this assembly as EditMode and empties the PlayMode/Player tabs. With
// the guard, it stays a PlayMode test in the Editor, and the "Player" run compiles it to nothing
// instead of failing on the stripped types. Do NOT remove the guard.
public sealed class GCExampleGamePlayModeTests
{
    private const float BallRadius = 0.5f;

    // Radius of the smallest circle that holds n unit circles, for n = 0..8
    // (https://erich-friedman.github.io/packing/cirincir/).
    private static readonly float[] PackingRadiusRatios = { 0f, 1f, 2f, 2.1547f, 2.4142f, 2.7013f, 3f, 3f, 3.3048f };

    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private GamingCouch gamingCouch;
    private IDisposable sessionOverride;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        DestroyGame();
        foreach (var createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }
        }

        createdObjects.Clear();
    }

    [UnityTest]
    public IEnumerator HumansAndBotsPlayUntilExactlyOneWinnerAndGameOver()
    {
        yield return StartGame(7, new[] { GCPlayerType.player, GCPlayerType.bot, GCPlayerType.bot }, ring =>
        {
            SetPrivateField(ring, "breathingSeconds", 0.2f);
            SetCycleSeconds(ring, 0.6f, 0.6f, 0.6f, 0.6f);
        });

        var players = Players();
        Assert.That(players.Length, Is.EqualTo(3), "the game did not spawn the three captured players");
        Assert.That(gamingCouch.Status, Is.EqualTo(GCStatus.Playing));
        foreach (var player in players)
        {
            Assert.That(player.GetComponent<GCExampleBotSource>() != null, Is.EqualTo(player.IsBot), "only bots get the bot component");
            Assert.That(player.Position.magnitude, Is.EqualTo(1.8f).Within(0.01f), "every ball starts the same distance from the centre");
            Assert.That(player.Meter, Is.EqualTo(100), "the HUD meter starts at full stamina");
        }

        yield return WaitUntil(() => gamingCouch.Status == GCStatus.GameOver, 10f);

        Assert.That(gamingCouch.Status, Is.EqualTo(GCStatus.GameOver));
        var survivors = players.Where(player => !player.IsEliminated).ToArray();
        Assert.That(survivors.Length, Is.EqualTo(1), "exactly one player must win");
        foreach (var player in players.Where(player => player.IsEliminated))
        {
            Assert.That(player.IsEliminatedPermanent, Is.True);
            Assert.That(player.gameObject.activeSelf, Is.False, "an eliminated ball must disappear");
        }
    }

    [UnityTest]
    public IEnumerator EightPlayersWhoStayPutAllSurviveTheFirstSqueeze()
    {
        // The real cycle length, run four times faster.
        Time.timeScale = 4f;
        yield return StartGame(13, Humans(8), null);
        var ring = Ring();

        yield return WaitUntil(() => ring.Cycle == 1, 8f);

        Assert.That(ring.Cycle, Is.EqualTo(1));
        Assert.That(Players().Count(player => player.IsEliminated), Is.EqualTo(0), "the first squeeze is generous, even as repulsion spreads the crowd");
    }

    [UnityTest]
    public IEnumerator BallsTouchingTheRingTogetherLeaveTheClosestToTheCentre()
    {
        yield return StartGame(11, Humans(3), null);
        var players = Players();

        // The closest ball is the last one in the store, so iteration order cannot pick it.
        PlaceAt(players[0], new Vector2(-2f, 0f));
        PlaceAt(players[1], new Vector2(0f, 2f));
        PlaceAt(players[2], new Vector2(1.95f, 0f));
        SetPrivateField(Ring(), "fullRadius", 2.3f);

        yield return WaitUntil(() => gamingCouch.Status == GCStatus.GameOver, 2f);

        Assert.That(gamingCouch.Status, Is.EqualTo(GCStatus.GameOver));
        Assert.That(players[2].IsEliminated, Is.False, "the ball closest to the centre must win");
        Assert.That(players[0].IsEliminatedPermanent && players[1].IsEliminatedPermanent, Is.True);
        Assert.That(players[0].LastSetEliminatedGameTime, Is.EqualTo(players[1].LastSetEliminatedGameTime), "both went out in the same step");
    }

    [UnityTest]
    public IEnumerator AnExactDistanceTieIsBrokenByTheSeedNotByPlayerOrder()
    {
        // Two seeds whose first pick among three differs, so at least one winner is not the first
        // player in the store's order.
        var seeds = FindSeedsWithDifferentFirstPick(3);
        var winningPicks = new List<int>();

        foreach (var seed in seeds)
        {
            yield return StartGame(seed, Humans(3), null);
            Assert.That(gamingCouch.GameSeed, Is.EqualTo(seed));
            var players = Players();

            PlaceAt(players[0], new Vector2(2f, 0f));
            PlaceAt(players[1], new Vector2(-2f, 0f));
            PlaceAt(players[2], new Vector2(0f, -2f));
            SetPrivateField(Ring(), "fullRadius", 2.3f);

            yield return WaitUntil(() => gamingCouch.Status == GCStatus.GameOver, 2f);

            var byIndex = players.OrderBy(player => player.Index).ToList();
            var expectedPick = new System.Random(seed).Next(byIndex.Count);
            var winners = players.Where(player => !player.IsEliminated).ToArray();
            Assert.That(winners.Length, Is.EqualTo(1));
            Assert.That(winners[0], Is.SameAs(byIndex[expectedPick]), "the seeded pick over index order decides an exact tie");
            winningPicks.Add(expectedPick);

            DestroyGame();
        }

        Assert.That(winningPicks.Distinct().Count(), Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator AnEliminationPausesTheRingWhileSurvivorsKeepMoving()
    {
        yield return StartGame(3, Humans(3), ring => SetPrivateField(ring, "breathingSeconds", 0f));
        var players = Players();
        var ring = Ring();

        // Let the ring start shrinking, then push one ball onto it.
        yield return new WaitForSeconds(0.5f);
        PlaceAt(players[0], new Vector2(ring.Radius - 0.2f, 0f));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.That(players[0].IsEliminatedPermanent, Is.True);
        Assert.That(players[0].gameObject.activeInHierarchy, Is.False, "an eliminated ball stops colliding");
        Assert.That(gamingCouch.InternalPlayerStore.Players, Has.Member(players[0]), "the GC player record stays for the HUD and results");
        Assert.That(ring.IsPaused, Is.True, "an elimination pauses the ring");

        var pausedRadius = ring.Radius;
        var mover = players[1];
        var start = mover.Position;
        var pauseStart = Time.time;
        while (Time.time - pauseStart < 0.3f)
        {
            gamingCouch.ApplyExternalPlayerInput(mover.Index, new GCControllerInputsData { a0 = 1f }, "test_input");
            yield return null;
        }

        Assert.That(ring.Radius, Is.EqualTo(pausedRadius), "the ring does not shrink during the pause");
        Assert.That(mover.Position.x - start.x, Is.GreaterThan(0.3f), "players keep moving during the pause");

        yield return new WaitForSeconds(0.4f);
        Assert.That(ring.IsPaused, Is.False);
        Assert.That(ring.Radius, Is.LessThan(pausedRadius), "the ring shrinks again after the pause");
    }

    [UnityTest]
    public IEnumerator RestartStartsAFreshGameWithNothingLeftOver()
    {
        yield return StartGame(5, Humans(2), null);
        var firstRun = Players();
        PlaceAt(firstRun[0], new Vector2(Ring().Radius, 0f));
        yield return WaitUntil(() => gamingCouch.Status == GCStatus.GameOver, 2f);
        Assert.That(gamingCouch.Status, Is.EqualTo(GCStatus.GameOver));

        gamingCouch.Restart();
        yield return WaitUntil(() => gamingCouch.Status == GCStatus.Playing && Players().Length == 2, 5f);

        var secondRun = Players();
        Assert.That(secondRun.Length, Is.EqualTo(2));
        Assert.That(secondRun.Intersect(firstRun), Is.Empty, "the first run's balls are gone");
        foreach (var player in secondRun)
        {
            Assert.That(player.IsEliminated, Is.False);
            Assert.That(player.Stamina, Is.EqualTo(1f));
            Assert.That(player.gameObject.activeSelf, Is.True);
        }

        Assert.That(Ring().Cycle, Is.EqualTo(0));
        Assert.That(Ring().Radius, Is.EqualTo(Ring().FullRadius));
        Assert.That(Ring().IsPaused, Is.False);
    }

    [UnityTest]
    public IEnumerator BotsSprintOnTheSameStaminaAndSpeedAsHumans()
    {
        yield return StartGame(9, new[] { GCPlayerType.bot, GCPlayerType.player }, null);
        var bot = Players().Single(player => player.IsBot);

        // Near the ring a bot sprints back toward the centre.
        PlaceAt(bot, new Vector2(Ring().Radius - 1.2f, 0f));
        var maxSpeed = 0f;
        var minStamina = 1f;
        var start = Time.time;
        while (Time.time - start < 0.5f)
        {
            maxSpeed = Mathf.Max(maxSpeed, bot.GetComponent<Rigidbody2D>().linearVelocity.magnitude);
            minStamina = Mathf.Min(minStamina, bot.Stamina);
            yield return new WaitForFixedUpdate();
        }

        Assert.That(bot.IsEliminated, Is.False);
        Assert.That(minStamina, Is.LessThan(1f), "the bot's sprint drains its stamina");
        Assert.That(minStamina, Is.GreaterThan(0.6f), "the bot drains stamina no faster than a human");
        Assert.That(maxSpeed, Is.LessThanOrEqualTo(5f * 1.5f + 0.01f), "the bot is capped at the sprint top speed");
    }

    [UnityTest]
    public IEnumerator SprintIsFasterUntilStaminaRunsOutAndRechargesOnlyAfterRelease()
    {
        var ball = CreateBall(Vector2.zero);
        SetPrivateField(ball, "sprintSeconds", 0.4f);
        SetPrivateField(ball, "rechargeSeconds", 0.4f);
        var body = ball.GetComponent<Rigidbody2D>();

        ball.SetControls(Vector2.right, false);
        yield return new WaitForSeconds(0.5f);
        Assert.That(body.linearVelocity.x, Is.EqualTo(5f).Within(0.1f), "the stick drives the ball at its top speed");
        Assert.That(body.gravityScale, Is.EqualTo(0f));

        ball.SetControls(Vector2.right, true);
        yield return new WaitForSeconds(0.25f);
        Assert.That(body.linearVelocity.x, Is.GreaterThan(6.5f), "sprint raises the top speed");
        Assert.That(ball.Stamina, Is.LessThan(0.6f));

        yield return new WaitForSeconds(0.4f);
        Assert.That(ball.Stamina, Is.EqualTo(0f));
        Assert.That(ball.IsSprinting, Is.False);
        yield return new WaitForSeconds(0.3f);
        Assert.That(body.linearVelocity.x, Is.EqualTo(5f).Within(0.1f), "empty stamina leaves normal movement");
        Assert.That(ball.Stamina, Is.EqualTo(0f), "holding sprint at empty does not recharge");

        ball.SetControls(Vector2.zero, false);
        yield return new WaitForSeconds(0.2f);
        Assert.That(ball.Stamina, Is.GreaterThan(0.2f), "releasing sprint recharges");
        Assert.That(body.linearVelocity.magnitude, Is.LessThan(1.5f), "releasing the stick slows the ball quickly");
    }

    [UnityTest]
    public IEnumerator ASprintingBallBumpsAnotherBall()
    {
        var attacker = CreateBall(new Vector2(-2f, 0f));
        var target = CreateBall(Vector2.zero);

        attacker.SetControls(Vector2.right, true);
        yield return new WaitForSeconds(0.6f);

        Assert.That(target.Position.x, Is.GreaterThan(0.5f), "the collision pushes the other ball away");
    }

    [UnityTest]
    public IEnumerator IdleBallsRepelEachOtherMoreStronglyWhenCloser()
    {
        var left = CreateBall(new Vector2(-0.5f, 0f));
        var right = CreateBall(new Vector2(0.5f, 0f));
        var farLeft = CreateBall(new Vector2(-10f, 5f));
        var farRight = CreateBall(new Vector2(-7.7f, 5f));

        yield return new WaitForSeconds(0.1f);

        var closeSpeed = right.GetComponent<Rigidbody2D>().linearVelocity.x;
        var farSpeed = farRight.GetComponent<Rigidbody2D>().linearVelocity.x;
        Assert.That(right.Position.x - left.Position.x, Is.GreaterThan(1.1f), "idle balls drift apart");
        Assert.That(farSpeed, Is.GreaterThan(0f), "balls push each other from a distance");
        Assert.That(closeSpeed, Is.GreaterThan(farSpeed * 2f), "the push weakens with distance");
    }

    [UnityTest]
    public IEnumerator EachRingCycleEasesToItsTargetThenResetsToFullSize()
    {
        var ring = CreateRing();
        ring.Begin(8);
        var target = ring.TargetRadius;

        Step(ring, 1.9f, 8);
        Assert.That(ring.Radius, Is.EqualTo(ring.FullRadius), "the ring holds full size while players breathe");
        Step(ring, 0.1f, 8);

        var radiusBeforeFirstSecond = ring.Radius;
        Step(ring, 1f, 8);
        var firstSecondShrink = radiusBeforeFirstSecond - ring.Radius;
        Step(ring, 12f, 8);
        var radiusBeforeLastSecond = ring.Radius;
        Step(ring, 0.9f, 8);
        var lastSecondShrink = radiusBeforeLastSecond - ring.Radius;

        Assert.That(ring.Cycle, Is.EqualTo(0));
        Assert.That(ring.Radius, Is.GreaterThan(target));
        Assert.That(lastSecondShrink, Is.LessThan(firstSecondShrink * 0.5f), "the shrink slows near the target");

        Step(ring, 0.2f, 8);
        Assert.That(ring.Cycle, Is.EqualTo(1), "the next cycle starts once the target is reached");
        Assert.That(ring.Radius, Is.EqualTo(ring.FullRadius), "the ring resets straight to full size");
        Assert.That(ring.TargetRadius, Is.LessThan(target), "each cycle's target is smaller");
        yield break;
    }

    [UnityTest]
    public IEnumerator TheRingReportsEachResetAndItsBlastPushesOutFromTheCentre()
    {
        var ring = CreateRing();
        ring.Begin(8);
        var resets = 0;
        var elapsedSeconds = 0f;
        while (ring.Radius > 0f && elapsedSeconds < 120f)
        {
            if (ring.Step(0.02f, 8))
            {
                resets++;
                Assert.That(ring.Radius, Is.EqualTo(ring.FullRadius), "a reset step is the step the ring is back at full size");
            }

            elapsedSeconds += 0.02f;
        }

        Assert.That(resets, Is.EqualTo(3), "three ordinary cycles reset, the final collapse does not");

        var nearBlast = ring.GetResetBlast(new Vector2(1f, 0f));
        var farBlast = ring.GetResetBlast(new Vector2(0f, -4f));
        Assert.That(nearBlast.x, Is.GreaterThan(0f));
        Assert.That(nearBlast.y, Is.EqualTo(0f));
        Assert.That(farBlast.y, Is.LessThan(0f), "the blast points away from the centre");
        Assert.That(nearBlast.magnitude, Is.GreaterThan(farBlast.magnitude), "the blast is hardest near the centre");
        Assert.That(ring.GetResetBlast(new Vector2(ring.FullRadius, 0f)).magnitude, Is.EqualTo(0f).Within(0.0001f));
        yield break;
    }

    [UnityTest]
    public IEnumerator AResetShowsAWhiteCircleThatGrowsAndFades()
    {
        var ring = CreateRing();
        ring.Begin(8);
        var blast = ring.transform.Find("Blast").GetComponent<LineRenderer>();
        yield return null;
        Assert.That(blast.enabled, Is.False, "no blast before the first reset");

        while (!ring.Step(0.02f, 8))
        {
        }

        yield return null;
        Assert.That(blast.enabled, Is.True);
        var firstRadius = blast.GetPosition(0).magnitude;
        var firstAlpha = blast.startColor.a;

        yield return new WaitForSeconds(0.3f);
        Assert.That(blast.GetPosition(0).magnitude, Is.GreaterThan(firstRadius), "the circle grows");
        Assert.That(blast.startColor.a, Is.LessThan(firstAlpha), "the circle fades");
        Assert.That(blast.startColor.r, Is.EqualTo(1f));

        yield return new WaitForSeconds(0.4f);
        Assert.That(blast.enabled, Is.False, "the circle is gone once it has faded");
    }

    [UnityTest]
    public IEnumerator WhenTheRingResetsItKnocksTheBallsOutward()
    {
        yield return StartGame(4, Humans(2), ring =>
        {
            SetPrivateField(ring, "breathingSeconds", 0.2f);
            SetCycleSeconds(ring, 0.6f, 5f, 5f, 5f);
        });
        var players = Players();
        var ring = Ring();

        yield return WaitUntil(() => ring.Cycle == 1, 3f);
        yield return new WaitForFixedUpdate();

        Assert.That(ring.Cycle, Is.EqualTo(1));
        foreach (var player in players)
        {
            var velocity = player.GetComponent<Rigidbody2D>().linearVelocity;
            Assert.That(Vector2.Dot(velocity, player.Position.normalized), Is.GreaterThan(3f), "the reset blast knocks each ball out from the centre");
        }
    }

    [UnityTest]
    public IEnumerator ACycleKeepsThePlayerCountItStartedWithAndTheNextUsesTheNewCount()
    {
        var ring = CreateRing();
        ring.Begin(8);
        var firstTarget = ring.TargetRadius;

        Step(ring, 10f, 2);
        Assert.That(ring.TargetRadius, Is.EqualTo(firstTarget), "a death does not move the current target");

        Step(ring, 6.1f, 2);
        Assert.That(ring.Cycle, Is.EqualTo(1));
        var eightPlayerRing = CreateRing();
        eightPlayerRing.Begin(8);
        Step(eightPlayerRing, 16.1f, 8);
        Assert.That(ring.TargetRadius, Is.EqualTo(eightPlayerRing.TargetRadius * 0.5f).Within(0.0001f), "two survivors get half the radius");

        // Two survivors also get half the shrink time: 2 s breathing plus 12 s * 0.5. The cycle
        // began up to 0.1 s ago.
        Step(ring, 7.7f, 2);
        Assert.That(ring.Cycle, Is.EqualTo(1));
        Step(ring, 0.4f, 2);
        Assert.That(ring.Cycle, Is.EqualTo(2));
        yield break;
    }

    [UnityTest]
    public IEnumerator TheSecondTargetFitsEveryoneAndTheThirdDoesNot()
    {
        for (var playerCount = 2; playerCount <= 8; playerCount++)
        {
            var targets = CycleTargets(playerCount);
            var fitRadius = PackingRadiusRatios[playerCount] * BallRadius;

            Assert.That(targets[0], Is.GreaterThan(fitRadius * 2f), "the first squeeze is generous for " + playerCount + " players");
            Assert.That(targets[1], Is.GreaterThanOrEqualTo(fitRadius), "the second squeeze still fits " + playerCount + " players");
            Assert.That(targets[2], Is.LessThan(fitRadius), "the third squeeze cannot fit " + playerCount + " players");
            Assert.That(targets[3], Is.EqualTo(0f), "the last cycle closes completely");
        }

        yield break;
    }

    [UnityTest]
    public IEnumerator AnEliminationPauseIsNotExtendedByMoreEliminations()
    {
        var ring = CreateRing();
        ring.Begin(8);
        Step(ring, 3f, 8);

        var pausedRadius = ring.Radius;
        ring.PauseForElimination();
        Step(ring, 0.3f, 8);
        Assert.That(ring.IsPaused, Is.True);
        ring.PauseForElimination();
        Step(ring, 0.24f, 8);

        Assert.That(ring.IsPaused, Is.False, "the second elimination shared the first pause");
        Assert.That(ring.Radius, Is.LessThan(pausedRadius));

        ring.PauseForElimination();
        Assert.That(ring.IsPaused, Is.True, "an elimination after the ring resumes starts a new pause");
        yield break;
    }

    [UnityTest]
    public IEnumerator TheRingClosesWithinSixtySecondsEvenWithAPauseForEveryElimination()
    {
        foreach (var playerCount in new[] { 8, 4, 2 })
        {
            var ring = CreateRing();
            ring.Begin(playerCount);
            var elapsedSeconds = 0f;
            var pausesLeft = playerCount - 2;
            while (ring.Radius > 0f && elapsedSeconds < 120f)
            {
                if (pausesLeft > 0 && elapsedSeconds > 5f * (playerCount - 1 - pausesLeft) && !ring.IsPaused)
                {
                    ring.PauseForElimination();
                    pausesLeft--;
                }

                ring.Step(Time.fixedDeltaTime, playerCount);
                elapsedSeconds += Time.fixedDeltaTime;
            }

            Assert.That(ring.Radius, Is.EqualTo(0f));
            Assert.That(elapsedSeconds, Is.LessThanOrEqualTo(60f), playerCount + " players");

            Step(ring, 1f, playerCount);
            Assert.That(ring.Radius, Is.EqualTo(0f), "the closed ring does not reset");
        }

        yield break;
    }

    private IEnumerator StartGame(int seed, GCPlayerType[] types, Action<GCExampleRingSource> configureRing)
    {
        var provider = new FakeLocalPlaySessionProvider(() => CreateSuccessfulCapture("example", seed, types));
        sessionOverride = GCLocalPlaySession.OverrideForTests(provider, null, null);
        Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.True, "test capture did not succeed");

        // The prefab sits under an inactive parent so it never simulates, while its own active flag
        // (which clones copy) stays on.
        var prefabParent = Track(new GameObject("Prefab parent"));
        prefabParent.SetActive(false);
        var playerPrefab = new GameObject("GCExamplePlayer");
        playerPrefab.transform.SetParent(prefabParent.transform, false);
        playerPrefab.AddComponent<GCExamplePlayerSource>();

        var listener = Track(new GameObject("Game"));
        listener.AddComponent<GCExampleGameSource>();
        configureRing?.Invoke(listener.GetComponent<GCExampleRingSource>());

        // Created inactive so listener and playerPrefab are wired before Awake. GamingCouch.Start
        // then runs setup and play on its own; the test only waits and observes.
        var gcObject = new GameObject("GamingCouch");
        gcObject.SetActive(false);
        gamingCouch = gcObject.AddComponent<GamingCouch>();
        SetPrivateField(gamingCouch, "listener", listener);
        SetPrivateField(gamingCouch, "playerPrefab", playerPrefab);
        gcObject.SetActive(true);

        yield return WaitUntil(() => gamingCouch.Status == GCStatus.Playing && Players().Length == types.Length, 8f);
        Assert.That(gamingCouch.Status, Is.EqualTo(GCStatus.Playing), "the platform did not reach Playing");
    }

    // GamingCouch is DontDestroyOnLoad, so scene teardown will not collect it or the players.
    private void DestroyGame()
    {
        if (gamingCouch != null)
        {
            gamingCouch.InternalPlayerStore.Clear();
            UnityEngine.Object.DestroyImmediate(gamingCouch.gameObject);
            gamingCouch = null;
        }

        foreach (var createdObject in createdObjects)
        {
            if (createdObject != null && createdObject.GetComponent<GCExampleGameSource>() != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }
        }

        sessionOverride?.Dispose();
        sessionOverride = null;
    }

    private GCExamplePlayerSource[] Players()
    {
        return gamingCouch.InternalPlayerStore.Players
            .Where(player => player != null)
            .Cast<GCExamplePlayerSource>()
            .ToArray();
    }

    private GCExampleRingSource Ring()
    {
        return createdObjects
            .Where(createdObject => createdObject != null)
            .Select(createdObject => createdObject.GetComponent<GCExampleRingSource>())
            .Single(ring => ring != null);
    }

    private GCExamplePlayerSource CreateBall(Vector2 position)
    {
        var ball = Track(new GameObject("Ball")).AddComponent<GCExamplePlayerSource>();
        ball.transform.position = position;
        return ball;
    }

    private GCExampleRingSource CreateRing()
    {
        return Track(new GameObject("Ring")).AddComponent<GCExampleRingSource>();
    }

    private GameObject Track(GameObject createdObject)
    {
        createdObjects.Add(createdObject);
        return createdObject;
    }

    private float[] CycleTargets(int playerCount)
    {
        var ring = CreateRing();
        ring.Begin(playerCount);
        var targets = new List<float> { ring.TargetRadius };
        while (targets.Count < 4)
        {
            var cycle = ring.Cycle;
            while (ring.Cycle == cycle)
            {
                ring.Step(Time.fixedDeltaTime, playerCount);
            }

            targets.Add(ring.TargetRadius);
        }

        return targets.ToArray();
    }

    private static void Step(GCExampleRingSource ring, float seconds, int playerCount)
    {
        var steps = Mathf.RoundToInt(seconds / 0.02f);
        for (var i = 0; i < steps; i++)
        {
            ring.Step(0.02f, playerCount);
        }
    }

    private static void PlaceAt(GCExamplePlayerSource player, Vector2 position)
    {
        var body = player.GetComponent<Rigidbody2D>();
        body.position = position;
        body.linearVelocity = Vector2.zero;
        player.transform.position = position;
    }

    private static int[] FindSeedsWithDifferentFirstPick(int candidateCount)
    {
        var firstPickSeed = Enumerable.Range(1, 100).First(seed => new System.Random(seed).Next(candidateCount) == 0);
        var otherPickSeed = Enumerable.Range(1, 100).First(seed => new System.Random(seed).Next(candidateCount) != 0);
        return new[] { firstPickSeed, otherPickSeed };
    }

    private static GCPlayerType[] Humans(int count)
    {
        return Enumerable.Repeat(GCPlayerType.player, count).ToArray();
    }

    private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
    {
        var start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeoutSeconds)
        {
            yield return null;
        }
    }

    private static void SetCycleSeconds(GCExampleRingSource ring, params float[] shrinkSeconds)
    {
        var field = typeof(GCExampleRingSource).GetField("cycles", BindingFlags.Instance | BindingFlags.NonPublic);
        var cycles = (GCExampleRingSource.RingCycle[])field.GetValue(ring);
        for (var i = 0; i < cycles.Length; i++)
        {
            cycles[i].shrinkSeconds = shrinkSeconds[i];
        }
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "missing private field " + name + " on " + target.GetType().Name);
        field.SetValue(target, value);
    }

    // Mirrors GCLocalPlaySessionTests.CreateSuccessfulCapture, with seats 1..n of the given types.
    private static GCLocalPlaySessionCaptureResult CreateSuccessfulCapture(
        string entryKey,
        int seed,
        GCPlayerType[] types
    )
    {
        var setupOptions = new GCSetupOptions
        {
            isServer = true,
            gameModeId = entryKey,
            mode = GCMode.Development,
        };
        var playOptions = new GCPlayOptions
        {
            players = new GCPlayerOptions[types.Length],
            seed = seed,
        };
        var seatIdentities = new GCSeatIdentity[types.Length];
        var colors = (GCPlayerColor[])Enum.GetValues(typeof(GCPlayerColor));

        for (var index = 0; index < types.Length; index++)
        {
            var seatIndex = index + 1;
            playOptions.players[index] = new GCPlayerOptions
            {
                playerIndex = index,
                type = types[index].ToString(),
                color = colors[index].ToString(),
            };
            seatIdentities[index] = new GCSeatIdentity
            {
                sourceSeatIndex = seatIndex,
                stableKey = seatIndex.ToString(),
                label = "Seat " + seatIndex,
                playerType = types[index],
                playerColor = colors[index],
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
