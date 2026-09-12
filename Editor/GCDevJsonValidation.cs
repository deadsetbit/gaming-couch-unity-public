#if UNITY_EDITOR
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DSB.GC.Dev
{
    internal enum GCDevJsonIssueSeverity
    {
        Error,
        Warning,
    }

    internal enum GCDevJsonIssueCode
    {
        MissingFile,
        InvalidJson,
        InvalidRoot,
        UnsupportedDevVersion,
        InvalidEntryKey,
        InvalidSeed,
        InvalidSeatCount,
        InvalidSeatFields,
        NoEnabledSeats,
        ReadError,
        WriteError,
        MissingPlatformDataFile,
        InvalidPlatformDataJson,
        InvalidPlatformDataRoot,
        InvalidPlatformDataFields,
        PlatformDataReadError,
        PlatformDataPlatformMismatch,
        PlatformDataEntryMissing,
        PlatformDataEnabledSeatsAboveMaximum,
        PlatformDataBotSupportDisabled,
    }

    internal sealed class GCDevJsonIssue
    {
        internal readonly GCDevJsonIssueSeverity severity;
        internal readonly GCDevJsonIssueCode code;
        internal readonly string message;
        internal readonly string path;
        internal readonly int seatIndex;
        internal readonly string fieldName;
        internal readonly string entryKey;

        private GCDevJsonIssue(
            GCDevJsonIssueSeverity severity,
            GCDevJsonIssueCode code,
            string message,
            string path,
            int seatIndex,
            string fieldName,
            string entryKey
        )
        {
            this.severity = severity;
            this.code = code;
            this.message = message;
            this.path = path;
            this.seatIndex = seatIndex;
            this.fieldName = fieldName;
            this.entryKey = entryKey;
        }

        internal static GCDevJsonIssue Error(GCDevJsonIssueCode code, string message, string path, int seatIndex = 0, string fieldName = null, string entryKey = null)
        {
            return new GCDevJsonIssue(GCDevJsonIssueSeverity.Error, code, message, path, seatIndex, fieldName, entryKey);
        }

        internal static GCDevJsonIssue Warning(GCDevJsonIssueCode code, string message, string path, int seatIndex = 0, string fieldName = null, string entryKey = null)
        {
            return new GCDevJsonIssue(GCDevJsonIssueSeverity.Warning, code, message, path, seatIndex, fieldName, entryKey);
        }
    }

    internal sealed class GCDevJsonValidationResult
    {
        internal readonly GCDevJsonIssue[] issues;

        private GCDevJsonValidationResult(GCDevJsonIssue[] issues)
        {
            this.issues = issues ?? new GCDevJsonIssue[0];
        }

        internal bool IsValid
        {
            get { return ErrorCount == 0; }
        }

        internal int ErrorCount
        {
            get
            {
                var errorCount = 0;
                for (var index = 0; index < issues.Length; index++)
                {
                    if (issues[index] != null && issues[index].severity == GCDevJsonIssueSeverity.Error)
                    {
                        errorCount++;
                    }
                }

                return errorCount;
            }
        }

        internal int WarningCount
        {
            get
            {
                var warningCount = 0;
                for (var index = 0; index < issues.Length; index++)
                {
                    if (issues[index] != null && issues[index].severity == GCDevJsonIssueSeverity.Warning)
                    {
                        warningCount++;
                    }
                }

                return warningCount;
            }
        }

        internal static GCDevJsonValidationResult Valid()
        {
            return new GCDevJsonValidationResult(new GCDevJsonIssue[0]);
        }

        internal static GCDevJsonValidationResult FromIssue(GCDevJsonIssue issue)
        {
            if (issue == null)
            {
                return Valid();
            }

            return new GCDevJsonValidationResult(new[] { issue });
        }

        internal static GCDevJsonValidationResult FromIssues(List<GCDevJsonIssue> issues)
        {
            return new GCDevJsonValidationResult(issues != null ? issues.ToArray() : new GCDevJsonIssue[0]);
        }
    }

    internal static class GCDevJsonIssueFormatter
    {
        internal static string Format(GCDevJsonIssue issue)
        {
            if (issue == null)
            {
                return string.Empty;
            }

            var message = IsValidPlatformDataGateIssue(issue.code)
                ? "Valid platform data gate failed. "
                : string.Empty;

            message += issue.code + ": " + issue.message;
            if (issue.seatIndex > 0)
            {
                message += " Seat " + issue.seatIndex + ".";
            }

            if (!string.IsNullOrEmpty(issue.fieldName))
            {
                message += " Field: " + issue.fieldName + ".";
            }

            if (!string.IsNullOrEmpty(issue.path))
            {
                message += " Path: " + issue.path + ".";
            }

            return message;
        }

        private static bool IsValidPlatformDataGateIssue(GCDevJsonIssueCode code)
        {
            return code == GCDevJsonIssueCode.PlatformDataPlatformMismatch ||
                   code == GCDevJsonIssueCode.PlatformDataEntryMissing ||
                   code == GCDevJsonIssueCode.PlatformDataEnabledSeatsAboveMaximum;
        }
    }

    internal static class GCDevJsonTokenReader
    {
        internal static bool TryReadString(JToken token, out string value)
        {
            value = null;
            if (token == null || token.Type != JTokenType.String)
            {
                return false;
            }

            value = token.Value<string>();
            return true;
        }

        internal static bool TryReadBool(JToken token, out bool value)
        {
            value = false;
            if (token == null || token.Type != JTokenType.Boolean)
            {
                return false;
            }

            value = token.Value<bool>();
            return true;
        }
    }

    internal static class GCDevJsonValidation
    {
        internal static GCDevJsonReadResult BuildReadResult(GCDevJsonParsedFile parsedFile)
        {
            return BuildReadResult(parsedFile, null);
        }

        internal static GCDevJsonReadResult BuildReadResult(GCDevJsonParsedFile parsedFile, GCPlatformDataReadResult platformDataReadResult)
        {
            if (parsedFile == null)
            {
                parsedFile = GCDevJsonParsedFile.ReadError(null, "gc.dev.json could not be read because parser state was missing.");
            }

            if (parsedFile.state == GCDevJsonParseState.MissingFile)
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.MissingFile, parsedFile.message);
            }

            if (parsedFile.state == GCDevJsonParseState.InvalidJson)
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.InvalidJson, parsedFile.message);
            }

            if (parsedFile.state == GCDevJsonParseState.InvalidRoot)
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.InvalidRoot, parsedFile.message);
            }

            if (parsedFile.state == GCDevJsonParseState.ReadError)
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.ReadError, parsedFile.message);
            }

            return ValidateParsedObject(parsedFile, platformDataReadResult);
        }

        internal static GCDevJsonValidationResult ValidateData(GCDevJsonFile data, string path = null)
        {
            return ValidateData(data, path, null);
        }

        internal static GCDevJsonValidationResult ValidateData(GCDevJsonFile data, string path, GCPlatformDataReadResult platformDataReadResult)
        {
            var issues = new List<GCDevJsonIssue>();
            AddDataIssues(data, path, issues);
            if (!HasErrors(issues))
            {
                AddPlatformDataContextIssues(data, path, platformDataReadResult, issues);
            }

            return GCDevJsonValidationResult.FromIssues(issues);
        }

        private static void AddDataIssues(GCDevJsonFile data, string path, List<GCDevJsonIssue> issues)
        {
            if (data == null)
            {
                issues.Add(GCDevJsonIssue.Error(GCDevJsonIssueCode.WriteError, "gc.dev.json data is missing.", path));
                return;
            }

            if (data.devVersion != GCDevJsonFile.SupportedDevVersion)
            {
                issues.Add(GCDevJsonIssue.Error(GCDevJsonIssueCode.UnsupportedDevVersion, "gc.dev.json must use devVersion " + GCDevJsonFile.SupportedDevVersion + ".", path));
            }

            if (!IsValidEntryKey(data.entryKey))
            {
                issues.Add(GCDevJsonIssue.Error(GCDevJsonIssueCode.InvalidEntryKey, GetInvalidEntryKeyMessage(), path));
            }

            if (!IsValidSeed(data.seed))
            {
                issues.Add(GCDevJsonIssue.Error(GCDevJsonIssueCode.InvalidSeed, GetInvalidSeedMessage(), path));
            }

            AddSeatIssues(data.seats, path, issues);
        }

        private static GCDevJsonReadResult ValidateParsedObject(GCDevJsonParsedFile parsedFile, GCPlatformDataReadResult platformDataReadResult)
        {
            var jsonObject = parsedFile.jsonObject;
            var path = parsedFile.path;
            if (jsonObject == null)
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.InvalidRoot, "gc.dev.json must be a JSON object.");
            }

            if (!TryReadSupportedDevVersion(jsonObject["devVersion"]))
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.UnsupportedDevVersion, "gc.dev.json must use devVersion " + GCDevJsonFile.SupportedDevVersion + ".");
            }

            string entryKey;
            if (!GCDevJsonTokenReader.TryReadString(jsonObject["entryKey"], out entryKey) || !IsValidEntryKey(entryKey))
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.InvalidEntryKey, GetInvalidEntryKeyMessage());
            }

            string seed;
            if (!GCDevJsonTokenReader.TryReadString(jsonObject["seed"], out seed) || !IsValidSeed(seed))
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.InvalidSeed, GetInvalidSeedMessage());
            }

            var seatsToken = jsonObject["seats"];
            var seatsArray = seatsToken as JArray;
            if (seatsArray == null || seatsArray.Count != GCDevJsonFile.SeatCount)
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.InvalidSeatCount, "gc.dev.json must contain exactly " + GCDevJsonFile.SeatCount + " seats.");
            }

            var seats = new GCDevJsonSeat[GCDevJsonFile.SeatCount];
            for (var index = 0; index < seatsArray.Count; index++)
            {
                string name;
                bool enabled;
                bool isBot;
                if (!TryReadSeat(seatsArray[index], index + 1, path, out name, out enabled, out isBot, out var issue))
                {
                    return new GCDevJsonReadResult(
                        parsedFile,
                        GCDevJsonValidationResult.FromIssue(issue),
                        null,
                        platformDataReadResult
                    );
                }

                seats[index] = new GCDevJsonSeat(name, enabled, isBot);
            }

            var data = new GCDevJsonFile(GCDevJsonFile.SupportedDevVersion, entryKey, seed, seats);
            var structuralValidation = ValidateData(data, path);
            if (!structuralValidation.IsValid)
            {
                return new GCDevJsonReadResult(parsedFile, structuralValidation, null, platformDataReadResult);
            }

            var validation = ValidateData(data, path, platformDataReadResult);
            return new GCDevJsonReadResult(parsedFile, validation, data, platformDataReadResult);
        }

        private static GCDevJsonReadResult InvalidReadResult(GCDevJsonParsedFile parsedFile, GCDevJsonIssueCode code, string message)
        {
            return new GCDevJsonReadResult(
                parsedFile,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(code, message, parsedFile.path)),
                null
            );
        }

        private static bool TryReadSupportedDevVersion(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer)
            {
                return false;
            }

            var devVersion = token.Value<long>();
            return devVersion == GCDevJsonFile.SupportedDevVersion;
        }

        private static bool TryReadSeat(
            JToken token,
            int seatIndex,
            string path,
            out string name,
            out bool enabled,
            out bool isBot,
            out GCDevJsonIssue issue
        )
        {
            name = null;
            enabled = false;
            isBot = false;
            issue = null;

            var seatObject = token as JObject;
            if (seatObject == null)
            {
                issue = InvalidSeatIssue(path, seatIndex, null);
                return false;
            }

            if (!GCDevJsonTokenReader.TryReadString(seatObject["name"], out name) || !IsValidPlayerName(name))
            {
                issue = InvalidSeatIssue(path, seatIndex, "name");
                return false;
            }

            if (!GCDevJsonTokenReader.TryReadBool(seatObject["enabled"], out enabled))
            {
                issue = InvalidSeatIssue(path, seatIndex, "enabled");
                return false;
            }

            if (!GCDevJsonTokenReader.TryReadBool(seatObject["isBot"], out isBot))
            {
                issue = InvalidSeatIssue(path, seatIndex, "isBot");
                return false;
            }

            return true;
        }

        private static void AddSeatIssues(GCDevJsonSeat[] seats, string path, List<GCDevJsonIssue> issues)
        {
            if (seats == null || seats.Length != GCDevJsonFile.SeatCount)
            {
                issues.Add(GCDevJsonIssue.Error(GCDevJsonIssueCode.InvalidSeatCount, "gc.dev.json must contain exactly " + GCDevJsonFile.SeatCount + " seats.", path));
                return;
            }

            var hasEnabledSeat = false;
            for (var index = 0; index < seats.Length; index++)
            {
                var seat = seats[index];
                if (seat == null)
                {
                    issues.Add(InvalidSeatIssue(path, index + 1, null));
                    continue;
                }

                if (!IsValidPlayerName(seat.name))
                {
                    issues.Add(InvalidSeatIssue(path, index + 1, "name"));
                }

                if (seat.enabled)
                {
                    hasEnabledSeat = true;
                }
            }

            if (!hasEnabledSeat)
            {
                issues.Add(GCDevJsonIssue.Error(GCDevJsonIssueCode.NoEnabledSeats, "gc.dev.json must enable at least one seat.", path));
            }
        }

        private static void AddPlatformDataContextIssues(
            GCDevJsonFile data,
            string path,
            GCPlatformDataReadResult platformDataReadResult,
            List<GCDevJsonIssue> issues
        )
        {
            if (platformDataReadResult == null)
            {
                return;
            }

            AddValidationIssues(platformDataReadResult.validation, issues);
            if (!platformDataReadResult.IsValid)
            {
                return;
            }

            var platformData = platformDataReadResult.data;
            var platformDataPath = platformDataReadResult.parsedFile != null ? platformDataReadResult.parsedFile.path : null;
            if (platformData.platformId != GCPlatformDataFile.UnityPlatformId)
            {
                issues.Add(GCDevJsonIssue.Warning(
                    GCDevJsonIssueCode.PlatformDataPlatformMismatch,
                    "gc.platform.json platform.id must be \"unity\" for Unity editor play settings. Local play will use fallback platform metadata.",
                    platformDataPath,
                    0,
                    "platform.id"
                ));
                return;
            }

            GCPlatformDataEntry entry;
            if (!platformData.TryGetEntry(data.entryKey, out entry))
            {
                issues.Add(GCDevJsonIssue.Error(
                    GCDevJsonIssueCode.PlatformDataEntryMissing,
                    "Entry \"" + data.entryKey + "\" was not found in gc.platform.json.",
                    platformDataPath,
                    0,
                    "game.entries",
                    data.entryKey
                ));
                return;
            }

            var enabledSeatCount = data.EnabledSeatCount;
            // Local editor playtests may run with one enabled seat even when production minPlayers is higher.
            if (enabledSeatCount > entry.maxPlayers)
            {
                issues.Add(GCDevJsonIssue.Error(
                    GCDevJsonIssueCode.PlatformDataEnabledSeatsAboveMaximum,
                    "gc.dev.json must enable at most " + entry.maxPlayers + " seats for \"" + data.entryKey + "\".",
                    path,
                    0,
                    "seats",
                    data.entryKey
                ));
            }

            if (!entry.botSupport && HasEnabledBotSeats(data))
            {
                issues.Add(GCDevJsonIssue.Warning(
                    GCDevJsonIssueCode.PlatformDataBotSupportDisabled,
                    "Entry \"" + data.entryKey + "\" does not declare bot support, but enabled bot seats are present.",
                    path,
                    0,
                    "seats",
                    data.entryKey
                ));
            }
        }

        private static void AddValidationIssues(GCDevJsonValidationResult validation, List<GCDevJsonIssue> issues)
        {
            if (validation == null || validation.issues == null)
            {
                return;
            }

            for (var index = 0; index < validation.issues.Length; index++)
            {
                if (validation.issues[index] != null)
                {
                    issues.Add(validation.issues[index]);
                }
            }
        }

        private static bool HasErrors(List<GCDevJsonIssue> issues)
        {
            for (var index = 0; index < issues.Count; index++)
            {
                if (issues[index] != null && issues[index].severity == GCDevJsonIssueSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasEnabledBotSeats(GCDevJsonFile data)
        {
            if (data == null || data.seats == null)
            {
                return false;
            }

            for (var index = 0; index < data.seats.Length; index++)
            {
                var seat = data.seats[index];
                if (seat != null && seat.enabled && seat.isBot)
                {
                    return true;
                }
            }

            return false;
        }

        private static GCDevJsonIssue InvalidSeatIssue(string path, int seatIndex, string fieldName)
        {
            return GCDevJsonIssue.Error(
                GCDevJsonIssueCode.InvalidSeatFields,
                "Each gc.dev.json seat must include a " + GCDevJsonFile.PlayerNameMinLength + "-" + GCDevJsonFile.PlayerNameMaxLength + " character name, enabled, and isBot.",
                path,
                seatIndex,
                fieldName
            );
        }

        internal static bool IsValidPlayerName(string name)
        {
            if (name == null)
            {
                return false;
            }

            var trimmed = name.Trim();
            return trimmed.Length >= GCDevJsonFile.PlayerNameMinLength &&
                   trimmed.Length <= GCDevJsonFile.PlayerNameMaxLength;
        }

        private static bool IsValidEntryKey(string entryKey)
        {
            return !string.IsNullOrWhiteSpace(entryKey);
        }

        private static bool IsValidSeed(string seed)
        {
            if (seed == GCDevJsonFile.RandomSeed)
            {
                return true;
            }

            if (string.IsNullOrEmpty(seed))
            {
                return false;
            }

            var parsedSeed = 0;
            for (var index = 0; index < seed.Length; index++)
            {
                var character = seed[index];
                if (character < '0' || character > '9')
                {
                    return false;
                }

                parsedSeed = parsedSeed * 10 + character - '0';
                if (parsedSeed > GCDevJsonFile.MaxSeed)
                {
                    return false;
                }
            }

            return parsedSeed >= GCDevJsonFile.MinSeed;
        }

        private static string GetInvalidSeedMessage()
        {
            return "gc.dev.json seed must be \"random\" or an integer string from " + GCDevJsonFile.MinSeed + " to " + GCDevJsonFile.MaxSeed + ".";
        }

        private static string GetInvalidEntryKeyMessage()
        {
            return "gc.dev.json entryKey must be a non-empty string.";
        }
    }

    internal static class GCPlatformDataValidation
    {
        internal static GCPlatformDataReadResult BuildReadResult(GCPlatformDataParsedFile parsedFile)
        {
            if (parsedFile == null)
            {
                parsedFile = GCPlatformDataParsedFile.ReadError(null, "gc.platform.json could not be read because parser state was missing.");
            }

            if (parsedFile.state == GCPlatformDataParseState.MissingFile)
            {
                return WarningReadResult(parsedFile, GCDevJsonIssueCode.MissingPlatformDataFile, parsedFile.message);
            }

            if (parsedFile.state == GCPlatformDataParseState.InvalidJson)
            {
                return WarningReadResult(parsedFile, GCDevJsonIssueCode.InvalidPlatformDataJson, parsedFile.message);
            }

            if (parsedFile.state == GCPlatformDataParseState.InvalidRoot)
            {
                return WarningReadResult(parsedFile, GCDevJsonIssueCode.InvalidPlatformDataRoot, parsedFile.message);
            }

            if (parsedFile.state == GCPlatformDataParseState.ReadError)
            {
                return WarningReadResult(parsedFile, GCDevJsonIssueCode.PlatformDataReadError, parsedFile.message);
            }

            return ValidateParsedObject(parsedFile);
        }

        private static GCPlatformDataReadResult ValidateParsedObject(GCPlatformDataParsedFile parsedFile)
        {
            var jsonObject = parsedFile.jsonObject;
            if (jsonObject == null)
            {
                return WarningReadResult(parsedFile, GCDevJsonIssueCode.InvalidPlatformDataRoot, "gc.platform.json must be a JSON object.");
            }

            int platformDataVersion;
            if (!TryReadNonNegativeInteger(jsonObject["platformDataVersion"], out platformDataVersion))
            {
                return InvalidFieldsReadResult(parsedFile, "gc.platform.json must include integer platformDataVersion.", "platformDataVersion");
            }

            var gameObject = jsonObject["game"] as JObject;
            if (gameObject == null)
            {
                return InvalidFieldsReadResult(parsedFile, "gc.platform.json must include game.", "game", platformDataVersion);
            }

            string gameKey;
            if (!TryReadNonEmptyString(gameObject["key"], out gameKey))
            {
                return InvalidFieldsReadResult(parsedFile, "gc.platform.json game.key must be a non-empty string.", "game.key", platformDataVersion);
            }

            string gameName;
            if (!TryReadNonEmptyString(gameObject["name"], out gameName))
            {
                return InvalidFieldsReadResult(parsedFile, "gc.platform.json game.name must be a non-empty string.", "game.name", platformDataVersion);
            }

            var platformObject = jsonObject["platform"] as JObject;
            string platformId;
            if (platformObject == null || !TryReadNonEmptyString(platformObject["id"], out platformId))
            {
                return InvalidFieldsReadResult(parsedFile, "gc.platform.json platform.id must be a non-empty string.", "platform.id", platformDataVersion);
            }

            Dictionary<string, GCPlatformDataEntry> entries;
            if (!TryReadEntries(gameObject["entries"], parsedFile, out entries, out var entryIssue))
            {
                return new GCPlatformDataReadResult(
                    parsedFile,
                    GCDevJsonValidationResult.FromIssue(entryIssue),
                    null,
                    platformDataVersion
                );
            }

            Dictionary<string, GCPlatformDataColorVariants> playerColors;
            if (!TryReadPlayerColors(jsonObject["properties"], parsedFile, out playerColors, out var colorIssue))
            {
                return new GCPlatformDataReadResult(
                    parsedFile,
                    GCDevJsonValidationResult.FromIssue(colorIssue),
                    null,
                    platformDataVersion
                );
            }

            return new GCPlatformDataReadResult(
                parsedFile,
                GCDevJsonValidationResult.Valid(),
                new GCPlatformDataFile(platformDataVersion, gameKey, gameName, platformId, entries, playerColors)
            );
        }

        private static GCPlatformDataReadResult WarningReadResult(GCPlatformDataParsedFile parsedFile, GCDevJsonIssueCode code, string message)
        {
            return new GCPlatformDataReadResult(
                parsedFile,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Warning(code, message, parsedFile.path)),
                null
            );
        }

        private static GCPlatformDataReadResult InvalidFieldsReadResult(
            GCPlatformDataParsedFile parsedFile,
            string message,
            string fieldName,
            int? platformDataVersion = null
        )
        {
            return new GCPlatformDataReadResult(
                parsedFile,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Warning(GCDevJsonIssueCode.InvalidPlatformDataFields, message, parsedFile.path, 0, fieldName)),
                null,
                platformDataVersion
            );
        }

        private static bool TryReadEntries(
            JToken entriesToken,
            GCPlatformDataParsedFile parsedFile,
            out Dictionary<string, GCPlatformDataEntry> entries,
            out GCDevJsonIssue issue
        )
        {
            entries = new Dictionary<string, GCPlatformDataEntry>();
            issue = null;

            var entriesObject = entriesToken as JObject;
            if (entriesObject == null)
            {
                issue = InvalidPlatformDataFieldsIssue(parsedFile, "gc.platform.json game.entries must be an object.", "game.entries");
                return false;
            }

            foreach (var entryProperty in entriesObject.Properties())
            {
                if (string.IsNullOrWhiteSpace(entryProperty.Name))
                {
                    issue = InvalidPlatformDataFieldsIssue(parsedFile, "gc.platform.json entry keys must be non-empty strings.", "game.entries");
                    return false;
                }

                var entryObject = entryProperty.Value as JObject;
                if (entryObject == null)
                {
                    issue = InvalidPlatformDataFieldsIssue(parsedFile, "Each gc.platform.json game entry must be an object.", "game.entries." + entryProperty.Name, entryProperty.Name);
                    return false;
                }

                string name;
                if (!TryReadNonEmptyString(entryObject["name"], out name))
                {
                    issue = InvalidPlatformDataFieldsIssue(parsedFile, "Each gc.platform.json game entry must include a non-empty name.", "game.entries." + entryProperty.Name + ".name", entryProperty.Name);
                    return false;
                }

                int minPlayers;
                if (!TryReadNonNegativeInteger(entryObject["minPlayers"], out minPlayers))
                {
                    issue = InvalidPlatformDataFieldsIssue(parsedFile, "Each gc.platform.json game entry must include non-negative integer minPlayers.", "game.entries." + entryProperty.Name + ".minPlayers", entryProperty.Name);
                    return false;
                }

                int maxPlayers;
                if (!TryReadNonNegativeInteger(entryObject["maxPlayers"], out maxPlayers) || maxPlayers < minPlayers)
                {
                    issue = InvalidPlatformDataFieldsIssue(parsedFile, "Each gc.platform.json game entry must include maxPlayers greater than or equal to minPlayers.", "game.entries." + entryProperty.Name + ".maxPlayers", entryProperty.Name);
                    return false;
                }

                bool botSupport;
                if (!GCDevJsonTokenReader.TryReadBool(entryObject["botSupport"], out botSupport))
                {
                    issue = InvalidPlatformDataFieldsIssue(parsedFile, "Each gc.platform.json game entry must include boolean botSupport.", "game.entries." + entryProperty.Name + ".botSupport", entryProperty.Name);
                    return false;
                }

                entries[entryProperty.Name] = new GCPlatformDataEntry(entryProperty.Name, name, minPlayers, maxPlayers, botSupport);
            }

            return true;
        }

        private static bool TryReadPlayerColors(
            JToken propertiesToken,
            GCPlatformDataParsedFile parsedFile,
            out Dictionary<string, GCPlatformDataColorVariants> playerColors,
            out GCDevJsonIssue issue
        )
        {
            playerColors = new Dictionary<string, GCPlatformDataColorVariants>();
            issue = null;

            var propertiesObject = propertiesToken as JObject;
            var colorsObject = propertiesObject != null ? propertiesObject["colors"] as JObject : null;
            var playersObject = colorsObject != null ? colorsObject["players"] as JObject : null;
            if (playersObject == null)
            {
                issue = InvalidPlatformDataFieldsIssue(parsedFile, "gc.platform.json properties.colors.players must be an object.", "properties.colors.players");
                return false;
            }

            foreach (var colorProperty in playersObject.Properties())
            {
                if (string.IsNullOrWhiteSpace(colorProperty.Name))
                {
                    issue = InvalidPlatformDataFieldsIssue(parsedFile, "gc.platform.json player color keys must be non-empty strings.", "properties.colors.players");
                    return false;
                }

                var variantsObject = colorProperty.Value as JObject;
                if (variantsObject == null)
                {
                    issue = InvalidPlatformDataFieldsIssue(parsedFile, "Each gc.platform.json player color must include base, muted, and mutedDarker RGB arrays.", "properties.colors.players." + colorProperty.Name);
                    return false;
                }

                GCPlatformDataRgbColor baseColor;
                GCPlatformDataRgbColor mutedColor;
                GCPlatformDataRgbColor mutedDarkerColor;
                if (!TryReadRgbColor(variantsObject["base"], out baseColor) ||
                    !TryReadRgbColor(variantsObject["muted"], out mutedColor) ||
                    !TryReadRgbColor(variantsObject["mutedDarker"], out mutedDarkerColor))
                {
                    issue = InvalidPlatformDataFieldsIssue(parsedFile, "Each gc.platform.json player color must include base, muted, and mutedDarker RGB arrays.", "properties.colors.players." + colorProperty.Name);
                    return false;
                }

                playerColors[colorProperty.Name] = new GCPlatformDataColorVariants(baseColor, mutedColor, mutedDarkerColor);
            }

            return true;
        }

        private static GCDevJsonIssue InvalidPlatformDataFieldsIssue(GCPlatformDataParsedFile parsedFile, string message, string fieldName, string entryKey = null)
        {
            return GCDevJsonIssue.Warning(GCDevJsonIssueCode.InvalidPlatformDataFields, message, parsedFile.path, 0, fieldName, entryKey);
        }

        private static bool TryReadNonEmptyString(JToken token, out string value)
        {
            return GCDevJsonTokenReader.TryReadString(token, out value) &&
                   !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryReadNonNegativeInteger(JToken token, out int value)
        {
            value = 0;
            if (token == null || token.Type != JTokenType.Integer)
            {
                return false;
            }

            var parsedValue = token.Value<long>();
            if (parsedValue < 0 || parsedValue > int.MaxValue)
            {
                return false;
            }

            value = (int)parsedValue;
            return true;
        }

        private static bool TryReadRgbColor(JToken token, out GCPlatformDataRgbColor color)
        {
            color = default(GCPlatformDataRgbColor);
            var array = token as JArray;
            if (array == null || array.Count != 3)
            {
                return false;
            }

            int r;
            int g;
            int b;
            if (!TryReadRgbComponent(array[0], out r) ||
                !TryReadRgbComponent(array[1], out g) ||
                !TryReadRgbComponent(array[2], out b))
            {
                return false;
            }

            color = new GCPlatformDataRgbColor(r, g, b);
            return true;
        }

        private static bool TryReadRgbComponent(JToken token, out int value)
        {
            value = 0;
            if (token == null || token.Type != JTokenType.Integer)
            {
                return false;
            }

            var parsedValue = token.Value<long>();
            if (parsedValue < 0 || parsedValue > 255)
            {
                return false;
            }

            value = (int)parsedValue;
            return true;
        }
    }
}
#endif
