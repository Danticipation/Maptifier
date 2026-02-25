using System;
using UnityEngine;

namespace Maptifier.Display
{
    /// <summary>
    /// Editor/mock implementation of IDisplayService. Simulates external display
    /// connection state for testing UI without a physical device.
    /// </summary>
    public class EditorDisplayService : IDisplayService
    {
        private bool _disposed;
        private bool _simulateConnected;

        public bool IsExternalDisplayConnected => _simulateConnected;
        public int ExternalDisplayId => _simulateConnected ? 1 : -1;
        public Vector2Int ExternalResolution => _simulateConnected ? new Vector2Int(1920, 1080) : default;
        public float ExternalRefreshRate => _simulateConnected ? 60f : 0f;

        public event Action<int, Vector2Int, float> OnDisplayConnected;
        public event Action<int> OnDisplayDisconnected;
        public event Action<Vector2Int> OnResolutionChanged;

        /// <summary>
        /// Toggle simulated connection state. Call from editor UI or tests.
        /// </summary>
        public void SetSimulateConnected(bool connected)
        {
            if (_simulateConnected == connected) return;

            _simulateConnected = connected;

            if (connected)
            {
                var resolution = ExternalResolution;
                var refreshRate = ExternalRefreshRate;
                Debug.Log($"[EditorDisplayService] Simulated display connected: id=1, resolution={resolution.x}x{resolution.y}, refreshRate={refreshRate:F2}Hz");
                OnDisplayConnected?.Invoke(1, resolution, refreshRate);
            }
            else
            {
                Debug.Log("[EditorDisplayService] Simulated display disconnected: id=1");
                OnDisplayDisconnected?.Invoke(1);
            }
        }

        public void Initialize()
        {
            Debug.Log("[EditorDisplayService] Initialized (mock)");
        }

        public void PollDisplayState()
        {
            if (_disposed) return;
            // Connection state is controlled by SetSimulateConnected; no automatic polling
        }

        public void PresentFrame(RenderTexture compositeRT)
        {
            // No-op in editor
        }

        public void Shutdown()
        {
            _simulateConnected = false;
            Debug.Log("[EditorDisplayService] Shutdown (mock)");
        }

        public void Dispose()
        {
            if (_disposed) return;

            Shutdown();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
