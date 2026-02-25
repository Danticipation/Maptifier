using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Maptifier.Core;

namespace Maptifier.Media
{
    /// <summary>
    /// Provides IMediaSource for static images. Loads from path asynchronously,
    /// handles EXIF orientation, downscales if needed, and blits to RenderTexture.
    /// </summary>
    public class ImageSource : IMediaSource
    {
        private Texture2D _texture;
        private RenderTexture _outputRT;
        private string _path;
        private bool _isReady;
        private bool _disposed;

        public MediaType Type => MediaType.Image;
        public RenderTexture OutputRT => _outputRT;
        public bool IsReady => _isReady;
        public string SourcePath => _path;
        public Vector2Int NativeResolution { get; private set; }

        public ImageSource(string path, int maxSize = 4096)
        {
            _path = path;
            NativeResolution = Vector2Int.zero;

            if (!ServiceLocator.TryGet<ICoroutineRunner>(out var runner))
            {
                Debug.LogError("[ImageSource] ICoroutineRunner not found. Cannot load image asynchronously.");
                return;
            }

            runner.RunCoroutine(LoadImageCoroutine(path, maxSize));
        }

        private IEnumerator LoadImageCoroutine(string path, int maxSize)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[ImageSource] File not found: {path}");
                yield break;
            }

            var url = path.StartsWith("file:") || path.StartsWith("http") ? path
                : path.StartsWith("/") ? "file://" + path
                : "file:///" + path.Replace("\\", "/");
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[ImageSource] Failed to load: {request.error}");
                    yield break;
                }

                var texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    Debug.LogError("[ImageSource] Downloaded texture is null.");
                    yield break;
                }

                var width = texture.width;
                var height = texture.height;

                // Apply EXIF orientation if needed
                var orientation = ExifHelper.GetOrientation(path);
                if (orientation != 1)
                {
                    ApplyExifOrientation(texture, orientation);
                    width = texture.width;
                    height = texture.height;
                }

                // Downscale if larger than maxSize on any dimension
                if (width > maxSize || height > maxSize)
                {
                    var scale = Mathf.Min((float)maxSize / width, (float)maxSize / height);
                    var newWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
                    var newHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));
                    var resized = ResizeTexture(texture, newWidth, newHeight);
                    if (texture != resized)
                    {
                        UnityEngine.Object.Destroy(texture);
                        texture = resized;
                    }
                    width = newWidth;
                    height = newHeight;
                }

                _texture = texture;
                NativeResolution = new Vector2Int(width, height);

                _outputRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
                _outputRT.Create();
                Graphics.Blit(texture, _outputRT);

                _isReady = true;
            }
        }

        private static void ApplyExifOrientation(Texture2D texture, int orientation)
        {
            switch (orientation)
            {
                case 2:
                    FlipTexture(texture, true, false);
                    break;
                case 3:
                    RotateTexture180(texture);
                    break;
                case 4:
                    FlipTexture(texture, false, true);
                    break;
                case 5:
                    FlipTexture(texture, true, false);
                    RotateTexture90(texture, true);
                    break;
                case 6:
                    RotateTexture90(texture, true);
                    break;
                case 7:
                    FlipTexture(texture, true, false);
                    RotateTexture90(texture, false);
                    break;
                case 8:
                    RotateTexture90(texture, false);
                    break;
            }
        }

        private static void FlipTexture(Texture2D texture, bool horizontal, bool vertical)
        {
            var pixels = texture.GetPixels();
            var w = texture.width;
            var h = texture.height;
            var result = new Color[pixels.Length];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var srcX = horizontal ? w - 1 - x : x;
                    var srcY = vertical ? h - 1 - y : y;
                    result[y * w + x] = pixels[srcY * w + srcX];
                }
            }
            texture.SetPixels(result);
            texture.Apply();
        }

        private static void RotateTexture180(Texture2D texture)
        {
            var pixels = texture.GetPixels();
            var w = texture.width;
            var h = texture.height;
            var result = new Color[pixels.Length];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    result[y * w + x] = pixels[(h - 1 - y) * w + (w - 1 - x)];
                }
            }
            texture.SetPixels(result);
            texture.Apply();
        }

        private static void RotateTexture90(Texture2D texture, bool clockwise)
        {
            var pixels = texture.GetPixels();
            var w = texture.width;
            var h = texture.height;
            var newWidth = h;
            var newHeight = w;
            var result = new Color[newWidth * newHeight];

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    int dstX, dstY;
                    if (clockwise)
                    {
                        dstX = h - 1 - y;
                        dstY = x;
                    }
                    else
                    {
                        dstX = y;
                        dstY = w - 1 - x;
                    }
                    result[dstY * newWidth + dstX] = pixels[y * w + x];
                }
            }

            texture.Resize(newWidth, newHeight);
            texture.SetPixels(result);
            texture.Apply();
        }

        private static Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
        {
            var rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            var result = new Texture2D(newWidth, newHeight);
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        public void Play() { }
        public void Pause() { }
        public void Stop() { }
        public void SetLoop(bool loop) { }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_outputRT != null)
            {
                _outputRT.Release();
                _outputRT = null;
            }

            if (_texture != null)
            {
                UnityEngine.Object.Destroy(_texture);
                _texture = null;
            }

            _isReady = false;
        }

        /// <summary>
        /// Simple EXIF orientation reader. Parses JPEG APP1 segment for orientation tag (0x0112).
        /// Returns 1-8 for orientation, or 1 if not found/parse failed.
        /// </summary>
        private static class ExifHelper
        {
            private const int OrientationTag = 0x0112;

            public static int GetOrientation(string path)
            {
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var br = new BinaryReader(fs))
                    {
                        if (br.ReadByte() != 0xFF || br.ReadByte() != 0xD8)
                            return 1;

                        while (fs.Position < fs.Length)
                        {
                            var marker = br.ReadByte();
                            if (marker != 0xFF) break;
                            var segType = br.ReadByte();
                            if (segType == 0x01) // APP1
                            {
                                var segLen = SwapBytes(br.ReadUInt16());
                                var start = fs.Position;
                                var sig = br.ReadBytes(6);
                                if (sig.Length >= 6 && sig[0] == 'E' && sig[1] == 'x' && sig[2] == 'i' && sig[3] == 'f')
                                {
                                    var tiffStart = start + 6;
                                    fs.Position = tiffStart;
                                    var byteOrder = br.ReadUInt16();
                                    var isLittleEndian = byteOrder == 0x4949;
                                    var magic = br.ReadUInt16();
                                    if (magic != 42) return 1;
                                    var ifd0 = ReadUInt32(br, isLittleEndian);
                                    fs.Position = tiffStart + ifd0;
                                    var numEntries = ReadUInt16(br, isLittleEndian);
                                    for (var i = 0; i < numEntries; i++)
                                    {
                                        var tag = ReadUInt16(br, isLittleEndian);
                                        if (tag == OrientationTag)
                                        {
                                            br.ReadUInt16();
                                            br.ReadUInt32();
                                            var val = ReadUInt16(br, isLittleEndian);
                                            return Mathf.Clamp(val, 1, 8);
                                        }
                                        fs.Position += 10;
                                    }
                                }
                                fs.Position = start + segLen;
                            }
                            else if (segType == 0x00)
                            {
                                break;
                            }
                            else
                            {
                                var segLen = SwapBytes(br.ReadUInt16());
                                fs.Position += segLen;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore parse errors
                }
                return 1;
            }

            private static ushort SwapBytes(ushort v) => (ushort)((v >> 8) | (v << 8));
            private static ushort ReadUInt16(BinaryReader br, bool little) => little ? br.ReadUInt16() : SwapBytes(br.ReadUInt16());
            private static uint ReadUInt32(BinaryReader br, bool little)
            {
                var bytes = br.ReadBytes(4);
                if (!little) Array.Reverse(bytes);
                return BitConverter.ToUInt32(bytes, 0);
            }
        }
    }
}
