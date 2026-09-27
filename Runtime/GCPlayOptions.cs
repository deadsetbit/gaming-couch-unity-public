using UnityEngine;
using System;

namespace DSB.GC
{
    /// <summary>
    /// Game-facing play options for one player in the current run.
    /// </summary>
    [System.Serializable]
    public struct GCPlayerOptions
    {
        public int playerIndex;
        public int playerSeed;
        public string type;
        public string color;
    }

    [System.Serializable]
    public class GCRuntimeOutputOptions
    {
        public bool stateSnapshots = true;
        public bool screenSpace = true;
        public string runtimeLogCapture = GCRuntimeUnityLogCaptureMode.Off;
    }

    public static class GCRuntimeUnityLogCaptureMode
    {
        public const string Off = "off";
        public const string ErrorOnly = "error_only";
        public const string Error = ErrorOnly;
        public const string WarningAndError = "warning_and_error";
        public const string Full = "full";
    }

    public static class GCPlatformRuntimeValidationState
    {
        public const string Valid = "valid";
        public const string Missing = "missing";
        public const string Invalid = "invalid";
    }

    [System.Serializable]
    public sealed class GCPlatformRuntimeView
    {
        internal const int CurrentSchemaVersion = 1;
        internal const string FallbackKey = "notdefined";
        internal const string UnityPlatformId = "unity";

        public int schemaVersion = CurrentSchemaVersion;
        public string validationState = GCPlatformRuntimeValidationState.Missing;
        public bool fallbackActive = true;
        public GCPlatformRuntimeSource source = GCPlatformRuntimeSource.Fallback("gc.platform.json was not found.");
        public GCPlatformRuntimeGame game = new GCPlatformRuntimeGame(FallbackKey, FallbackKey);
        public GCPlatformRuntimePlatform platform = new GCPlatformRuntimePlatform(UnityPlatformId);
        public string selectedEntryKey = FallbackKey;
        public GCPlatformRuntimeEntry[] entries = { GCPlatformRuntimeEntry.Fallback() };
        public GCPlatformRuntimePlayerColors playerColors = GCPlatformRuntimePlayerColors.CreateDefault();

        internal static GCPlatformRuntimeView CreateValid(
            GCPlatformRuntimeSource source,
            GCPlatformRuntimeGame game,
            GCPlatformRuntimePlatform platform,
            string selectedEntryKey,
            GCPlatformRuntimeEntry[] entries,
            GCPlatformRuntimePlayerColors playerColors
        )
        {
            return new GCPlatformRuntimeView
            {
                schemaVersion = CurrentSchemaVersion,
                validationState = GCPlatformRuntimeValidationState.Valid,
                fallbackActive = false,
                source = source ?? GCPlatformRuntimeSource.Valid(),
                game = game ?? new GCPlatformRuntimeGame(FallbackKey, FallbackKey),
                platform = platform ?? new GCPlatformRuntimePlatform(UnityPlatformId),
                selectedEntryKey = string.IsNullOrWhiteSpace(selectedEntryKey) ? FallbackKey : selectedEntryKey,
                entries = entries ?? Array.Empty<GCPlatformRuntimeEntry>(),
                playerColors = playerColors ?? GCPlatformRuntimePlayerColors.CreateDefault(),
            };
        }

        internal static GCPlatformRuntimeView CreateFallbackMissing()
        {
            return CreateFallback(
                GCPlatformRuntimeValidationState.Missing,
                GCPlatformRuntimeSource.Fallback("gc.platform.json was not found.")
            );
        }

        internal static GCPlatformRuntimeView CreateFallback(
            string validationState,
            GCPlatformRuntimeSource source
        )
        {
            var normalizedState = string.Equals(validationState, GCPlatformRuntimeValidationState.Invalid, StringComparison.Ordinal)
                ? GCPlatformRuntimeValidationState.Invalid
                : GCPlatformRuntimeValidationState.Missing;

            return new GCPlatformRuntimeView
            {
                schemaVersion = CurrentSchemaVersion,
                validationState = normalizedState,
                fallbackActive = true,
                source = source ?? GCPlatformRuntimeSource.Fallback("gc.platform.json metadata is unavailable."),
                game = new GCPlatformRuntimeGame(FallbackKey, FallbackKey),
                platform = new GCPlatformRuntimePlatform(UnityPlatformId),
                selectedEntryKey = FallbackKey,
                entries = new[] { GCPlatformRuntimeEntry.Fallback() },
                playerColors = GCPlatformRuntimePlayerColors.CreateDefault(),
            };
        }

        internal GCPlatformRuntimeView NormalizeForRuntime()
        {
            schemaVersion = CurrentSchemaVersion;

            if (!string.Equals(validationState, GCPlatformRuntimeValidationState.Valid, StringComparison.Ordinal) &&
                !string.Equals(validationState, GCPlatformRuntimeValidationState.Invalid, StringComparison.Ordinal))
            {
                validationState = GCPlatformRuntimeValidationState.Missing;
            }

            source = source ?? GCPlatformRuntimeSource.Fallback("gc.platform.json metadata is unavailable.");
            source.NormalizeForRuntime();

            if (!string.Equals(validationState, GCPlatformRuntimeValidationState.Valid, StringComparison.Ordinal))
            {
                fallbackActive = true;
                game = new GCPlatformRuntimeGame(FallbackKey, FallbackKey);
                platform = new GCPlatformRuntimePlatform(UnityPlatformId);
                selectedEntryKey = FallbackKey;
                entries = new[] { GCPlatformRuntimeEntry.Fallback() };
                playerColors = GCPlatformRuntimePlayerColors.CreateDefault();
                return this;
            }

            fallbackActive = false;
            game = game ?? new GCPlatformRuntimeGame(FallbackKey, FallbackKey);
            game.NormalizeForRuntime();
            platform = platform ?? new GCPlatformRuntimePlatform(UnityPlatformId);
            platform.NormalizeForRuntime();
            selectedEntryKey = string.IsNullOrWhiteSpace(selectedEntryKey) ? FallbackKey : selectedEntryKey;
            entries = entries ?? Array.Empty<GCPlatformRuntimeEntry>();
            for (var index = 0; index < entries.Length; index++)
            {
                if (entries[index] != null)
                {
                    entries[index].NormalizeForRuntime();
                }
            }

            playerColors = GCPlatformRuntimePlayerColors.MergeWithDefaults(playerColors);
            return this;
        }

        internal static GCPlatformRuntimeView CopyForRuntime(GCPlatformRuntimeView source)
        {
            if (source == null)
            {
                return CreateFallbackMissing();
            }

            var json = JsonUtility.ToJson(source);
            var copy = string.IsNullOrEmpty(json)
                ? null
                : JsonUtility.FromJson<GCPlatformRuntimeView>(json);
            return copy != null
                ? copy.NormalizeForRuntime()
                : CreateFallbackMissing();
        }
    }

    [System.Serializable]
    public sealed class GCPlatformRuntimeSource
    {
        internal const int PlatformDataVersionUnavailable = -1;

        public string fileName = "gc.platform.json";
        public int platformDataVersion = PlatformDataVersionUnavailable;
        public string path;
        public string message;
        public string fieldName;

        public GCPlatformRuntimeSource()
        {
        }

        internal GCPlatformRuntimeSource(
            string fileName,
            int? platformDataVersion,
            string path,
            string message,
            string fieldName
        )
        {
            this.fileName = string.IsNullOrWhiteSpace(fileName) ? "gc.platform.json" : fileName;
            this.platformDataVersion = platformDataVersion.HasValue
                ? platformDataVersion.Value
                : PlatformDataVersionUnavailable;
            this.path = path;
            this.message = message;
            this.fieldName = fieldName;
        }

        internal static GCPlatformRuntimeSource Valid(int? platformDataVersion = null, string path = null)
        {
            return new GCPlatformRuntimeSource("gc.platform.json", platformDataVersion, path, null, null);
        }

        internal static GCPlatformRuntimeSource Fallback(
            string message,
            string path = null,
            string fieldName = null,
            int? platformDataVersion = null
        )
        {
            return new GCPlatformRuntimeSource("gc.platform.json", platformDataVersion, path, message, fieldName);
        }

        internal void NormalizeForRuntime()
        {
            fileName = string.IsNullOrWhiteSpace(fileName) ? "gc.platform.json" : fileName;
            if (platformDataVersion < PlatformDataVersionUnavailable)
            {
                platformDataVersion = PlatformDataVersionUnavailable;
            }
        }
    }

    [System.Serializable]
    public sealed class GCPlatformRuntimeGame
    {
        public string key;
        public string name;

        public GCPlatformRuntimeGame()
        {
        }

        internal GCPlatformRuntimeGame(string key, string name)
        {
            this.key = key;
            this.name = name;
        }

        internal void NormalizeForRuntime()
        {
            key = string.IsNullOrWhiteSpace(key) ? GCPlatformRuntimeView.FallbackKey : key;
            name = string.IsNullOrWhiteSpace(name) ? GCPlatformRuntimeView.FallbackKey : name;
        }
    }

    [System.Serializable]
    public sealed class GCPlatformRuntimePlatform
    {
        public string id;

        public GCPlatformRuntimePlatform()
        {
        }

        internal GCPlatformRuntimePlatform(string id)
        {
            this.id = id;
        }

        internal void NormalizeForRuntime()
        {
            id = string.IsNullOrWhiteSpace(id) ? GCPlatformRuntimeView.UnityPlatformId : id;
        }
    }

    [System.Serializable]
    public sealed class GCPlatformRuntimeEntry
    {
        public string entryKey;
        public string name;
        public int minPlayers;
        public int maxPlayers;
        public bool botSupport;

        public GCPlatformRuntimeEntry()
        {
        }

        internal GCPlatformRuntimeEntry(
            string entryKey,
            string name,
            int minPlayers,
            int maxPlayers,
            bool botSupport
        )
        {
            this.entryKey = entryKey;
            this.name = name;
            this.minPlayers = minPlayers;
            this.maxPlayers = maxPlayers;
            this.botSupport = botSupport;
        }

        internal static GCPlatformRuntimeEntry Fallback()
        {
            return new GCPlatformRuntimeEntry(
                GCPlatformRuntimeView.FallbackKey,
                GCPlatformRuntimeView.FallbackKey,
                1,
                8,
                true
            );
        }

        internal void NormalizeForRuntime()
        {
            entryKey = string.IsNullOrWhiteSpace(entryKey) ? GCPlatformRuntimeView.FallbackKey : entryKey;
            name = string.IsNullOrWhiteSpace(name) ? GCPlatformRuntimeView.FallbackKey : name;
            minPlayers = Math.Max(0, minPlayers);
            maxPlayers = Math.Max(minPlayers, maxPlayers);
        }
    }

    [System.Serializable]
    public sealed class GCPlatformRuntimePlayerColor
    {
        public int[] @base;
        public int[] muted;
        public int[] mutedDarker;

        public GCPlatformRuntimePlayerColor()
        {
        }

        internal GCPlatformRuntimePlayerColor(int[] baseColor, int[] mutedColor, int[] mutedDarkerColor)
        {
            @base = CloneRgb(baseColor);
            muted = CloneRgb(mutedColor);
            mutedDarker = CloneRgb(mutedDarkerColor);
        }

        internal GCPlatformRuntimePlayerColor Clone()
        {
            return new GCPlatformRuntimePlayerColor(@base, muted, mutedDarker);
        }

        internal GCPlatformRuntimePlayerColor WithFallback(GCPlatformRuntimePlayerColor fallback)
        {
            if (fallback == null)
            {
                fallback = new GCPlatformRuntimePlayerColor(new[] { 0, 0, 0 }, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });
            }

            return new GCPlatformRuntimePlayerColor(
                IsRgb(@base) ? @base : fallback.@base,
                IsRgb(muted) ? muted : fallback.muted,
                IsRgb(mutedDarker) ? mutedDarker : fallback.mutedDarker
            );
        }

        private static bool IsRgb(int[] color)
        {
            return color != null &&
                   color.Length == 3 &&
                   IsRgbComponent(color[0]) &&
                   IsRgbComponent(color[1]) &&
                   IsRgbComponent(color[2]);
        }

        private static bool IsRgbComponent(int value)
        {
            return value >= 0 && value <= 255;
        }

        private static int[] CloneRgb(int[] color)
        {
            if (color == null || color.Length != 3)
            {
                return new[] { 0, 0, 0 };
            }

            return new[] { color[0], color[1], color[2] };
        }
    }

    [System.Serializable]
    public sealed class GCPlatformRuntimePlayerColors
    {
        public GCPlatformRuntimePlayerColor blue;
        public GCPlatformRuntimePlayerColor red;
        public GCPlatformRuntimePlayerColor green;
        public GCPlatformRuntimePlayerColor yellow;
        public GCPlatformRuntimePlayerColor purple;
        public GCPlatformRuntimePlayerColor pink;
        public GCPlatformRuntimePlayerColor cyan;
        public GCPlatformRuntimePlayerColor brown;

        internal static GCPlatformRuntimePlayerColors CreateDefault()
        {
            var colors = new GCPlatformRuntimePlayerColors();
            colors.blue = FromBuiltIn(GCPlayerColor.blue);
            colors.red = FromBuiltIn(GCPlayerColor.red);
            colors.green = FromBuiltIn(GCPlayerColor.green);
            colors.yellow = FromBuiltIn(GCPlayerColor.yellow);
            colors.purple = FromBuiltIn(GCPlayerColor.purple);
            colors.pink = FromBuiltIn(GCPlayerColor.pink);
            colors.cyan = FromBuiltIn(GCPlayerColor.cyan);
            colors.brown = FromBuiltIn(GCPlayerColor.brown);
            return colors;
        }

        internal void Set(string colorKey, GCPlatformRuntimePlayerColor color)
        {
            if (color == null)
            {
                return;
            }

            switch (colorKey)
            {
                case "blue":
                    blue = color.Clone();
                    break;
                case "red":
                    red = color.Clone();
                    break;
                case "green":
                    green = color.Clone();
                    break;
                case "yellow":
                    yellow = color.Clone();
                    break;
                case "purple":
                    purple = color.Clone();
                    break;
                case "pink":
                    pink = color.Clone();
                    break;
                case "cyan":
                    cyan = color.Clone();
                    break;
                case "brown":
                    brown = color.Clone();
                    break;
            }
        }

        internal static GCPlatformRuntimePlayerColors MergeWithDefaults(GCPlatformRuntimePlayerColors source)
        {
            var colors = CreateDefault();
            if (source == null)
            {
                return colors;
            }

            colors.blue = MergeColor(source.blue, colors.blue);
            colors.red = MergeColor(source.red, colors.red);
            colors.green = MergeColor(source.green, colors.green);
            colors.yellow = MergeColor(source.yellow, colors.yellow);
            colors.purple = MergeColor(source.purple, colors.purple);
            colors.pink = MergeColor(source.pink, colors.pink);
            colors.cyan = MergeColor(source.cyan, colors.cyan);
            colors.brown = MergeColor(source.brown, colors.brown);
            return colors;
        }

        private static GCPlatformRuntimePlayerColor MergeColor(
            GCPlatformRuntimePlayerColor source,
            GCPlatformRuntimePlayerColor fallback
        )
        {
            return source != null ? source.WithFallback(fallback) : fallback;
        }

        private static GCPlatformRuntimePlayerColor FromBuiltIn(GCPlayerColor color)
        {
            var variants = GCPlayerColorData.Variants[color];
            return new GCPlatformRuntimePlayerColor(
                ToRgbArray(variants.BaseColor),
                ToRgbArray(variants.Light),
                ToRgbArray(variants.Dark)
            );
        }

        private static int[] ToRgbArray(Color color)
        {
            return new[]
            {
                Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
            };
        }
    }

    [System.Serializable]
    public class GCPlayOptions
    {
        internal const string MissingPlayersPayloadErrorMessage =
            "[GamingCouch] Hosted play payloads must include players[] entries with playerIndex.";

        internal const string LegacyPlayersPayloadErrorMessage =
            "[GamingCouch] Legacy play payloads containing playerId/name roster entries are not accepted by the Unity package. Use players[] entries with playerIndex, playerSeed, type, and color.";

        internal const string MissingPlayerIndexPayloadErrorMessage =
            "[GamingCouch] players[] entries must include playerIndex.";

        internal const string DensePlayerIndexPayloadErrorMessage =
            "[GamingCouch] players[] must use dense zero-based playerIndex values.";

        internal const string DuplicatePlayerIndexPayloadErrorMessage =
            "[GamingCouch] players[] must not contain duplicate playerIndex values.";

        public GCPlayerOptions[] players;
        /**
        * Value between 1-999999.
        *
        * Seed provided by the platform. This is unique for each round.
        * The seed can be used to generate random levels and such.
        * The idea is that the seed should always result in the same game.
        *
        * Use cases:
        * 1) for online multiplayer to generate levels or other
        * parts of the game that would require a lot of syncing over net when done one by one
        * (think level tiles, randomized atmosphere fx etc.).
        *
        * 2) potentially to generate repayable levels/games if we decide to allow players to define the seed in the future.
        */
        public int seed;
        public GCRuntimeOutputOptions runtimeOutput = new GCRuntimeOutputOptions();
        public GCPlatformRuntimeView platformData = GCPlatformRuntimeView.CreateFallbackMissing();

        [NonSerialized]
        internal bool usesProvidedPlayerIndexMapping;

        public static GCPlayOptions CreateFromJSON(string optionsJson)
        {
            var hasPlayersJsonField = GCPlayOptionsPayloadValidator.TryFindTopLevelJsonFieldValueRange(
                optionsJson,
                "players",
                out var playersValueStart,
                out var playersValueEnd
            );
            if (hasPlayersJsonField)
            {
                GCPlayOptionsPayloadValidator.ValidatePlayersJsonShape(optionsJson, playersValueStart, playersValueEnd, nameof(optionsJson));
            }

            var transport = JsonUtility.FromJson<GCPlayOptionsTransport>(optionsJson);
            if (transport == null)
            {
                return null;
            }

            if (!hasPlayersJsonField || transport.players == null)
            {
                throw new ArgumentException(MissingPlayersPayloadErrorMessage, nameof(optionsJson));
            }

            var hasTransportRoster = transport.players != null && transport.players.Length > 0;
            if (hasTransportRoster)
            {
                GCPlayOptionsPayloadValidator.ValidateProvidedPlayerIndices(transport.players, nameof(optionsJson));
            }

            return new GCPlayOptions
            {
                players = transport.players,
                seed = transport.seed,
                runtimeOutput = transport.runtimeOutput ?? new GCRuntimeOutputOptions(),
                platformData = GCPlatformRuntimeView.CopyForRuntime(transport.platformData),
                usesProvidedPlayerIndexMapping = hasTransportRoster,
            };
        }
    }

    [System.Serializable]
    internal sealed class GCPlayOptionsTransport
    {
        public GCPlayerOptions[] players;
        public int seed;
        public GCRuntimeOutputOptions runtimeOutput;
        public GCPlatformRuntimeView platformData;
    }
}
