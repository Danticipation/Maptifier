using System;
using UnityEngine;

namespace Maptifier.Core
{
    public class CompositeRenderer : IDisposable
    {
        private RenderTexture _compositeRT;
        private Material _compositeMaterial;
        private Material _externalBlitMaterial;
        private readonly IRenderTexturePool _rtPool;

        public RenderTexture CompositeRT => _compositeRT;

        public CompositeRenderer(IRenderTexturePool rtPool)
        {
            _rtPool = rtPool;
        }

        public void Initialize(int width, int height)
        {
            _compositeRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "Maptifier_CompositeRT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            _compositeRT.Create();

            _compositeMaterial = new Material(Shader.Find("Maptifier/Composite"));
            // sRGB write-off material for projectors expecting linear input
            _externalBlitMaterial = new Material(Shader.Find("Maptifier/ExternalBlit"));
        }

        public void Composite(RenderTexture layerA, RenderTexture layerB, float mixValue, BlendMode blendMode)
        {
            if (_compositeMaterial == null) return;

            _compositeMaterial.SetTexture("_LayerA", layerA);
            _compositeMaterial.SetTexture("_LayerB", layerB);
            _compositeMaterial.SetFloat("_MixValue", mixValue);
            _compositeMaterial.SetInt("_BlendMode", (int)blendMode);

            Graphics.Blit(null, _compositeRT, _compositeMaterial);
        }

        public void BlitToExternal(RenderTexture target)
        {
            if (_externalBlitMaterial != null)
                Graphics.Blit(_compositeRT, target, _externalBlitMaterial);
            else
                Graphics.Blit(_compositeRT, target);
        }

        public void Dispose()
        {
            if (_compositeRT != null)
            {
                _compositeRT.Release();
                UnityEngine.Object.Destroy(_compositeRT);
            }
            if (_compositeMaterial != null) UnityEngine.Object.Destroy(_compositeMaterial);
            if (_externalBlitMaterial != null) UnityEngine.Object.Destroy(_externalBlitMaterial);
        }
    }
}
