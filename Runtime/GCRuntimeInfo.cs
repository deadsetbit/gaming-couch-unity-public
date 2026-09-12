#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Text;
using DSB.GC.RuntimeMessages;

namespace DSB.GC
{
    [Serializable]
    internal sealed class GCRuntimeInfo
    {
        public string platform;
        public string packageName;
        public string packageVersion;
        public int gameProtocolVersion;
    }

    internal static class GCRuntimeInfoJson
    {
        internal static string Serialize(GCRuntimeInfo runtimeInfo)
        {
            if (runtimeInfo == null)
            {
                throw new ArgumentNullException(nameof(runtimeInfo));
            }

            var builder = new StringBuilder();
            builder.Append('{');
            AppendJsonProperty(builder, "platform", runtimeInfo.platform);
            builder.Append(',');
            AppendJsonProperty(builder, "packageName", runtimeInfo.packageName);
            builder.Append(',');
            AppendJsonProperty(builder, "packageVersion", runtimeInfo.packageVersion);
            builder.Append(',');
            AppendJsonProperty(builder, "gameProtocolVersion", runtimeInfo.gameProtocolVersion);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendJsonProperty(StringBuilder builder, string propertyName, string value)
        {
            GCRuntimeJson.AppendString(builder, propertyName);
            builder.Append(':');
            GCRuntimeJson.AppendString(builder, value);
        }

        private static void AppendJsonProperty(StringBuilder builder, string propertyName, int value)
        {
            GCRuntimeJson.AppendString(builder, propertyName);
            builder.Append(':');
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
#endif
