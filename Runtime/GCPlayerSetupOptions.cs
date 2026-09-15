using UnityEngine;

namespace DSB.GC
{
    internal struct GCPlayerSetupOptions
    {
        public GCPlayerType type;
        public int playerIndex;
        public int playerSeed;
        public GCPlayerColor colorEnum;
        public string colorName;
    }
}
