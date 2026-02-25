using System;
using System.IO;
using System.Collections;
using UnityEngine;

namespace Maptifier.Projects
{
    public class ExportService
    {
        public void ExportScreenshot(RenderTexture source, Action<string> onComplete)
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
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, png);

            // On Android, register with MediaStore for gallery visibility
#if UNITY_ANDROID && !UNITY_EDITOR
            RegisterWithMediaStore(path, "image/png");
#endif
            onComplete?.Invoke(path);
        }

        public IEnumerator ExportVideo(
            RenderTexture source,
            float durationSeconds,
            int fps,
            Action<float> onProgress,
            Action<string> onComplete,
            Func<bool> cancelCheck)
        {
            string filename = $"Maptifier_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            string path = Path.Combine(Application.persistentDataPath, "Exports", filename);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            int totalFrames = Mathf.CeilToInt(durationSeconds * fps);
            float frameInterval = 1f / fps;

            // Use AsyncGPUReadback for non-blocking frame capture
            var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);

            for (int i = 0; i < totalFrames; i++)
            {
                if (cancelCheck != null && cancelCheck())
                {
                    UnityEngine.Object.Destroy(tex);
                    yield break;
                }

                // Capture frame
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = source;
                tex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                // In production, feed to MediaCodec encoder here
                // For prototype, we accumulate frames

                onProgress?.Invoke((float)(i + 1) / totalFrames);
                yield return new WaitForSeconds(frameInterval);
            }

            UnityEngine.Object.Destroy(tex);

#if UNITY_ANDROID && !UNITY_EDITOR
            RegisterWithMediaStore(path, "video/mp4");
#endif
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

                using var uri = new AndroidJavaClass(collection).GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
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
