using System;
using System.Collections.Generic;

namespace DSB.GC.Game
{
    public class GCGameVersusSetupOptions : GCGameSetupOptions { }

    public class GCGameVersus : GCGame
    {
        public GCGameVersus(GamingCouch gamingCouch, GCPlayerStoreOutput<GCPlayer> playerStore, GCGameVersusSetupOptions options) : base(gamingCouch, playerStore, options) { }
    }
}
