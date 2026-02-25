using System;
using UnityEngine;
using Maptifier.Core;

namespace Maptifier.Layers
{
    public interface ILayerManager : IDisposable
    {
        Layer LayerA { get; }
        Layer LayerB { get; }
        int ActiveLayerIndex { get; set; }
        Layer ActiveLayer { get; }

        float MixValue { get; set; }
        BlendMode CurrentBlendMode { get; set; }

        RenderTexture CompositeOutput { get; }

        void Initialize(int width, int height);
        void Reinitialize(int width, int height);
        void RenderFrame();
        void SetLayerOpacity(int layerIndex, float opacity);
        void SetLayerSolo(int layerIndex, bool solo);
        void SetLayerMute(int layerIndex, bool mute);
    }
}
