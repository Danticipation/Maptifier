using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Maptifier.Media
{
    /// <summary>
    /// Implements IMediaImportService. Imports from gallery (Android intent / Editor file panel),
    /// copies to MediaCache, and provides LoadImage/LoadVideo/GenerateThumbnail.
    /// </summary>
    public class MediaImportService : IMediaImportService
    {
        private const string MediaCacheFolder = "MediaCache";
        private const int GalleryRequestCode = 9001;

        private static MediaImportCallbackReceiver _callbackReceiver;

        public void ImportFromGallery(Action<IMediaSource> onComplete, Action<string> onError)
        {
#if UNITY_EDITOR
            var path = EditorUtility.OpenFilePanel("Select Image, Video, or Vector", "", "jpg;jpeg;png;gif;webp;svg;mp4;mov;webm;avi;mkv");
            if (string.IsNullOrEmpty(path))
            {
                onError?.Invoke("No file selected.");
                return;
            }
            ProcessImportedFile(path, onComplete, onError);
#elif UNITY_ANDROID
            LaunchAndroidGalleryPicker(onComplete, onError);
#else
            onError?.Invoke("Gallery import not supported on this platform.");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void LaunchAndroidGalleryPicker(Action<IMediaSource> onComplete, Action<string> onError)
        {
            EnsureCallbackReceiver();
            _callbackReceiver.SetPendingCallback(onComplete, onError);

            try
            {
                using (var unity = new AndroidJavaObject("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.GET_CONTENT"))
                    {
                        intent.Call<AndroidJavaObject>("setType", "*/*");
                        intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.OPENABLE");
                        var mimeTypes = new string[] { "image/*", "video/*", "image/svg+xml" };
                        intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.MIME_TYPES", mimeTypes);
                        activity.Call("startActivityForResult", intent, GalleryRequestCode);
                    }
                }
            }
            catch (Exception ex)
            {
                _callbackReceiver.ClearPendingCallback();
                onError?.Invoke($"Failed to launch gallery: {ex.Message}");
            }
        }

        /// <summary>
        /// Called by MediaImportCallbackReceiver when Android plugin delivers the picked file path.
        /// </summary>
        internal static void OnAndroidMediaPicked(string uriOrPath)
        {
            if (_callbackReceiver == null) return;
            _callbackReceiver.InvokeWithPath(uriOrPath);
        }
#endif

        private void EnsureCallbackReceiver()
        {
            if (_callbackReceiver != null) return;

            var go = new GameObject("[MediaImportCallbackReceiver]");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            _callbackReceiver = go.AddComponent<MediaImportCallbackReceiver>();
        }

        private void ProcessImportedFile(string sourcePath, Action<IMediaSource> onComplete, Action<string> onError)
        {
            try
            {
                var cachePath = CopyToMediaCache(sourcePath);
                var mediaType = GetMediaTypeFromExtension(cachePath);

                IMediaSource source = mediaType switch
                {
                    MediaType.Image => LoadImage(cachePath),
                    MediaType.Video => LoadVideo(cachePath),
                    MediaType.Vector => LoadVector(cachePath),
                    _ => LoadImage(cachePath)
                };

                onComplete?.Invoke(source);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
            }
        }

        private string CopyToMediaCache(string sourcePath)
        {
            var cacheDir = Path.Combine(Application.persistentDataPath, MediaCacheFolder);
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            var fileName = Path.GetFileName(sourcePath);
            if (string.IsNullOrEmpty(fileName))
                fileName = $"import_{DateTime.UtcNow.Ticks}";

            var destPath = Path.Combine(cacheDir, fileName);
            File.Copy(sourcePath, destPath, overwrite: true);
            return destPath;
        }

        private static MediaType GetMediaTypeFromExtension(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
            switch (ext)
            {
                case ".mp4":
                case ".webm":
                case ".mov":
                case ".avi":
                case ".mkv":
                    return MediaType.Video;
                case ".svg":
                    return MediaType.Vector;
                default:
                    return MediaType.Image;
            }
        }

        public IMediaSource LoadImage(string path, int maxSize = 4096)
        {
            return new ImageSource(path, maxSize);
        }

        public IMediaSource LoadVideo(string path)
        {
            return new VideoSource(path);
        }

        public IMediaSource LoadVector(string path, int rasterWidth = 1024, int rasterHeight = 1024)
        {
            return new VectorSource(path, rasterWidth, rasterHeight);
        }

        public IMediaSource LoadText(TextConfig config = null, int width = 1024, int height = 512)
        {
            return new TextSource(config, width, height);
        }

        public Texture2D GenerateThumbnail(IMediaSource source, int size = 256)
        {
            if (source == null || !source.IsReady || source.OutputRT == null)
                return null;

            var rt = source.OutputRT;
            var thumbRT = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(rt, thumbRT);

            var thumb = new Texture2D(size, size);
            var prev = RenderTexture.active;
            RenderTexture.active = thumbRT;
            thumb.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            thumb.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(thumbRT);

            return thumb;
        }
    }

    /// <summary>
    /// Receives callbacks from Android gallery picker. On Android, a native plugin
    /// should call UnitySendMessage to this object's "OnMediaPicked" method with the file path.
    /// </summary>
    internal class MediaImportCallbackReceiver : MonoBehaviour
    {
        private Action<IMediaSource> _onComplete;
        private Action<string> _onError;

        public void SetPendingCallback(Action<IMediaSource> onComplete, Action<string> onError)
        {
            _onComplete = onComplete;
            _onError = onError;
        }

        public void ClearPendingCallback()
        {
            _onComplete = null;
            _onError = null;
        }

        /// <summary>
        /// Invokes the pending callback with the picked file path. Called from OnMediaPicked
        /// (UnitySendMessage) or from C# when a plugin delivers the path.
        /// </summary>
        public void InvokeWithPath(string pathOrUri)
        {
            ProcessPickedPath(pathOrUri);
        }

        /// <summary>
        /// Called via UnitySendMessage from Android plugin when user picks a file.
        /// </summary>
        public void OnMediaPicked(string pathOrUri)
        {
            ProcessPickedPath(pathOrUri);
        }

        private void ProcessPickedPath(string pathOrUri)
        {
            if (_onComplete == null && _onError == null) return;

            if (string.IsNullOrEmpty(pathOrUri))
            {
                _onError?.Invoke("No file selected.");
                ClearPendingCallback();
                return;
            }

            try
            {
                var service = Core.ServiceLocator.Get<IMediaImportService>();
                var cachePath = pathOrUri;

#if UNITY_ANDROID && !UNITY_EDITOR
                if (pathOrUri.StartsWith("content://"))
                {
                    cachePath = CopyContentUriToCache(pathOrUri);
                }
#endif

                var mediaType = MediaImportServiceExtensions.GetMediaTypeFromExtensionStatic(cachePath);
                IMediaSource source = mediaType switch
                {
                    MediaType.Image => service.LoadImage(cachePath),
                    MediaType.Video => service.LoadVideo(cachePath),
                    MediaType.Vector => service.LoadVector(cachePath),
                    _ => service.LoadImage(cachePath)
                };

                _onComplete?.Invoke(source);
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex.Message);
            }

            ClearPendingCallback();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private string CopyContentUriToCache(string contentUri)
        {
            using (var unity = new AndroidJavaObject("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
            using (var uriClass = new AndroidJavaClass("android.net.Uri"))
            using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", contentUri))
            {
                var stream = resolver.Call<AndroidJavaObject>("openInputStream", uri);
                if (stream == null) throw new Exception("Could not open content URI.");

                var cacheDir = Path.Combine(Application.persistentDataPath, "MediaCache");
                if (!Directory.Exists(cacheDir))
                    Directory.CreateDirectory(cacheDir);

                var mimeType = resolver.Call<string>("getType", uri);
                var ext = GetExtensionFromMimeType(mimeType);
                var fileName = $"import_{DateTime.UtcNow.Ticks}{ext}";
                var destPath = Path.Combine(cacheDir, fileName);

                var buffer = new byte[8192];
                int read;
                using (var fs = new FileStream(destPath, FileMode.Create))
                {
                    while ((read = stream.Call<int>("read", buffer)) > 0)
                        fs.Write(buffer, 0, read);
                }
                stream.Call("close");
                return destPath;
            }
        }

        private static string GetExtensionFromMimeType(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType)) return ".tmp";
            if (mimeType.Contains("svg")) return ".svg";
            if (mimeType.StartsWith("image/"))
            {
                if (mimeType.Contains("jpeg") || mimeType.Contains("jpg")) return ".jpg";
                if (mimeType.Contains("png")) return ".png";
                if (mimeType.Contains("gif")) return ".gif";
                if (mimeType.Contains("webp")) return ".webp";
                return ".jpg";
            }
            if (mimeType.StartsWith("video/"))
            {
                if (mimeType.Contains("mp4")) return ".mp4";
                if (mimeType.Contains("webm")) return ".webm";
                if (mimeType.Contains("quicktime")) return ".mov";
                return ".mp4";
            }
            return ".tmp";
        }
#endif

        private void OnDestroy()
        {
            ClearPendingCallback();
        }
    }

    internal static class MediaImportServiceExtensions
    {
        public static MediaType GetMediaTypeFromExtensionStatic(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
            switch (ext)
            {
                case ".mp4":
                case ".webm":
                case ".mov":
                case ".avi":
                case ".mkv":
                    return MediaType.Video;
                case ".svg":
                    return MediaType.Vector;
                default:
                    return MediaType.Image;
            }
        }
    }
}
