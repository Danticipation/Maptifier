using System;
using UnityEngine;
using UnityEngine.Video;

namespace Maptifier.Media
{
    /// <summary>
    /// Provides IMediaSource for video. Uses VideoPlayer with RenderTexture output.
    /// Resolution capped at 1920x1080 for performance.
    /// </summary>
    public class VideoSource : IMediaSource
    {
        private const int MaxWidth = 1920;
        private const int MaxHeight = 1080;

        private GameObject _helperObject;
        private VideoPlayer _videoPlayer;
        private RenderTexture _outputRT;
        private string _path;
        private bool _disposed;

        public MediaType Type => MediaType.Video;
        public RenderTexture OutputRT => _outputRT;
        public bool IsReady => _videoPlayer != null && _videoPlayer.isPrepared;
        public string SourcePath => _path;
        public Vector2Int NativeResolution { get; private set; }

        public VideoSource(string path)
        {
            _path = path;
            NativeResolution = Vector2Int.zero;

            _helperObject = new GameObject("[VideoSource]");
            _helperObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(_helperObject);

            _videoPlayer = _helperObject.AddComponent<VideoPlayer>();
            _videoPlayer.source = VideoSourceType.Url;
            _videoPlayer.url = path;
            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = false;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;

            _videoPlayer.prepareCompleted += OnPrepareCompleted;
            _videoPlayer.Prepare();
        }

        private void OnPrepareCompleted(VideoPlayer source)
        {
            var width = (int)source.width;
            var height = (int)source.height;

            if (width <= 0 || height <= 0)
            {
                width = 1920;
                height = 1080;
            }

            if (width > MaxWidth || height > MaxHeight)
            {
                var scale = Mathf.Min((float)MaxWidth / width, (float)MaxHeight / height);
                width = Mathf.RoundToInt(width * scale);
                height = Mathf.RoundToInt(height * scale);
            }

            NativeResolution = new Vector2Int(width, height);
            _outputRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            _outputRT.Create();
            _videoPlayer.targetTexture = _outputRT;
        }

        public void Play() => _videoPlayer?.Play();
        public void Pause() => _videoPlayer?.Pause();
        public void Stop() => _videoPlayer?.Stop();
        public void SetLoop(bool loop)
        {
            if (_videoPlayer != null)
                _videoPlayer.isLooping = loop;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_videoPlayer != null)
            {
                _videoPlayer.prepareCompleted -= OnPrepareCompleted;
                _videoPlayer.Stop();
                _videoPlayer.targetTexture = null;
                _videoPlayer = null;
            }

            if (_outputRT != null)
            {
                _outputRT.Release();
                _outputRT = null;
            }

            if (_helperObject != null)
            {
                UnityEngine.Object.Destroy(_helperObject);
                _helperObject = null;
            }
        }
    }
}
