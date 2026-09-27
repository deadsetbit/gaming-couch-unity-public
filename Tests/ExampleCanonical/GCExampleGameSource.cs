// Canonical master for the generated GCExampleGame (the full playable example game).
//
// This file lives in the non-shipping, UNITY_INCLUDE_TESTS-gated
// GamingCouch.Tests.ExampleCanonical assembly so it compiles against the real runtime API in
// the editor and CI — the anti-drift compile guarantee (ADR 0017). The generator copies this
// file into the user's project, drops this namespace, strips the "Source" type-name suffix
// (GCExampleGameSource -> GCExampleGame, and likewise for the player, ring and bot), and injects
// the move/rename header. The "Source" suffix keeps this master's simple type name distinct from
// the generated GCExampleGame so the FindTypeByName guard in GamingCouchActiveSceneSetup does
// not refuse generation.
//
// Everything from the first `using` below is what the user receives. Keep the whole platform
// contract visible in this one file (no adapter, no base class).
using System.Collections.Generic;
using DSB.GC;
using DSB.GC.Game;
using DSB.GC.Hud;
using UnityEngine;

namespace DSB.GC.ExampleCanonical
{
    // Last ball standing. Balls bump each other inside a ring that keeps shrinking, and a ball
    // that touches the ring is out. The last ball left wins.
    //
    // This script is the listener on the scene's "Game" object. GamingCouch calls its
    // GamingCouchSetup and GamingCouchPlay methods by SendMessage, and it calls GameOver when one
    // ball is left. The other scripts each have one job:
    // - GCExamplePlayer moves a ball and tracks its sprint stamina.
    // - GCExampleRing shrinks the lethal boundary, one cycle after another.
    // - GCExampleBot picks the stick and sprint input for bot players.
    [RequireComponent(typeof(GCExampleRingSource))]
    public class GCExampleGameSource : MonoBehaviour
    {
        // Well inside the first cycle's target, so the first squeeze does not reach a ball that
        // stays put.
        [SerializeField]
        private float spawnRadius = 1.8f;

        private readonly GCPlayerStore<GCExamplePlayerSource> players = new GCPlayerStore<GCExamplePlayerSource>();
        private readonly List<GCExamplePlayerSource> touchingRing = new List<GCExamplePlayerSource>();
        private GCExampleRingSource ring;
        private System.Random tieBreakRandom;

        private void Awake()
        {
            ring = GetComponent<GCExampleRingSource>();
        }

        // Called by the platform before play. Configure the game mode and HUD, then call SetupDone.
        private void GamingCouchSetup(GCSetupOptions options)
        {
            FrameCameraOnArena();

            GamingCouch.Instance.SetupGameVersus(new GCGameVersusSetupOptions
            {
                // Players still in the game place first, then the others from the last one out.
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
            });

            GamingCouch.Instance.SetupDone();
        }

        // Called by the platform to start play, with one GCPlayerOptions per human or bot player.
        private void GamingCouchPlay(GCPlayOptions options)
        {
            players.Clear();
            tieBreakRandom = new System.Random(options.seed);
            var playerCount = options.players.Length;

            GamingCouch.Instance.SetupPlayers<GCExamplePlayerSource>(options.players, player =>
            {
                players.AddPlayer(player);
                player.transform.position = GetSpawnPosition(player.Index, playerCount);
                player.ApplyPlayerColor();
                player.SetMeter(100, "Stamina");

                if (player.IsBot)
                {
                    player.gameObject.AddComponent<GCExampleBotSource>().Init(player.PlayerSeed);
                }
            });

            ring.Begin(playerCount);
        }

        // Reads each player's input once per frame. Bots decide theirs; humans use their controller.
        private void Update()
        {
            if (!IsPlaying())
            {
                return;
            }

            var survivors = players.PlayersUneliminated;
            foreach (var player in survivors)
            {
                Vector2 move;
                bool sprint;
                if (player.IsBot)
                {
                    player.GetComponent<GCExampleBotSource>().Decide(player, ring, survivors, Time.deltaTime, out move, out sprint);
                }
                else
                {
                    var input = GamingCouch.Instance.GetInputsByPlayerIndex(player.Index);
                    move = input != null ? new Vector2(input.leftX, input.leftY) : Vector2.zero;
                    sprint = input != null && input.primary;
                }

                player.SetControls(move, sprint);

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
            if (!IsPlaying())
            {
                return;
            }

            var survivors = players.PlayersUneliminated;
            if (ring.Step(Time.fixedDeltaTime, survivors.Count))
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
                player.SetEliminatedPermanent("Touched the ring");
                player.gameObject.SetActive(false);
            }

            if (players.PlayersUneliminated.Count <= 1)
            {
                EndGame();
                return;
            }

            ring.PauseForElimination();
        }

        private void EndGame()
        {
            foreach (var player in players.Players)
            {
                player.SetControls(Vector2.zero, false);
            }

            GamingCouch.Instance.GameOver();
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
        private Vector3 GetSpawnPosition(int playerIndex, int playerCount)
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

        private static bool IsPlaying()
        {
            return GamingCouch.Instance != null && GamingCouch.Instance.Status == GCStatus.Playing;
        }
    }
}
