using System;
using System.IO;

internal enum GCUnityBuildInfoNormalizedPathKind
{
    Empty,
    Relative,
    ProjectRelative,
    BuildOutputRelative,
    UserHomeRelative,
    UnknownAbsolute,
}

internal sealed class GCUnityBuildInfoNormalizedPath
{
    internal readonly GCUnityBuildInfoNormalizedPathKind kind;
    internal readonly string value;
    internal readonly string redactionReason;

    internal GCUnityBuildInfoNormalizedPath(
        GCUnityBuildInfoNormalizedPathKind kind,
        string value,
        string redactionReason
    )
    {
        this.kind = kind;
        this.value = value;
        this.redactionReason = redactionReason;
    }

    internal bool ShouldEmitValue
    {
        get { return !string.IsNullOrEmpty(value); }
    }

    internal bool WasRedacted
    {
        get { return kind == GCUnityBuildInfoNormalizedPathKind.UnknownAbsolute; }
    }
}

internal sealed class GCUnityBuildInfoPathNormalizationContext
{
    internal readonly string projectRootPath;
    internal readonly string buildOutputRootPath;
    internal readonly string userHomePath;

    internal GCUnityBuildInfoPathNormalizationContext(
        string projectRootPath,
        string buildOutputRootPath,
        string userHomePath
    )
    {
        this.projectRootPath = projectRootPath;
        this.buildOutputRootPath = buildOutputRootPath;
        this.userHomePath = userHomePath;
    }
}

internal static class GCUnityBuildInfoPathNormalizer
{
    internal const string UserHomeToken = "${USER_HOME}";
    internal const string UnknownAbsolutePathRedactionReason = "unknown-absolute-path";

    internal static GCUnityBuildInfoNormalizedPath Normalize(
        string path,
        GCUnityBuildInfoPathNormalizationContext context
    )
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new GCUnityBuildInfoNormalizedPath(
                GCUnityBuildInfoNormalizedPathKind.Empty,
                null,
                null
            );
        }

        var normalizedPath = NormalizeSeparators(path.Trim());

        // Unity normally hands us absolute build output paths, but a relative outputPath would
        // fail IsAbsolutePath, match none of the (already-absolute) comparison roots, and fall
        // through to the Relative branch emitting its raw value with outputPathRedacted=false —
        // weakening the redaction guarantee. Canonicalize relative inputs to an absolute path up
        // front so classification (and redaction) can see them. Path.GetFullPath resolves a
        // relative input against the current working directory, which during a build is the Unity
        // project root — the intended anchor for a relative build path. We only canonicalize
        // inputs our cross-platform check does not already treat as absolute so Windows-style
        // paths evaluated on a non-Windows host keep their manual separator/drive handling and
        // stay deterministic. Path.GetFullPath throws on malformed input (mirrors
        // GCWebGLBuildSidecarOutputPaths.ResolveOutputRootPath); on failure we fall back to the
        // existing Relative/UnknownAbsolute handling below.
        if (!IsAbsolutePath(normalizedPath))
        {
            try
            {
                normalizedPath = NormalizeSeparators(Path.GetFullPath(normalizedPath));
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (PathTooLongException)
            {
            }
        }

        if (TryNormalizeUnderRoot(
                normalizedPath,
                context != null ? context.buildOutputRootPath : null,
                GCUnityBuildInfoNormalizedPathKind.BuildOutputRelative,
                out var buildOutputPath
            ))
        {
            return buildOutputPath;
        }

        if (TryNormalizeUnderRoot(
                normalizedPath,
                context != null ? context.projectRootPath : null,
                GCUnityBuildInfoNormalizedPathKind.ProjectRelative,
                out var projectPath
            ))
        {
            return projectPath;
        }

        if (TryNormalizeUnderRoot(
                normalizedPath,
                context != null ? context.userHomePath : null,
                GCUnityBuildInfoNormalizedPathKind.UserHomeRelative,
                out var userHomePath
            ))
        {
            return userHomePath;
        }

        if (IsAbsolutePath(normalizedPath))
        {
            return new GCUnityBuildInfoNormalizedPath(
                GCUnityBuildInfoNormalizedPathKind.UnknownAbsolute,
                null,
                UnknownAbsolutePathRedactionReason
            );
        }

        return new GCUnityBuildInfoNormalizedPath(
            GCUnityBuildInfoNormalizedPathKind.Relative,
            normalizedPath,
            null
        );
    }

    private static bool TryNormalizeUnderRoot(
        string normalizedPath,
        string rootPath,
        GCUnityBuildInfoNormalizedPathKind kind,
        out GCUnityBuildInfoNormalizedPath normalized
    )
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return false;
        }

        var normalizedRoot = TrimRootSuffix(NormalizeSeparators(rootPath.Trim()));
        if (!IsAbsolutePath(normalizedPath) || !IsAbsolutePath(normalizedRoot))
        {
            return false;
        }

        var comparison = UsesWindowsPathSemantics(normalizedPath) || UsesWindowsPathSemantics(normalizedRoot)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(normalizedPath, normalizedRoot, comparison))
        {
            normalized = new GCUnityBuildInfoNormalizedPath(
                kind,
                kind == GCUnityBuildInfoNormalizedPathKind.UserHomeRelative ? UserHomeToken : ".",
                null
            );
            return true;
        }

        var rootPrefix = normalizedRoot.EndsWith("/", StringComparison.Ordinal)
            ? normalizedRoot
            : normalizedRoot + "/";

        if (!normalizedPath.StartsWith(rootPrefix, comparison))
        {
            return false;
        }

        var relativePath = normalizedPath.Substring(rootPrefix.Length);
        if (kind == GCUnityBuildInfoNormalizedPathKind.UserHomeRelative)
        {
            relativePath = UserHomeToken + "/" + relativePath;
        }

        normalized = new GCUnityBuildInfoNormalizedPath(kind, relativePath, null);
        return true;
    }

    private static string NormalizeSeparators(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
    }

    private static string TrimRootSuffix(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        var minimumLength = GetMinimumRootLength(path);
        while (path.Length > minimumLength && path.EndsWith("/", StringComparison.Ordinal))
        {
            path = path.Substring(0, path.Length - 1);
        }

        return path;
    }

    private static int GetMinimumRootLength(string path)
    {
        if (path.Length >= 3 && IsWindowsDriveAbsolutePath(path))
        {
            return 3;
        }

        if (path.StartsWith("//", StringComparison.Ordinal))
        {
            return 2;
        }

        return path.StartsWith("/", StringComparison.Ordinal) ? 1 : 0;
    }

    private static bool IsAbsolutePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.StartsWith("/", StringComparison.Ordinal) ||
               path.StartsWith("//", StringComparison.Ordinal) ||
               IsWindowsDriveAbsolutePath(path);
    }

    // A UNC path (\\server\share) is Windows-shaped without carrying a drive letter, so it needs
    // the same case-insensitive comparison; otherwise a case-mismatched share root misses every
    // comparison root and the path is redacted as unknown-absolute.
    private static bool UsesWindowsPathSemantics(string path)
    {
        return IsWindowsDriveAbsolutePath(path) ||
               path.StartsWith("//", StringComparison.Ordinal);
    }

    private static bool IsWindowsDriveAbsolutePath(string path)
    {
        return path.Length >= 3 &&
               char.IsLetter(path[0]) &&
               path[1] == ':' &&
               path[2] == '/';
    }
}
