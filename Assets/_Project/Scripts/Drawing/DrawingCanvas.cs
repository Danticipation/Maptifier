using System.Collections.Generic;
using UnityEngine;
using Maptifier.Core;
using Maptifier.Input;

namespace Maptifier.Drawing
{
    /// <summary>
    /// MonoBehaviour that manages a drawing canvas per layer. Subscribes to input events
    /// when the Draw tool is active, and provides the drawing RT for compositing.
    /// </summary>
    public class DrawingCanvas : MonoBehaviour
    {
        [SerializeField] private int _layerIndex;
        [SerializeField] private int _canvasWidth = 1920;
        [SerializeField] private int _canvasHeight = 1080;
        [SerializeField] private int _maxUndoCount = 20;
        [SerializeField] private CanvasViewport _canvasViewport;

        private IDrawingService _drawingService;
        private IInputService _inputService;
        private RenderTexture _drawingRT;
        private RingBuffer<RenderTexture> _undoBuffer;
        private RingBuffer<RenderTexture> _redoBuffer;
        private ToolType _currentTool = ToolType.Select;
        private bool _servicesReady;

        public RenderTexture DrawingRT => _drawingRT;
        public int LayerIndex => _layerIndex;

        private void Start()
        {
            if (ServiceLocator.TryGet<IDrawingService>(out _drawingService) &&
                ServiceLocator.TryGet<IInputService>(out _inputService))
            {
                _servicesReady = true;
                _undoBuffer = new RingBuffer<RenderTexture>(_maxUndoCount, OnSnapshotEvicted);
                _redoBuffer = new RingBuffer<RenderTexture>(_maxUndoCount, OnSnapshotEvicted);
                _drawingService.Initialize(_canvasWidth, _canvasHeight);
                CreateDrawingRT();
                SubscribeToInput();
                SubscribeToEvents();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromInput();
            UnsubscribeFromEvents();
            ReleaseDrawingRT();
            _undoBuffer?.Clear();
            _redoBuffer?.Clear();
        }

        private static void OnSnapshotEvicted(RenderTexture rt)
        {
            if (rt != null) { rt.Release(); Object.Destroy(rt); }
        }

        private void CreateDrawingRT()
        {
            if (_drawingRT != null) return;

            _drawingRT = new RenderTexture(_canvasWidth, _canvasHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = $"Maptifier_DrawingRT_Layer{_layerIndex}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            _drawingRT.Create();

            ClearDrawing();
        }

        private void ReleaseDrawingRT()
        {
            if (_drawingRT != null)
            {
                _drawingRT.Release();
                Destroy(_drawingRT);
                _drawingRT = null;
            }
            if (_undoBuffer != null)
            {
                foreach (var rt in _undoBuffer.Items)
                {
                    if (rt != null) { rt.Release(); Destroy(rt); }
                }
                _undoBuffer.Clear();
            }
            if (_redoBuffer != null)
            {
                foreach (var rt in _redoBuffer.Items)
                {
                    if (rt != null) { rt.Release(); Destroy(rt); }
                }
                _redoBuffer.Clear();
            }
        }

        private void SubscribeToInput()
        {
            if (_inputService == null) return;

            _inputService.OnDragStart += HandleDragStart;
            _inputService.OnDragMove += HandleDragMove;
            _inputService.OnDragEnd += HandleDragEnd;
        }

        private void UnsubscribeFromInput()
        {
            if (_inputService != null)
            {
                _inputService.OnDragStart -= HandleDragStart;
                _inputService.OnDragMove -= HandleDragMove;
                _inputService.OnDragEnd -= HandleDragEnd;
            }
        }

        private void SubscribeToEvents()
        {
            EventBus.Subscribe<ToolChangedEvent>(OnToolChanged);
            EventBus.Subscribe<LayerSelectedEvent>(OnLayerSelected);
        }

        private void UnsubscribeFromEvents()
        {
            EventBus.Unsubscribe<ToolChangedEvent>(OnToolChanged);
            EventBus.Unsubscribe<LayerSelectedEvent>(OnLayerSelected);
        }

        private void OnToolChanged(ToolChangedEvent evt)
        {
            _currentTool = evt.Tool;
        }

        private void OnLayerSelected(LayerSelectedEvent evt)
        {
            // Could enable/disable drawing based on selected layer
        }

        private void HandleDragStart(Vector2 from, Vector2 to)
        {
            if (_currentTool != ToolType.Draw || !_servicesReady || _drawingRT == null) return;

            var canvasPoint = ScreenToCanvasPoint(to);
            var pressure = GetCurrentPressure();

            var snapshot = _drawingService.CaptureSnapshot(_drawingRT);
            if (snapshot != null)
            {
                _redoBuffer.Clear();
                PushUndo(snapshot);
            }

            _drawingService.BeginStroke(_drawingRT, canvasPoint, pressure);
        }

        private void HandleDragMove(Vector2 from, Vector2 to)
        {
            if (_currentTool != ToolType.Draw || !_servicesReady || _drawingRT == null) return;

            var canvasPoint = ScreenToCanvasPoint(to);
            var pressure = GetCurrentPressure();

            _drawingService.ContinueStroke(_drawingRT, canvasPoint, pressure);
        }

        private void HandleDragEnd(Vector2 position)
        {
            if (_currentTool != ToolType.Draw || !_servicesReady) return;

            _drawingService.EndStroke(_drawingRT);
        }

        private Vector2 ScreenToCanvasPoint(Vector2 screenPoint)
        {
            if (_canvasViewport == null || _inputService == null)
                return screenPoint;

            var canvasPoint = _inputService.ScreenToCanvasPoint(screenPoint, _canvasViewport.CanvasTransform);

            // Map from canvas local space (typically -width/2 to width/2 with center pivot) to RT pixel space (0 to width, 0 to height)
            var x = canvasPoint.x + _canvasWidth * 0.5f;
            var y = canvasPoint.y + _canvasHeight * 0.5f;

            return new Vector2(x, y);
        }

        private float GetCurrentPressure()
        {
            if (_inputService == null || _inputService.ActiveTouchCount == 0)
                return 1f;

            var touch = _inputService.GetTouch(0);
            return touch.Pressure;
        }

        public void Undo()
        {
            if (!_servicesReady || _drawingRT == null) return;
            if (_drawingService.IsDrawing) return;

            var snapshot = _undoBuffer.PopBack();
            if (snapshot == null) return;

            var currentSnapshot = _drawingService.CaptureSnapshot(_drawingRT);
            if (currentSnapshot != null)
                _redoBuffer.PushBack(currentSnapshot);

            _drawingService.RestoreSnapshot(_drawingRT, snapshot);
            snapshot.Release();
            Destroy(snapshot);
        }

        public void Redo()
        {
            if (!_servicesReady || _drawingRT == null) return;
            if (_drawingService.IsDrawing) return;

            var snapshot = _redoBuffer.PopBack();
            if (snapshot == null) return;

            var currentSnapshot = _drawingService.CaptureSnapshot(_drawingRT);
            if (currentSnapshot != null)
                _undoBuffer.PushBack(currentSnapshot);

            _drawingService.RestoreSnapshot(_drawingRT, snapshot);
            snapshot.Release();
            Destroy(snapshot);
        }

        public void ClearDrawing()
        {
            if (_drawingService != null && _drawingRT != null)
                _drawingService.Clear(_drawingRT);
        }

        private void PushUndo(RenderTexture snapshot)
        {
            _undoBuffer.PushBack(snapshot);
        }

        /// <summary>
        /// Sets the canvas resolution. Call before Start or when layer resolution changes.
        /// </summary>
        public void SetResolution(int width, int height)
        {
            if (_canvasWidth == width && _canvasHeight == height) return;

            _canvasWidth = width;
            _canvasHeight = height;

            if (_drawingRT != null)
            {
                ReleaseDrawingRT();
                if (_drawingService != null)
                {
                    _drawingService.Initialize(_canvasWidth, _canvasHeight);
                    CreateDrawingRT();
                }
            }
        }

        private class RingBuffer<T> where T : class
        {
            private readonly List<T> _items = new List<T>();
            private readonly int _capacity;
            private readonly System.Action<T> _onEvict;

            public RingBuffer(int capacity, System.Action<T> onEvict = null)
            {
                _capacity = Mathf.Max(1, capacity);
                _onEvict = onEvict;
            }

            public T PushBack(T item)
            {
                T evicted = null;
                if (_items.Count >= _capacity)
                {
                    evicted = _items[0];
                    _items.RemoveAt(0);
                    _onEvict?.Invoke(evicted);
                }
                _items.Add(item);
                return evicted;
            }

            public T PopBack()
            {
                if (_items.Count == 0) return null;
                var idx = _items.Count - 1;
                var item = _items[idx];
                _items.RemoveAt(idx);
                return item;
            }

            public void Clear()
            {
                _items.Clear();
            }

            public IReadOnlyList<T> Items => _items;
        }
    }
}
