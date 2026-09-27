using System;
using System.IO;
using System.Text.RegularExpressions;
using DSB.GC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GCWebGLBuildSidecarPostprocessWriterTests
{
    [Test]
    public void SwallowsIdentityResolutionFailureAfterBuildCompletionAndLogsError()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();

        try
        {
            Func<GCPackageIdentity> throwingResolver = () =>
                throw new InvalidOperationException(
                    "Could not resolve Gaming Couch package identity from Unity package metadata."
                );

            LogAssert.Expect(
                LogType.Error,
                new Regex("Gaming Couch WebGL build sidecar emission failed")
            );

            Assert.DoesNotThrow(
                () =>
                    GCWebGLBuildSidecarPostprocessWriter.WriteForBuild(
                        BuildTarget.WebGL,
                        outputRootPath,
                        GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
                        throwingResolver,
                        null,
                        null,
                        null,
                        null
                    )
            );

            Assert.That(
                File.Exists(Path.Combine(outputRootPath, GCWebGLRuntimeInfoSidecarWriter.SidecarFileName)),
                Is.False
            );
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    private static string CreateTemporaryBuildOutputRoot()
    {
        var outputRootPath = Path.Combine(
            Path.GetTempPath(),
            "GCWebGLBuildSidecarPostprocessWriterTests_" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(outputRootPath);
        File.WriteAllText(Path.Combine(outputRootPath, "index.html"), "<!doctype html>");
        return outputRootPath;
    }

    private static void DeleteTemporaryPath(string path)
    {
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}
