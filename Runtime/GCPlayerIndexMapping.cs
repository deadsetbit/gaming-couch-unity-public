using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DSB.GC.Dev;
using DSB.GC.RuntimeMessages;

namespace DSB.GC
{
    internal sealed class GCPlayerIndexMapping
    {
        private readonly GCPlayerIndexMappingEntry[] entriesByIndex;
        private readonly Dictionary<int, int> playerIndexBySourceSeatIndex = new Dictionary<int, int>();
        private readonly HashSet<string> emittedInvalidPlayerIndexKeys = new HashSet<string>();

        internal string MappingId { get; }
        internal int Seed { get; }
        internal int ParticipantCount => entriesByIndex.Length;

        private GCPlayerIndexMapping(string mappingId, int seed, GCPlayerIndexMappingEntry[] entriesByIndex)
        {
            MappingId = mappingId;
            Seed = seed;
            this.entriesByIndex = entriesByIndex ?? Array.Empty<GCPlayerIndexMappingEntry>();

            for (var index = 0; index < this.entriesByIndex.Length; index++)
            {
                var entry = this.entriesByIndex[index];
                // Seats with sourceSeatIndex <= 0 carry no local seat identity (e.g. hosted
                // player-index mappings) and are intentionally not indexed for reverse lookup.
                if (entry.SourceSeatIndex > 0)
                {
                    if (playerIndexBySourceSeatIndex.ContainsKey(entry.SourceSeatIndex))
                    {
                        throw new ArgumentException("[GamingCouch] Seat identities must not contain duplicate sourceSeatIndex values.", nameof(entriesByIndex));
                    }

                    playerIndexBySourceSeatIndex[entry.SourceSeatIndex] = entry.PlayerIndex;
                }
            }
        }

        internal static GCPlayerIndexMapping Create(GCPlayOptions options, GCSeatIdentity[] seatIdentities)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var players = options.players ?? Array.Empty<GCPlayerOptions>();
            var identities = seatIdentities ?? Array.Empty<GCSeatIdentity>();
            if (identities.Length != players.Length)
            {
                throw new ArgumentException("[GamingCouch] Seat identity count must match player count.", nameof(seatIdentities));
            }

            if (options.usesProvidedPlayerIndexMapping)
            {
                return CreateFromProvidedPlayerIndices(options, identities);
            }

            var participants = new List<Participant>(players.Length);
            for (var capturedOrder = 0; capturedOrder < players.Length; capturedOrder++)
            {
                var identity = identities[capturedOrder];
                var stableKey = !string.IsNullOrWhiteSpace(identity.stableKey)
                    ? identity.stableKey
                    : identity.sourceSeatIndex > 0
                        ? identity.sourceSeatIndex.ToString()
                        : capturedOrder.ToString();

                participants.Add(new Participant
                {
                    CapturedOrder = capturedOrder,
                    Hash = ComputeFnv1A32(options.seed.ToString() + ":" + stableKey),
                    SourceSeatIndex = identity.sourceSeatIndex,
                    StableKey = stableKey,
                    PlayerSeed = GCPlayerSeed.NormalizeOrFallback(players[capturedOrder].playerSeed, null, capturedOrder),
                    Type = identity.playerType != GCPlayerType.unset
                        ? identity.playerType
                        : GCPlayerOptionResolver.ResolvePlayerType(players[capturedOrder].type),
                    ColorName = identity.playerColor.ToString(),
                });
            }

            var entries = participants
                .OrderBy(participant => participant.Hash)
                .ThenBy(participant => participant.CapturedOrder)
                .Select((participant, playerIndex) => new GCPlayerIndexMappingEntry(
                    playerIndex,
                    participant.CapturedOrder,
                    participant.SourceSeatIndex,
                    participant.StableKey,
                    participant.PlayerSeed,
                    participant.Hash,
                    participant.Type,
                    GCPlayerOptionResolver.ResolvePlayerColor(participant.ColorName)
                ))
                .ToArray();

            return new GCPlayerIndexMapping(BuildMappingId(options.seed, entries), options.seed, entries);
        }

        private static GCPlayerIndexMapping CreateFromProvidedPlayerIndices(GCPlayOptions options, GCSeatIdentity[] identities)
        {
            var players = options.players ?? Array.Empty<GCPlayerOptions>();
            var entries = new GCPlayerIndexMappingEntry[players.Length];
            var seenPlayerIndices = new bool[players.Length];

            for (var capturedOrder = 0; capturedOrder < players.Length; capturedOrder++)
            {
                var player = players[capturedOrder];
                var playerIndex = player.playerIndex;
                if (playerIndex < 0 || playerIndex >= players.Length)
                {
                    throw new ArgumentException("[GamingCouch] players[] must use dense zero-based playerIndex values.", nameof(options));
                }

                if (seenPlayerIndices[playerIndex])
                {
                    throw new ArgumentException("[GamingCouch] players[] must not contain duplicate playerIndex values.", nameof(options));
                }

                seenPlayerIndices[playerIndex] = true;

                var identity = identities[capturedOrder];
                var stableKey = !string.IsNullOrWhiteSpace(identity.stableKey)
                    ? identity.stableKey
                    : playerIndex.ToString();

                entries[playerIndex] = new GCPlayerIndexMappingEntry(
                    playerIndex,
                    capturedOrder,
                    identity.sourceSeatIndex,
                    stableKey,
                    GCPlayerSeed.NormalizeOrFallback(player.playerSeed, null, playerIndex),
                    ComputeFnv1A32(options.seed.ToString() + ":" + stableKey),
                    GCPlayerOptionResolver.ResolvePlayerType(player.type),
                    GCPlayerOptionResolver.ResolvePlayerColor(player.color)
                );
            }

            return new GCPlayerIndexMapping(BuildMappingId(options.seed, entries), options.seed, entries);
        }

        internal static uint ComputeFnv1A32(string value)
        {
            return GCFnv1A32.Compute(value);
        }

        internal GCPlayOptions CreateGameFacingPlayOptions()
        {
            var players = new GCPlayerOptions[entriesByIndex.Length];
            for (var index = 0; index < entriesByIndex.Length; index++)
            {
                var entry = entriesByIndex[index];
                players[index] = new GCPlayerOptions
                {
                    playerIndex = entry.PlayerIndex,
                    playerSeed = entry.PlayerSeed,
                    type = entry.PlayerType.ToString(),
                    color = entry.PlayerColor.ToString(),
                };
            }

            return new GCPlayOptions
            {
                players = players,
                seed = Seed,
            };
        }

        internal bool IsValidPlayerIndex(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < entriesByIndex.Length;
        }

        internal bool TryGetPlayerIndexForSourceSeat(int sourceSeatIndex, out int playerIndex)
        {
            return playerIndexBySourceSeatIndex.TryGetValue(sourceSeatIndex, out playerIndex);
        }

        internal GCPlayerIndexMappingEntry GetByPlayerIndex(int playerIndex)
        {
            if (!IsValidPlayerIndex(playerIndex))
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            }

            return entriesByIndex[playerIndex];
        }

        internal bool TryValidatePlayerIndex(int playerIndex, string source, out GCPlayerIndexMappingEntry entry)
        {
            if (IsValidPlayerIndex(playerIndex))
            {
                entry = entriesByIndex[playerIndex];
                return true;
            }

            entry = default;
            EmitInvalidPlayerIndex(playerIndex, source);
            return false;
        }

        internal bool TryValidatePlacement(int[] playerIndicesByPlacement, string source)
        {
            if (playerIndicesByPlacement == null || playerIndicesByPlacement.Length != entriesByIndex.Length)
            {
                EmitInvalidPlayerIndex(-1, source + ":length");
                return false;
            }

            var seen = new bool[entriesByIndex.Length];
            for (var index = 0; index < playerIndicesByPlacement.Length; index++)
            {
                var playerIndex = playerIndicesByPlacement[index];
                if (!IsValidPlayerIndex(playerIndex))
                {
                    EmitInvalidPlayerIndex(playerIndex, source + "[" + index + "]");
                    return false;
                }

                if (seen[playerIndex])
                {
                    EmitInvalidPlayerIndex(playerIndex, source + "[" + index + "]:duplicate");
                    return false;
                }

                seen[playerIndex] = true;
            }

            return true;
        }

        internal GCDiagnosticMappingContext CreateDiagnosticContext(string offendingReference)
        {
            var context = new GCDiagnosticMappingContext()
                .WithMappingId(MappingId)
                .WithSeed(Seed)
                .WithParticipantCount(ParticipantCount);

            if (!string.IsNullOrWhiteSpace(offendingReference))
            {
                context.WithOffendingReference(offendingReference);
            }

            return context;
        }

        private void EmitInvalidPlayerIndex(int playerIndex, string source)
        {
            // A bad index arrives once per input frame, and each emit allocates a payload, queues a
            // runtime message and logs a warning. Suppressing repeats per (source, playerIndex)
            // keeps the first occurrence and every genuinely new offender, and a mapping is built
            // per run, so the set is empty again whenever the roster is rebuilt.
            if (!emittedInvalidPlayerIndexKeys.Add(source + ":" + playerIndex))
            {
                return;
            }

            var context = new GCDiagnosticContext()
                .WithMapping(CreateDiagnosticContext(source + ":playerIndex:" + playerIndex));

            if (playerIndex >= 0)
            {
                context.WithPlayerIndex(playerIndex);
            }

            GCDiagnostics.Emit(
                GCDiagnosticCodes.InvalidPlayerIndex,
                GCDiagnosticSeverity.Warning,
                GCDiagnosticSourceAreas.Mapping,
                "Player index is outside the active mapping.",
                context
            );
        }

        private static string BuildMappingId(int seed, GCPlayerIndexMappingEntry[] entries)
        {
            var builder = new StringBuilder();
            builder.Append(seed);
            builder.Append(":");
            for (var index = 0; index < entries.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                builder.Append(entries[index].Hash);
            }

            return "map-" + ComputeFnv1A32(builder.ToString()).ToString("x8");
        }

        private struct Participant
        {
            internal int CapturedOrder;
            internal uint Hash;
            internal int SourceSeatIndex;
            internal string StableKey;
            internal int PlayerSeed;
            internal GCPlayerType Type;
            internal string ColorName;
        }
    }

    internal readonly struct GCPlayerIndexMappingEntry
    {
        internal readonly int PlayerIndex;
        internal readonly int CapturedOrder;
        internal readonly int SourceSeatIndex;
        internal readonly string StableKey;
        internal readonly int PlayerSeed;
        internal readonly uint Hash;
        internal readonly GCPlayerType PlayerType;
        internal readonly GCPlayerColor PlayerColor;

        internal GCPlayerIndexMappingEntry(
            int playerIndex,
            int capturedOrder,
            int sourceSeatIndex,
            string stableKey,
            int playerSeed,
            uint hash,
            GCPlayerType playerType,
            GCPlayerColor playerColor
        )
        {
            PlayerIndex = playerIndex;
            CapturedOrder = capturedOrder;
            SourceSeatIndex = sourceSeatIndex;
            StableKey = stableKey;
            PlayerSeed = playerSeed;
            Hash = hash;
            PlayerType = playerType;
            PlayerColor = playerColor;
        }
    }

    internal static class GCPlayerOptionResolver
    {
        internal static GCPlayerType ResolvePlayerType(string value)
        {
            return string.Equals(value, GCPlayerType.bot.ToString(), StringComparison.OrdinalIgnoreCase)
                ? GCPlayerType.bot
                : GCPlayerType.player;
        }

        internal static GCPlayerColor ResolvePlayerColor(string value)
        {
            return !string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out GCPlayerColor playerColor)
                ? playerColor
                : GCPlayerColor.blue;
        }
    }
}
