using UnityEngine;
using Maptifier.Core;

namespace Maptifier.Input
{
    /// <summary>
    /// MonoBehaviour that manages canvas pan and zoom via touch gestures.
    /// Subscribes to IInputService events for pinch zoom and 2-finger pan.
    /// </summary>
    public class CanvasViewport : MonoBehaviour
    {
        private const float MinZoom = 0.25f;
        private const float MaxZoom = 4f;
        private const float InertiaDecay = 0.92f;
        private const float InertiaThreshold = 0.5f;

        [SerializeField] private RectTransform _viewportRect;
        [SerializeField] private RectTransform _canvasRect;

        private IInputService _inputService;
        private Matrix4x4 _canvasTransform;
        private float _zoom = 1f;
        private Vector2 _pan;
        private Vector2 _velocity;
        private bool _hasInputService;

        public Matrix4x4 CanvasTransform => _canvasTransform;
        public float Zoom => _zoom;
        public Vector2 Pan => _pan;

        private void Start()
        {
            if (ServiceLocator.TryGet<IInputService>(out _inputService))
            {
                _hasInputService = true;
                _inputService.OnPinch += HandlePinch;
                _inputService.OnDragMove += HandleDragMove;
                _inputService.OnDragEnd += HandleDragEnd;
            }
        }

        private void OnDestroy()
        {
            if (_hasInputService && _inputService != null)
            {
                _inputService.OnPinch -= HandlePinch;
                _inputService.OnDragMove -= HandleDragMove;
                _inputService.OnDragEnd -= HandleDragEnd;
            }
        }

        private void Update()
        {
            if (_inputService != null)
                _inputService.ProcessInput();

            ApplyInertia();
            RebuildTransform();
        }

        private void HandlePinch(Vector2 center, float scale)
        {
            var newZoom = Mathf.Clamp(_zoom * scale, MinZoom, MaxZoom);
            var zoomDelta = newZoom / _zoom;
            var canvasPointUnderCenter = (center - _pan) / _zoom;
            _zoom = newZoom;
            _pan = center - canvasPointUnderCenter * _zoom;
        }

        private void HandleDragMove(Vector2 from, Vector2 to)
        {
            if (_inputService != null && _inputService.ActiveTouchCount == 2)
            {
                var delta = to - from;
                _pan += delta;
                _velocity = delta / Time.deltaTime;
                if (Time.deltaTime <= 0f) _velocity = Vector2.zero;
            }
        }

        private void HandleDragEnd(Vector2 position)
        {
            // Velocity from last OnDragMove is used for inertia in ApplyInertia
        }

        private void ApplyInertia()
        {
            if (_velocity.sqrMagnitude < InertiaThreshold * InertiaThreshold)
            {
                _velocity = Vector2.zero;
                return;
            }

            _pan += _velocity * Time.deltaTime;
            _velocity *= InertiaDecay;
        }

        private void RebuildTransform()
        {
            ClampToBounds();

            _canvasTransform = Matrix4x4.TRS(
                new Vector3(_pan.x, _pan.y, 0f),
                Quaternion.identity,
                new Vector3(_zoom, _zoom, 1f)
            );
        }

        private void ClampToBounds()
        {
            if (_viewportRect == null || _canvasRect == null) return;

            var viewportSize = _viewportRect.rect.size;
            var canvasSize = _canvasRect.rect.size * _zoom;

            var maxPanX = Mathf.Max(0f, (canvasSize.x - viewportSize.x) * 0.5f);
            var maxPanY = Mathf.Max(0f, (canvasSize.y - viewportSize.y) * 0.5f);

            _pan.x = Mathf.Clamp(_pan.x, -maxPanX, maxPanX);
            _pan.y = Mathf.Clamp(_pan.y, -maxPanY, maxPanY);
        }
    }
}
