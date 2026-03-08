using System;
using UnityEngine;

namespace Maptifier.Drawing
{
    public interface IDrawingService
    {
        Color BrushColor { get; set; }
        float BrushSize { get; set; }
        float BrushOpacity { get; set; }
        bool IsEraser { get; set; }
        bool IsDrawing { get; }

        void Initialize(int width, int height);
        void BeginStroke(RenderTexture canvas, Vector2 position, float pressure);
        void ContinueStroke(RenderTexture canvas, Vector2 position, float pressure);
        void EndStroke(RenderTexture canvas);
        void Clear(RenderTexture canvas);

        // Undo support - snapshots
        RenderTexture CaptureSnapshot(RenderTexture canvas);
        void RestoreSnapshot(RenderTexture canvas, RenderTexture snapshot);

        void Dispose();
    }
}
