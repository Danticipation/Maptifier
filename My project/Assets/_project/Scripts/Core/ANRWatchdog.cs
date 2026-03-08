using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Maptifier.Core
{
    /// <summary>
    /// Monitors main thread responsiveness. If the main thread is blocked for 4+ seconds
    /// (approaching Android's 5s ANR threshold), logs warnings and sends analytics.
    /// Only active on Android builds.
    /// </summary>
    public class ANRWatchdog : MonoBehaviour
    {
        private const float WarningThresholdSeconds = 4f;
        private const float AnrThresholdSeconds = 5f;
        private const float TickIntervalSeconds = 1f;

#if UNITY_ANDROID && !UNITY_EDITOR
        private Thread _watchdogThread;
        private volatile bool _running = true;
        private long _lastResetMs;
#endif

        private void Awake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _lastResetMs = Environment.TickCount64;
            _watchdogThread = new Thread(WatchdogLoop)
            {
                IsBackground = true,
                Name = "ANRWatchdog"
            };
            _watchdogThread.Start();
            DontDestroyOnLoad(gameObject);
#endif
        }

        private void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Interlocked.Exchange(ref _lastResetMs, Environment.TickCount64);
#endif
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _running = false;
            _watchdogThread?.Join(2000);
            _watchdogThread = null;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void WatchdogLoop()
        {
            var tickMs = (int)(TickIntervalSeconds * 1000);
            while (_running)
            {
                Thread.Sleep(tickMs);
                if (!_running) break;

                var lastReset = Interlocked.Read(ref _lastResetMs);
                var elapsed = (Environment.TickCount64 - lastReset) / 1000.0;
                if (elapsed >= AnrThresholdSeconds)
                {
                    var stackTrace = GetMainThreadStackTrace();
                    Debug.LogError($"[ANRWatchdog] Main thread blocked for {elapsed:F1}s (ANR threshold). Stack trace:\n{stackTrace}");
                    SendNearAnrAnalytics(elapsed, stackTrace);
                }
                else if (elapsed >= WarningThresholdSeconds)
                {
                    Debug.LogWarning($"[ANRWatchdog] Main thread approaching ANR: blocked for {elapsed:F1}s");
                    SendNearAnrAnalytics(elapsed, null);
                }
            }
        }

        private static string GetMainThreadStackTrace()
        {
            try
            {
                return new StackTrace(true).ToString();
            }
            catch (Exception ex)
            {
                return $"Stack trace unavailable: {ex.Message}";
            }
        }

        private void SendNearAnrAnalytics(float blockedSeconds, string stackTrace)
        {
#if MAPTIFIER_ANALYTICS
            try
            {
                Firebase.Analytics.FirebaseAnalytics.LogEvent("near_anr", new Firebase.Analytics.Parameter("blocked_seconds", blockedSeconds));
                if (!string.IsNullOrEmpty(stackTrace))
                    Firebase.Analytics.FirebaseAnalytics.LogEvent("anr_stack", new Firebase.Analytics.Parameter("trace", stackTrace.Length > 500 ? stackTrace.Substring(0, 500) : stackTrace));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ANRWatchdog] Analytics failed: {ex.Message}");
            }
#else
            // No-op when Firebase Analytics not linked
#endif
        }
#endif
    }
}
