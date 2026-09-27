using System;
using System.IO;
using DSB.GC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class GCWebGLRuntimeInfoSidecarWriterTests
{
    [Test]
    public void WritesRuntimeInfoSidecarForGamingCouchWebGLTemplate()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();

        try
        {
            var identity = CreatePackageIdentity();
            var result = GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
                BuildTarget.WebGL,
                outputRootPath,
                GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
                identity
            );

            Assert.That(result.WasWritten, Is.True);
            Assert.That(result.status, Is.EqualTo(GCWebGLRuntimeInfoSidecarWriteStatus.Written));
            Assert.That(result.sidecarPath, Is.EqualTo(Path.Combine(outputRootPath, GCWebGLRuntimeInfoSidecarWriter.SidecarFileName)));
            AssertSidecarJsonMatchesIdentity(result.sidecarPath, identity);
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    [Test]
    public void WritesRuntimeInfoSidecarUsingSharedPackageIdentity()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();

        try
        {
            var identity = GCEditorPackageIdentity.Resolve();
            var result = GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
                BuildTarget.WebGL,
                outputRootPath,
                GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
                identity
            );

            Assert.That(result.WasWritten, Is.True);
            AssertSidecarJsonMatchesIdentity(result.sidecarPath, identity);
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    [Test]
    public void RuntimeInfoSidecarStringEqualsCanonicalBuilderOutputForFixtureIdentity()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();

        try
        {
            var identity = CreatePackageIdentity();
            var result = GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
                BuildTarget.WebGL,
                outputRootPath,
                GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
                identity
            );

            Assert.That(File.ReadAllText(result.sidecarPath), Is.EqualTo(GCRuntimeInfoJson.Serialize(identity.ToRuntimeInfo())));
            Assert.That(
                File.ReadAllText(result.sidecarPath),
                Is.EqualTo("{\"platform\":\"unity\",\"packageName\":\"com.test.sidecar\",\"packageVersion\":\"3.2.1-test.0\",\"gameProtocolVersion\":1}")
            );
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    [Test]
    public void WritesRuntimeInfoSidecarNextToIndexWhenBuildOutputPathIsIndexFile()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();

        try
        {
            var identity = CreatePackageIdentity();
            var indexPath = Path.Combine(outputRootPath, "index.html");
            var result = GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
                BuildTarget.WebGL,
                indexPath,
                GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
                identity
            );

            Assert.That(result.WasWritten, Is.True);
            Assert.That(result.sidecarPath, Is.EqualTo(Path.Combine(outputRootPath, GCWebGLRuntimeInfoSidecarWriter.SidecarFileName)));
            AssertSidecarJsonMatchesIdentity(result.sidecarPath, identity);
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    [Test]
    public void SkipsNonWebGLBuildWithoutWritingSidecar()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();

        try
        {
            var result = GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
                BuildTarget.StandaloneOSX,
                outputRootPath,
                GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
                CreatePackageIdentity()
            );
            var sidecarPath = Path.Combine(outputRootPath, GCWebGLRuntimeInfoSidecarWriter.SidecarFileName);

            Assert.That(result.WasWritten, Is.False);
            Assert.That(result.status, Is.EqualTo(GCWebGLRuntimeInfoSidecarWriteStatus.SkippedNonWebGLBuild));
            Assert.That(result.sidecarPath, Is.EqualTo(sidecarPath));
            Assert.That(File.Exists(sidecarPath), Is.False);
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    [Test]
    public void SkipsNonWebGLBuildWithoutValidatingEmptyOutputPath()
    {
        var result = GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
            BuildTarget.StandaloneOSX,
            string.Empty,
            GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
            CreatePackageIdentity()
        );

        Assert.That(result.WasWritten, Is.False);
        Assert.That(result.status, Is.EqualTo(GCWebGLRuntimeInfoSidecarWriteStatus.SkippedNonWebGLBuild));
        Assert.That(result.sidecarPath, Is.Null);
    }

    [Test]
    public void SkipsOtherWebGLTemplatesWithoutWritingSidecar()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();

        try
        {
            var result = GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
                BuildTarget.WebGL,
                outputRootPath,
                "PROJECT:OtherTemplate",
                CreatePackageIdentity()
            );
            var sidecarPath = Path.Combine(outputRootPath, GCWebGLRuntimeInfoSidecarWriter.SidecarFileName);

            Assert.That(result.WasWritten, Is.False);
            Assert.That(result.status, Is.EqualTo(GCWebGLRuntimeInfoSidecarWriteStatus.SkippedTemplate));
            Assert.That(result.sidecarPath, Is.EqualTo(sidecarPath));
            Assert.That(File.Exists(sidecarPath), Is.False);
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    [Test]
    public void SkipsOtherWebGLTemplatesWithoutValidatingEmptyOutputPath()
    {
        var result = GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
            BuildTarget.WebGL,
            string.Empty,
            "PROJECT:OtherTemplate",
            CreatePackageIdentity()
        );

        Assert.That(result.WasWritten, Is.False);
        Assert.That(result.status, Is.EqualTo(GCWebGLRuntimeInfoSidecarWriteStatus.SkippedTemplate));
        Assert.That(result.sidecarPath, Is.Null);
    }

    [Test]
    public void SkipsOtherWebGLTemplatesWithoutValidatingInvalidOutputPath()
    {
        var result = GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
            BuildTarget.WebGL,
            CreateInvalidOutputPath(),
            "PROJECT:OtherTemplate",
            CreatePackageIdentity()
        );

        Assert.That(result.WasWritten, Is.False);
        Assert.That(result.status, Is.EqualTo(GCWebGLRuntimeInfoSidecarWriteStatus.SkippedTemplate));
        Assert.That(result.sidecarPath, Is.Null);
    }

    [Test]
    public void RemovesStaleRuntimeInfoSidecarWhenSkippingOtherWebGLTemplates()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();

        try
        {
            var sidecarPath = Path.Combine(outputRootPath, GCWebGLRuntimeInfoSidecarWriter.SidecarFileName);
            File.WriteAllText(sidecarPath, "{\"stale\":true}");

            var result = GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
                BuildTarget.WebGL,
                outputRootPath,
                "PROJECT:OtherTemplate",
                CreatePackageIdentity()
            );

            Assert.That(result.WasWritten, Is.False);
            Assert.That(result.status, Is.EqualTo(GCWebGLRuntimeInfoSidecarWriteStatus.SkippedTemplate));
            Assert.That(result.sidecarPath, Is.EqualTo(sidecarPath));
            Assert.That(File.Exists(sidecarPath), Is.False);
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    [Test]
    public void OverwritesStaleRuntimeInfoSidecarOnGamingCouchWebGLRebuild()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();

        try
        {
            var sidecarPath = Path.Combine(outputRootPath, GCWebGLRuntimeInfoSidecarWriter.SidecarFileName);
            File.WriteAllText(sidecarPath, "{\"stale\":true}");

            var identity = CreatePackageIdentity();
            var result = GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
                BuildTarget.WebGL,
                outputRootPath,
                GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
                identity
            );

            Assert.That(result.WasWritten, Is.True);
            Assert.That(File.ReadAllText(sidecarPath), Does.Not.Contain("\"stale\""));
            AssertSidecarJsonMatchesIdentity(sidecarPath, identity);
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    [Test]
    public void ThrowsForEmptyOutputPathWhenGamingCouchWebGLTemplateWouldWrite()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
                BuildTarget.WebGL,
                string.Empty,
                GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
                CreatePackageIdentity()
            )
        );

        Assert.That(exception.Message, Does.Contain("output path is empty"));
    }

    [Test]
    public void ThrowsForInvalidOutputPathWhenGamingCouchWebGLTemplateWouldWrite()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
                BuildTarget.WebGL,
                CreateInvalidOutputPath(),
                GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
                CreatePackageIdentity()
            )
        );

        Assert.That(exception.Message, Does.Contain("output path is invalid"));
    }

    private static GCPackageIdentity CreatePackageIdentity()
    {
        return GCEditorPackageIdentity.ResolveFromPackageMetadata("com.test.sidecar", "3.2.1-test.0");
    }

    private static string CreateInvalidOutputPath()
    {
        return "invalid" + '\0' + "path";
    }

    private static string CreateTemporaryBuildOutputRoot()
    {
        var outputRootPath = Path.Combine(
            Path.GetTempPath(),
            "GCWebGLRuntimeInfoSidecarWriterTests_" + Guid.NewGuid().ToString("N")
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

    private static void AssertSidecarJsonMatchesIdentity(string sidecarPath, GCPackageIdentity identity)
    {
        Assert.That(File.Exists(sidecarPath), Is.True);

        var json = File.ReadAllText(sidecarPath);
        var sidecar = JsonUtility.FromJson<RuntimeInfoSidecar>(json);

        Assert.That(sidecar.platform, Is.EqualTo(identity.platform));
        Assert.That(sidecar.packageName, Is.EqualTo(identity.packageName));
        Assert.That(sidecar.packageVersion, Is.EqualTo(identity.packageVersion));
        Assert.That(sidecar.gameProtocolVersion, Is.EqualTo(identity.gameProtocolVersion));
        Assert.That(json, Is.EqualTo(GCRuntimeInfoJson.Serialize(identity.ToRuntimeInfo())));
        Assert.That(json, Does.Contain("\"platform\""));
        Assert.That(json, Does.Contain("\"packageName\""));
        Assert.That(json, Does.Contain("\"packageVersion\""));
        Assert.That(json, Does.Contain("\"gameProtocolVersion\""));
    }

    [Serializable]
    private sealed class RuntimeInfoSidecar
    {
        public string platform;
        public string packageName;
        public string packageVersion;
        public int gameProtocolVersion;
    }
}
