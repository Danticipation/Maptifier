using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maptifier.Masking
{
    public enum MaskMode { Polygon, Brush }
    
    public interface IMaskService
    {
        MaskMode CurrentMode { get; }
        void SetMode(MaskMode mode);
        
        RenderTexture GetMaskRT(int width, int height);
        void ClearMask(RenderTexture maskRT);
        
        // Polygon masking
        void AddPolygonVertex(RenderTexture maskRT, Vector2 vertex);
        void ClosePolygon(RenderTexture maskRT, bool inverted = false);
        void CancelPolygon();
        List<Vector2> GetCurrentPolygonVertices();
        
        // Brush masking
        void BeginBrushStroke(RenderTexture maskRT, Vector2 position, float radius, float hardness, bool eraser);
        void ContinueBrushStroke(RenderTexture maskRT, Vector2 position);
        void EndBrushStroke();
        
        // Serialization
        MaskData Serialize();
        void Deserialize(MaskData data, RenderTexture maskRT);
    }
    
    [Serializable]
    public struct MaskData
    {
        public MaskMode Mode;
        public List<PolygonData> Polygons;
        public byte[] BrushMaskPng;
        public bool Inverted;
    }
    
    [Serializable]
    public struct PolygonData
    {
        public Vector2[] Vertices;
        public bool IsSubtractive;
    }
}
