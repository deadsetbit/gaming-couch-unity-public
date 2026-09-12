using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DSB.GC.Log
{
    public enum LogLevel { None, Error, Warning, Info, Debug }

    public class GCLog
    {
        public static LogLevel logLevel = LogLevel.None;

        public static void Log(LogLevel level, string message)
        {
            if (logLevel == LogLevel.None) return;

            var logLevelIndex = (int)logLevel;
            switch (level)
            {
                case LogLevel.Error:
                    if (logLevelIndex < (int)LogLevel.Error) return;
                    Debug.LogError($"[GC] ({level}) {message}");
                    break;
                case LogLevel.Warning:
                    if (logLevelIndex < (int)LogLevel.Warning) return;
                    Debug.LogWarning($"[GC] ({level}) {message}");
                    break;
                case LogLevel.Info:
                    if (logLevelIndex < (int)LogLevel.Info) return;
                    Debug.Log($"[GC] ({level}) {message}");
                    break;
                case LogLevel.Debug:
                    if (logLevelIndex < (int)LogLevel.Debug) return;
                    Debug.Log($"[GC] ({level}) {message}");
                    break;
            }
        }

        public static void LogError(string message)
        {
            Log(LogLevel.Error, message);
        }

        public static void LogWarning(string message)
        {
            Log(LogLevel.Warning, message);
        }

        public static void LogInfo(string message)
        {
            Log(LogLevel.Info, message);
        }

        public static void LogDebug(string message)
        {
            Log(LogLevel.Debug, message);
        }
    }
}
