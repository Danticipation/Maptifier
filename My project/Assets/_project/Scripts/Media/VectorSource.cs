using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using Maptifier.Core;

namespace Maptifier.Media
{
    /// <summary>
    /// Provides IMediaSource for SVG vector graphics. Parses SVG XML at runtime,
    /// rasterizes paths to a Texture2D via a lightweight software renderer,
    /// and blits to a RenderTexture for downstream compositing.
    ///
    /// Supports: basic shapes (rect, circle, ellipse, line, polyline, polygon),
    /// path elements (M, L, C, Z commands), fill colors, stroke, and viewBox scaling.
    /// Complex features (gradients, filters, text, CSS) are best-effort.
    /// </summary>
    public class VectorSource : IMediaSource
    {
        private Texture2D _rasterizedTexture;
        private RenderTexture _outputRT;
        private string _sourcePath;
        private bool _isReady;
        private bool _disposed;
        private Color _backgroundColor = Color.clear;

        public MediaType Type => MediaType.Vector;
        public RenderTexture OutputRT => _outputRT;
        public bool IsReady => _isReady;
        public string SourcePath => _sourcePath;
        public Vector2Int NativeResolution { get; private set; }

        /// <summary>
        /// Resolution to rasterize the SVG at. Higher = sharper but more memory.
        /// Default 1024x1024 is a good balance for projection mapping.
        /// </summary>
        public int RasterWidth { get; }
        public int RasterHeight { get; }

        public VectorSource(string path, int rasterWidth = 1024, int rasterHeight = 1024)
        {
            _sourcePath = path;
            RasterWidth = rasterWidth;
            RasterHeight = rasterHeight;
            NativeResolution = new Vector2Int(rasterWidth, rasterHeight);

            if (!ServiceLocator.TryGet<ICoroutineRunner>(out var runner))
            {
                Debug.LogError("[VectorSource] ICoroutineRunner not found.");
                return;
            }

            runner.RunCoroutine(LoadSVGCoroutine(path));
        }

        /// <summary>
        /// Creates a VectorSource directly from SVG byte content (for embedded/cached vectors).
        /// </summary>
        public VectorSource(byte[] svgBytes, string virtualPath, int rasterWidth = 1024, int rasterHeight = 1024)
        {
            _sourcePath = virtualPath;
            RasterWidth = rasterWidth;
            RasterHeight = rasterHeight;
            NativeResolution = new Vector2Int(rasterWidth, rasterHeight);

            var svgContent = Encoding.UTF8.GetString(svgBytes);
            RasterizeFromSvgString(svgContent);
        }

        private IEnumerator LoadSVGCoroutine(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[VectorSource] SVG file not found: {path}");
                yield break;
            }

            string svgContent;
            try
            {
                svgContent = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VectorSource] Failed to read SVG: {ex.Message}");
                yield break;
            }

            // Yield a frame to avoid blocking
            yield return null;

            RasterizeFromSvgString(svgContent);
        }

        private void RasterizeFromSvgString(string svgContent)
        {
            try
            {
                _rasterizedTexture = new Texture2D(RasterWidth, RasterHeight, TextureFormat.RGBA32, false);
                var pixels = new Color[RasterWidth * RasterHeight];

                // Fill with transparent background
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = _backgroundColor;

                // Parse SVG and render shapes
                var svgData = SVGParser.Parse(svgContent);
                SVGRasterizer.Rasterize(svgData, pixels, RasterWidth, RasterHeight);

                _rasterizedTexture.SetPixels(pixels);
                _rasterizedTexture.Apply();

                _outputRT = new RenderTexture(RasterWidth, RasterHeight, 0, RenderTextureFormat.ARGB32)
                {
                    name = $"VectorSource_{Path.GetFileNameWithoutExtension(_sourcePath)}",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                _outputRT.Create();
                Graphics.Blit(_rasterizedTexture, _outputRT);

                _isReady = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VectorSource] SVG rasterization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Re-rasterizes the SVG at a new resolution (e.g., when projector resolution changes).
        /// </summary>
        public void Rerasterize(int newWidth, int newHeight)
        {
            if (_disposed || string.IsNullOrEmpty(_sourcePath) || !File.Exists(_sourcePath)) return;

            if (_outputRT != null) { _outputRT.Release(); UnityEngine.Object.Destroy(_outputRT); }
            if (_rasterizedTexture != null) UnityEngine.Object.Destroy(_rasterizedTexture);

            var svgContent = File.ReadAllText(_sourcePath, Encoding.UTF8);

            _rasterizedTexture = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            var pixels = new Color[newWidth * newHeight];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = _backgroundColor;

            var svgData = SVGParser.Parse(svgContent);
            SVGRasterizer.Rasterize(svgData, pixels, newWidth, newHeight);

            _rasterizedTexture.SetPixels(pixels);
            _rasterizedTexture.Apply();

            NativeResolution = new Vector2Int(newWidth, newHeight);

            _outputRT = new RenderTexture(newWidth, newHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = $"VectorSource_{Path.GetFileNameWithoutExtension(_sourcePath)}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _outputRT.Create();
            Graphics.Blit(_rasterizedTexture, _outputRT);
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
                UnityEngine.Object.Destroy(_outputRT);
                _outputRT = null;
            }

            if (_rasterizedTexture != null)
            {
                UnityEngine.Object.Destroy(_rasterizedTexture);
                _rasterizedTexture = null;
            }

            _isReady = false;
        }
    }
}
