using System;
using DSB.GC.Dev;

namespace DSB.GC
{
    internal sealed class GCActiveRunProjection
    {
        internal GCPlayerIndexMapping PlayerIndexMapping { get; }
        internal GCPlayOptions GameFacingPlayOptions { get; }
        internal GCSeatIdentity[] MappedSeatIdentities { get; }
        internal GCPlatformRuntimeView PlatformData => GameFacingPlayOptions.platformData;

        private GCActiveRunProjection(
            GCPlayerIndexMapping playerIndexMapping,
            GCPlayOptions gameFacingPlayOptions,
            GCSeatIdentity[] mappedSeatIdentities
        )
        {
            PlayerIndexMapping = playerIndexMapping;
            GameFacingPlayOptions = gameFacingPlayOptions;
            MappedSeatIdentities = mappedSeatIdentities ?? Array.Empty<GCSeatIdentity>();
        }

        internal static GCActiveRunProjection Create(GCPlayOptions options)
        {
            return Create(options, null);
        }

        internal static GCActiveRunProjection Create(GCPlayOptions options, GCSeatIdentity[] seatIdentities)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var playerCount = options.players?.Length ?? 0;
            var resolvedSeatIdentities = seatIdentities ?? CreateFallbackSeatIdentities(options);
            if (resolvedSeatIdentities.Length != playerCount)
            {
                throw new ArgumentException("[GamingCouch] Seat identity count must match play player count.");
            }

            var playerIndexMapping = GCPlayerIndexMapping.Create(options, resolvedSeatIdentities);
            var gameFacingPlayOptions = playerIndexMapping.CreateGameFacingPlayOptions();
            gameFacingPlayOptions.runtimeOutput = options.runtimeOutput ?? new GCRuntimeOutputOptions();
            gameFacingPlayOptions.platformData = GCPlatformRuntimeView.CopyForRuntime(options.platformData);
#if UNITY_EDITOR
            gameFacingPlayOptions.runtimeOutput = GCDevAppRuntimeOutputSettings.Apply(gameFacingPlayOptions.runtimeOutput);
#endif

            return new GCActiveRunProjection(
                playerIndexMapping,
                gameFacingPlayOptions,
                CreateMappedSeatIdentities(resolvedSeatIdentities, playerIndexMapping)
            );
        }

        private static GCSeatIdentity[] CreateFallbackSeatIdentities(GCPlayOptions options)
        {
            if (options?.players == null)
            {
                return Array.Empty<GCSeatIdentity>();
            }

            var seatIdentities = new GCSeatIdentity[options.players.Length];
            for (var index = 0; index < options.players.Length; index++)
            {
                var playerOption = options.players[index];
                var sourceSeatIndex = options.usesProvidedPlayerIndexMapping ? 0 : index + 1;
                var stableKey = options.usesProvidedPlayerIndexMapping
                    ? playerOption.playerIndex.ToString()
                    : sourceSeatIndex.ToString();
                seatIdentities[index] = new GCSeatIdentity
                {
                    sourceSeatIndex = sourceSeatIndex,
                    stableKey = stableKey,
                    label = sourceSeatIndex > 0 ? "Seat " + sourceSeatIndex : null,
                    playerType = GCPlayerOptionResolver.ResolvePlayerType(playerOption.type),
                    playerColor = GCPlayerOptionResolver.ResolvePlayerColor(playerOption.color),
                };
            }

            return seatIdentities;
        }

        private static GCSeatIdentity[] CreateMappedSeatIdentities(
            GCSeatIdentity[] capturedSeatIdentities,
            GCPlayerIndexMapping mapping
        )
        {
            if (mapping == null || capturedSeatIdentities == null || capturedSeatIdentities.Length == 0)
            {
                return Array.Empty<GCSeatIdentity>();
            }

            var mappedSeatIdentities = new GCSeatIdentity[capturedSeatIdentities.Length];
            for (var playerIndex = 0; playerIndex < capturedSeatIdentities.Length; playerIndex++)
            {
                var entry = mapping.GetByPlayerIndex(playerIndex);
                mappedSeatIdentities[playerIndex] = capturedSeatIdentities[entry.CapturedOrder];
            }

            return mappedSeatIdentities;
        }
    }
}
