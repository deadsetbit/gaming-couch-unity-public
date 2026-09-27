#if UNITY_EDITOR
namespace DSB.GC.Dev
{
    /// <summary>
    /// The seat number DevApp shows for a game-facing player index, and its inverse.
    ///
    /// A run's game-facing player indices are a seed-dependent permutation of its seats, so an
    /// index says nothing about which seat a developer is looking at. DevApp's run configuration
    /// numbers seats from the list the runtime emits, and that list is built from this same rule,
    /// so anything in Unity that has to agree with what the developer reads goes through here.
    /// </summary>
    internal static class GCSeatNumbering
    {
        /// <summary>The seat the editor keyboard drives unless the developer picks another.</summary>
        internal const int PreferredDefaultSeatNumber = 1;

        /// <summary>
        /// The seat number for a player index. A run that carries no local seat identity, which is
        /// how a hosted-shaped payload arrives, is numbered by position instead.
        /// </summary>
        internal static int GetSeatNumber(GCSeatIdentity seatIdentity, int playerIndex)
        {
            return seatIdentity.sourceSeatIndex > 0 ? seatIdentity.sourceSeatIndex : playerIndex + 1;
        }

        /// <summary>
        /// The player index behind a seat number, or false when the run has no such seat.
        /// </summary>
        internal static bool TryGetPlayerIndex(
            GCSeatIdentity[] mappedSeatIdentities,
            int seatNumber,
            out int playerIndex
        )
        {
            for (var index = 0; index < mappedSeatIdentities.Length; index++)
            {
                if (GetSeatNumber(mappedSeatIdentities[index], index) == seatNumber)
                {
                    playerIndex = index;
                    return true;
                }
            }

            playerIndex = -1;
            return false;
        }

        /// <summary>
        /// Seat 1, or the lowest seat number the run does have. A seat keeps its number when it is
        /// disabled in DevApp, so a run can start at seat 2 and have no seat 1 at all.
        /// </summary>
        internal static int ResolveDefaultSeatNumber(GCSeatIdentity[] mappedSeatIdentities)
        {
            var lowestSeatNumber = PreferredDefaultSeatNumber;
            var hasSeat = false;

            for (var index = 0; index < mappedSeatIdentities.Length; index++)
            {
                var seatNumber = GetSeatNumber(mappedSeatIdentities[index], index);
                if (!hasSeat || seatNumber < lowestSeatNumber)
                {
                    lowestSeatNumber = seatNumber;
                    hasSeat = true;
                }
            }

            return hasSeat ? lowestSeatNumber : PreferredDefaultSeatNumber;
        }
    }
}
#endif
