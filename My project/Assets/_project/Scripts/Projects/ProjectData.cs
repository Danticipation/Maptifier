using System;
using System.Collections.Generic;
using UnityEngine;
using Maptifier.Core;
using Maptifier.Warping;
using Maptifier.Masking;

namespace Maptifier.Projects
{
    [Serializable]
    public class ProjectData
    {
        public string Version = "1.0";
        public string Id;
        public string Name;
        public string Created;
        public string Modified;
        public ResolutionData Resolution = new() { Width = 1920, Height = 1080 };
        public List<LayerData> Layers = new();
        public MixerData Mixer = new();
    }

    [Serializable]
    public class ResolutionData
    {
        public int Width;
        public int Height;
    }

    [Serializable]
    public class LayerData
    {
        public string Id;
        public MediaReferenceData Media;
        public WarpData Warp;
        public MaskData Mask;
        public string DrawingTexturePath;
        public List<EffectData> Effects = new();
        public float Opacity = 1f;
        public bool Muted;
    }

    [Serializable]
    public class MediaReferenceData
    {
        public string Type; // "image", "video", "vector", or "text"
        public string Path;
        public float StartTime;
        public bool Loop = true;
        public int RasterWidth;
        public int RasterHeight;

        // Text source fields
        public string TextContent;
        public int TextFontSize;
        public float TextColorR, TextColorG, TextColorB, TextColorA;
        public float BgColorR, BgColorG, BgColorB, BgColorA;
        public float OutlineColorR, OutlineColorG, OutlineColorB, OutlineColorA;
        public float OutlineWidth;
        public int TextAlignment; // maps to TextAnchor enum
        public int TextStyle; // maps to FontStyle enum
        public bool TextWordWrap;
    }

    [Serializable]
    public class WarpData
    {
        public string Type; // "fourCorner" or "meshGrid"
        public float[] CornersFlat; // flattened [x0,y0,x1,y1,...]
        public int GridWidth;
        public int GridHeight;
    }

    [Serializable]
    public class EffectData
    {
        public string EffectId;
        public bool Enabled = true;
        public List<EffectParamData> Params = new();
    }

    [Serializable]
    public class EffectParamData
    {
        public string Name;
        public float Value;
    }

    [Serializable]
    public class MixerData
    {
        public float Crossfade = 0.5f;
        public string BlendMode = "Normal";
    }
}
