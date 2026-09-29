using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DSB.GC;
using NUnit.Framework;
using UnityEngine;

public sealed class GCRuntimeInfoTests
{
    [Test]
    public void RuntimeInfoShapeCanSerializePackageIdentityFields()
    {
        var manifest = ReadPackageManifest();
        var runtimeInfo = GCEditorPackageIdentity.Resolve().ToRuntimeInfo();
        var json = GCRuntimeInfoJson.Serialize(runtimeInfo);
        var expectedJson = "{\"platform\":\"" + GCEditorPackageIdentity.Platform
            + "\",\"packageName\":\"" + manifest.name
            + "\",\"packageVersion\":\"" + manifest.version
            + "\",\"gameProtocolVersion\":" + GCEditorPackageIdentity.GameProtocolVersion
            + "}";

        Assert.That(runtimeInfo.platform, Is.EqualTo(GCEditorPackageIdentity.Platform));
        Assert.That(runtimeInfo.packageName, Is.EqualTo(manifest.name));
        Assert.That(runtimeInfo.packageVersion, Is.EqualTo(manifest.version));
        Assert.That(runtimeInfo.gameProtocolVersion, Is.EqualTo(GCEditorPackageIdentity.GameProtocolVersion));
        Assert.That(json, Is.EqualTo(expectedJson));
    }

    [Test]
    public void RuntimeInfoIsSerializableDtoOnly()
    {
        var fields = typeof(GCRuntimeInfo).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly
        );
        var staticFields = typeof(GCRuntimeInfo).GetFields(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
        );
        var declaredMethods = typeof(GCRuntimeInfo).GetMethods(
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
        );

        Assert.That(Attribute.IsDefined(typeof(GCRuntimeInfo), typeof(SerializableAttribute)), Is.True);
        Assert.That(Array.ConvertAll(fields, field => field.Name), Is.EquivalentTo(new[]
        {
            "platform",
            "packageName",
            "packageVersion",
            "gameProtocolVersion",
        }));
        Assert.That(typeof(GCRuntimeInfo).GetField("platform").FieldType, Is.EqualTo(typeof(string)));
        Assert.That(typeof(GCRuntimeInfo).GetField("packageName").FieldType, Is.EqualTo(typeof(string)));
        Assert.That(typeof(GCRuntimeInfo).GetField("packageVersion").FieldType, Is.EqualTo(typeof(string)));
        Assert.That(typeof(GCRuntimeInfo).GetField("gameProtocolVersion").FieldType, Is.EqualTo(typeof(int)));
        Assert.That(staticFields, Is.Empty);
        Assert.That(declaredMethods, Is.Empty);
    }

    [Test]
    public void WebGLRuntimeAttestationPathUsesBakedCanonicalPayload()
    {
        var packageRootPath = GamingCouchEditorTestSupport.FindPackageRootPath();
        var bridgePath = Path.Combine(packageRootPath, "Plugins", "GamingCouch.jslib");
        var bridge = File.ReadAllText(bridgePath);
        var bootstrapPath = Path.Combine(packageRootPath, "Runtime", "GCWebGLRuntimeInfoBootstrap.cs");
        var bootstrap = File.ReadAllText(bootstrapPath);
        var bakedPayloadPath = Path.Combine(
            packageRootPath,
            "Runtime",
            "Resources",
            GCWebGLRuntimeInfoBootstrap.RuntimeInfoResourceName + ".json"
        );
        var bakedPayload = File.ReadAllText(bakedPayloadPath).TrimEnd('\r', '\n');
        var canonicalPayload = GCRuntimeInfoJson.Serialize(GCEditorPackageIdentity.Resolve().ToRuntimeInfo());
        var runtimePath = Path.Combine(packageRootPath, "Runtime", "GamingCouch.cs");
        var runtime = File.ReadAllText(runtimePath);

        Assert.That(bakedPayload, Is.EqualTo(canonicalPayload));
        Assert.That(GCWebGLRuntimeInfoBootstrap.LoadBakedRuntimeInfoJson(), Is.EqualTo(canonicalPayload));
        Assert.That(bootstrap, Does.Contain("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]"));
        Assert.That(bootstrap, Does.Contain("LoadBakedResourceJson(RuntimeInfoResourceName)"));
        Assert.That(bootstrap, Does.Contain("Resources.Load<TextAsset>(resourceName)"));
        Assert.That(bootstrap, Does.Contain("Debug.LogError(\"Gaming Couch runtime info payload is missing.\");\n                return;"));
        Assert.That(bootstrap, Does.Contain("GamingCouchRegisterRuntimeInfo(runtimeInfoJson);"));
        Assert.That(bootstrap, Does.Not.Contain("GCRuntimeInfoJson.Serialize"));
        Assert.That(runtime, Does.Contain("private static extern void GamingCouchInstanceStarted();"));
        Assert.That(runtime, Does.Not.Contain("GamingCouchRegisterRuntimeInfo"));
        Assert.That(runtime, Does.Not.Contain("SendRuntimeInfo"));
        Assert.That(runtime, Does.Not.Contain("GCRuntimeInfo.ToJson"));
        Assert.That(bridge, Does.Contain("GamingCouchRegisterRuntimeInfo: function (runtimeInfoJsonString)"));
        Assert.That(bridge, Does.Contain("window.gamingCouchRegisterRuntimeInfo"));
        Assert.That(bridge, Does.Contain("JSON.parse(runtimeInfoJson)"));
    }

    [Test]
    public void WebGLUnityBuildInfoCallbackPathUsesOptionalBakedPayload()
    {
        var packageRootPath = GamingCouchEditorTestSupport.FindPackageRootPath();
        var bridgePath = Path.Combine(packageRootPath, "Plugins", "GamingCouch.jslib");
        var bridge = File.ReadAllText(bridgePath);
        var bootstrapPath = Path.Combine(packageRootPath, "Runtime", "GCWebGLRuntimeInfoBootstrap.cs");
        var bootstrap = File.ReadAllText(bootstrapPath);
        var bakedPayloadPath = Path.Combine(
            packageRootPath,
            "Runtime",
            "Resources",
            GCWebGLRuntimeInfoBootstrap.UnityBuildInfoResourceName + ".json"
        );

        Assert.That(File.Exists(bakedPayloadPath), Is.False);
        Assert.That(GCWebGLRuntimeInfoBootstrap.LoadBakedUnityBuildInfoJson(), Is.Null);
        Assert.That(bootstrap, Does.Contain("internal const string UnityBuildInfoResourceName = \"GamingCouchUnityBuildInfo\";"));
        Assert.That(bootstrap, Does.Contain("LoadBakedResourceJson(UnityBuildInfoResourceName)"));
        Assert.That(bootstrap, Does.Contain("Resources.Load<TextAsset>(resourceName)"));
        Assert.That(bootstrap, Does.Contain("if (!string.IsNullOrWhiteSpace(unityBuildInfoJson))"));
        Assert.That(bootstrap, Does.Contain("GamingCouchRegisterUnityBuildInfo(unityBuildInfoJson);"));
        Assert.That(bootstrap, Does.Not.Contain("GCUnityBuildInfoSidecarFactory"));
        Assert.That(bridge, Does.Contain("GamingCouchRegisterUnityBuildInfo: function (unityBuildInfoJsonString)"));
        Assert.That(bridge, Does.Contain("if (!window.gamingCouchRegisterUnityBuildInfo)"));
        Assert.That(bridge, Does.Not.Contain("gamingCouchRegisterUnityBuildInfo is not defined"));
        Assert.That(bridge, Does.Contain("JSON.parse(unityBuildInfoJson)"));
        Assert.That(bridge, Does.Contain("window.gamingCouchRegisterUnityBuildInfo(unityBuildInfo)"));
        Assert.That(bridge, Does.Contain("GamingCouchRegisterUnityBuildInfo callback failed"));
    }

    [Test]
    public void PackageManifestNameAndVersionAreNotHardcodedInPackageSources()
    {
        var packageRootPath = GamingCouchEditorTestSupport.FindPackageRootPath();
        var manifest = ReadPackageManifest();
        var failures = new List<string>();

        foreach (var sourcePath in EnumeratePackageSourcePaths(packageRootPath))
        {
            var source = File.ReadAllText(sourcePath);
            var relativePath = ToPackageRelativePath(packageRootPath, sourcePath);

            if (SourceContainsStringLiteral(source, manifest.name))
            {
                failures.Add(relativePath + " hardcodes package name literal");
            }

            if (SourceContainsStringLiteral(source, manifest.version))
            {
                failures.Add(relativePath + " hardcodes package version literal");
            }
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    [Test]
    public void PackageLiteralScannerDetectsOnlyExactExecutableStringLiterals()
    {
        const string literal = "package.identity.example";

        var positiveSources = new[]
        {
            "var value = \"" + literal + "\";",
            "var value = @\"" + literal + "\";",
            "var value = $\"" + literal + "\";",
            "var value = $@\"" + literal + "\";",
            "var value = @$\"" + literal + "\";",
            "var value = '" + literal + "';",
            "const value = `" + literal + "`;",
            "var value = $\"{ \"" + literal + "\" }\";",
            "const value = `${\"" + literal + "\"}`;",
        };

        foreach (var source in positiveSources)
        {
            Assert.That(SourceContainsStringLiteral(source, literal), Is.True, source);
        }

        var negativeSources = new[]
        {
            "// \"" + literal + "\"",
            "/* `" + literal + "` */",
            "var value = \"prefix " + literal + "\";",
            "var value = \"" + literal + " suffix\";",
            "var value = \"\\\"" + literal + "\\\"\";",
            "var value = `prefix " + literal + " suffix`;",
            "var value = `prefix ${packageName} suffix`;",
        };

        foreach (var source in negativeSources)
        {
            Assert.That(SourceContainsStringLiteral(source, literal), Is.False, source);
        }
    }

    private static PackageManifest ReadPackageManifest()
    {
        var manifestPath = Path.Combine(GamingCouchEditorTestSupport.FindPackageRootPath(), "package.json");
        return JsonUtility.FromJson<PackageManifest>(File.ReadAllText(manifestPath));
    }

    private static IEnumerable<string> EnumeratePackageSourcePaths(string packageRootPath)
    {
        foreach (var sourceDirectoryName in new[] { "Runtime", "Editor", "Plugins" })
        {
            var sourceDirectoryPath = Path.Combine(packageRootPath, sourceDirectoryName);
            if (!Directory.Exists(sourceDirectoryPath))
            {
                continue;
            }

            var sourcePaths = Directory.EnumerateFiles(sourceDirectoryPath, "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(sourceDirectoryPath, "*.jslib", SearchOption.AllDirectories))
                .OrderBy(path => path, StringComparer.Ordinal);

            foreach (var sourcePath in sourcePaths)
            {
                yield return sourcePath;
            }
        }
    }

    private static string ToPackageRelativePath(string packageRootPath, string sourcePath)
    {
        var normalizedPackageRootPath = Path.GetFullPath(packageRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedSourcePath = Path.GetFullPath(sourcePath);

        if (normalizedSourcePath.StartsWith(normalizedPackageRootPath, StringComparison.Ordinal))
        {
            return normalizedSourcePath.Substring(normalizedPackageRootPath.Length);
        }

        return normalizedSourcePath;
    }

    private static bool SourceContainsStringLiteral(string source, string value)
    {
        return SourceContainsStringLiteral(source, value, 0, source.Length);
    }

    // Small lexer for exact package-value literals: skip comments, avoid substring
    // matches inside larger literals, and recurse into interpolation expressions.
    private static bool SourceContainsStringLiteral(string source, string value, int startIndex, int endIndex)
    {
        var index = startIndex;
        while (index < endIndex)
        {
            var current = source[index];
            var next = index + 1 < endIndex ? source[index + 1] : '\0';

            if (current == '/' && next == '/')
            {
                index = SkipLineComment(source, index + 2, endIndex);
                continue;
            }

            if (current == '/' && next == '*')
            {
                index = SkipBlockComment(source, index + 2, endIndex);
                continue;
            }

            if (TryReadStringLiteral(source, index, endIndex, out var literal, out var nextIndex, out var interpolationRanges))
            {
                if (literal == value)
                {
                    return true;
                }

                foreach (var interpolationRange in interpolationRanges)
                {
                    if (SourceContainsStringLiteral(
                            source,
                            value,
                            interpolationRange.StartIndex,
                            interpolationRange.EndIndex
                        ))
                    {
                        return true;
                    }
                }

                index = nextIndex;
                continue;
            }

            index++;
        }

        return false;
    }

    private static bool TryReadStringLiteral(
        string source,
        int startIndex,
        int endIndex,
        out string literal,
        out int nextIndex,
        out List<SourceRange> interpolationRanges
    )
    {
        literal = string.Empty;
        nextIndex = startIndex;
        interpolationRanges = new List<SourceRange>();

        if (!TryGetStringLiteralStart(source, startIndex, endIndex, out var quoteIndex, out var quote, out var isVerbatim, out var isInterpolated))
        {
            return false;
        }

        var builder = new StringBuilder();
        var index = quoteIndex + 1;
        while (index < endIndex)
        {
            var current = source[index];
            var next = index + 1 < endIndex ? source[index + 1] : '\0';

            if (quote == '`')
            {
                if (current == '\\' && index + 1 < endIndex)
                {
                    builder.Append(current);
                    builder.Append(next);
                    index += 2;
                    continue;
                }

                if (current == '$' && next == '{')
                {
                    var interpolationEnd = FindInterpolationEnd(source, index + 2, endIndex);
                    if (interpolationEnd >= 0)
                    {
                        interpolationRanges.Add(new SourceRange(index + 2, interpolationEnd));
                        index = interpolationEnd + 1;
                        continue;
                    }
                }

                if (current == quote)
                {
                    literal = builder.ToString();
                    nextIndex = index + 1;
                    return true;
                }
            }

            if (quote == '"' && isInterpolated)
            {
                if (current == '{' && next == '{')
                {
                    builder.Append(current);
                    builder.Append(next);
                    index += 2;
                    continue;
                }

                if (current == '{')
                {
                    var interpolationEnd = FindInterpolationEnd(source, index + 1, endIndex);
                    if (interpolationEnd >= 0)
                    {
                        interpolationRanges.Add(new SourceRange(index + 1, interpolationEnd));
                        index = interpolationEnd + 1;
                        continue;
                    }
                }

                if (current == '}' && next == '}')
                {
                    builder.Append(current);
                    builder.Append(next);
                    index += 2;
                    continue;
                }
            }

            if (isVerbatim && quote == '"')
            {
                if (current == '"' && next == '"')
                {
                    builder.Append(current);
                    builder.Append(next);
                    index += 2;
                    continue;
                }

                if (current == quote)
                {
                    literal = builder.ToString();
                    nextIndex = index + 1;
                    return true;
                }
            }
            else
            {
                if (current == '\\' && index + 1 < endIndex)
                {
                    builder.Append(current);
                    builder.Append(next);
                    index += 2;
                    continue;
                }

                if (current == quote)
                {
                    literal = builder.ToString();
                    nextIndex = index + 1;
                    return true;
                }
            }

            builder.Append(current);
            index++;
        }

        literal = builder.ToString();
        nextIndex = endIndex;
        return true;
    }

    private static bool TryGetStringLiteralStart(
        string source,
        int startIndex,
        int endIndex,
        out int quoteIndex,
        out char quote,
        out bool isVerbatim,
        out bool isInterpolated
    )
    {
        quoteIndex = startIndex;
        quote = source[startIndex];
        isVerbatim = false;
        isInterpolated = false;

        var current = source[startIndex];
        var next = startIndex + 1 < endIndex ? source[startIndex + 1] : '\0';
        var following = startIndex + 2 < endIndex ? source[startIndex + 2] : '\0';

        if (current == '$' && next == '"')
        {
            quoteIndex = startIndex + 1;
            quote = '"';
            isInterpolated = true;
            return true;
        }

        if (current == '$' && next == '@' && following == '"')
        {
            quoteIndex = startIndex + 2;
            quote = '"';
            isVerbatim = true;
            isInterpolated = true;
            return true;
        }

        if (current == '@' && next == '"')
        {
            quoteIndex = startIndex + 1;
            quote = '"';
            isVerbatim = true;
            return true;
        }

        if (current == '@' && next == '$' && following == '"')
        {
            quoteIndex = startIndex + 2;
            quote = '"';
            isVerbatim = true;
            isInterpolated = true;
            return true;
        }

        if (current == '"' || current == '\'' || current == '`')
        {
            quote = current;
            isInterpolated = current == '`';
            return true;
        }

        return false;
    }

    private static int FindInterpolationEnd(string source, int startIndex, int endIndex)
    {
        var depth = 0;
        var index = startIndex;
        while (index < endIndex)
        {
            var current = source[index];
            var next = index + 1 < endIndex ? source[index + 1] : '\0';

            if (current == '/' && next == '/')
            {
                index = SkipLineComment(source, index + 2, endIndex);
                continue;
            }

            if (current == '/' && next == '*')
            {
                index = SkipBlockComment(source, index + 2, endIndex);
                continue;
            }

            if (TryReadStringLiteral(source, index, endIndex, out _, out var nextIndex, out _))
            {
                index = nextIndex;
                continue;
            }

            if (current == '{')
            {
                depth++;
                index++;
                continue;
            }

            if (current == '}')
            {
                if (depth == 0)
                {
                    return index;
                }

                depth--;
                index++;
                continue;
            }

            index++;
        }

        return -1;
    }

    private static int SkipLineComment(string source, int startIndex, int endIndex)
    {
        var index = startIndex;
        while (index < endIndex && source[index] != '\n')
        {
            index++;
        }

        return index;
    }

    private static int SkipBlockComment(string source, int startIndex, int endIndex)
    {
        var index = startIndex;
        while (index + 1 < endIndex)
        {
            if (source[index] == '*' && source[index + 1] == '/')
            {
                return index + 2;
            }

            index++;
        }

        return endIndex;
    }

    [Serializable]
    private sealed class PackageManifest
    {
        public string name;
        public string version;
    }

    private struct SourceRange
    {
        public SourceRange(int startIndex, int endIndex)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public int StartIndex { get; }
        public int EndIndex { get; }
    }
}
