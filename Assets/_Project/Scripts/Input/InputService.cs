using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Maptifier.Input
{
    public class InputService : IInputService
    {
        private const int MaxTouches = 10;
        private const float TapMaxDuration = 0.2f;
        private const float TapMaxMovement = 20f;
        private const float PressureVelocityScale = 1000f;

        private readonly TouchData[] _touches = new TouchData[MaxTouches];
        private int _touchCount;
        private GestureState _gestureState;
        private bool _enhancedTouchEnabled;

        private float _singleTouchStartTime;
        private Vector2 _singleTouchStartPos;
        private Vector2 _lastSingleTouchPos;
        private bool _singleTouchBegan;

        private Vector2 _twoFingerCenter;
        private float _twoFingerDistance;
        private float _twoFingerAngle;
        private bool _twoFingerBegan;

        public GestureState CurrentGesture => _gestureState;
        public int ActiveTouchCount => _touchCount;

        public event Action<Vector2> OnTap;
        public event Action<Vector2, Vector2> OnDragStart;
        public event Action<Vector2, Vector2> OnDragMove;
        public event Action<Vector2> OnDragEnd;
        public event Action<Vector2, float> OnPinch;
        public event Action<Vector2, float> OnRotate;

        public InputService()
        {
            if (!_enhancedTouchEnabled)
            {
                EnhancedTouchSupport.Enable();
                _enhancedTouchEnabled = true;
            }
        }

        public TouchData GetTouch(int index)
        {
            if (index < 0 || index >= _touchCount)
                return default;
            return _touches[index];
        }

        public void ProcessInput()
        {
            if (!_enhancedTouchEnabled)
            {
                EnhancedTouchSupport.Enable();
                _enhancedTouchEnabled = true;
            }

            var fingers = Touch.activeFingers;
            _touchCount = Mathf.Min(fingers.Count, MaxTouches);

            for (int i = 0; i < _touchCount; i++)
            {
                var finger = fingers[i];
                var touch = finger.currentTouch;
                var pos = touch.screenPosition;
                var delta = touch.delta;
                var pressure = GetPressure(touch, delta);
                var radiusVec = touch.radius;
                var radius = (radiusVec.x + radiusVec.y) * 0.5f;

                _touches[i] = new TouchData(finger.index, pos, delta, pressure, radius, touch.phase);
            }

            UpdateGestureState(fingers);
        }

        public Vector2 ScreenToCanvasPoint(Vector2 screenPoint, Matrix4x4 canvasTransform)
        {
            var inv = canvasTransform.inverse;
            var p = new Vector4(screenPoint.x, screenPoint.y, 0f, 1f);
            var result = inv * p;
            return new Vector2(result.x, result.y);
        }

        private float GetPressure(UnityEngine.InputSystem.EnhancedTouch.Touch touch, Vector2 delta)
        {
            var p = touch.pressure;
            if (p > 0f && p < 1f)
                return Mathf.Clamp01(p);

            var velocity = delta.magnitude / Time.deltaTime;
            if (Time.deltaTime <= 0f) return 1f;
            return Mathf.Clamp01(1f - velocity / PressureVelocityScale);
        }

        private void UpdateGestureState(ReadOnlyArray<UnityEngine.InputSystem.EnhancedTouch.Finger> fingers)
        {
            switch (fingers.Count)
            {
                case 0:
                    HandleZeroFingers();
                    break;
                case 1:
                    HandleOneFinger(fingers[0]);
                    break;
                default:
                    HandleTwoOrMoreFingers(fingers);
                    break;
            }
        }

        private void HandleZeroFingers()
        {
            if (_gestureState == GestureState.Dragging && _singleTouchBegan)
            {
                OnDragEnd?.Invoke(_lastSingleTouchPos);
            }

            _gestureState = GestureState.Idle;
            _singleTouchBegan = false;
            _twoFingerBegan = false;
        }

        private void HandleOneFinger(UnityEngine.InputSystem.EnhancedTouch.Finger finger)
        {
            var touch = finger.currentTouch;
            var pos = touch.screenPosition;
            var delta = touch.delta;

            if (touch.began)
            {
                _singleTouchStartTime = (float)Time.realtimeSinceStartup;
                _singleTouchStartPos = pos;
                _lastSingleTouchPos = pos;
                _singleTouchBegan = true;
                _gestureState = GestureState.Tapping;
            }
            else if (_singleTouchBegan)
            {
                if (touch.ended || touch.canceled)
                {
                    var duration = (float)Time.realtimeSinceStartup - _singleTouchStartTime;
                    var movement = Vector2.Distance(pos, _singleTouchStartPos);

                    if (duration < TapMaxDuration && movement < TapMaxMovement)
                    {
                        _gestureState = GestureState.Tapping;
                        OnTap?.Invoke(pos);
                    }
                    else if (_gestureState == GestureState.Dragging)
                    {
                        OnDragEnd?.Invoke(pos);
                    }

                    _singleTouchBegan = false;
                    _gestureState = GestureState.Idle;
                }
                else if (touch.inProgress)
                {
                    var duration = (float)Time.realtimeSinceStartup - _singleTouchStartTime;
                    var movement = Vector2.Distance(pos, _singleTouchStartPos);
                    if (movement >= TapMaxMovement || duration > TapMaxDuration)
                    {
                        if (_gestureState == GestureState.Tapping)
                        {
                            _gestureState = GestureState.Dragging;
                            OnDragStart?.Invoke(_singleTouchStartPos, pos);
                        }
                        else if (_gestureState == GestureState.Dragging)
                        {
                            OnDragMove?.Invoke(_lastSingleTouchPos, pos);
                        }
                    }
                    _lastSingleTouchPos = pos;
                }
            }
            }
        }

        private void HandleTwoOrMoreFingers(ReadOnlyArray<UnityEngine.InputSystem.EnhancedTouch.Finger> fingers)
        {
            var f0 = fingers[0];
            var f1 = fingers[1];
            var p0 = f0.currentTouch.screenPosition;
            var p1 = f1.currentTouch.screenPosition;

            var center = (p0 + p1) * 0.5f;
            var delta = p1 - p0;
            var distance = delta.magnitude;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            if (!_twoFingerBegan)
            {
                _twoFingerCenter = center;
                _twoFingerDistance = distance;
                _twoFingerAngle = angle;
                _twoFingerBegan = true;
                _singleTouchBegan = false;
            }

            var distDelta = distance - _twoFingerDistance;
            var angleDelta = Mathf.DeltaAngle(_twoFingerAngle, angle);

            if (Mathf.Abs(distDelta) > 0.1f)
            {
                _gestureState = GestureState.Pinching;
                var scale = distance > 0.001f ? distance / _twoFingerDistance : 1f;
                OnPinch?.Invoke(center, scale);
            }

            if (Mathf.Abs(angleDelta) > 0.1f)
            {
                _gestureState = GestureState.Rotating;
                OnRotate?.Invoke(center, angleDelta);
            }

            if (_gestureState != GestureState.Pinching && _gestureState != GestureState.Rotating)
            {
                _gestureState = GestureState.Pinching;
            }

            _twoFingerDistance = distance;
            _twoFingerAngle = angle;
        }
    }
}
