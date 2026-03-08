using UnityEngine;

namespace Maptifier.Core
{
    /// <summary>
    /// Static utility for subtle haptic feedback on Android.
    /// Uses VibrationEffect.createOneShot on Android, Handheld.Vibrate fallback elsewhere.
    /// </summary>
    public static class HapticFeedback
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrationEffectClass;
        private static AndroidJavaObject _vibrator;
        private static AndroidJavaObject _context;
        private static bool _initialized;

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            try
            {
                using var unityPlayer = new AndroidJavaObject("com.unity3d.player.UnityPlayer");
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                _context = activity?.Call<AndroidJavaObject>("getApplicationContext");
                var vibratorService = _context?.Call<AndroidJavaObject>("getSystemService", "vibrator");
                _vibrator = vibratorService;
                _vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                _initialized = _vibrator != null && _vibrationEffectClass != null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HapticFeedback] Init failed: {ex.Message}");
            }
        }
#endif

        /// <summary>Light tap (10ms, amplitude 80).</summary>
        public static void Tap()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                EnsureInitialized();
                if (!_initialized || _vibrator == null) return;

                var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", 10L, 80);
                _vibrator.Call("vibrate", effect);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HapticFeedback] Tap failed: {ex.Message}");
            }
#else
            try
            {
                if (SystemInfo.deviceType == DeviceType.Handheld)
                    Handheld.Vibrate();
            }
            catch { /* no-op in editor */ }
#endif
        }

        /// <summary>Medium tap (20ms, amplitude 128).</summary>
        public static void Medium()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                EnsureInitialized();
                if (!_initialized || _vibrator == null) return;

                var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", 20L, 128);
                _vibrator.Call("vibrate", effect);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HapticFeedback] Medium failed: {ex.Message}");
            }
#else
            try
            {
                if (SystemInfo.deviceType == DeviceType.Handheld)
                    Handheld.Vibrate();
            }
            catch { /* no-op in editor */ }
#endif
        }

        /// <summary>Heavy tap (30ms, amplitude 200).</summary>
        public static void Heavy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                EnsureInitialized();
                if (!_initialized || _vibrator == null) return;

                var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", 30L, 200);
                _vibrator.Call("vibrate", effect);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HapticFeedback] Heavy failed: {ex.Message}");
            }
#else
            try
            {
                if (SystemInfo.deviceType == DeviceType.Handheld)
                    Handheld.Vibrate();
            }
            catch { /* no-op in editor */ }
#endif
        }
    }
}
