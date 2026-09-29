// Canonical master for the generated GCExampleSurvivalMode (the full example game's rules).
//
// This file lives in the non-shipping, UNITY_INCLUDE_TESTS-gated
// GamingCouch.Tests.ExampleCanonical assembly so it compiles against the real runtime API in
// the editor and CI, the anti-drift compile guarantee (ADR 0017). The generator copies this
// file into the user's project, drops this namespace, strips the "Source" type-name suffix
// (GCExampleSurvivalModeSource -> GCExampleSurvivalMode, and likewise for the other example
// scripts), and injects the move/rename header.
//
// Everything from the first `using` below is what the user receives.
using System.Collections.Generic;
using DSB.GC;
using DSB.GC.Game;
using DSB.GC.Hud;
using UnityEngine;

namespace DSB.GC.ExampleCanonical
{
    // The survival mode: spawning, the ring, bots, eliminations and the winner. GCExampleGame
    // makes every call on GamingCouch.Instance and hands this mode the players and their input.
    // The mode calls methods on its own players where a rule happens, such as SetMeter and
    // SetEliminatedPermanent, and calls the callback given to Begin when one ball is left.
    [RequireComponent(typeof(GCExampleRingSource))]
    public class GCExampleSurvivalModeSource : MonoBehaviour
    {
        // Well inside the first cycle's target, so the first squeeze does not reach a ball that
        // stays put.
        [SerializeField]
        private float spawnRadius = 1.8f;

        private readonly GCPlayerStore<GCExamplePlayerSource> players = new GCPlayerStore<GCExamplePlayerSource>();
        private readonly List<GCExamplePlayerSource> touchingRing = new List<GCExamplePlayerSource>();
        private GCExampleRingSource ring;
        private System.Random tieBreakRandom;
        private int playerCount;

        private System.Action finished;

        // True from Begin until one ball is left.
        public bool IsRunning { get; private set; }
        public IReadOnlyList<GCExamplePlayerSource> Survivors => players.PlayersUneliminated;

        private void Awake()
        {
            ring = GetComponent<GCExampleRingSource>();
        }

        // Frames the camera on the arena and returns the placement and HUD settings for
        // SetupGameVersus. A restart runs setup again while a round may still be running, and
        // destroys that round's players, so setup stops the round first.
        public GCGameVersusSetupOptions Setup()
        {
            IsRunning = false;
            players.Clear();
            FrameCameraOnArena();

            return new GCGameVersusSetupOptions
            {
                // Players still in the game place first. The others follow from the last one out
                // to the first, because SetEliminatedPermanent records when each player went out.
                placementCriteria = new[] { GCPlacementSortCriteria.EliminatedDescending },
                hud = new GCGameHudOptions
                {
                    isPlayersAutoUpdateEnabled = true,
                    players = new GCHudPlayersConfig
                    {
                        valueTypeEnum = PlayersHudValueType.None,
                        // The meter shows each player's sprint stamina.
                        meterTypeEnum = PlayersHudMeterType.Bar,
                    },
                },
            };
        }

        // Call before AddPlayer. It seeds the tie-break and stores the player count that spawn
        // positions use.
        public void Prepare(int seed, int roundPlayerCount)
        {
            players.Clear();
            tieBreakRandom = new System.Random(seed);
            playerCount = roundPlayerCount;
        }

        public void AddPlayer(GCExamplePlayerSource player)
        {
            players.AddPlayer(player);
            player.transform.position = GetSpawnPosition(player.Index);
            player.ApplyPlayerColor();
            player.SetMeter(100, "Stamina");

            if (player.IsBot)
            {
                player.gameObject.AddComponent<GCExampleBotSource>().Init(player.PlayerSeed);
            }
        }

        public void Begin(System.Action onFinished)
        {
            finished = onFinished;
            ring.Begin(playerCount);
            IsRunning = true;
        }

        // Human input from GCExampleGame. Bots set theirs in Update.
        public void SetInput(GCExamplePlayerSource player, Vector2 move, bool sprint)
        {
            player.SetControls(move, sprint);
        }

        // Drives the bots and updates the stamina meters once per frame.
        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            var survivors = players.PlayersUneliminated;
            foreach (var player in survivors)
            {
                if (player.IsBot)
                {
                    player.GetComponent<GCExampleBotSource>().Decide(player, ring, survivors, Time.deltaTime, out var move, out var sprint);
                    player.SetControls(move, sprint);
                }

                // Update the HUD meter in steps of 5 so it does not send a change every frame.
                var stamina = Mathf.RoundToInt(player.Stamina * 20f) * 5;
                if (stamina != player.Meter)
                {
                    player.SetMeter(stamina, "Stamina");
                }
            }
        }

        // Applies the rules once per physics step, so every ball touching the ring in the same step
        // is handled together.
        private void FixedUpdate()
        {
            if (!IsRunning)
            {
                return;
            }

            var survivors = players.PlayersUneliminated;
            var ringReset = ring.Step(Time.fixedDeltaTime, survivors.Count);
            if (ringReset)
            {
                foreach (var player in survivors)
                {
                    player.Knock(ring.GetResetBlast(player.Position));
                }
            }

            touchingRing.Clear();
            foreach (var player in survivors)
            {
                if (ring.IsTouching(player.Position, player.Radius))
                {
                    touchingRing.Add(player);
                }
            }

            if (touchingRing.Count == 0)
            {
                return;
            }

            // Somebody must win: if every remaining ball touches the ring at once, the one closest
            // to the centre stays in.
            if (touchingRing.Count == survivors.Count)
            {
                touchingRing.Remove(PickClosestToCentre(touchingRing));
            }

            foreach (var player in touchingRing)
            {
                // SetEliminatedPermanent tells Gaming Couch the player is out, for the HUD and the
                // results, but it leaves the ball in the scene. Deactivating the GameObject takes
                // the ball out of play. The player record stays, so the results still list it.
                player.SetEliminatedPermanent("Touched the ring");
                player.gameObject.SetActive(false);
            }

            if (players.PlayersUneliminated.Count <= 1)
            {
                Finish();
                return;
            }

            ring.PauseForElimination();
        }

        private void Finish()
        {
            foreach (var player in players.Players)
            {
                player.SetControls(Vector2.zero, false);
            }

            IsRunning = false;
            finished();
        }

        // An exact distance tie is broken by the run seed, never by the order players are listed in.
        private GCExamplePlayerSource PickClosestToCentre(List<GCExamplePlayerSource> candidates)
        {
            var closest = new List<GCExamplePlayerSource>();
            var closestDistance = float.MaxValue;
            foreach (var player in candidates)
            {
                var distance = player.Position.magnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest.Clear();
                }

                if (distance == closestDistance)
                {
                    closest.Add(player);
                }
            }

            closest.Sort((a, b) => a.Index.CompareTo(b.Index));
            return closest[tieBreakRandom.Next(closest.Count)];
        }

        // Spaces the balls evenly on a circle, so each starts the same distance from the centre.
        private Vector3 GetSpawnPosition(int playerIndex)
        {
            var angle = Mathf.PI * 0.5f + playerIndex * 2f * Mathf.PI / playerCount;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spawnRadius;
        }

        private void FrameCameraOnArena()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.orthographic = true;
            camera.orthographicSize = ring.FullRadius + 1f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
        }
    }
}
