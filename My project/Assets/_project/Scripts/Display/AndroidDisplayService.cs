#if UNITY_ANDROID

using System;
using UnityEngine;

namespace Maptifier.Display
{
    /// <summary>
    /// Android implementation of IDisplayService. Uses DisplayManager and Presentation
    /// to detect and present content on external displays (HDMI, USB-C, etc.).
    /// </summary>
    public class AndroidDisplayService : IDisplayService
    {
        private const string DisplayClass = "android.view.Display";
        private const string SamsungDeXFeature = "com.samsung.android.feature.samsung_dex";

        private bool _disposed;
        private bool _initialized;
        private bool _isExternalDisplayConnected;
        private int _externalDisplayId = -1;
        private Vector2Int _externalResolution;
        private float _externalRefreshRate;
        private object _presentationSurface;
        private object _displayManager;
        private object _activity;

        public bool IsExternalDisplayConnected => _isExternalDisplayConnected;
        public int ExternalDisplayId => _externalDisplayId;
        public Vector2Int ExternalResolution => _externalResolution;
        public float ExternalRefreshRate => _externalRefreshRate;

        public event Action<int, Vector2Int, float> OnDisplayConnected;
        public event Action<int> OnDisplayDisconnected;
        public event Action<Vector2Int> OnResolutionChanged;

        public void Initialize()
        {
            if (_initialized) return;

            try
            {
                CheckSamsungDeX();
                AcquireDisplayManager();
                _initialized = true;
                Debug.Log("[AndroidDisplayService] Initialized");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidDisplayService] Initialize failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void PollDisplayState()
        {
            if (!_initialized || _disposed) return;

            try
            {
                var displayManager = _displayManager;
                if (displayManager == null)
                {
                    AcquireDisplayManager();
                    displayManager = _displayManager;
                }

                if (displayManager == null) return;

                var displays = GetDisplays(displayManager);
                if (displays == null || displays.Length == 0) return;

                var primaryDisplayId = GetPrimaryDisplayId();
                object externalDisplay = null;
                int externalId = -1;

                foreach (var display in displays)
                {
                    try
                    {
                        var id = GetDisplayId(display);
                        if (id < 0) continue;
                        if (id == primaryDisplayId) continue;

                        externalDisplay = display;
                        externalId = id;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[AndroidDisplayService] Error inspecting display: {ex.Message}");
                    }
                }

                if (externalDisplay != null && externalId >= 0)
                {
                    var resolution = GetDisplayResolution(externalDisplay);
                    var refreshRate = GetDisplayRefreshRate(externalDisplay);

                    if (!_isExternalDisplayConnected || _externalDisplayId != externalId ||
                        _externalResolution != resolution || Math.Abs(_externalRefreshRate - refreshRate) > 0.01f)
                    {
                        var wasConnected = _isExternalDisplayConnected;
                        var prevResolution = _externalResolution;

                        _isExternalDisplayConnected = true;
                        _externalDisplayId = externalId;
                        _externalResolution = resolution;
                        _externalRefreshRate = refreshRate;

                        if (!wasConnected)
                        {
                            Debug.Log($"[AndroidDisplayService] Display connected: id={externalId}, resolution={resolution.x}x{resolution.y}, refreshRate={refreshRate:F2}Hz");
                            OnDisplayConnected?.Invoke(externalId, resolution, refreshRate);
                        }
                        else if (prevResolution != resolution)
                        {
                            Debug.Log($"[AndroidDisplayService] Resolution changed: {resolution.x}x{resolution.y}");
                            OnResolutionChanged?.Invoke(resolution);
                        }
                    }
                }
                else
                {
                    if (_isExternalDisplayConnected)
                    {
                        var disconnectedId = _externalDisplayId;
                        _isExternalDisplayConnected = false;
                        _externalDisplayId = -1;
                        _externalResolution = default;
                        _externalRefreshRate = 0f;
                        _presentationSurface = null;

                        Debug.Log($"[AndroidDisplayService] Display disconnected: id={disconnectedId}");
                        OnDisplayDisconnected?.Invoke(disconnectedId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidDisplayService] PollDisplayState error: {ex.Message}");
            }
        }

        public void PresentFrame(RenderTexture compositeRT)
        {
            if (!_initialized || _disposed || compositeRT == null) return;
            if (!_isExternalDisplayConnected) return;

            try
            {
                var surface = _presentationSurface;
                if (surface == null)
                {
                    surface = AcquirePresentationSurface();
                    _presentationSurface = surface;
                }

                if (surface == null) return;

                BlitToSurface(compositeRT, surface);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidDisplayService] PresentFrame error: {ex.Message}");
            }
        }

        public void Shutdown()
        {
            if (!_initialized) return;

            try
            {
                _presentationSurface = null;
                _displayManager = null;
                _activity = null;
                _initialized = false;
                _isExternalDisplayConnected = false;
                _externalDisplayId = -1;
                _externalResolution = default;
                _externalRefreshRate = 0f;

                Debug.Log("[AndroidDisplayService] Shutdown");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidDisplayService] Shutdown error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            Shutdown();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private void CheckSamsungDeX()
        {
            try
            {
                var activity = GetCurrentActivity();
                if (activity == null) return;

                var pm = activity.Call<UnityEngine.AndroidJavaObject>("getPackageManager");
                if (pm == null) return;

                var hasDeX = pm.Call<bool>("hasSystemFeature", SamsungDeXFeature);
                if (hasDeX)
                {
                    Debug.LogWarning("[AndroidDisplayService] Samsung DeX detected. External display behavior may differ.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidDisplayService] DeX check failed: {ex.Message}");
            }
        }

        private void AcquireDisplayManager()
        {
            try
            {
                var activity = GetCurrentActivity();
                if (activity == null) return;

                _activity = activity;
                var context = activity.Call<UnityEngine.AndroidJavaObject>("getApplicationContext");
                if (context == null) return;

                var dm = context.Call<UnityEngine.AndroidJavaObject>("getSystemService", "display");
                _displayManager = dm;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidDisplayService] AcquireDisplayManager failed: {ex.Message}");
            }
        }

        private UnityEngine.AndroidJavaObject GetCurrentActivity()
        {
            try
            {
                using var unityPlayer = new UnityEngine.AndroidJavaObject("com.unity3d.player.UnityPlayer");
                if (unityPlayer == null) return null;

                var activity = unityPlayer.GetStatic<UnityEngine.AndroidJavaObject>("currentActivity");
                return activity;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidDisplayService] GetCurrentActivity failed: {ex.Message}");
                return null;
            }
        }

        private object[] GetDisplays(object displayManager)
        {
            try
            {
                var dm = displayManager as UnityEngine.AndroidJavaObject;
                if (dm == null) return null;

                var displays = dm.Call<UnityEngine.AndroidJavaObject[]>("getDisplays");
                return displays;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidDisplayService] GetDisplays failed: {ex.Message}");
                return null;
            }
        }

        private int GetPrimaryDisplayId()
        {
            try
            {
                using var displayClass = new UnityEngine.AndroidJavaClass(DisplayClass);
                if (displayClass == null) return 0;

                var id = displayClass.GetStatic<int>("DEFAULT_DISPLAY");
                return id;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidDisplayService] GetPrimaryDisplayId failed: {ex.Message}");
                return 0;
            }
        }

        private int GetDisplayId(object display)
        {
            try
            {
                var d = display as UnityEngine.AndroidJavaObject;
                if (d == null) return -1;

                return d.Call<int>("getDisplayId");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidDisplayService] GetDisplayId failed: {ex.Message}");
                return -1;
            }
        }

        private Vector2Int GetDisplayResolution(object display)
        {
            try
            {
                var d = display as UnityEngine.AndroidJavaObject;
                if (d == null) return default;

                var point = new UnityEngine.AndroidJavaObject("android.graphics.Point");
                d.Call("getRealSize", point);

                var w = point.Get<int>("x");
                var h = point.Get<int>("y");
                return new Vector2Int(w, h);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidDisplayService] GetDisplayResolution failed: {ex.Message}");
                return default;
            }
        }

        private float GetDisplayRefreshRate(object display)
        {
            try
            {
                var d = display as UnityEngine.AndroidJavaObject;
                if (d == null) return 0f;

                return d.Call<float>("getRefreshRate");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidDisplayService] GetDisplayRefreshRate failed: {ex.Message}");
                return 0f;
            }
        }

        private object AcquirePresentationSurface()
        {
            try
            {
                var activity = _activity as UnityEngine.AndroidJavaObject;
                if (activity == null) return null;

                using var displayManager = new UnityEngine.AndroidJavaClass("android.hardware.display.DisplayManager");
                var dm = _displayManager as UnityEngine.AndroidJavaObject;
                if (dm == null) return null;

                var displays = dm.Call<UnityEngine.AndroidJavaObject[]>("getDisplays");
                if (displays == null || displays.Length == 0) return null;

                var primaryId = GetPrimaryDisplayId();
                UnityEngine.AndroidJavaObject targetDisplay = null;

                foreach (var d in displays)
                {
                    var id = d.Call<int>("getDisplayId");
                    if (id != primaryId)
                    {
                        targetDisplay = d;
                        break;
                    }
                }

                if (targetDisplay == null) return null;

                using var presentationClass = new UnityEngine.AndroidJavaClass("android.app.Presentation");
                var context = activity.Call<UnityEngine.AndroidJavaObject>("getApplicationContext");
                if (context == null) return null;

                var presentation = new UnityEngine.AndroidJavaObject("android.app.Presentation", context, targetDisplay);
                presentation.Call("show");

                var window = presentation.Call<UnityEngine.AndroidJavaObject>("getWindow");
                if (window == null) return null;

                var decorView = window.Call<UnityEngine.AndroidJavaObject>("getDecorView");
                if (decorView == null) return null;

                var surfaceHolder = decorView.Call<UnityEngine.AndroidJavaObject>("getHolder");
                if (surfaceHolder == null) return null;

                var surface = surfaceHolder.Call<UnityEngine.AndroidJavaObject>("getSurface");
                return surface;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidDisplayService] AcquirePresentationSurface failed: {ex.Message}");
                return null;
            }
        }

        private void BlitToSurface(RenderTexture compositeRT, object surface)
        {
            try
            {
                var surf = surface as UnityEngine.AndroidJavaObject;
                if (surf == null) return;

                var rt = RenderTexture.active;
                RenderTexture.active = compositeRT;

                var width = compositeRT.width;
                var height = compositeRT.height;

                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                var unityPixels = tex.GetPixels32();
                var pixels = new int[width * height];
                for (var i = 0; i < unityPixels.Length; i++)
                {
                    var c = unityPixels[i];
                    pixels[i] = (c.a << 24) | (c.r << 16) | (c.g << 8) | c.b;
                }

                UnityEngine.Object.Destroy(tex);
                RenderTexture.active = rt;

                using var configClass = new UnityEngine.AndroidJavaClass("android.graphics.Bitmap$Config");
                var config = configClass.GetStatic<UnityEngine.AndroidJavaObject>("ARGB_8888");
                if (config == null) return;

                using var bitmapClass = new UnityEngine.AndroidJavaClass("android.graphics.Bitmap");
                using var bmp = bitmapClass.CallStatic<UnityEngine.AndroidJavaObject>("createBitmap", width, height, config);
                if (bmp == null) return;

                bmp.Call("setPixels", pixels);

                var canvas = surf.Call<UnityEngine.AndroidJavaObject>("lockCanvas", null);
                if (canvas != null)
                {
                    try
                    {
                        canvas.Call("drawBitmap", bmp, 0f, 0f, null);
                        surf.Call("unlockCanvasAndPost", canvas);
                    }
                    catch
                    {
                        surf.Call("unlockCanvasAndPost", canvas);
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidDisplayService] BlitToSurface failed: {ex.Message}");
            }
        }
    }
}

#endif
