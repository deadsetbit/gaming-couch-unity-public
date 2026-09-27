// Canonical master for the generated GCExampleTemplate (the barebones "wiring demo").
//
// This file lives in the non-shipping, UNITY_INCLUDE_TESTS-gated
// GamingCouch.Tests.ExampleCanonical assembly so it compiles against the real runtime API in
// the editor and CI — the anti-drift compile guarantee (ADR 0017). The generator copies this
// file into the user's project, drops this namespace, strips the "Source" type-name suffix, and
// injects the move/rename header. The "Source" suffix keeps this master's simple type name
// distinct from the generated GCExampleTemplate so the FindTypeByName guard in
// GamingCouchActiveSceneSetup does not refuse generation.
//
// Everything from the first `using` below is what the user receives. Keep it self-contained: the
// whole platform contract must stay visible in one readable file (no adapter, no base class).
using DSB.GC;
using DSB.GC.Game;
using DSB.GC.Hud;
using UnityEngine;

namespace DSB.GC.ExampleCanonical
{
    // A barebones Gaming Couch game — the minimum a listener must do to complete the platform
    // loop. It is the script attached to the scene's "Game" object: GamingCouch talks to it by
    // SendMessage. It spawns the stock GCPlayer, logs each lifecycle step, and ends the game.
    // Copy this script into your project and grow it into your own game; the GamingCouch object
    // and its playerPrefab wiring never change.
    public class GCExampleTemplateSource : MonoBehaviour
    {
        private readonly GCPlayerStore<GCPlayer> players = new GCPlayerStore<GCPlayer>();

        // Called by the platform (via SendMessage) to configure the game before play. Configure
        // the game mode and HUD here, load any assets, then call SetupDone.
        private void GamingCouchSetup(GCSetupOptions options)
        {
            Debug.Log("[GCExampleTemplate] GamingCouchSetup received. Configuring the game, then calling SetupDone.");

            GamingCouch.Instance.SetupGameVersus(new GCGameVersusSetupOptions
            {
                maxScore = 1,
                placementCriteria = new[]
                {
                    GCPlacementSortCriteria.ScoreDescending,
                },
                hud = new GCGameHudOptions
                {
                    isPlayersAutoUpdateEnabled = true,
                    players = new GCHudPlayersConfig
                    {
                        valueTypeEnum = PlayersHudValueType.None,
                        meterTypeEnum = PlayersHudMeterType.None,
                    },
                },
            });

            GamingCouch.Instance.SetupDone();
        }

        // Called by the platform (via SendMessage) to start play. Spawn your players here. The
        // platform provides one GCPlayerOptions per player via options.players.
        private void GamingCouchPlay(GCPlayOptions options)
        {
            players.Clear();
            Debug.Log("[GCExampleTemplate] GamingCouchPlay received for " + options.players.Length + " player(s). Spawning the stock GCPlayer.");

            GamingCouch.Instance.SetupPlayers<GCPlayer>(options.players, player =>
            {
                players.AddPlayer(player);
                Debug.Log("[GCExampleTemplate] Spawned player index " + player.Index + ".");
            });

            // This is where your game runs. When it ends, call GameOver so the platform can show
            // the results. The barebones template has no gameplay, so it ends immediately.
            Debug.Log("[GCExampleTemplate] Players spawned. Your game runs here; calling GameOver to complete the loop.");
            GamingCouch.Instance.GameOver();
        }
    }
}
