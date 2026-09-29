// Canonical master for the generated GCExampleGame (the full playable example game).
//
// This file lives in the non-shipping, UNITY_INCLUDE_TESTS-gated
// GamingCouch.Tests.ExampleCanonical assembly so it compiles against the real runtime API in
// the editor and CI — the anti-drift compile guarantee (ADR 0017). The generator copies this
// file into the user's project, drops this namespace, strips the "Source" type-name suffix
// (GCExampleGameSource -> GCExampleGame, and likewise for the other example scripts), and injects
// the move/rename header. The "Source" suffix keeps this master's simple type name distinct from
// the generated GCExampleGame so the FindTypeByName guard in GamingCouchActiveSceneSetup does
// not refuse generation.
//
// Everything from the first `using` below is what the user receives. Every call on
// GamingCouch.Instance stays in this file, and the rules stay in the mode (ADR 0019).
using DSB.GC;
using UnityEngine;

namespace DSB.GC.ExampleCanonical
{
    // Last ball standing. Balls bump each other inside a ring that keeps shrinking, and a ball
    // that touches the ring is out. The last ball left wins.
    //
    // This script is the listener on the scene's "Game" object, and it makes every call on
    // GamingCouch.Instance, so read it first. The other scripts each have one job:
    // - GCExampleSurvivalMode runs the rules: spawning, the ring, eliminations and the winner.
    // - GCExamplePlayer moves a ball and tracks its sprint stamina.
    // - GCExampleRing shrinks the lethal boundary, one cycle after another.
    // - GCExampleBot picks the stick and sprint input for bot players.
    [RequireComponent(typeof(GCExampleSurvivalModeSource))]
    public class GCExampleGameSource : MonoBehaviour
    {
        private GCExampleSurvivalModeSource mode;

        // GamingCouch calls this with SendMessage on the object in its Listener field. SendMessage
        // finds a method by name, so it can be private, but the name and the single GCSetupOptions
        // parameter must stay as they are. It runs before any players exist.
        private void GamingCouchSetup(GCSetupOptions options)
        {
            // The game mode is chosen here, from the setup options. A game with several modes would
            // switch on options.gameModeId to pick one. This example has only the survival mode.
            mode = GetComponent<GCExampleSurvivalModeSource>();
            GamingCouch.Instance.SetupGameVersus(mode.Setup());

            // GamingCouch calls GamingCouchPlay only after SetupDone, so a game that loads a level
            // first calls it once the level is ready.
            GamingCouch.Instance.SetupDone();
        }

        // Called the same way to start the round, with one GCPlayerOptions per human or bot player.
        private void GamingCouchPlay(GCPlayOptions options)
        {
            mode.Prepare(options.seed, options.players.Length);
            GamingCouch.Instance.SetupPlayers<GCExamplePlayerSource>(options.players, mode.AddPlayer);
            mode.Begin(OnModeFinished);
        }

        // Bots decide their own input in the mode. Humans use their controller.
        private void Update()
        {
            if (mode == null || !mode.IsRunning)
            {
                return;
            }

            foreach (var player in mode.Survivors)
            {
                if (player.IsBot)
                {
                    continue;
                }

                var input = GamingCouch.Instance.GetInputsByPlayerIndex(player.Index);
                var move = input != null ? new Vector2(input.leftX, input.leftY) : Vector2.zero;
                var sprint = input != null && input.primary;
                mode.SetInput(player, move, sprint);
            }
        }

        private void OnModeFinished()
        {
            GamingCouch.Instance.GameOver();
        }
    }
}
