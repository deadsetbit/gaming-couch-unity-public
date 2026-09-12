using System;
using System.IO;
using System.Text;

// Files written here (gc.dev.json, the build sidecars) are read concurrently by external tools and
// by the editor's own stamp poller. Writing through a sibling temp file and renaming it into place
// means a reader either sees the previous file or the complete new one, never a half-written one.
internal static class GCEditorAtomicFileWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

    internal static void WriteAllText(string path, string contents)
    {
        var temporaryPath = CreateTemporaryPath(path);
        try
        {
            File.WriteAllText(temporaryPath, contents, Utf8WithoutBom);

            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string CreateTemporaryPath(string path)
    {
        var directoryPath = Path.GetDirectoryName(path);
        var temporaryFileName = Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
        return string.IsNullOrEmpty(directoryPath)
            ? temporaryFileName
            : Path.Combine(directoryPath, temporaryFileName);
    }
}
