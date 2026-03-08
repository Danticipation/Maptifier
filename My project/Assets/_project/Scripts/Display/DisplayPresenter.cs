using UnityEngine;
using Maptifier.Core;

namespace Maptifier.Display
{
    /// <summary>
    /// MonoBehaviour that bridges IDisplayService to the game loop. Polls display state,
    /// presents frames to external displays, and publishes EventBus events for connect/disconnect.
    /// </summary>
    public class DisplayPresenter : MonoBehaviour
    {
        [SerializeField] private RenderTexture _compositeRT;

        private IDisplayService _displayService;
        private bool _subscribed;

        private void Awake()
        {
            if (!ServiceLocator.TryGet<IDisplayService>(out _displayService))
            {
                Debug.LogWarning("[DisplayPresenter] IDisplayService not found in ServiceLocator.");
                return;
            }

            _displayService.Initialize();
            SubscribeToEvents();
        }

        private void Update()
        {
            _displayService?.PollDisplayState();
        }

        private void LateUpdate()
        {
            if (_displayService == null || _compositeRT == null) return;

            _displayService.PresentFrame(_compositeRT);
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            _displayService?.Shutdown();
            _displayService = null;
        }

        private void SubscribeToEvents()
        {
            if (_displayService == null || _subscribed) return;

            _displayService.OnDisplayConnected += HandleDisplayConnected;
            _displayService.OnDisplayDisconnected += HandleDisplayDisconnected;
            _subscribed = true;
        }

        private void UnsubscribeFromEvents()
        {
            if (_displayService == null || !_subscribed) return;

            _displayService.OnDisplayConnected -= HandleDisplayConnected;
            _displayService.OnDisplayDisconnected -= HandleDisplayDisconnected;
            _subscribed = false;
        }

        private void HandleDisplayConnected(int displayId, Vector2Int resolution, float refreshRate)
        {
            EventBus.Publish(new DisplayConnectedEvent(displayId, resolution, refreshRate));
        }

        private void HandleDisplayDisconnected(int displayId)
        {
            EventBus.Publish(new DisplayDisconnectedEvent(displayId));
        }
    }
}
