using UnityEngine;

namespace Maptifier.Core
{
    /// <summary>
    /// Lightweight analytics wrapper. Wraps Firebase Analytics behind #if FIREBASE_ANALYTICS.
    /// Falls back to Debug.Log when not defined. Respects GDPR consent flag.
    /// </summary>
    public static class AnalyticsService
    {
        private const string ConsentKey = "Maptifier_AnalyticsConsent";

        public static bool HasConsent
        {
            get => PlayerPrefs.GetInt(ConsentKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(ConsentKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static void LogEvent(string name, params (string key, string value)[] parameters)
        {
            if (!HasConsent) return;

#if FIREBASE_ANALYTICS
            try
            {
                var @params = new Firebase.Analytics.Parameter[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    @params[i] = new Firebase.Analytics.Parameter(parameters[i].key, parameters[i].value);
                }
                Firebase.Analytics.FirebaseAnalytics.LogEvent(name, @params);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Analytics] LogEvent failed: {ex.Message}");
            }
#else
            var paramStr = parameters.Length > 0
                ? string.Join(", ", System.Array.ConvertAll(parameters, p => $"{p.key}={p.value}"))
                : "";
            Debug.Log($"[Analytics] {name}({paramStr})");
#endif
        }

        public static void TrackAppOpen()
        {
            LogEvent("app_open");
        }

        public static void TrackProjectCreated(string name)
        {
            LogEvent("project_created", ("name", name ?? "Untitled"));
        }

        public static void TrackProjectLoaded(string projectId)
        {
            LogEvent("project_loaded", ("id", projectId ?? ""));
        }

        public static void TrackMediaImported(string type)
        {
            LogEvent("media_imported", ("type", type ?? "unknown"));
        }

        public static void TrackDisplayConnected(string resolution)
        {
            LogEvent("display_connected", ("resolution", resolution ?? ""));
        }

        public static void TrackExportCompleted(string format)
        {
            LogEvent("export_completed", ("format", format ?? ""));
        }

        public static void TrackEffectApplied(string effectId)
        {
            LogEvent("effect_applied", ("effect_id", effectId ?? ""));
        }

        public static void TrackSessionDuration(float seconds)
        {
            LogEvent("session_duration", ("seconds", seconds.ToString("F1")));
        }
    }
}
