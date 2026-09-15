#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DSB.GC.Dev
{
    internal sealed class GCPlatformDataStore
    {
        private readonly IGCLocalProjectRootResolver projectRootResolver;

        internal GCPlatformDataStore()
            : this(new GCUnityLocalProjectRootResolver())
        {
        }

        internal GCPlatformDataStore(IGCLocalProjectRootResolver projectRootResolver)
        {
            if (projectRootResolver == null)
            {
                throw new ArgumentNullException(nameof(projectRootResolver));
            }

            this.projectRootResolver = projectRootResolver;
        }

        internal string ResolveFilePath()
        {
            return Path.Combine(projectRootResolver.ResolveProjectRootPath(), GCPlatformDataFile.FileName);
        }

        internal GCRootJsonFileStamp ReadFileStamp()
        {
            return GCRootJsonFileStamp.Read(ResolveFilePath());
        }

        internal GCPlatformDataReadResult Read()
        {
            return GCPlatformDataValidation.BuildReadResult(ReadParsedFile(ResolveFilePath()));
        }

        private static GCPlatformDataParsedFile ReadParsedFile(string path)
        {
            if (!File.Exists(path))
            {
                return GCPlatformDataParsedFile.Missing(path);
            }

            try
            {
                var token = JToken.Parse(File.ReadAllText(path, Encoding.UTF8));
                var jsonObject = token as JObject;
                if (jsonObject == null)
                {
                    return GCPlatformDataParsedFile.InvalidRoot(path);
                }

                return GCPlatformDataParsedFile.Parsed(path, jsonObject);
            }
            catch (JsonException exception)
            {
                return GCPlatformDataParsedFile.InvalidJson(path, "gc.platform.json is not valid JSON: " + exception.Message);
            }
            catch (Exception exception)
            {
                return GCPlatformDataParsedFile.ReadError(path, "gc.platform.json could not be read: " + exception.Message);
            }
        }
    }
}
#endif
