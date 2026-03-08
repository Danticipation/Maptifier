using System;
using UnityEngine;
using Maptifier.Core;
using Maptifier.Effects;

namespace Maptifier.Layers
{
    /// <summary>
    /// Manages two layers (A and B) with mix, blend, opacity, solo, and mute controls.
    /// Renders each layer to its output RT, then composites via CompositeRenderer.
    /// </summary>
    public class LayerManager : ILayerManager
    {
        private Layer _layerA;
        private Layer _layerB;
        private int _activeLayerIndex;
        private float _mixValue = 0.5f;
        private BlendMode _currentBlendMode = BlendMode.Normal;

        private CompositeRenderer _compositeRenderer;
        private IEffectPipeline _effectPipeline;
        private Material _layerCompositeMaterial;
        private bool _initialized;
        private bool _disposed;

        public Layer LayerA => _layerA;
        public Layer LayerB => _layerB;

        public int ActiveLayerIndex
        {
            get => _activeLayerIndex;
            set => _activeLayerIndex = Mathf.Clamp(value, 0, 1);
        }

        public Layer ActiveLayer => _activeLayerIndex == 0 ? _layerA : _layerB;

        public float MixValue
        {
            get => _mixValue;
            set => _mixValue = Mathf.Clamp01(value);
        }

        public BlendMode CurrentBlendMode
        {
            get => _currentBlendMode;
            set => _currentBlendMode = value;
        }

        public RenderTexture CompositeOutput =>
            _compositeRenderer != null ? _compositeRenderer.CompositeRT : null;

        public void Initialize(int width, int height)
        {
            if (_initialized) return;

            _compositeRenderer = ServiceLocator.Get<CompositeRenderer>();
            _effectPipeline = ServiceLocator.Get<IEffectPipeline>();

            var shader = Shader.Find("Maptifier/LayerComposite");
            if (shader == null)
            {
                Debug.LogError("[LayerManager] Maptifier/LayerComposite shader not found.");
                return;
            }
            _layerCompositeMaterial = new Material(shader);

            _compositeRenderer.Initialize(width, height);

            _layerA = new Layer("A", width, height);
            _layerB = new Layer("B", width, height);

            _initialized = true;
        }

        public void Reinitialize(int width, int height)
        {
            if (!_initialized) return;

            _layerA?.Dispose();
            _layerB?.Dispose();
            _layerA = new Layer("A", width, height);
            _layerB = new Layer("B", width, height);
        }

        public void RenderFrame()
        {
            if (!_initialized || _layerA == null || _layerB == null || _compositeRenderer == null)
                return;

            bool renderA = _layerA.IsSolo || (!_layerB.IsSolo && !_layerA.IsMuted);
            bool renderB = _layerB.IsSolo || (!_layerA.IsSolo && !_layerB.IsMuted);

            if (renderA)
                _layerA.RenderToOutput(_effectPipeline, _layerCompositeMaterial);
            else
                ClearToBlack(_layerA.OutputRT);

            if (renderB)
                _layerB.RenderToOutput(_effectPipeline, _layerCompositeMaterial);
            else
                ClearToBlack(_layerB.OutputRT);

            _compositeRenderer.Composite(
                _layerA.OutputRT,
                _layerB.OutputRT,
                _mixValue,
                _currentBlendMode);
        }

        public void SetLayerOpacity(int layerIndex, float opacity)
        {
            var layer = GetLayer(layerIndex);
            if (layer != null)
                layer.Opacity = Mathf.Clamp01(opacity);
        }

        public void SetLayerSolo(int layerIndex, bool solo)
        {
            var layer = GetLayer(layerIndex);
            if (layer != null)
                layer.IsSolo = solo;
        }

        public void SetLayerMute(int layerIndex, bool mute)
        {
            var layer = GetLayer(layerIndex);
            if (layer != null)
                layer.IsMuted = mute;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _layerA?.Dispose();
            _layerB?.Dispose();
            _layerA = null;
            _layerB = null;

            if (_layerCompositeMaterial != null)
            {
                UnityEngine.Object.Destroy(_layerCompositeMaterial);
                _layerCompositeMaterial = null;
            }

            _compositeRenderer = null;
            _effectPipeline = null;
            _initialized = false;
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private Layer GetLayer(int layerIndex)
        {
            return layerIndex == 0 ? _layerA : layerIndex == 1 ? _layerB : null;
        }

        private static void ClearToBlack(RenderTexture rt)
        {
            if (rt == null) return;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prev;
        }
    }
}
