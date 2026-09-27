using System.Collections.Generic;
using DSB.GC;

public interface GCPlayerStoreOutput<T> where T : GCPlayer
{
    IReadOnlyList<T> Players { get; }
    IReadOnlyList<T> PlayersBot { get; }
    IReadOnlyList<T> PlayersNonBot { get; }
    IReadOnlyList<T> PlayersUneliminated { get; }
    IReadOnlyList<T> PlayersUneliminatedBot { get; }
    IReadOnlyList<T> PlayersUneliminatedNonBot { get; }
    IReadOnlyList<T> PlayersEliminated { get; }
    IReadOnlyList<T> PlayersEliminatedBot { get; }
    IReadOnlyList<T> PlayersEliminatedNonBot { get; }
    IReadOnlyList<T> PlayersEliminatedPermanent { get; }
    IReadOnlyList<T> PlayersEliminatedPermanentBot { get; }
    IReadOnlyList<T> PlayersEliminatedPermanentNonBot { get; }
    IReadOnlyList<T> PlayersEliminatedRevokable { get; }
    IReadOnlyList<T> PlayersEliminatedRevokableBot { get; }
    IReadOnlyList<T> PlayersEliminatedRevokableNonBot { get; }
    IReadOnlyList<T> PlayersFinished { get; }
    IReadOnlyList<T> PlayersFinishedBot { get; }
    IReadOnlyList<T> PlayersFinishedNonBot { get; }
    IReadOnlyList<T> PlayersFinishedPermanent { get; }
    IReadOnlyList<T> PlayersFinishedPermanentBot { get; }
    IReadOnlyList<T> PlayersFinishedPermanentNonBot { get; }
    IReadOnlyList<T> PlayersFinishedRevokable { get; }
    IReadOnlyList<T> PlayersFinishedRevokableBot { get; }
    IReadOnlyList<T> PlayersFinishedRevokableNonBot { get; }
    [System.Obsolete("Use Players.Count.", true)]
    int PlayerCount { get; }
    [System.Obsolete("Use Players.", true)]
    IEnumerable<T> PlayersEnumerable { get; }
    [System.Obsolete("Use PlayersUneliminated; broad uneliminated means eliminationState is None.", true)]
    IEnumerable<T> UneliminatedPlayersEnumerable { get; }
    [System.Obsolete("Use PlayersUneliminated.Count.", true)]
    int UneliminatedPlayerCount { get; }
    [System.Obsolete("Use PlayersEliminated; broad eliminated includes permanent and revokable elimination.", true)]
    IEnumerable<T> EliminatedPlayersEnumerable { get; }
    [System.Obsolete("Use PlayersEliminated.Count.", true)]
    int EliminatedPlayerCount { get; }
    [System.Obsolete("GetPlayerById has been removed from the game-facing runtime contract. Use GetPlayerByIndex.", true)]
    T GetPlayerById(int playerId);
    /// <summary>
    /// Returns the player with the given index, or <c>null</c> when no player has that index.
    /// The lookup never falls back to list position, so an unknown index returns <c>null</c>.
    /// </summary>
    T GetPlayerByIndex(int playerIndex);
    void Clear();
}

public interface GCPlayerStoreInput<in T> where T : GCPlayer
{
    void AddPlayer(T player);
}
