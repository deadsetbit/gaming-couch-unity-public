using System;
using System.IO;
using DSB.GC;
using NUnit.Framework;
using UnityEngine;

public sealed class GCEditorPackageIdentityTests
{
    [Test]
    public void PlayerTransitionRefactorKeepsGameProtocolVersionUnchanged()
    {
        const int compatiblePlayerTransitionProtocolVersion = 1;

        Assert.That(
            GCEditorPackageIdentity.GameProtocolVersion,
            Is.EqualTo(compatiblePlayerTransitionProtocolVersion)
        );
    }

    [Test]
    public void ResolveUsesCurrentPackageManifestValuesAndRuntimeFields()
    {
        var manifest = ReadPackageManifest(GamingCouchEditorTestSupport.FindPackageRootPath());
        var identity = GCEditorPackageIdentity.Resolve();
        var runtimeInfo = identity.ToRuntimeInfo();
        var json = JsonUtility.ToJson(identity);

        Assert.That(identity.platform, Is.EqualTo(GCEditorPackageIdentity.Platform));
        Assert.That(identity.packageName, Is.EqualTo(manifest.name));
        Assert.That(identity.packageVersion, Is.EqualTo(manifest.version));
        Assert.That(identity.gameProtocolVersion, Is.EqualTo(GCEditorPackageIdentity.GameProtocolVersion));
        Assert.That(runtimeInfo.platform, Is.EqualTo(identity.platform));
        Assert.That(runtimeInfo.packageName, Is.EqualTo(identity.packageName));
        Assert.That(runtimeInfo.packageVersion, Is.EqualTo(identity.packageVersion));
        Assert.That(runtimeInfo.gameProtocolVersion, Is.EqualTo(identity.gameProtocolVersion));
        Assert.That(json, Does.Contain("\"platform\":\"" + GCEditorPackageIdentity.Platform + "\""));
        Assert.That(json, Does.Contain("\"packageName\":\"" + manifest.name + "\""));
        Assert.That(json, Does.Contain("\"packageVersion\":\"" + manifest.version + "\""));
        Assert.That(json, Does.Contain("\"gameProtocolVersion\":" + GCEditorPackageIdentity.GameProtocolVersion));
    }

    [Test]
    public void ResolveFromPackageRootReadsPackageManifestAsVersionSource()
    {
        var packageRootPath = CreateTemporaryPackageRoot("{\"name\":\"com.test.identity\",\"version\":\"9.8.7-test.0\"}");

        try
        {
            var identity = GCEditorPackageIdentity.ResolveFromPackageRoot(packageRootPath);

            Assert.That(identity.packageName, Is.EqualTo("com.test.identity"));
            Assert.That(identity.packageVersion, Is.EqualTo("9.8.7-test.0"));
            Assert.That(identity.platform, Is.EqualTo(GCEditorPackageIdentity.Platform));
            Assert.That(identity.gameProtocolVersion, Is.EqualTo(GCEditorPackageIdentity.GameProtocolVersion));
        }
        finally
        {
            DeleteTemporaryPath(packageRootPath);
        }
    }

    [Test]
    public void ResolveFromPackageMetadataUsesUnityPackageMetadataFields()
    {
        var identity = GCEditorPackageIdentity.ResolveFromPackageMetadata("com.test.metadata", "2.0.0-test.1");

        Assert.That(identity.packageName, Is.EqualTo("com.test.metadata"));
        Assert.That(identity.packageVersion, Is.EqualTo("2.0.0-test.1"));
        Assert.That(identity.platform, Is.EqualTo(GCEditorPackageIdentity.Platform));
        Assert.That(identity.gameProtocolVersion, Is.EqualTo(GCEditorPackageIdentity.GameProtocolVersion));
    }

    [Test]
    public void ResolveFromPackageRootFailsWhenPackageManifestIsMissing()
    {
        var packageRootPath = CreateTemporaryPackageRoot(null);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => GCEditorPackageIdentity.ResolveFromPackageRoot(packageRootPath)
            );

            Assert.That(exception.Message, Does.Contain("package manifest was not found"));
            Assert.That(exception.Message, Does.Contain("package.json"));
        }
        finally
        {
            DeleteTemporaryPath(packageRootPath);
        }
    }

    [Test]
    public void ResolveFromPackageRootFailsWhenPackageManifestJsonIsInvalid()
    {
        var packageRootPath = CreateTemporaryPackageRoot("{ invalid json");

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => GCEditorPackageIdentity.ResolveFromPackageRoot(packageRootPath)
            );

            Assert.That(exception.Message, Does.Contain("package manifest is not valid JSON"));
        }
        finally
        {
            DeleteTemporaryPath(packageRootPath);
        }
    }

    [Test]
    public void ResolveFromPackageRootFailsWhenPackageManifestNameIsMissing()
    {
        var packageRootPath = CreateTemporaryPackageRoot("{\"version\":\"9.8.7-test.0\"}");

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => GCEditorPackageIdentity.ResolveFromPackageRoot(packageRootPath)
            );

            Assert.That(exception.Message, Does.Contain("missing name"));
        }
        finally
        {
            DeleteTemporaryPath(packageRootPath);
        }
    }

    [Test]
    public void ResolveFromPackageRootFailsWhenPackageManifestVersionIsMissing()
    {
        var packageRootPath = CreateTemporaryPackageRoot("{\"name\":\"com.test.identity\"}");

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => GCEditorPackageIdentity.ResolveFromPackageRoot(packageRootPath)
            );

            Assert.That(exception.Message, Does.Contain("missing version"));
        }
        finally
        {
            DeleteTemporaryPath(packageRootPath);
        }
    }

    private static PackageManifest ReadPackageManifest(string packageRootPath)
    {
        var manifestPath = Path.Combine(packageRootPath, GCEditorPackageIdentity.PackageManifestFileName);
        return JsonUtility.FromJson<PackageManifest>(File.ReadAllText(manifestPath));
    }

    private static string CreateTemporaryPackageRoot(string packageManifestJson)
    {
        var packageRootPath = Path.Combine(
            Path.GetTempPath(),
            "GCEditorPackageIdentityTests_" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(packageRootPath);

        if (packageManifestJson != null)
        {
            File.WriteAllText(
                Path.Combine(packageRootPath, GCEditorPackageIdentity.PackageManifestFileName),
                packageManifestJson
            );
        }

        return packageRootPath;
    }

    private static void DeleteTemporaryPath(string path)
    {
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    [Serializable]
    private sealed class PackageManifest
    {
        public string name;
        public string version;
    }
}
