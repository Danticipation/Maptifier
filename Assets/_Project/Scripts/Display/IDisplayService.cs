using System;
using UnityEngine;

namespace Maptifier.Display
{
    public interface IDisplayService : IDisposable
    {
        bool IsExternalDisplayConnected { get; }
        int ExternalDisplayId { get; }
        Vector2Int ExternalResolution { get; }
        float ExternalRefreshRate { get; }

        event Action<int, Vector2Int, float> OnDisplayConnected;
        event Action<int> OnDisplayDisconnected;
        event Action<Vector2Int> OnResolutionChanged;

        void Initialize();
        void PollDisplayState();
        void PresentFrame(RenderTexture compositeRT);
        void Shutdown();
    }
}
