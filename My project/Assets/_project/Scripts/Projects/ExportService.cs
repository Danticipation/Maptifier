using System;
using System.IO;
using System.Collections;
using UnityEngine;
using Maptifier.Core;

namespace Maptifier.Projects
{
    public class ExportService
    {
        private readonly IPermissionService _permissionService;

        public ExportService()
        {
            ServiceLocator.TryGet<IPermissionService>(out _permissionService);
        }

        public void ExportScreenshot(RenderTexture source, Action<string> onComplete)
        {
            if (_permissionService != null && !_permissionService.HasPermission(PermissionType.Storage))
            {
                _permissionService.RequestPermission(PermissionType.Storage, (status) =>
                {
                    if (status == PermissionStatus.Granted)
                        PerformScreenshotExport(source, onComplete);
                    else
                        onComplete?.Invoke(null);
                });
            }
            else
            {
                PerformScreenshotExport(source, onComplete);
            }
        }

        private void PerformScreenshotExport(RenderTexture source, Action<string> onComplete)
        {
            var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = source;
            tex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.Destroy(tex);

            string filename = $"Maptifier_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string path = Path.Combine(Application.persistentDataPath, "Exports", filename);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, png);

#if UNITY_ANDROID && !UNITY_EDITOR
                RegisterWithMediaStore(path, "image/png");
#endif
                AnalyticsService.TrackExportCompleted("image");
                onComplete?.Invoke(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ExportService] Screenshot export failed: {ex.Message}");
                onComplete?.Invoke(null);
            }
        }

        public IEnumerator ExportVideo(
            RenderTexture source,
            float durationSeconds,
            int fps,
            Action<float> onProgress,
            Action<string> onComplete,
            Func<bool> cancelCheck)
        {
            bool permissionReady = false;
            bool permissionDenied = false;

            if (_permissionService != null && !_permissionService.HasPermission(PermissionType.Storage))
            {
                _permissionService.RequestPermission(PermissionType.Storage, (status) =>
                {
                    if (status == PermissionStatus.Granted)
                        permissionReady = true;
                    else
                        permissionDenied = true;
                });

                while (!permissionReady && !permissionDenied)
                    yield return null;

                if (permissionDenied)
                {
                    onComplete?.Invoke(null);
                    yield break;
                }
            }

            string filename = $"Maptifier_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            string path = Path.Combine(Application.persistentDataPath, "Exports", filename);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

#if UNITY_ANDROID && !UNITY_EDITOR
            yield return ExportVideoAndroid(source, path, durationSeconds, fps, onProgress, onComplete, cancelCheck);
#else
            yield return ExportVideoEditor(source, path, durationSeconds, fps, onProgress, onComplete, cancelCheck);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private IEnumerator ExportVideoAndroid(
            RenderTexture source,
            string path,
            float durationSeconds,
            int fps,
            Action<float> onProgress,
            Action<string> onComplete,
            Func<bool> cancelCheck)
        {
            int width = source.width;
            int height = source.height;
            int bitRate = width * height * 4; // Approx 8Mbps for 1080p

            using (var encoder = new AndroidJavaObject("com.maptifier.core.MaptifierEncoder"))
            {
                try
                {
                    encoder.Call("init", path, width, height, bitRate, fps);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ExportService] Encoder init failed: {ex.Message}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                int totalFrames = Mathf.CeilToInt(durationSeconds * fps);
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                long frameTimeUs = 1000000 / fps;

                for (int i = 0; i < totalFrames; i++)
                {
                    if (cancelCheck != null && cancelCheck()) break;

                    RenderTexture prev = RenderTexture.active;
                    RenderTexture.active = source;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    RenderTexture.active = prev;

                    byte[] rgbaData = tex.GetRawTextureData();
                    encoder.Call("encodeFrame", rgbaData, (long)i * frameTimeUs);

                    onProgress?.Invoke((float)(i + 1) / totalFrames);
                    yield return null; // Wait for next frame
                }

                encoder.Call("release");
                UnityEngine.Object.Destroy(tex);

                RegisterWithMediaStore(path, "video/mp4");
                AnalyticsService.TrackExportCompleted("video");
                onComplete?.Invoke(path);
            }
        }
#endif

        private IEnumerator ExportVideoEditor(
            RenderTexture source,
            string path,
            float durationSeconds,
            int fps,
            Action<float> onProgress,
            Action<string> onComplete,
            Func<bool> cancelCheck)
        {
            // Placeholder for Editor - in a real scenario, use Unity's MediaRecorder or similar
            int totalFrames = Mathf.CeilToInt(durationSeconds * fps);
            for (int i = 0; i < totalFrames; i++)
            {
                if (cancelCheck != null && cancelCheck()) yield break;
                onProgress?.Invoke((float)(i + 1) / totalFrames);
                yield return new WaitForSeconds(1f / fps);
            }
            onComplete?.Invoke(path);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void RegisterWithMediaStore(string filePath, string mimeType)
        {
            try
            {
                using var context = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity");
                using var resolver = context.Call<AndroidJavaObject>("getContentResolver");
                using var values = new AndroidJavaObject("android.content.ContentValues");

                string displayName = Path.GetFileName(filePath);
                values.Call("put", "android.provider.MediaStore.MediaColumns.DISPLAY_NAME", displayName);
                values.Call("put", "android.provider.MediaStore.MediaColumns.MIME_TYPE", mimeType);
                values.Call("put", "android.provider.MediaStore.MediaColumns.DATA", filePath);

                string collection = mimeType.StartsWith("image")
                    ? "android.provider.MediaStore.Images.Media.EXTERNAL_CONTENT_URI"
                    : "android.provider.MediaStore.Video.Media.EXTERNAL_CONTENT_URI";

                using var uriClass = new AndroidJavaClass("android.provider.MediaStore$" +
                    (mimeType.StartsWith("image") ? "Images" : "Video") + "$Media");
                using var uri = uriClass.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");

                resolver.Call<AndroidJavaObject>("insert", uri, values);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ExportService] Failed to register with MediaStore: {e.Message}");
            }
        }
#endif
    }
}
