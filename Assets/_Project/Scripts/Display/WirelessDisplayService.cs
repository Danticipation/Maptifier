#if UNITY_ANDROID

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maptifier.Display
{
    /// <summary>
    /// Wireless display fallback using Android MediaRouter (Cast/Miracast).
    /// 100-200ms latency; best-effort v1 implementation.
    /// Integrates with IDisplayService for ServiceLocator swap.
    /// </summary>
    public class WirelessDisplayService : IDisplayService
    {
        private const string MediaRouterService = "media_router";
        private const string ToastMessage = "Wireless display has 100-200ms latency. For best results, use a wired connection.";

        private bool _disposed;
        private bool _initialized;
        private bool _isExternalDisplayConnected;
        private int _externalDisplayId = -1;
        private Vector2Int _externalResolution;
        private float _externalRefreshRate;
        private object _mediaRouter;
        private object _activity;
        private object _currentRoute;
        private object _presentationSurface;

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
                var activity = GetCurrentActivity();
                if (activity == null)
                {
                    Debug.LogWarning("[WirelessDisplayService] Could not get activity");
                    return;
                }

                _activity = activity;
                var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                if (context == null) return;

                _mediaRouter = context.Call<AndroidJavaObject>("getSystemService", MediaRouterService);
                if (_mediaRouter == null)
                {
                    Debug.LogWarning("[WirelessDisplayService] MediaRouter not available");
                    return;
                }

                _initialized = true;
                Debug.Log("[WirelessDisplayService] Initialized (MediaRouter)");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WirelessDisplayService] Initialize failed: {ex.Message}");
            }
        }

        public void PollDisplayState()
        {
            if (!_initialized || _disposed) return;

            try
            {
                var routes = GetRoutes();
                if (routes == null || routes.Count == 0)
                {
                    if (_isExternalDisplayConnected)
                    {
                        DisconnectDisplay();
                    }
                    return;
                }

                var route = routes[0];
                if (_currentRoute == null || !RouteEquals(_currentRoute, route))
                {
                    ConnectToRoute(route);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WirelessDisplayService] PollDisplayState error: {ex.Message}");
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

                if (surface != null)
                    BlitToSurface(compositeRT, surface);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WirelessDisplayService] PresentFrame error: {ex.Message}");
            }
        }

        public void Shutdown()
        {
            if (!_initialized) return;

            try
            {
                DisconnectDisplay();
                _mediaRouter = null;
                _activity = null;
                _initialized = false;
                Debug.Log("[WirelessDisplayService] Shutdown");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WirelessDisplayService] Shutdown error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Shutdown();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private AndroidJavaObject GetCurrentActivity()
        {
            try
            {
                using var unityPlayer = new AndroidJavaObject("com.unity3d.player.UnityPlayer");
                return unityPlayer?.GetStatic<AndroidJavaObject>("currentActivity");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WirelessDisplayService] GetCurrentActivity failed: {ex.Message}");
                return null;
            }
        }

        private List<AndroidJavaObject> GetRoutes()
        {
            var list = new List<AndroidJavaObject>();
            try
            {
                var router = _mediaRouter as AndroidJavaObject;
                if (router == null) return list;

                using var routeInfoClass = new AndroidJavaClass("android.media.MediaRouter$RouteInfo");
                var routeType = routeInfoClass.GetStatic<int>("ROUTE_TYPE_LIVE_VIDEO");

                var routesObj = router.Call<AndroidJavaObject>("getRoutes");
                if (routesObj == null) return list;

                var size = routesObj.Call<int>("size");
                for (var i = 0; i < size; i++)
                {
                    var route = routesObj.Call<AndroidJavaObject>("get", i);
                    if (route == null) continue;
                    var routeTypeVal = route.Call<int>("getSupportedTypes");
                    if ((routeTypeVal & routeType) != 0)
                        list.Add(route);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WirelessDisplayService] GetRoutes failed: {ex.Message}");
            }
            return list;
        }

        private bool RouteEquals(object a, object b)
        {
            if (a == null || b == null) return a == b;
            try
            {
                var ra = a as AndroidJavaObject;
                var rb = b as AndroidJavaObject;
                if (ra == null || rb == null) return false;
                var idA = ra.Call<string>("getId");
                var idB = rb.Call<string>("getId");
                return idA == idB;
            }
            catch
            {
                return false;
            }
        }

        private void ConnectToRoute(AndroidJavaObject route)
        {
            try
            {
                var router = _mediaRouter as AndroidJavaObject;
                if (router == null) return;

                var routeType = 4; // ROUTE_TYPE_LIVE_VIDEO
                router.Call("selectRoute", routeType, route);
                _currentRoute = route;

                _isExternalDisplayConnected = true;
                _externalDisplayId = 1;
                _externalResolution = new Vector2Int(1920, 1080);
                _externalRefreshRate = 30f;

                ShowToast(ToastMessage);
                Debug.Log("[WirelessDisplayService] Connected to wireless display (100-200ms latency)");
                OnDisplayConnected?.Invoke(_externalDisplayId, _externalResolution, _externalRefreshRate);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WirelessDisplayService] ConnectToRoute failed: {ex.Message}");
            }
        }

        private void DisconnectDisplay()
        {
            try
            {
                var router = _mediaRouter as AndroidJavaObject;
                if (router != null)
                {
                    using var routeInfoClass = new AndroidJavaClass("android.media.MediaRouter$RouteInfo");
                    var defaultRoute = router.Call<AndroidJavaObject>("getDefaultRoute");
                    if (defaultRoute != null)
                        router.Call("selectRoute", 4, defaultRoute);
                }
            }
            catch { }

            var disconnectedId = _externalDisplayId;
            _currentRoute = null;
            _presentationSurface = null;
            _isExternalDisplayConnected = false;
            _externalDisplayId = -1;
            _externalResolution = default;
            _externalRefreshRate = 0f;
            OnDisplayDisconnected?.Invoke(disconnectedId);
        }

        private object AcquirePresentationSurface()
        {
            try
            {
                var activity = _activity as AndroidJavaObject;
                if (activity == null) return null;

                using var presentationClass = new AndroidJavaClass("android.app.Presentation");
                var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                if (context == null) return null;

                var display = _currentRoute is AndroidJavaObject route
                    ? route.Call<AndroidJavaObject>("getPresentationDisplay")
                    : null;
                if (display == null) return null;

                var presentation = new AndroidJavaObject("android.app.Presentation", context, display);
                presentation.Call("show");

                var window = presentation.Call<AndroidJavaObject>("getWindow");
                if (window == null) return null;

                var decorView = window.Call<AndroidJavaObject>("getDecorView");
                if (decorView == null) return null;

                var surfaceHolder = decorView.Call<AndroidJavaObject>("getHolder");
                if (surfaceHolder == null) return null;

                return surfaceHolder.Call<AndroidJavaObject>("getSurface");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WirelessDisplayService] AcquirePresentationSurface failed: {ex.Message}");
                return null;
            }
        }

        private void BlitToSurface(RenderTexture compositeRT, object surface)
        {
            try
            {
                var surf = surface as AndroidJavaObject;
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

                Destroy(tex);
                RenderTexture.active = rt;

                using var configClass = new AndroidJavaClass("android.graphics.Bitmap$Config");
                var config = configClass.GetStatic<AndroidJavaObject>("ARGB_8888");
                if (config == null) return;

                using var bitmapClass = new AndroidJavaClass("android.graphics.Bitmap");
                using var bmp = bitmapClass.CallStatic<AndroidJavaObject>("createBitmap", width, height, config);
                if (bmp == null) return;

                bmp.Call("setPixels", pixels);

                var canvas = surf.Call<AndroidJavaObject>("lockCanvas", null);
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
                Debug.LogWarning($"[WirelessDisplayService] BlitToSurface failed: {ex.Message}");
            }
        }

        private void ShowToast(string message)
        {
            try
            {
                var activity = _activity as AndroidJavaObject;
                if (activity == null) return;

                var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                if (context == null) return;

                using var toastClass = new AndroidJavaClass("android.widget.Toast");
                var toast = toastClass.CallStatic<AndroidJavaObject>("makeText", context, message, 1); // LENGTH_LONG
                toast?.Call("show");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WirelessDisplayService] Toast failed: {ex.Message}");
            }
        }
    }
}

#endif
