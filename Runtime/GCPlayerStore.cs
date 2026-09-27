using System;
using System.Collections.Generic;
using DSB.GC.Utils;
using UnityEngine;

namespace DSB.GC
{
    public class GCPlayerStore<T> : GCPlayerStoreOutput<T>, GCPlayerStoreInput<T> where T : GCPlayer
    {
        private List<T> players = new List<T>();
        /// <summary>Every player in the store, in the order they were added. This is the store's live list: <see cref="AddPlayer"/> and <see cref="Clear"/> mutate it in place, so snapshot it (<c>.ToList()</c>) before adding or clearing players while enumerating.</summary>
        public List<T> Players => players;
        private List<T> playersBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersBot => playersBot;
        private List<T> playersNonBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersNonBot => playersNonBot;
        private List<T> playersUneliminated = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersUneliminated => playersUneliminated;
        private List<T> playersUneliminatedNonBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersUneliminatedNonBot => playersUneliminatedNonBot;
        private List<T> playersUneliminatedBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersUneliminatedBot => playersUneliminatedBot;
        private List<T> playersEliminated = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersEliminated => playersEliminated;
        private List<T> playersEliminatedNonBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersEliminatedNonBot => playersEliminatedNonBot;
        private List<T> playersEliminatedBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersEliminatedBot => playersEliminatedBot;
        private List<T> playersEliminatedPermanent = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersEliminatedPermanent => playersEliminatedPermanent;
        private List<T> playersEliminatedPermanentNonBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersEliminatedPermanentNonBot => playersEliminatedPermanentNonBot;
        private List<T> playersEliminatedPermanentBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersEliminatedPermanentBot => playersEliminatedPermanentBot;
        private List<T> playersEliminatedRevokable = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersEliminatedRevokable => playersEliminatedRevokable;
        private List<T> playersEliminatedRevokableNonBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersEliminatedRevokableNonBot => playersEliminatedRevokableNonBot;
        private List<T> playersEliminatedRevokableBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersEliminatedRevokableBot => playersEliminatedRevokableBot;
        private List<T> playersFinished = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersFinished => playersFinished;
        private List<T> playersFinishedNonBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersFinishedNonBot => playersFinishedNonBot;
        private List<T> playersFinishedBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersFinishedBot => playersFinishedBot;
        private List<T> playersFinishedPermanent = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersFinishedPermanent => playersFinishedPermanent;
        private List<T> playersFinishedPermanentNonBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersFinishedPermanentNonBot => playersFinishedPermanentNonBot;
        private List<T> playersFinishedPermanentBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersFinishedPermanentBot => playersFinishedPermanentBot;
        private List<T> playersFinishedRevokable = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersFinishedRevokable => playersFinishedRevokable;
        private List<T> playersFinishedRevokableNonBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersFinishedRevokableNonBot => playersFinishedRevokableNonBot;
        private List<T> playersFinishedRevokableBot = new List<T>();
        /// <summary>Live store collection, rebuilt in place on every accepted elimination or finish transition; snapshot it (<c>.ToList()</c>) before mutating player state while enumerating.</summary>
        public List<T> PlayersFinishedRevokableBot => playersFinishedRevokableBot;

        // The interface narrows these to IReadOnlyList<T> so package-internal consumers cannot
        // mutate the store's collections; the class keeps List<T> for games that surface them.
        IReadOnlyList<T> GCPlayerStoreOutput<T>.Players => players;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersBot => playersBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersNonBot => playersNonBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersUneliminated => playersUneliminated;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersUneliminatedNonBot => playersUneliminatedNonBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersUneliminatedBot => playersUneliminatedBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersEliminated => playersEliminated;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersEliminatedNonBot => playersEliminatedNonBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersEliminatedBot => playersEliminatedBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersEliminatedPermanent => playersEliminatedPermanent;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersEliminatedPermanentNonBot => playersEliminatedPermanentNonBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersEliminatedPermanentBot => playersEliminatedPermanentBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersEliminatedRevokable => playersEliminatedRevokable;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersEliminatedRevokableNonBot => playersEliminatedRevokableNonBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersEliminatedRevokableBot => playersEliminatedRevokableBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersFinished => playersFinished;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersFinishedNonBot => playersFinishedNonBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersFinishedBot => playersFinishedBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersFinishedPermanent => playersFinishedPermanent;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersFinishedPermanentNonBot => playersFinishedPermanentNonBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersFinishedPermanentBot => playersFinishedPermanentBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersFinishedRevokable => playersFinishedRevokable;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersFinishedRevokableNonBot => playersFinishedRevokableNonBot;
        IReadOnlyList<T> GCPlayerStoreOutput<T>.PlayersFinishedRevokableBot => playersFinishedRevokableBot;

        [Obsolete("Use Players.", true)]
        public IEnumerable<T> PlayersEnumerable => players;
        [Obsolete("Use Players.Count.", true)]
        public int PlayerCount => players.Count;
        [Obsolete("Use PlayersUneliminated; broad uneliminated means eliminationState is None.", true)]
        public List<T> UneliminatedPlayers => throw new InvalidOperationException("Use PlayersUneliminated.");
        [Obsolete("Use PlayersUneliminated.Count.", true)]
        public int UneliminatedPlayerCount => throw new InvalidOperationException("Use PlayersUneliminated.Count.");
        [Obsolete("Use PlayersUneliminated; broad uneliminated means eliminationState is None.", true)]
        public IEnumerable<T> UneliminatedPlayersEnumerable => throw new InvalidOperationException("Use PlayersUneliminated.");
        [Obsolete("Use PlayersUneliminatedNonBot.", true)]
        public List<T> UneliminatedNonBotPlayers => throw new InvalidOperationException("Use PlayersUneliminatedNonBot.");
        [Obsolete("Use PlayersUneliminatedNonBot.", true)]
        public IEnumerable<T> UneliminatedNonBotPlayersEnumerable => throw new InvalidOperationException("Use PlayersUneliminatedNonBot.");
        [Obsolete("Use PlayersUneliminatedBot.", true)]
        public List<T> UneliminatedBotPlayers => throw new InvalidOperationException("Use PlayersUneliminatedBot.");
        [Obsolete("Use PlayersUneliminatedBot.", true)]
        public IEnumerable<T> UneliminatedBotPlayersEnumerable => throw new InvalidOperationException("Use PlayersUneliminatedBot.");
        [Obsolete("Use PlayersEliminated; broad eliminated includes permanent and revokable elimination.", true)]
        public List<T> EliminatedPlayers => throw new InvalidOperationException("Use PlayersEliminated.");
        [Obsolete("Use PlayersEliminated.Count.", true)]
        public int EliminatedPlayerCount => throw new InvalidOperationException("Use PlayersEliminated.Count.");
        [Obsolete("Use PlayersEliminated; broad eliminated includes permanent and revokable elimination.", true)]
        public IEnumerable<T> EliminatedPlayersEnumerable => throw new InvalidOperationException("Use PlayersEliminated.");
        [Obsolete("Use PlayersEliminatedNonBot.", true)]
        public List<T> EliminatedNonBotPlayers => throw new InvalidOperationException("Use PlayersEliminatedNonBot.");
        [Obsolete("Use PlayersEliminatedNonBot.", true)]
        public IEnumerable<T> EliminatedNonBotPlayersEnumerable => throw new InvalidOperationException("Use PlayersEliminatedNonBot.");
        [Obsolete("Use PlayersEliminatedBot.", true)]
        public List<T> EliminatedBotPlayers => throw new InvalidOperationException("Use PlayersEliminatedBot.");
        [Obsolete("Use PlayersEliminatedBot.", true)]
        public IEnumerable<T> EliminatedBotPlayersEnumerable => throw new InvalidOperationException("Use PlayersEliminatedBot.");
        private Dictionary<int, T> playerByIndex = new Dictionary<int, T>();

        public GCPlayerStore() { }

        [Obsolete("GetPlayerById has been removed from the game-facing runtime contract. Use GetPlayerByIndex.", true)]
        public T GetPlayerById(int playerId)
        {
            throw new InvalidOperationException("GetPlayerById has been removed. Use GetPlayerByIndex.");
        }

        /// <summary>
        /// Returns the player registered under <paramref name="playerIndex"/>, or <c>null</c> when no
        /// player has that index. Lookup is keyed strictly by player index; it never falls back to list
        /// position, so an unknown index yields <c>null</c> rather than an unrelated player or an exception.
        /// </summary>
        public T GetPlayerByIndex(int playerIndex)
        {
            if (playerByIndex.TryGetValue(playerIndex, out var player))
            {
                return player;
            }

            return null;
        }

        public void AddPlayer(T player)
        {
            Assert.IsNotNull(player, "Trying to add null player to store. This could be due to invalid player type casting?");
            Assert.IsTrue(player.Index != -1, "Player not properly initialized before adding to store");
            if (players.Contains(player))
            {
                throw new InvalidOperationException("Player already added to store.");
            }

            if (playerByIndex.ContainsKey(player.Index))
            {
                throw new InvalidOperationException("Player index already added to store.");
            }

            players.Add(player);
            playerByIndex[player.Index] = player;
            RebuildStateCollections();

            player.AcceptedTransition += HandleAcceptedTransition;
        }

        public void Clear()
        {
            // Clear() is the runtime round-reset path, where Destroy is correct. It also runs in
            // EditMode tests (and could run from editor tooling); outside play mode Destroy logs
            // "Destroy may not be called from edit mode" and defers, so use DestroyImmediate there.
            // DestroyImmediate runs OnDestroy synchronously, so iterate a snapshot: a GCPlayer
            // subclass whose OnDestroy touched this store would otherwise break the enumeration.
            // Play-mode Destroy is deferred to end of frame, so undo the AddPlayer subscription
            // here rather than relying on the player going away.
            try
            {
                foreach (var player in players.ToArray())
                {
                    player.AcceptedTransition -= HandleAcceptedTransition;

                    // A player whose GameObject was destroyed outside the store leaves a live C#
                    // wrapper behind, and reading gameObject on it throws MissingReferenceException.
                    if (player == null)
                    {
                        continue;
                    }

                    if (UnityEngine.Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(player.gameObject);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(player.gameObject);
                    }
                }
            }
            finally
            {
                // The store must end up empty even if a subclass OnDestroy threw, or the next run
                // would start on a half-cleared store.
                players.Clear();
                ClearStateCollections();
                playerByIndex.Clear();
            }
        }

        private void RebuildStateCollections()
        {
            ClearStateCollections();

            foreach (var player in players)
            {
                AddByBotState(player, playersBot, playersNonBot);

                if (player.IsEliminated)
                {
                    AddByBotState(player, playersEliminatedBot, playersEliminatedNonBot);
                    playersEliminated.Add(player);

                    if (player.IsEliminatedPermanent)
                    {
                        playersEliminatedPermanent.Add(player);
                        AddByBotState(player, playersEliminatedPermanentBot, playersEliminatedPermanentNonBot);
                    }
                    else if (player.IsEliminatedRevokable)
                    {
                        playersEliminatedRevokable.Add(player);
                        AddByBotState(player, playersEliminatedRevokableBot, playersEliminatedRevokableNonBot);
                    }
                }
                else
                {
                    playersUneliminated.Add(player);
                    AddByBotState(player, playersUneliminatedBot, playersUneliminatedNonBot);
                }

                if (!player.IsFinished)
                {
                    continue;
                }

                playersFinished.Add(player);
                AddByBotState(player, playersFinishedBot, playersFinishedNonBot);

                if (player.IsFinishedPermanent)
                {
                    playersFinishedPermanent.Add(player);
                    AddByBotState(player, playersFinishedPermanentBot, playersFinishedPermanentNonBot);
                }
                else if (player.IsFinishedRevokable)
                {
                    playersFinishedRevokable.Add(player);
                    AddByBotState(player, playersFinishedRevokableBot, playersFinishedRevokableNonBot);
                }
            }

            Debug.Assert(playersUneliminated.Count + playersEliminated.Count == players.Count, "Player store elimination lists out of sync");
            Debug.Assert(playersBot.Count + playersNonBot.Count == players.Count, "Player store bot lists out of sync");
            Debug.Assert(playersEliminatedPermanent.Count + playersEliminatedRevokable.Count == playersEliminated.Count, "Player store eliminated state lists out of sync");
            Debug.Assert(playersFinishedPermanent.Count + playersFinishedRevokable.Count == playersFinished.Count, "Player store finished state lists out of sync");
        }

        private void HandleAcceptedTransition(GCPlayerAcceptedTransition transition)
        {
            if (transition.Kind != GCPlayerTransitionKind.PlayerEliminationStateChanged &&
                transition.Kind != GCPlayerTransitionKind.PlayerFinishStateChanged)
            {
                return;
            }

            RebuildStateCollections();
        }

        private void ClearStateCollections()
        {
            playersBot.Clear();
            playersNonBot.Clear();
            playersUneliminated.Clear();
            playersUneliminatedNonBot.Clear();
            playersUneliminatedBot.Clear();
            playersEliminated.Clear();
            playersEliminatedNonBot.Clear();
            playersEliminatedBot.Clear();
            playersEliminatedPermanent.Clear();
            playersEliminatedPermanentNonBot.Clear();
            playersEliminatedPermanentBot.Clear();
            playersEliminatedRevokable.Clear();
            playersEliminatedRevokableNonBot.Clear();
            playersEliminatedRevokableBot.Clear();
            playersFinished.Clear();
            playersFinishedNonBot.Clear();
            playersFinishedBot.Clear();
            playersFinishedPermanent.Clear();
            playersFinishedPermanentNonBot.Clear();
            playersFinishedPermanentBot.Clear();
            playersFinishedRevokable.Clear();
            playersFinishedRevokableNonBot.Clear();
            playersFinishedRevokableBot.Clear();
        }

        private static void AddByBotState(T player, List<T> botPlayers, List<T> nonBotPlayers)
        {
            if (player.IsBot)
            {
                botPlayers.Add(player);
                return;
            }

            nonBotPlayers.Add(player);
        }
    }
}
