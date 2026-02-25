using System;
using UnityEngine;

namespace Maptifier.Media
{
    public enum MediaType { Image, Video, Vector, Text }

    public interface IMediaSource : IDisposable
    {
        MediaType Type { get; }
        RenderTexture OutputRT { get; }
        bool IsReady { get; }
        string SourcePath { get; }
        Vector2Int NativeResolution { get; }
        void Play();
        void Pause();
        void Stop();
        void SetLoop(bool loop);
    }

    public interface IMediaImportService
    {
        void ImportFromGallery(Action<IMediaSource> onComplete, Action<string> onError);
        IMediaSource LoadImage(string path, int maxSize = 4096);
        IMediaSource LoadVideo(string path);
        IMediaSource LoadVector(string path, int rasterWidth = 1024, int rasterHeight = 1024);
        IMediaSource LoadText(TextConfig config = null, int width = 1024, int height = 512);
        Texture2D GenerateThumbnail(IMediaSource source, int size = 256);
    }
}
