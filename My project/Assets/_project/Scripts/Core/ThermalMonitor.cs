#if UNITY_ANDROID && !UNITY_EDITOR

using System;
using UnityEngine;

namespace Maptifier.Core
{
    /// <summary>
    /// Monitors device thermal state on Android via PowerManager.getCurrentThermalStatus().
    /// Reduces quality tier when device is hot. No-op on non-Android platforms.
    /// </summary>
    public class ThermalMonitor : MonoBehaviour
    {
        private const float PollIntervalSeconds = 10f;

        // PowerManager.THERMAL_STATUS_* (API 29+)
        private const int ThermalNone = 0;
        private const int ThermalLight = 1;
        private const int ThermalModerate = 2;
        private const int ThermalSevere = 3;
        private const int ThermalCritical = 4;
        private const int ThermalEmergency = 5;
        private const int ThermalShutdown = 6;

        private float _lastPollTime;
        private int _lastThermalStatus = -1;
        private IAdaptiveQuality _adaptiveQuality;
        private object _powerManager;

        private void Awake()
        {
            try
            {
                using var unityPlayer = new AndroidJavaObject("com.unity3d.player.UnityPlayer");
                var activity = unityPlayer?.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return;

                var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                if (context == null) return;

                _powerManager = context.Call<AndroidJavaObject>("getSystemService", "power");
                _adaptiveQuality = ServiceLocator.TryGet<IAdaptiveQuality>(out var aq) ? aq : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ThermalMonitor] Init failed: {ex.Message}");
            }
        }

        private void Update()
        {
            if (_powerManager == null) return;
            if (Time.realtimeSinceStartup - _lastPollTime < PollIntervalSeconds) return;

            _lastPollTime = Time.realtimeSinceStartup;
            PollThermalStatus();
        }

        private void PollThermalStatus()
        {
            try
            {
                var pm = _powerManager as AndroidJavaObject;
                if (pm == null) return;

                var status = pm.Call<int>("getCurrentThermalStatus");
                if (status == _lastThermalStatus) return;
                _lastThermalStatus = status;

                if (status >= ThermalCritical)
                {
                    Debug.LogWarning($"[ThermalMonitor] Critical thermal: {status}");
                    ForceMinimumQuality();
                    if (status >= ThermalEmergency)
                        ConsiderPausingEffects();
                }
                else if (status >= ThermalSevere)
                {
                    Debug.LogWarning($"[ThermalMonitor] Severe thermal: {status}");
                    ShowUserWarning();
                    ReduceQualityTier();
                }
                else if (status >= ThermalModerate)
                {
                    Debug.LogWarning($"[ThermalMonitor] Moderate thermal: {status}");
                    ReduceQualityTier();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ThermalMonitor] Poll failed: {ex.Message}");
            }
        }

        private void ReduceQualityTier()
        {
            if (_adaptiveQuality == null) return;
            if (_adaptiveQuality.CurrentTier == PerformanceTier.Performance) return;

            _adaptiveQuality.SetPerformanceMode(true);
        }

        private void ForceMinimumQuality()
        {
            if (_adaptiveQuality == null) return;
            _adaptiveQuality.SetPerformanceMode(true);
        }

        private void ConsiderPausingEffects()
        {
            EventBus.Publish(new ThermalCriticalEvent());
        }

        private void ShowUserWarning()
        {
            try
            {
                using var unityPlayer = new AndroidJavaObject("com.unity3d.player.UnityPlayer");
                var activity = unityPlayer?.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return;

                var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                if (context == null) return;

                using var toastClass = new AndroidJavaClass("android.widget.Toast");
                var toast = toastClass.CallStatic<AndroidJavaObject>("makeText", context, "Device is running hot — reducing quality", 1);
                toast?.Call("show");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ThermalMonitor] Toast failed: {ex.Message}");
            }
        }
    }

    public readonly struct ThermalCriticalEvent { }
}

#endif
