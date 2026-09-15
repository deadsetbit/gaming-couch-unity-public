#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;

namespace DSB.GC.Dev
{
    internal struct GCRootJsonFileStamp
    {
        internal readonly string path;
        internal readonly bool exists;
        internal readonly long lastWriteTimeUtcTicks;
        internal readonly long length;
        internal readonly string contentHash;
        internal readonly string readError;

        private GCRootJsonFileStamp(
            string path,
            bool exists,
            long lastWriteTimeUtcTicks,
            long length,
            string contentHash,
            string readError
        )
        {
            this.path = path;
            this.exists = exists;
            this.lastWriteTimeUtcTicks = lastWriteTimeUtcTicks;
            this.length = length;
            this.contentHash = contentHash;
            this.readError = readError;
        }

        internal static GCRootJsonFileStamp Read(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return new GCRootJsonFileStamp(path, false, 0, 0, null, "Path is missing.");
            }

            try
            {
                var fileInfo = new FileInfo(path);
                if (!fileInfo.Exists)
                {
                    return new GCRootJsonFileStamp(path, false, 0, 0, null, null);
                }

                var bytes = File.ReadAllBytes(path);
                return new GCRootJsonFileStamp(
                    path,
                    true,
                    fileInfo.LastWriteTimeUtc.Ticks,
                    bytes.LongLength,
                    ComputeHash(bytes),
                    null
                );
            }
            catch (Exception exception)
            {
                return new GCRootJsonFileStamp(
                    path,
                    true,
                    0,
                    -1,
                    null,
                    exception.GetType().Name + ": " + exception.Message
                );
            }
        }

        internal bool IsSameAs(GCRootJsonFileStamp other)
        {
            return string.Equals(path, other.path, StringComparison.Ordinal) &&
                   exists == other.exists &&
                   lastWriteTimeUtcTicks == other.lastWriteTimeUtcTicks &&
                   length == other.length &&
                   string.Equals(contentHash, other.contentHash, StringComparison.Ordinal) &&
                   string.Equals(readError, other.readError, StringComparison.Ordinal);
        }

        internal bool IsExternalChangeFrom(GCRootJsonFileStamp previous)
        {
            // A transient read failure yields an "unknown" stamp (readError != null); treat it as
            // no change so an in-progress dirty draft is not falsely flagged as conflicted. The
            // last-known-good baseline is retained until a healthy stamp is read again. A genuine
            // deletion (exists == false, readError == null) is not a read error and still reports
            // as changed.
            if (readError != null)
            {
                return false;
            }

            return !IsSameAs(previous);
        }

        internal static GCRootJsonFileStamp CreateReadErrorStampForTests(string path, string readError)
        {
            return new GCRootJsonFileStamp(path, true, 0, -1, null, readError);
        }

        private static string ComputeHash(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
            {
                return Convert.ToBase64String(sha256.ComputeHash(bytes));
            }
        }
    }
}
#endif
