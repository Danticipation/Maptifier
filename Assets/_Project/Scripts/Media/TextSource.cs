using System;
using UnityEngine;
using Maptifier.Core;

namespace Maptifier.Media
{
    /// <summary>
    /// Configuration for a text layer. Serializable for project save/load.
    /// </summary>
    [Serializable]
    public class TextConfig
    {
        public string Content = "Hello World";
        public int FontSize = 72;
        public Color TextColor = Color.white;
        public Color BackgroundColor = Color.clear;
        public Color OutlineColor = Color.black;
        public float OutlineWidth = 0f;
        public TextAnchor Alignment = TextAnchor.MiddleCenter;
        public FontStyle Style = FontStyle.Normal;
        public bool WordWrap = true;
        public float LineSpacing = 1.0f;
        public int PaddingPixels = 20;
    }

    /// <summary>
    /// Provides IMediaSource for dynamic text content. Renders text to a RenderTexture
    /// using Unity's built-in GUI/font system, allowing real-time updates.
    /// Text is rendered on a transparent background so it composites cleanly
    /// with warp, mask, and effects pipelines.
    /// </summary>
    public class TextSource : IMediaSource
    {
        private RenderTexture _outputRT;
        private Texture2D _textTexture;
        private bool _isReady;
        private bool _disposed;
        private bool _isDirty = true;
        private TextConfig _config;
        private Font _font;
        private readonly int _width;
        private readonly int _height;

        public MediaType Type => MediaType.Text;
        public RenderTexture OutputRT => _outputRT;
        public bool IsReady => _isReady;
        public string SourcePath => "text://dynamic";
        public Vector2Int NativeResolution { get; private set; }
        public TextConfig Config => _config;

        public TextSource(TextConfig config = null, int width = 1024, int height = 512)
        {
            _config = config ?? new TextConfig();
            _width = width;
            _height = height;
            NativeResolution = new Vector2Int(width, height);

            _font = Font.CreateDynamicFontFromOSFont("Roboto", _config.FontSize);
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont("sans-serif", _config.FontSize);
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Subscribe to font texture rebuild (needed for dynamic fonts)
            Font.textureRebuilt += OnFontTextureRebuilt;

            _outputRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "TextSource_Output",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _outputRT.Create();

            _textTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            RenderText();
            _isReady = true;
        }

        private void OnFontTextureRebuilt(Font changedFont)
        {
            if (changedFont == _font)
                _isDirty = true;
        }

        /// <summary>
        /// Updates the text configuration and re-renders. Call this when user edits text.
        /// </summary>
        public void UpdateConfig(TextConfig config)
        {
            _config = config ?? new TextConfig();
            _isDirty = true;
            RenderText();
        }

        /// <summary>
        /// Updates just the text content string (most common update path).
        /// </summary>
        public void UpdateContent(string text)
        {
            _config.Content = text;
            _isDirty = true;
            RenderText();
        }

        /// <summary>
        /// Call from Update() or LateUpdate() to re-render if dirty.
        /// Returns true if a re-render occurred.
        /// </summary>
        public bool RefreshIfDirty()
        {
            if (!_isDirty) return false;
            RenderText();
            return true;
        }

        private void RenderText()
        {
            if (_disposed) return;
            _isDirty = false;

            // Clear to background color
            var pixels = _textTexture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = _config.BackgroundColor;
            _textTexture.SetPixels(pixels);
            _textTexture.Apply();

            // Use RenderTexture + GL to draw text
            var prev = RenderTexture.active;
            RenderTexture.active = _outputRT;
            GL.Clear(true, true, _config.BackgroundColor);

            // Set up GUI rendering to the RT
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, _width, _height, 0);

            var style = new GUIStyle
            {
                font = _font,
                fontSize = _config.FontSize,
                fontStyle = _config.Style,
                alignment = _config.Alignment,
                wordWrap = _config.WordWrap,
                normal = { textColor = _config.TextColor },
                padding = new RectOffset(
                    _config.PaddingPixels, _config.PaddingPixels,
                    _config.PaddingPixels, _config.PaddingPixels)
            };

            var rect = new Rect(0, 0, _width, _height);

            // Draw outline/shadow if configured
            if (_config.OutlineWidth > 0 && _config.OutlineColor.a > 0)
            {
                var outlineStyle = new GUIStyle(style)
                {
                    normal = { textColor = _config.OutlineColor }
                };

                float ow = _config.OutlineWidth;
                var offsets = new Vector2[]
                {
                    new(-ow, -ow), new(0, -ow), new(ow, -ow),
                    new(-ow, 0),                  new(ow, 0),
                    new(-ow, ow),  new(0, ow),   new(ow, ow)
                };

                foreach (var offset in offsets)
                {
                    var outlineRect = new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height);
                    GUI.Label(outlineRect, _config.Content, outlineStyle);
                }
            }

            // Draw main text
            GUI.Label(rect, _config.Content, style);

            GL.PopMatrix();
            RenderTexture.active = prev;
        }

        public void Play() { }
        public void Pause() { }
        public void Stop() { }
        public void SetLoop(bool loop) { }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Font.textureRebuilt -= OnFontTextureRebuilt;

            if (_outputRT != null)
            {
                _outputRT.Release();
                UnityEngine.Object.Destroy(_outputRT);
                _outputRT = null;
            }

            if (_textTexture != null)
            {
                UnityEngine.Object.Destroy(_textTexture);
                _textTexture = null;
            }

            _isReady = false;
        }
    }
}
