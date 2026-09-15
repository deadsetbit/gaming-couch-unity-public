#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DSB.GC.Dev
{
    internal sealed class GCDevJsonStore
    {
        private readonly IGCLocalProjectRootResolver projectRootResolver;

        internal GCDevJsonStore()
            : this(new GCUnityLocalProjectRootResolver())
        {
        }

        internal GCDevJsonStore(IGCLocalProjectRootResolver projectRootResolver)
        {
            if (projectRootResolver == null)
            {
                throw new ArgumentNullException(nameof(projectRootResolver));
            }

            this.projectRootResolver = projectRootResolver;
        }

        internal string ResolveFilePath()
        {
            return Path.Combine(projectRootResolver.ResolveProjectRootPath(), GCDevJsonFile.FileName);
        }

        internal GCRootJsonFileStamp ReadFileStamp()
        {
            return GCRootJsonFileStamp.Read(ResolveFilePath());
        }

        internal GCDevJsonReadResult Read()
        {
            return Read(ReadPlatformData());
        }

        internal GCDevJsonReadResult Read(GCPlatformDataReadResult platformDataReadResult)
        {
            var path = ResolveFilePath();
            return GCDevJsonValidation.BuildReadResult(ReadParsedFile(path), platformDataReadResult);
        }

        internal GCDevJsonWriteResult Write(GCDevJsonFile data)
        {
            return Write(data, ReadPlatformData());
        }

        internal GCDevJsonWriteResult Write(GCDevJsonFile data, GCPlatformDataReadResult platformDataReadResult)
        {
            var path = ResolveFilePath();
            if (data == null)
            {
                return GCDevJsonWriteResult.Failed(
                    path,
                    GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.WriteError, "gc.dev.json write data is missing.", path))
                );
            }

            var dataValidation = GCDevJsonValidation.ValidateData(data, path, platformDataReadResult);
            if (!dataValidation.IsValid)
            {
                return GCDevJsonWriteResult.Failed(path, dataValidation);
            }

            if (!File.Exists(path))
            {
                return GCDevJsonWriteResult.Failed(
                    path,
                    GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.MissingFile, "gc.dev.json was not found at " + path + ".", path))
                );
            }

            JObject jsonObject;
            try
            {
                var token = JToken.Parse(File.ReadAllText(path, Encoding.UTF8));
                jsonObject = token as JObject;
                if (jsonObject == null)
                {
                    return GCDevJsonWriteResult.Failed(
                        path,
                        GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.InvalidRoot, "gc.dev.json must be a JSON object.", path))
                    );
                }
            }
            catch (JsonException exception)
            {
                return GCDevJsonWriteResult.Failed(
                    path,
                    GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.InvalidJson, "gc.dev.json is not valid JSON: " + exception.Message, path))
                );
            }
            catch (Exception exception)
            {
                return GCDevJsonWriteResult.Failed(
                    path,
                    GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.WriteError, "gc.dev.json could not be read before write: " + exception.Message, path))
                );
            }

            jsonObject["devVersion"] = data.devVersion;
            jsonObject["entryKey"] = data.entryKey;
            jsonObject["seed"] = data.seed;
            jsonObject["seats"] = BuildSeatsArray(data.seats);

            try
            {
                GCEditorAtomicFileWriter.WriteAllText(path, jsonObject.ToString(Formatting.Indented) + "\n");
            }
            catch (Exception exception)
            {
                return GCDevJsonWriteResult.Failed(
                    path,
                    GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.WriteError, "gc.dev.json could not be written: " + exception.Message, path))
                );
            }

            return GCDevJsonWriteResult.Succeeded(path, dataValidation);
        }

        private GCPlatformDataReadResult ReadPlatformData()
        {
            return new GCPlatformDataStore(projectRootResolver).Read();
        }

        private static GCDevJsonParsedFile ReadParsedFile(string path)
        {
            if (!File.Exists(path))
            {
                return GCDevJsonParsedFile.Missing(path);
            }

            try
            {
                var token = JToken.Parse(File.ReadAllText(path, Encoding.UTF8));
                var jsonObject = token as JObject;
                if (jsonObject == null)
                {
                    return GCDevJsonParsedFile.InvalidRoot(path);
                }

                return GCDevJsonParsedFile.Parsed(path, jsonObject);
            }
            catch (JsonException exception)
            {
                return GCDevJsonParsedFile.InvalidJson(path, "gc.dev.json is not valid JSON: " + exception.Message);
            }
            catch (Exception exception)
            {
                return GCDevJsonParsedFile.ReadError(path, "gc.dev.json could not be read: " + exception.Message);
            }
        }

        private static JArray BuildSeatsArray(GCDevJsonSeat[] seats)
        {
            var seatsArray = new JArray();
            for (var index = 0; index < seats.Length; index++)
            {
                seatsArray.Add(new JObject
                {
                    ["name"] = seats[index].name,
                    ["enabled"] = seats[index].enabled,
                    ["isBot"] = seats[index].isBot,
                });
            }

            return seatsArray;
        }
    }

    internal sealed class GCDevJsonWriteResult
    {
        internal readonly string path;
        internal readonly bool success;
        internal readonly GCDevJsonValidationResult validation;

        private GCDevJsonWriteResult(string path, bool success, GCDevJsonValidationResult validation)
        {
            this.path = path;
            this.success = success;
            this.validation = validation;
        }

        internal static GCDevJsonWriteResult Succeeded(string path)
        {
            return new GCDevJsonWriteResult(path, true, GCDevJsonValidationResult.Valid());
        }

        internal static GCDevJsonWriteResult Succeeded(string path, GCDevJsonValidationResult validation)
        {
            return new GCDevJsonWriteResult(path, true, validation ?? GCDevJsonValidationResult.Valid());
        }

        internal static GCDevJsonWriteResult Failed(string path, GCDevJsonValidationResult validation)
        {
            return new GCDevJsonWriteResult(path, false, validation);
        }
    }
}
#endif
