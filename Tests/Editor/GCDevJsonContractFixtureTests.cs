using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DSB.GC;
using DSB.GC.Dev;
using NUnit.Framework;
using UnityEngine;

public sealed class GCDevJsonContractFixtureTests
{
    private const string CorpusRelativePath = "ContractFixtures/LocalPlay";

    [Test]
    public void ValidSparseRosterCapturesDensePlayersAndStableSeatIdentities()
    {
        RunContractFixtureCase("valid-sparse-roster-capture");
    }

    [Test]
    public void ValidFullRosterCapturesEverySeatColorInTheFixedMap()
    {
        RunContractFixtureCase("valid-full-roster-seat-color-map");
    }

    [Test]
    public void MissingPlatformDataKeepsValidDevJsonReadableWithWarningOnlyIssue()
    {
        RunContractFixtureCase("missing-platform-data-warning-only");
    }

    [Test]
    public void PlatformDataMaxPlayerGateFailsValidationAndCapture()
    {
        RunContractFixtureCase("platform-data-max-player-gate-failure");
    }

    [Test]
    public void WrongSeatCountFailsWithExistingIssueCode()
    {
        RunContractFixtureCase("wrong-seat-count-failure");
    }

    [Test]
    public void UnsupportedDevVersionFailsWithExistingIssueCode()
    {
        RunContractFixtureCase("unsupported-dev-version-failure");
    }

    [Test]
    public void PreservingWriteKeepsUnrelatedTopLevelDevJsonFields()
    {
        RunContractFixtureCase("preserving-write-unrelated-top-level-fields");
    }

    // gc.dev.json is owned by the DevApp side of the contract: a missing file is an error to
    // report, never a file to bootstrap.
    [Test]
    public void MissingDevJsonIsReportedAndNeitherCreatedNorRepaired()
    {
        using (var fixture = new ContractFixture())
        {
            var platformData = fixture.PlatformDataStore.Read();
            var readResult = fixture.DevStore.Read(platformData);
            var readIssue = FindIssue(readResult.validation, GCDevJsonIssueCode.MissingFile.ToString());

            Assert.That(readResult.IsValid, Is.False);
            Assert.That(readIssue, Is.Not.Null, "the missing gc.dev.json was not reported");
            Assert.That(readIssue.severity, Is.EqualTo(GCDevJsonIssueSeverity.Error));
            Assert.That(File.Exists(fixture.DevJsonPath), Is.False, "the read created gc.dev.json");

            var writeResult = fixture.DevStore.Write(CreateValidDevJsonFile(), platformData);
            var writeIssue = FindIssue(writeResult.validation, GCDevJsonIssueCode.MissingFile.ToString());

            Assert.That(writeResult.success, Is.False);
            Assert.That(writeIssue, Is.Not.Null, "the write did not report the missing gc.dev.json");
            Assert.That(File.Exists(fixture.DevJsonPath), Is.False, "the write created gc.dev.json");
            Assert.That(
                Directory.GetFiles(Path.GetDirectoryName(fixture.DevJsonPath)),
                Is.Empty,
                "the store left files in the project root"
            );
        }
    }

    [Test]
    public void RandomSeedIsAcceptedAndResolvedIntoTheDocumentedRange()
    {
        using (var fixture = new ContractFixture())
        {
            fixture.CopyCorpusFiles(ResolveCasePath("valid-sparse-roster-capture"));
            File.WriteAllText(
                fixture.DevJsonPath,
                File.ReadAllText(fixture.DevJsonPath, Encoding.UTF8).Replace("\"12345\"", "\"random\""),
                Encoding.UTF8
            );

            var readResult = fixture.DevStore.Read(fixture.PlatformDataStore.Read());
            var capture = new GCDevJsonLocalPlaySessionProvider(fixture.DevStore).Capture(readResult);

            Assert.That(readResult.IsValid, Is.True, "\"random\" was rejected by validation");
            Assert.That(readResult.data.seed, Is.EqualTo(GCDevJsonFile.RandomSeed));
            Assert.That(capture.success, Is.True);
            Assert.That(
                capture.playOptions.seed,
                Is.InRange(GCDevJsonFile.MinSeed, GCDevJsonFile.MaxSeed),
                "\"random\" was not resolved into a usable seed"
            );
        }
    }

    [Test]
    public void PaddedSeatNameIsTrimmedBeforeItReachesDevJson()
    {
        var casePath = ResolveCasePath("valid-sparse-roster-capture");
        using (var fixture = new ContractFixture())
        {
            fixture.CopyCorpusFiles(casePath);
            var platformData = fixture.PlatformDataStore.Read();
            var draft = GCDevJsonDraft.FromFile(fixture.DevStore.Read(platformData).data);

            // Raw length 10, trimmed length 8: accepted by the 1-8 rule, which measures the
            // trimmed value, but over the ceiling the DevApp applies to the raw string. Persisting
            // the padding there costs the whole file, not just the seat.
            draft.seats[0].name = " ABCDEFGH ";
            var file = draft.ToFile();
            var writeResult = fixture.DevStore.Write(file, platformData);
            var writtenText = File.ReadAllText(fixture.DevJsonPath, Encoding.UTF8);

            Assert.That(file.seats[0].name, Is.EqualTo("ABCDEFGH"));
            Assert.That(writeResult.success, Is.True);
            Assert.That(writtenText, Does.Contain("\"name\": \"ABCDEFGH\""));
            Assert.That(writtenText, Does.Not.Contain(" ABCDEFGH "));
            Assert.That(
                fixture.DevStore.Read(platformData).data.seats[0].name,
                Is.EqualTo("ABCDEFGH")
            );
        }
    }

    [Test]
    public void ValidPlatformRuntimeViewExposesSortedEntriesAndColors()
    {
        var view = BuildPlatformRuntimeView(
            @"{
  ""platformDataVersion"": 1,
  ""game"": {
    ""key"": ""contract-game"",
    ""name"": ""Contract Game"",
    ""entries"": {
      ""trio"": { ""name"": ""Trio"", ""minPlayers"": 2, ""maxPlayers"": 3, ""botSupport"": false },
      ""duel"": { ""name"": ""Duel"", ""minPlayers"": 1, ""maxPlayers"": 2, ""botSupport"": true }
    }
  },
  ""platform"": { ""id"": ""unity"" },
  ""properties"": {
    ""colors"": {
      ""players"": {
        ""blue"": { ""base"": [1, 2, 3], ""muted"": [4, 5, 6], ""mutedDarker"": [7, 8, 9] }
      }
    }
  }
}",
            "trio"
        );

        Assert.That(view.schemaVersion, Is.EqualTo(1));
        Assert.That(view.validationState, Is.EqualTo(GCPlatformRuntimeValidationState.Valid));
        Assert.That(view.fallbackActive, Is.False);
        Assert.That(view.source.fileName, Is.EqualTo(GCPlatformDataFile.FileName));
        Assert.That(view.source.platformDataVersion, Is.EqualTo(1));
        Assert.That(view.game.key, Is.EqualTo("contract-game"));
        Assert.That(view.game.name, Is.EqualTo("Contract Game"));
        Assert.That(view.platform.id, Is.EqualTo(GCPlatformDataFile.UnityPlatformId));
        Assert.That(view.selectedEntryKey, Is.EqualTo("trio"));
        Assert.That(view.entries, Has.Length.EqualTo(2));
        Assert.That(view.entries[0].entryKey, Is.EqualTo("duel"));
        Assert.That(view.entries[0].minPlayers, Is.EqualTo(1));
        Assert.That(view.entries[0].maxPlayers, Is.EqualTo(2));
        Assert.That(view.entries[0].botSupport, Is.True);
        Assert.That(view.entries[1].entryKey, Is.EqualTo("trio"));
        Assert.That(view.entries[1].botSupport, Is.False);
        Assert.That(view.playerColors.blue.@base, Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(view.playerColors.blue.muted, Is.EqualTo(new[] { 4, 5, 6 }));
        Assert.That(view.playerColors.blue.mutedDarker, Is.EqualTo(new[] { 7, 8, 9 }));
    }

    [Test]
    public void MissingPlatformRuntimeViewUsesNotdefinedFallback()
    {
        var view = GCPlatformRuntimeViewBuilder.Build(
            GCPlatformDataValidation.BuildReadResult(GCPlatformDataParsedFile.Missing("/tmp/gc.platform.json")),
            "duel"
        );

        AssertFallbackPlatformView(view, GCPlatformRuntimeValidationState.Missing);
        Assert.That(view.source.message, Does.Contain("gc.platform.json was not found"));
    }

    [Test]
    public void InvalidPlatformRuntimeViewUsesNotdefinedFallback()
    {
        var view = GCPlatformRuntimeViewBuilder.Build(
            GCPlatformDataValidation.BuildReadResult(GCPlatformDataParsedFile.InvalidJson("/tmp/gc.platform.json", "bad json")),
            "duel"
        );

        AssertFallbackPlatformView(view, GCPlatformRuntimeValidationState.Invalid);
        Assert.That(view.source.message, Is.EqualTo("bad json"));
    }

    [Test]
    public void PlatformMismatchRuntimeViewUsesInvalidFallback()
    {
        var view = BuildPlatformRuntimeView(
            @"{
  ""platformDataVersion"": 1,
  ""game"": {
    ""key"": ""contract-game"",
    ""name"": ""Contract Game"",
    ""entries"": {
      ""duel"": { ""name"": ""Duel"", ""minPlayers"": 1, ""maxPlayers"": 2, ""botSupport"": true }
    }
  },
  ""platform"": { ""id"": ""web"" },
  ""properties"": { ""colors"": { ""players"": {} } }
}",
            "duel"
        );

        AssertFallbackPlatformView(view, GCPlatformRuntimeValidationState.Invalid);
        Assert.That(view.source.fieldName, Is.EqualTo("platform.id"));
        Assert.That(view.source.platformDataVersion, Is.EqualTo(1));
    }

    [Test]
    public void MissingPlatformColorKeysUsePackageDefaults()
    {
        var view = BuildPlatformRuntimeView(
            @"{
  ""platformDataVersion"": 1,
  ""game"": {
    ""key"": ""contract-game"",
    ""name"": ""Contract Game"",
    ""entries"": {
      ""duel"": { ""name"": ""Duel"", ""minPlayers"": 1, ""maxPlayers"": 2, ""botSupport"": true }
    }
  },
  ""platform"": { ""id"": ""unity"" },
  ""properties"": {
    ""colors"": {
      ""players"": {
        ""blue"": { ""base"": [1, 2, 3], ""muted"": [4, 5, 6], ""mutedDarker"": [7, 8, 9] }
      }
    }
  }
}",
            "duel"
        );

        Assert.That(view.validationState, Is.EqualTo(GCPlatformRuntimeValidationState.Valid));
        Assert.That(view.playerColors.blue.@base, Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(view.playerColors.red.@base, Is.EqualTo(new[] { 243, 63, 94 }));
        Assert.That(view.playerColors.red.muted, Is.EqualTo(new[] { 253, 204, 210 }));
        Assert.That(view.playerColors.red.mutedDarker, Is.EqualTo(new[] { 76, 5, 25 }));
    }

    [Test]
    public void MalformedPlatformColorUsesInvalidFallback()
    {
        var view = BuildPlatformRuntimeView(
            @"{
  ""platformDataVersion"": 1,
  ""game"": {
    ""key"": ""contract-game"",
    ""name"": ""Contract Game"",
    ""entries"": {
      ""duel"": { ""name"": ""Duel"", ""minPlayers"": 1, ""maxPlayers"": 2, ""botSupport"": true }
    }
  },
  ""platform"": { ""id"": ""unity"" },
  ""properties"": {
    ""colors"": {
      ""players"": {
        ""blue"": { ""base"": [1, 2], ""muted"": [4, 5, 6], ""mutedDarker"": [7, 8, 9] }
      }
    }
  }
}",
            "duel"
        );

        AssertFallbackPlatformView(view, GCPlatformRuntimeValidationState.Invalid);
        Assert.That(view.source.fieldName, Is.EqualTo("properties.colors.players.blue"));
        Assert.That(view.source.platformDataVersion, Is.EqualTo(1));
    }

    [Test]
    public void LocalPlayCaptureAttachesPlatformRuntimeView()
    {
        var casePath = ResolveCasePath("valid-sparse-roster-capture");
        using (var fixture = new ContractFixture())
        {
            fixture.CopyCorpusFiles(casePath);
            var readResult = fixture.DevStore.Read(fixture.PlatformDataStore.Read());
            var capture = new GCDevJsonLocalPlaySessionProvider(fixture.DevStore).Capture(readResult);

            Assert.That(capture.success, Is.True);
            Assert.That(capture.playOptions.platformData, Is.Not.Null);
            Assert.That(capture.playOptions.platformData.validationState, Is.EqualTo(GCPlatformRuntimeValidationState.Valid));
            Assert.That(capture.playOptions.platformData.entries[0].entryKey, Is.EqualTo("duel"));
        }
    }

    [Test]
    public void HostedPlayJsonPreservesPlatformRuntimeView()
    {
        var options = GCPlayOptions.CreateFromJSON(
            @"{
  ""seed"": 424242,
  ""players"": [
    { ""playerIndex"": 0, ""type"": ""player"", ""color"": ""blue"" }
  ],
  ""platformData"": {
    ""schemaVersion"": 1,
    ""validationState"": ""valid"",
    ""fallbackActive"": false,
    ""source"": {
      ""fileName"": ""gc.platform.json"",
      ""platformDataVersion"": 1
    },
    ""game"": {
      ""key"": ""contract-game"",
      ""name"": ""Contract Game""
    },
    ""platform"": { ""id"": ""unity"" },
    ""selectedEntryKey"": ""duel"",
    ""entries"": [
      { ""entryKey"": ""duel"", ""name"": ""Duel"", ""minPlayers"": 1, ""maxPlayers"": 2, ""botSupport"": true }
    ],
    ""playerColors"": {
      ""blue"": { ""base"": [1, 2, 3], ""muted"": [4, 5, 6], ""mutedDarker"": [7, 8, 9] }
    }
  }
}"
        );

        Assert.That(options, Is.Not.Null);
        Assert.That(options.platformData, Is.Not.Null);
        Assert.That(options.platformData.validationState, Is.EqualTo(GCPlatformRuntimeValidationState.Valid));
        Assert.That(options.platformData.fallbackActive, Is.False);
        Assert.That(options.platformData.source.platformDataVersion, Is.EqualTo(1));
        Assert.That(options.platformData.game.key, Is.EqualTo("contract-game"));
        Assert.That(options.platformData.selectedEntryKey, Is.EqualTo("duel"));
        Assert.That(options.platformData.entries, Has.Length.EqualTo(1));
        Assert.That(options.platformData.entries[0].maxPlayers, Is.EqualTo(2));
        Assert.That(options.platformData.playerColors.blue.@base, Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(options.platformData.playerColors.red.@base, Is.EqualTo(new[] { 243, 63, 94 }));
        Assert.That(JsonUtility.ToJson(options.platformData), Does.Contain("\"platformDataVersion\":1"));
    }

    [Test]
    public void HostedFallbackPlatformRuntimeViewNormalizesToExactNotdefinedDefaults()
    {
        var options = GCPlayOptions.CreateFromJSON(
            @"{
  ""seed"": 424242,
  ""players"": [
    { ""playerIndex"": 0, ""type"": ""player"", ""color"": ""blue"" }
  ],
  ""platformData"": {
    ""schemaVersion"": 99,
    ""validationState"": ""invalid"",
    ""fallbackActive"": false,
    ""source"": {
      ""fileName"": ""gc.platform.json"",
      ""platformDataVersion"": 1,
      ""message"": ""bad metadata"",
      ""fieldName"": ""game.entries""
    },
    ""game"": {
      ""key"": ""wrong"",
      ""name"": ""Wrong""
    },
    ""platform"": { ""id"": ""web"" },
    ""selectedEntryKey"": ""duel"",
    ""entries"": [],
    ""playerColors"": {
      ""blue"": { ""base"": [999, -1, 3], ""muted"": [4, 5, 6], ""mutedDarker"": [7, 8, 9] }
    }
  }
}"
        );

        Assert.That(options, Is.Not.Null);
        AssertFallbackPlatformView(options.platformData, GCPlatformRuntimeValidationState.Invalid);
        Assert.That(options.platformData.source.message, Is.EqualTo("bad metadata"));
        Assert.That(options.platformData.source.fieldName, Is.EqualTo("game.entries"));
        Assert.That(options.platformData.source.platformDataVersion, Is.EqualTo(1));
    }

    [Test]
    public void RuntimePlatformViewCopyIsIndependentAndNormalizesColors()
    {
        var source = GCPlatformRuntimeView.CreateValid(
            GCPlatformRuntimeSource.Valid(1, "/tmp/gc.platform.json"),
            new GCPlatformRuntimeGame("contract-game", "Contract Game"),
            new GCPlatformRuntimePlatform(GCPlatformDataFile.UnityPlatformId),
            "duel",
            new[]
            {
                new GCPlatformRuntimeEntry("duel", "Duel", 1, 2, true),
            },
            new GCPlatformRuntimePlayerColors
            {
                blue = new GCPlatformRuntimePlayerColor(
                    new[] { 999, -1, 3 },
                    new[] { 4, 5, 6 },
                    new[] { 7, 8, 9 }
                ),
            }
        );

        var copy = GCPlatformRuntimeView.CopyForRuntime(source);
        source.game.key = "mutated";
        source.entries[0].entryKey = "mutated";
        source.playerColors.blue.muted[0] = 99;

        Assert.That(copy.validationState, Is.EqualTo(GCPlatformRuntimeValidationState.Valid));
        Assert.That(copy.fallbackActive, Is.False);
        Assert.That(copy.game.key, Is.EqualTo("contract-game"));
        Assert.That(copy.entries[0].entryKey, Is.EqualTo("duel"));
        Assert.That(copy.playerColors.blue.@base, Is.EqualTo(new[] { 7, 78, 234 }));
        Assert.That(copy.playerColors.blue.muted, Is.EqualTo(new[] { 4, 5, 6 }));
        Assert.That(copy.playerColors.red.@base, Is.EqualTo(new[] { 243, 63, 94 }));
    }

    // The section counters are what make the harness's "asserted nothing" guard bite; an
    // expectation that opts out of every section is exactly the case it exists to reject.
    [Test]
    public void ContractFixtureSectionCountIsZeroWhenNothingIsAsserted()
    {
        var expected = new ExpectedFixture
        {
            id = "asserts-nothing",
            valid = true,
            issues = Array.Empty<ExpectedIssue>(),
            capture = new ExpectedCapture { assert = false },
            write = new ExpectedWrite { assert = false },
        };

        using (var fixture = new ContractFixture())
        {
            fixture.CopyCorpusFiles(ResolveCasePath("valid-sparse-roster-capture"));
            var readResult = fixture.DevStore.Read(fixture.PlatformDataStore.Read());

            Assert.That(
                AssertReadResult(readResult, expected) +
                AssertCapture(fixture, readResult, expected.capture) +
                AssertWrite(fixture, expected.write),
                Is.Zero
            );
        }
    }

    private static void RunContractFixtureCase(string caseName)
    {
        var casePath = ResolveCasePath(caseName);
        var expected = LoadExpected(caseName, casePath);

        using (var fixture = new ContractFixture())
        {
            fixture.CopyCorpusFiles(casePath);

            var platformDataReadResult = fixture.PlatformDataStore.Read();
            var readResult = fixture.DevStore.Read(platformDataReadResult);

            var assertedSections =
                AssertReadResult(readResult, expected) +
                AssertCapture(fixture, readResult, expected.capture) +
                AssertWrite(fixture, expected.write);

            // Capture, write and read-back are all opt-in ("assert": false skips them) and the read
            // section pins nothing beyond the validity flag unless the case expects issues, so a
            // case can be authored that exercises the corpus without asserting anything.
            Assert.That(assertedSections, Is.GreaterThan(0), "Contract fixture case asserted nothing: " + caseName);
        }
    }

    private static GCDevJsonFile CreateValidDevJsonFile()
    {
        var seats = new GCDevJsonSeat[GCDevJsonFile.SeatCount];
        for (var index = 0; index < seats.Length; index++)
        {
            seats[index] = new GCDevJsonSeat("P" + (index + 1), index == 0, false);
        }

        return new GCDevJsonFile("duel", "12345", seats);
    }

    private static GCPlatformRuntimeView BuildPlatformRuntimeView(string json, string selectedEntryKey)
    {
        using (var fixture = new ContractFixture())
        {
            File.WriteAllText(fixture.PlatformDataJsonPath, json, Encoding.UTF8);
            return GCPlatformRuntimeViewBuilder.Build(fixture.PlatformDataStore.Read(), selectedEntryKey);
        }
    }

    private static void AssertFallbackPlatformView(GCPlatformRuntimeView view, string validationState)
    {
        Assert.That(view.schemaVersion, Is.EqualTo(1));
        Assert.That(view.validationState, Is.EqualTo(validationState));
        Assert.That(view.fallbackActive, Is.True);
        Assert.That(view.game.key, Is.EqualTo("notdefined"));
        Assert.That(view.game.name, Is.EqualTo("notdefined"));
        Assert.That(view.platform.id, Is.EqualTo(GCPlatformDataFile.UnityPlatformId));
        Assert.That(view.selectedEntryKey, Is.EqualTo("notdefined"));
        Assert.That(view.entries, Has.Length.EqualTo(1));
        Assert.That(view.entries[0].entryKey, Is.EqualTo("notdefined"));
        Assert.That(view.entries[0].name, Is.EqualTo("notdefined"));
        Assert.That(view.entries[0].minPlayers, Is.EqualTo(1));
        Assert.That(view.entries[0].maxPlayers, Is.EqualTo(8));
        Assert.That(view.entries[0].botSupport, Is.True);
        Assert.That(view.playerColors.blue.@base, Is.EqualTo(new[] { 7, 78, 234 }));
        Assert.That(view.playerColors.brown.mutedDarker, Is.EqualTo(new[] { 67, 20, 7 }));
    }

    private static string ResolveCasePath(string caseName)
    {
        var corpusRoot = ResolveCorpusRoot();
        var casePath = Path.Combine(corpusRoot, caseName);
        Assert.That(Directory.Exists(casePath), Is.True, "Missing contract fixture case: " + casePath);
        return casePath;
    }

    private static string ResolveCorpusRoot()
    {
        var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(GCDevJsonContractFixtureTests).Assembly);
        if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
        {
            var packageCorpusRoot = Path.Combine(packageInfo.resolvedPath, CorpusRelativePath);
            if (Directory.Exists(packageCorpusRoot))
            {
                return packageCorpusRoot;
            }
        }

        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, CorpusRelativePath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not locate contract fixture corpus at " + CorpusRelativePath + ".");
        return null;
    }

    private static ExpectedFixture LoadExpected(string caseName, string casePath)
    {
        var expectedPath = Path.Combine(casePath, "expected.json");
        Assert.That(File.Exists(expectedPath), Is.True, "Missing expected.json for contract fixture case: " + caseName);

        var expected = JsonUtility.FromJson<ExpectedFixture>(File.ReadAllText(expectedPath, Encoding.UTF8));
        Assert.That(expected, Is.Not.Null, "expected.json could not be parsed for contract fixture case: " + caseName);
        Assert.That(expected.id, Is.EqualTo(caseName));
        return expected;
    }

    // Returns 1 only when the case pins issue expectations; the validity flag alone is not a
    // section, or every case would count as asserting something.
    private static int AssertReadResult(GCDevJsonReadResult readResult, ExpectedFixture expected)
    {
        Assert.That(readResult, Is.Not.Null);
        Assert.That(readResult.IsValid, Is.EqualTo(expected.valid));
        Assert.That(readResult.validation, Is.Not.Null);

        var expectedIssues = expected.issues ?? Array.Empty<ExpectedIssue>();
        Assert.That(readResult.validation.ErrorCount, Is.EqualTo(CountIssues(expectedIssues, "Error")));
        Assert.That(readResult.validation.WarningCount, Is.EqualTo(CountIssues(expectedIssues, "Warning")));
        Assert.That(readResult.validation.issues, Has.Length.EqualTo(expectedIssues.Length));

        for (var index = 0; index < expectedIssues.Length; index++)
        {
            var expectedIssue = expectedIssues[index];
            var issue = FindIssue(readResult.validation, expectedIssue.code);
            Assert.That(issue, Is.Not.Null, "Expected issue code was not found: " + expectedIssue.code);
            Assert.That(issue.severity.ToString(), Is.EqualTo(expectedIssue.severity));
        }

        return expectedIssues.Length > 0 ? 1 : 0;
    }

    private static int CountIssues(ExpectedIssue[] issues, string severity)
    {
        var count = 0;
        for (var index = 0; index < issues.Length; index++)
        {
            if (issues[index] != null && issues[index].severity == severity)
            {
                count++;
            }
        }

        return count;
    }

    private static int AssertCapture(ContractFixture fixture, GCDevJsonReadResult readResult, ExpectedCapture expected)
    {
        if (expected == null || !expected.assert)
        {
            return 0;
        }

        var capture = new GCDevJsonLocalPlaySessionProvider(fixture.DevStore).Capture(readResult);
        Assert.That(capture.success, Is.EqualTo(expected.success));

        if (!expected.success)
        {
            Assert.That(capture.validation, Is.Not.Null);
            Assert.That(capture.validation.ErrorCount, Is.EqualTo(readResult.validation.ErrorCount));
            Assert.That(capture.validation.WarningCount, Is.EqualTo(readResult.validation.WarningCount));
            return 1;
        }

        Assert.That(expected.entryKey, Is.Not.Null.And.Not.Empty, "Successful capture fixture must include an entryKey expectation.");
        Assert.That(expected.players, Is.Not.Null, "Successful capture fixture must include player expectations.");
        Assert.That(capture.setupOptions, Is.Not.Null);
        Assert.That(capture.setupOptions.mode, Is.EqualTo(GCMode.Development));
        Assert.That(capture.setupOptions.isServer, Is.True);
        Assert.That(capture.setupOptions.gameModeId, Is.EqualTo(expected.entryKey));

        Assert.That(capture.playOptions, Is.Not.Null);
        Assert.That(capture.playOptions.seed, Is.EqualTo(expected.seed));
        var mapping = GCPlayerIndexMapping.Create(capture.playOptions, capture.seatIdentities);
        AssertPlayers(mapping.CreateGameFacingPlayOptions().players, expected.players);
        AssertSeatIdentities(capture.seatIdentities, expected.seatIdentities);
        return 1;
    }

    private static void AssertPlayers(GCPlayerOptions[] players, ExpectedPlayer[] expectedPlayers)
    {
        expectedPlayers = expectedPlayers ?? Array.Empty<ExpectedPlayer>();
        Assert.That(players, Has.Length.EqualTo(expectedPlayers.Length));

        for (var index = 0; index < expectedPlayers.Length; index++)
        {
            var expectedPlayer = expectedPlayers[index];
            var player = players[index];
            Assert.That(player.playerIndex, Is.EqualTo(expectedPlayer.playerIndex), "players[" + index + "].playerIndex");
            Assert.That(player.type, Is.EqualTo(expectedPlayer.type), "players[" + index + "].type");
            Assert.That(player.color, Is.EqualTo(expectedPlayer.color), "players[" + index + "].color");
        }
    }

    private static void AssertSeatIdentities(GCSeatIdentity[] identities, ExpectedSeatIdentity[] expectedIdentities)
    {
        expectedIdentities = expectedIdentities ?? Array.Empty<ExpectedSeatIdentity>();
        Assert.That(identities, Has.Length.EqualTo(expectedIdentities.Length));

        for (var index = 0; index < expectedIdentities.Length; index++)
        {
            var expectedIdentity = expectedIdentities[index];
            var identity = identities[index];
            Assert.That(identity.sourceSeatIndex, Is.EqualTo(expectedIdentity.sourceSeatIndex));
            Assert.That(identity.stableKey, Is.EqualTo(expectedIdentity.stableKey));
            Assert.That(identity.label, Is.EqualTo(expectedIdentity.label));
            Assert.That(identity.playerType.ToString(), Is.EqualTo(expectedIdentity.playerType));
            Assert.That(identity.playerColor.ToString(), Is.EqualTo(expectedIdentity.playerColor));
        }
    }

    private static int AssertWrite(ContractFixture fixture, ExpectedWrite expected)
    {
        if (expected == null || !expected.assert)
        {
            return 0;
        }

        Assert.That(expected.data, Is.Not.Null, "Write fixture must include canonical gc.dev.json data.");
        var writeResult = fixture.DevStore.Write(ToDevJsonFile(expected.data), fixture.PlatformDataStore.Read());
        Assert.That(writeResult.success, Is.EqualTo(expected.success));
        Assert.That(writeResult.validation, Is.Not.Null);
        Assert.That(writeResult.validation.IsValid, Is.EqualTo(expected.resultValid));

        var writtenText = File.ReadAllText(fixture.DevJsonPath, Encoding.UTF8);
        AssertWrittenText(writtenText, expected.writtenText);
        return 1 + AssertReadBack(fixture, expected.readBack);
    }

    private static GCDevJsonFile ToDevJsonFile(ExpectedDevJson data)
    {
        var expectedSeats = data.seats ?? Array.Empty<ExpectedSeat>();
        var seats = new GCDevJsonSeat[expectedSeats.Length];
        for (var index = 0; index < expectedSeats.Length; index++)
        {
            var expectedSeat = expectedSeats[index];
            seats[index] = new GCDevJsonSeat(expectedSeat.name, expectedSeat.enabled, expectedSeat.isBot);
        }

        return new GCDevJsonFile(data.devVersion, data.entryKey, data.seed, seats);
    }

    private static void AssertWrittenText(string writtenText, ExpectedWrittenText expected)
    {
        if (expected == null)
        {
            return;
        }

        var expectedContains = expected.contains ?? Array.Empty<string>();
        for (var index = 0; index < expectedContains.Length; index++)
        {
            Assert.That(writtenText, Does.Contain(expectedContains[index]));
        }

        if (expected.endsWithNewline)
        {
            Assert.That(writtenText.EndsWith("\n", StringComparison.Ordinal), Is.True);
        }
    }

    private static int AssertReadBack(ContractFixture fixture, ExpectedReadBack expected)
    {
        if (expected == null || !expected.assert)
        {
            return 0;
        }

        var readResult = fixture.DevStore.Read(fixture.PlatformDataStore.Read());
        Assert.That(readResult.IsValid, Is.EqualTo(expected.valid));
        Assert.That(readResult.data, Is.Not.Null);
        Assert.That(readResult.data.devVersion, Is.EqualTo(expected.devVersion));
        Assert.That(readResult.data.entryKey, Is.EqualTo(expected.entryKey));
        Assert.That(readResult.data.seed, Is.EqualTo(expected.seed));
        Assert.That(readResult.data.seats, Has.Length.EqualTo(expected.seatCount));
        Assert.That(GetEnabledSeatIndexes(readResult.data.seats), Is.EqualTo(expected.enabledSeatIndexes));
        return 1;
    }

    private static int[] GetEnabledSeatIndexes(GCDevJsonSeat[] seats)
    {
        var enabledSeatIndexes = new List<int>();
        for (var index = 0; index < seats.Length; index++)
        {
            if (seats[index].enabled)
            {
                enabledSeatIndexes.Add(index + 1);
            }
        }

        return enabledSeatIndexes.ToArray();
    }

    private static GCDevJsonIssue FindIssue(GCDevJsonValidationResult validation, string code)
    {
        if (validation == null || validation.issues == null)
        {
            return null;
        }

        for (var index = 0; index < validation.issues.Length; index++)
        {
            var issue = validation.issues[index];
            if (issue != null && issue.code.ToString() == code)
            {
                return issue;
            }
        }

        return null;
    }

    private sealed class ContractFixture : IDisposable
    {
        private readonly string rootPath;

        internal ContractFixture()
        {
            rootPath = Path.Combine(Path.GetTempPath(), "GCDevJsonContractFixtureTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            var resolver = new FakeLocalProjectRootResolver(rootPath);
            DevStore = new GCDevJsonStore(resolver);
            PlatformDataStore = new GCPlatformDataStore(resolver);
        }

        internal GCDevJsonStore DevStore { get; private set; }

        internal GCPlatformDataStore PlatformDataStore { get; private set; }

        internal string DevJsonPath
        {
            get { return Path.Combine(rootPath, GCDevJsonFile.FileName); }
        }

        internal string PlatformDataJsonPath
        {
            get { return Path.Combine(rootPath, GCPlatformDataFile.FileName); }
        }

        internal void CopyCorpusFiles(string casePath)
        {
            CopyRequiredFile(Path.Combine(casePath, GCDevJsonFile.FileName), DevJsonPath);

            var platformDataSourcePath = Path.Combine(casePath, GCPlatformDataFile.FileName);
            if (File.Exists(platformDataSourcePath))
            {
                File.Copy(platformDataSourcePath, PlatformDataJsonPath, true);
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, true);
            }
        }

        private static void CopyRequiredFile(string sourcePath, string destinationPath)
        {
            Assert.That(File.Exists(sourcePath), Is.True, "Missing contract fixture file: " + sourcePath);
            File.Copy(sourcePath, destinationPath, true);
        }
    }

    private sealed class FakeLocalProjectRootResolver : IGCLocalProjectRootResolver
    {
        private readonly string rootPath;

        internal FakeLocalProjectRootResolver(string rootPath)
        {
            this.rootPath = rootPath;
        }

        public string ResolveProjectRootPath()
        {
            return rootPath;
        }

        public string ResolveProjectName()
        {
            return "Contract Fixture";
        }
    }

    [Serializable]
    private sealed class ExpectedFixture
    {
        public string id;
        public bool valid;
        public ExpectedIssue[] issues;
        public ExpectedCapture capture;
        public ExpectedWrite write;
    }

    [Serializable]
    private sealed class ExpectedIssue
    {
        public string code;
        public string severity;
    }

    [Serializable]
    private sealed class ExpectedCapture
    {
        public bool assert;
        public bool success;
        public string entryKey;
        public int seed;
        public ExpectedPlayer[] players;
        public ExpectedSeatIdentity[] seatIdentities;
    }

    [Serializable]
    private sealed class ExpectedPlayer
    {
        public int playerIndex;
        public string type;
        public string color;
    }

    [Serializable]
    private sealed class ExpectedSeatIdentity
    {
        public int sourceSeatIndex;
        public string stableKey;
        public string label;
        public string playerType;
        public string playerColor;
    }

    [Serializable]
    private sealed class ExpectedWrite
    {
        public bool assert;
        public bool success;
        public bool resultValid;
        public ExpectedDevJson data;
        public ExpectedWrittenText writtenText;
        public ExpectedReadBack readBack;
    }

    [Serializable]
    private sealed class ExpectedDevJson
    {
        public int devVersion;
        public string entryKey;
        public string seed;
        public ExpectedSeat[] seats;
    }

    [Serializable]
    private sealed class ExpectedSeat
    {
        public string name;
        public bool enabled;
        public bool isBot;
    }

    [Serializable]
    private sealed class ExpectedWrittenText
    {
        public string[] contains;
        public bool endsWithNewline;
    }

    [Serializable]
    private sealed class ExpectedReadBack
    {
        public bool assert;
        public bool valid;
        public int devVersion;
        public string entryKey;
        public string seed;
        public int seatCount;
        public int[] enabledSeatIndexes;
    }
}
