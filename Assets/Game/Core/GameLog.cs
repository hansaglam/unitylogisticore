using System;
using System.Diagnostics;
using UnityEngine;

namespace LogistiCore
{
    /// <summary>
    /// Development logs are removed from release players. Errors remain.
    /// Callers must not pass credentials, tokens, or other secrets.
    /// </summary>
    public static class GameLog
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string context, string message)
        {
            UnityEngine.Debug.Log(Format(context, message));
        }

        public static void Error(string context, string message, Exception exception = null)
        {
            if (exception == null)
            {
                UnityEngine.Debug.LogError(Format(context, message));
                return;
            }

            UnityEngine.Debug.LogError(Format(
                context,
                message + " (" + exception.GetType().Name + ": " + exception.Message + ")"));
        }

        static string Format(string context, string message)
        {
            return "[LogistiCore] [" + context + "] " + message;
        }
    }
}
