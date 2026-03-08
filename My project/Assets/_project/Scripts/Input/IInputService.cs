using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Maptifier.Input
{
    public enum GestureState { Idle, Tapping, Dragging, Pinching, Rotating }

    public readonly struct TouchData
    {
        public readonly int FingerId;
        public readonly Vector2 Position;
        public readonly Vector2 Delta;
        public readonly float Pressure;
        public readonly float Radius;
        public readonly TouchPhase Phase;

        public TouchData(int fingerId, Vector2 position, Vector2 delta, float pressure, float radius, TouchPhase phase)
        {
            FingerId = fingerId;
            Position = position;
            Delta = delta;
            Pressure = pressure;
            Radius = radius;
            Phase = phase;
        }
    }

    public interface IInputService
    {
        GestureState CurrentGesture { get; }
        int ActiveTouchCount { get; }

        event Action<Vector2> OnTap;
        event Action<Vector2, Vector2> OnDragStart;
        event Action<Vector2, Vector2> OnDragMove;
        event Action<Vector2> OnDragEnd;
        event Action<Vector2, float> OnPinch;
        event Action<Vector2, float> OnRotate;

        TouchData GetTouch(int index);
        void ProcessInput();
        Vector2 ScreenToCanvasPoint(Vector2 screenPoint, Matrix4x4 canvasTransform);
    }
}
