using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

public sealed class GamingCouchDocumentationTests
{
    [Serializable]
    private sealed class PackageManifest
    {
        public string documentationUrl = null;
    }

    [TestCase("https://example.com/0.1.0-alpha.12/manual/#getting-started")]
    [TestCase("https://docs.example.com/0.2.0/")]
    [TestCase("http://localhost:8080/manual/")]
    public void UsesManifestUrlWithoutChangingVersionOrDestination(string documentationUrl)
    {
        string url;
        Assert.That(GamingCouchDocumentation.TryResolveUrl(documentationUrl, out url), Is.True);
        Assert.That(url, Is.EqualTo(documentationUrl));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("manual/index.html")]
    [TestCase("https://")]
    [TestCase("not a URL")]
    [TestCase("file:///tmp/manual.html")]
    [TestCase("javascript:alert('docs')")]
    public void RejectsMissingOrInvalidBrowserDestinations(string documentationUrl)
    {
        string url;
        Assert.That(GamingCouchDocumentation.TryResolveUrl(documentationUrl, out url), Is.False);
        Assert.That(url, Is.Null);
    }

    [Test]
    public void InstalledPackageMetadataResolvesTheShippedManifestDocumentationUrl()
    {
        var package = PackageInfo.FindForAssembly(typeof(GamingCouchDocumentation).Assembly);
        Assert.That(package, Is.Not.Null);
        Assert.That(package.name, Is.EqualTo("com.dsb.gamingcouch"));

        var manifest = JsonUtility.FromJson<PackageManifest>(
            File.ReadAllText(Path.Combine(package.resolvedPath, "package.json"))
        );
        string url;
        Assert.That(GamingCouchDocumentation.TryResolveUrl(package.documentationUrl, out url), Is.True);
        Assert.That(url, Is.EqualTo(manifest.documentationUrl));
    }
}
