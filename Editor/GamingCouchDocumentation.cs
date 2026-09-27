using System;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

internal static class GamingCouchDocumentation
{
    internal static void Open()
    {
        var package = PackageInfo.FindForAssembly(typeof(GamingCouchDocumentation).Assembly);
        string url;
        if (!TryResolveUrl(package != null ? package.documentationUrl : null, out url))
        {
            EditorUtility.DisplayDialog(
                "Gaming Couch documentation unavailable",
                "The installed Gaming Couch package has no valid documentation link. " +
                "Repair or update the Unity package from DevApp, then try Documentation again.",
                "OK"
            );
            return;
        }

        Application.OpenURL(url);
    }

    internal static bool TryResolveUrl(string documentationUrl, out string url)
    {
        url = null;
        Uri uri;
        if (string.IsNullOrWhiteSpace(documentationUrl) ||
            !Uri.TryCreate(documentationUrl, UriKind.Absolute, out uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        // Preserve the manifest's version, path and fragment; release tooling owns this URL.
        url = documentationUrl;
        return true;
    }
}
