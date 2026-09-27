// Canonical master for the generated GCExampleGame (the full playable example game).
//
// This file lives in the non-shipping, UNITY_INCLUDE_TESTS-gated
// GamingCouch.Tests.ExampleCanonical assembly so it compiles against the real runtime API in
// the editor and CI — the anti-drift compile guarantee (ADR 0017). The generator copies this
// file into the user's project, drops this namespace, strips the "Source" type-name suffix
// (GCExampleGameSource -> GCExampleGame, GCExamplePlayerSource -> GCExamplePlayer), and injects
// the move/rename header. The "Source" suffix keeps this master's simple type name distinct from
// the generated GCExampleGame so the FindTypeByName guard in GamingCouchActiveSceneSetup does
// not refuse generation.
//
// Everything from the first `using` below is what the user receives. Keep it self-contained: the
// whole platform contract must stay visible in one readable file (no adapter, no base class).
using System.Collections;
using DSB.GC;
using DSB.GC.Game;
using DSB.GC.Hud;
using UnityEngine;

namespace DSB.GC.ExampleCanonical
{
    public class GCExampleGameSource : MonoBehaviour
    {
        [SerializeField]
        private float roundSeconds = 10.0f;

        [SerializeField]
        private int maxScore = 100;

        private readonly GCPlayerStore<GCExamplePlayerSource> players = new GCPlayerStore<GCExamplePlayerSource>();
        private Coroutine roundCoroutine;
        private bool emittedExampleDiagnostic;

        private void GamingCouchSetup(GCSetupOptions options)
        {
            var clampedMaxScore = Mathf.Max(1, maxScore);
            Debug.Log("GamingCouch setup received. Configure game mode and HUD before SetupDone.");

            GamingCouch.Instance.SetupGameVersus(new GCGameVersusSetupOptions
            {
                maxScore = clampedMaxScore,
                placementCriteria = new[]
                {
                    GCPlacementSortCriteria.ScoreDescending,
                    GCPlacementSortCriteria.Finished,
                    GCPlacementSortCriteria.EliminatedDescending,
                },
                hud = new GCGameHudOptions
                {
                    isPlayersAutoUpdateEnabled = true,
                    players = new GCHudPlayersConfig
                    {
                        valueTypeEnum = PlayersHudValueType.PointsSmall,
                        meterTypeEnum = PlayersHudMeterType.Bar,
                    },
                },
            });

            GamingCouch.Instance.SetupDone();
        }

        private void GamingCouchPlay(GCPlayOptions options)
        {
            players.Clear();
            emittedExampleDiagnostic = false;
            Debug.Log("GamingCouch play received for " + options.players.Length + " players.");

            GamingCouch.Instance.SetupPlayers<GCExamplePlayerSource>(options.players, player =>
            {
                players.AddPlayer(player);
                player.ApplyPlayerColor();
                player.SetLives(3, "Example play start");
                player.SetStatus(GCPlayerStatus.Pending, "Ready", "Example play start");
                player.SetMeter(0, "Example play start");
                Debug.Log("Spawned player index " + player.Index + ".");
            });

            if (roundCoroutine != null)
            {
                StopCoroutine(roundCoroutine);
            }

            roundCoroutine = StartCoroutine(RunRound());
        }

        private void Update()
        {
            if (GamingCouch.Instance == null || GamingCouch.Instance.Status != GCStatus.Playing)
            {
                return;
            }

            foreach (var player in players.Players)
            {
                HandlePlayerInput(player);
            }
        }

        private void HandlePlayerInput(GCExamplePlayerSource player)
        {
            var input = GamingCouch.Instance.GetInputsByPlayerIndex(player.Index);
            if (input == null)
            {
                return;
            }

            if (input.primary)
            {
                player.AddScore(1, "Primary input");
                player.SetStatus(GCPlayerStatus.Success, "Scored", "Primary input");
                if (!player.IsFinished)
                {
                    player.SetFinishedRevokable("Primary input");
                }
            }

            if (input.secondary)
            {
                if (player.IsFinishedRevokable)
                {
                    player.SetRevokeFinished("Secondary input");
                }

                player.SetStatus(GCPlayerStatus.Pending, "Playing", "Secondary input");
            }

            if (input.alt && !player.IsEliminated)
            {
                player.SetEliminatedRevokable("Alt input");
            }
        }

        private IEnumerator RunRound()
        {
            var clampedRoundSeconds = Mathf.Max(0.1f, roundSeconds);
            yield return new WaitForSeconds(clampedRoundSeconds * 0.5f);

            UpdateRuntimeStateForHud();
            EmitDiagnosticLogExample();

            yield return new WaitForSeconds(clampedRoundSeconds * 0.5f);

            ApplyRandomFinalScores();
            GamingCouch.Instance.GameOver();
        }

        private void UpdateRuntimeStateForHud()
        {
            foreach (var player in players.Players)
            {
                player.SetStatus(GCPlayerStatus.Pending, "Halfway", "Example runtime state");
                player.SetMeter(50, "Example runtime state");
            }
        }

        private void EmitDiagnosticLogExample()
        {
            if (emittedExampleDiagnostic || players.Players.Count == 0)
            {
                return;
            }

            emittedExampleDiagnostic = true;
            Debug.Log("Example diagnostic checkpoint: runtime state and HUD updated for " + players.Players.Count + " players.");
        }

        private void ApplyRandomFinalScores()
        {
            var clampedMaxScore = Mathf.Max(1, maxScore);
            foreach (var player in players.Players)
            {
                player.SetScore(Random.Range(0, clampedMaxScore + 1), "Example round complete");
                if (player.IsEliminatedRevokable)
                {
                    player.SetEliminatedPermanent("Example round complete");
                }

                player.SetFinishedPermanent("Example round complete");
            }
        }
    }
}
