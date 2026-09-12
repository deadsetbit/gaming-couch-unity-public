using DSB.GC;
using DSB.GC.Dev;
using System;
using System.Globalization;

internal sealed class GCDevJsonLocalPlaySessionProvider : IGCLocalPlaySessionProvider
{
    private static readonly GCPlayerColor[] SeatColors =
    {
        GCPlayerColor.blue,
        GCPlayerColor.red,
        GCPlayerColor.green,
        GCPlayerColor.yellow,
        GCPlayerColor.purple,
        GCPlayerColor.pink,
        GCPlayerColor.cyan,
        GCPlayerColor.brown,
    };

    private readonly GCDevJsonStore devStore;

    internal GCDevJsonLocalPlaySessionProvider()
        : this(new GCDevJsonStore())
    {
    }

    internal GCDevJsonLocalPlaySessionProvider(GCDevJsonStore devStore)
    {
        if (devStore == null)
        {
            throw new ArgumentNullException(nameof(devStore));
        }

        this.devStore = devStore;
    }

    public GCLocalPlaySessionCaptureResult Capture()
    {
        return Capture(devStore.Read());
    }

    public GCLocalPlaySessionPreflightResult Validate(GCLocalPlaySessionBoundary context)
    {
        GCDevJsonReadResult readResult;
        try
        {
            readResult = devStore.Read();
        }
        catch (Exception exception)
        {
            var exceptionMessage = GCLocalPlaySession.GetBoundaryDisplayName(context) +
                                   " blocked because gc.dev.json preflight failed: " + exception.Message;
            return GCLocalPlaySessionPreflightResult.Failed(
                exceptionMessage,
                null,
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    exceptionMessage,
                    null,
                    code: GCDevJsonIssueCode.ReadError.ToString()
                ))
            );
        }

        if (readResult != null && readResult.IsValid)
        {
            return GCLocalPlaySessionPreflightResult.Succeeded();
        }

        var message = GCLocalPlaySession.GetBoundaryDisplayName(context) +
                      " blocked because root gc.dev.json is missing, invalid, or rejected by valid platform data gates.";
        return GCLocalPlaySessionPreflightResult.Failed(
            message,
            GetPath(readResult),
            MapValidation(GetValidation(readResult, "gc.dev.json could not be read because the read result was missing."))
        );
    }

    internal GCLocalPlaySessionCaptureResult Capture(GCDevJsonReadResult readResult)
    {
        if (readResult == null)
        {
            return GCLocalPlaySessionCaptureResult.Failed(
                null,
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    "gc.dev.json could not be read because the read result was missing.",
                    null,
                    code: GCDevJsonIssueCode.ReadError.ToString()
                ))
            );
        }

        if (!readResult.IsValid)
        {
            return GCLocalPlaySessionCaptureResult.Failed(GetPath(readResult), MapValidation(readResult.validation));
        }

        var data = readResult.data;
        int seed;
        if (!TryResolveSeed(data.seed, out seed))
        {
            return GCLocalPlaySessionCaptureResult.Failed(
                GetPath(readResult),
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    "gc.dev.json seed must be \"random\" or an integer string from " +
                    GCDevJsonFile.MinSeed + " to " + GCDevJsonFile.MaxSeed + ".",
                    GetPath(readResult),
                    code: GCDevJsonIssueCode.InvalidSeed.ToString()
                ))
            );
        }

        var setupOptions = CreateSetupOptions(data);
        var playOptions = CreatePlayOptions(
            data,
            seed,
            GCPlatformRuntimeViewBuilder.Build(readResult.platformDataReadResult, data.entryKey),
            out var seatIdentities
        );
        return GCLocalPlaySessionCaptureResult.Succeeded(
            setupOptions,
            playOptions,
            seatIdentities,
            MapValidation(readResult.validation),
            GetPath(readResult)
        );
    }

    internal static GCLocalPlaySessionValidationResult MapValidation(GCDevJsonValidationResult validation)
    {
        if (validation == null || validation.issues == null)
        {
            return GCLocalPlaySessionValidationResult.Valid();
        }

        var issues = new GCLocalPlaySessionIssue[validation.issues.Length];
        for (var index = 0; index < validation.issues.Length; index++)
        {
            issues[index] = MapIssue(validation.issues[index]);
        }

        return GCLocalPlaySessionValidationResult.FromIssues(issues);
    }

    private static GCLocalPlaySessionIssue MapIssue(GCDevJsonIssue issue)
    {
        if (issue == null)
        {
            return null;
        }

        if (issue.severity == GCDevJsonIssueSeverity.Warning)
        {
            return GCLocalPlaySessionIssue.Warning(
                issue.message,
                issue.path,
                issue.seatIndex,
                issue.fieldName,
                issue.entryKey,
                issue.code.ToString()
            );
        }

        return GCLocalPlaySessionIssue.Error(
            issue.message,
            issue.path,
            issue.seatIndex,
            issue.fieldName,
            issue.entryKey,
            issue.code.ToString()
        );
    }

    private static GCDevJsonValidationResult GetValidation(GCDevJsonReadResult readResult, string fallbackMessage)
    {
        if (readResult != null && readResult.validation != null)
        {
            return readResult.validation;
        }

        return GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
            GCDevJsonIssueCode.ReadError,
            fallbackMessage,
            GetPath(readResult)
        ));
    }

    private static GCSetupOptions CreateSetupOptions(GCDevJsonFile data)
    {
        return new GCSetupOptions
        {
            isServer = true,
            gameModeId = data.entryKey,
            mode = GCMode.Development,
        };
    }

    private static GCPlayOptions CreatePlayOptions(
        GCDevJsonFile data,
        int seed,
        GCPlatformRuntimeView platformData,
        out GCSeatIdentity[] seatIdentities
    )
    {
        var playerCount = data.EnabledSeatCount;
        var options = new GCPlayOptions
        {
            players = new GCPlayerOptions[playerCount],
            seed = seed,
            platformData = platformData ?? GCPlatformRuntimeView.CreateFallbackMissing(),
        };
        seatIdentities = new GCSeatIdentity[playerCount];

        var playerIndex = 0;
        for (var sourceSeatIndex = 0; sourceSeatIndex < data.seats.Length; sourceSeatIndex++)
        {
            var seat = data.seats[sourceSeatIndex];
            if (!seat.enabled)
            {
                continue;
            }

            var playerType = seat.isBot ? GCPlayerType.bot : GCPlayerType.player;
            var playerColor = SeatColors[sourceSeatIndex];
            var oneBasedSourceSeatIndex = sourceSeatIndex + 1;

            options.players[playerIndex] = new GCPlayerOptions
            {
                playerIndex = playerIndex,
                playerSeed = GCPlayerSeed.FromPlayerName(seat.name),
                type = playerType.ToString(),
                color = playerColor.ToString(),
            };

            seatIdentities[playerIndex] = new GCSeatIdentity
            {
                sourceSeatIndex = oneBasedSourceSeatIndex,
                stableKey = oneBasedSourceSeatIndex.ToString(),
                label = "Seat " + oneBasedSourceSeatIndex,
                playerType = playerType,
                playerColor = playerColor,
            };

            playerIndex++;
        }

        return options;
    }

    private static bool TryResolveSeed(string seed, out int value)
    {
        if (seed == GCDevJsonFile.RandomSeed)
        {
            value = UnityEngine.Random.Range(GCDevJsonFile.MinSeed, GCDevJsonFile.MaxSeed + 1);
            return true;
        }

        return TryParseSeed(seed, out value) &&
               value >= GCDevJsonFile.MinSeed &&
               value <= GCDevJsonFile.MaxSeed;
    }

    internal static bool TryParseSeed(string seed, out int value)
    {
        return int.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static string GetPath(GCDevJsonReadResult readResult)
    {
        return readResult != null && readResult.parsedFile != null ? readResult.parsedFile.path : null;
    }
}
