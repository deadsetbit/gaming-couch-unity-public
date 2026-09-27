#if UNITY_EDITOR
using Newtonsoft.Json.Linq;

namespace DSB.GC.Dev
{
    internal sealed class GCDevJsonFile
    {
        internal const string FileName = "gc.dev.json";
        internal const int SupportedDevVersion = 2;
        internal const int SeatCount = 8;
        internal const int PlayerNameMinLength = 1;
        internal const int PlayerNameMaxLength = 8;
        internal const int MinSeed = 1;
        internal const int MaxSeed = 999999;
        internal const string RandomSeed = "random";

        internal readonly int devVersion;
        internal readonly string entryKey;
        internal readonly string seed;
        internal readonly GCDevJsonSeat[] seats;

        internal GCDevJsonFile(string entryKey, string seed, GCDevJsonSeat[] seats)
            : this(SupportedDevVersion, entryKey, seed, seats)
        {
        }

        internal GCDevJsonFile(int devVersion, string entryKey, string seed, GCDevJsonSeat[] seats)
        {
            this.devVersion = devVersion;
            this.entryKey = entryKey;
            this.seed = seed;
            this.seats = CloneSeats(seats);
        }

        internal GCDevJsonFile Clone()
        {
            return new GCDevJsonFile(devVersion, entryKey, seed, seats);
        }

        internal int EnabledSeatCount
        {
            get
            {
                var enabledSeatCount = 0;
                if (seats == null)
                {
                    return enabledSeatCount;
                }

                for (var index = 0; index < seats.Length; index++)
                {
                    if (seats[index] != null && seats[index].enabled)
                    {
                        enabledSeatCount++;
                    }
                }

                return enabledSeatCount;
            }
        }

        private static GCDevJsonSeat[] CloneSeats(GCDevJsonSeat[] sourceSeats)
        {
            if (sourceSeats == null)
            {
                return null;
            }

            var clonedSeats = new GCDevJsonSeat[sourceSeats.Length];
            for (var index = 0; index < sourceSeats.Length; index++)
            {
                if (sourceSeats[index] != null)
                {
                    clonedSeats[index] = sourceSeats[index].Clone();
                }
            }

            return clonedSeats;
        }
    }

    internal sealed class GCDevJsonSeat
    {
        internal readonly string name;
        internal readonly bool enabled;
        internal readonly bool isBot;

        internal GCDevJsonSeat(string name, bool enabled, bool isBot)
        {
            this.name = name;
            this.enabled = enabled;
            this.isBot = isBot;
        }

        internal GCDevJsonSeat Clone()
        {
            return new GCDevJsonSeat(name, enabled, isBot);
        }
    }

    internal enum GCDevJsonParseState
    {
        MissingFile,
        InvalidJson,
        InvalidRoot,
        ParsedObject,
        ReadError,
    }

    internal sealed class GCDevJsonParsedFile
    {
        internal readonly string path;
        internal readonly GCDevJsonParseState state;
        internal readonly JObject jsonObject;
        internal readonly string message;

        private GCDevJsonParsedFile(string path, GCDevJsonParseState state, JObject jsonObject, string message)
        {
            this.path = path;
            this.state = state;
            this.jsonObject = jsonObject;
            this.message = message;
        }

        internal static GCDevJsonParsedFile Missing(string path)
        {
            return new GCDevJsonParsedFile(path, GCDevJsonParseState.MissingFile, null, "gc.dev.json was not found at " + path + ".");
        }

        internal static GCDevJsonParsedFile InvalidJson(string path, string message)
        {
            return new GCDevJsonParsedFile(path, GCDevJsonParseState.InvalidJson, null, message);
        }

        internal static GCDevJsonParsedFile InvalidRoot(string path)
        {
            return new GCDevJsonParsedFile(path, GCDevJsonParseState.InvalidRoot, null, "gc.dev.json must be a JSON object.");
        }

        internal static GCDevJsonParsedFile Parsed(string path, JObject jsonObject)
        {
            return new GCDevJsonParsedFile(path, GCDevJsonParseState.ParsedObject, jsonObject, null);
        }

        internal static GCDevJsonParsedFile ReadError(string path, string message)
        {
            return new GCDevJsonParsedFile(path, GCDevJsonParseState.ReadError, null, message);
        }
    }

    internal sealed class GCDevJsonReadResult
    {
        internal readonly GCDevJsonParsedFile parsedFile;
        internal readonly GCDevJsonValidationResult validation;
        internal readonly GCDevJsonFile data;
        internal readonly GCPlatformDataReadResult platformDataReadResult;

        internal GCDevJsonReadResult(
            GCDevJsonParsedFile parsedFile,
            GCDevJsonValidationResult validation,
            GCDevJsonFile data,
            GCPlatformDataReadResult platformDataReadResult = null
        )
        {
            this.parsedFile = parsedFile;
            this.validation = validation;
            this.data = data;
            this.platformDataReadResult = platformDataReadResult;
        }

        internal bool IsValid
        {
            get { return data != null && validation != null && validation.IsValid; }
        }
    }
}
#endif
