using System;
using System.Collections.Generic;
using UnityEngine;
using Maptifier.Media;
using Maptifier.Warping;
using Maptifier.Masking;
using Maptifier.Drawing;
using Maptifier.Effects;

namespace Maptifier.Layers
{
    public class Layer : IDisposable
    {
        public string Id { get; }
        public IMediaSource MediaSource { get; set; }
        public RenderTexture OutputRT { get; private set; }
        public RenderTexture DrawingRT { get; private set; }
        public RenderTexture MaskRT { get; private set; }

        public float Opacity { get; set; } = 1f;
        public bool IsSolo { get; set; }
        public bool IsMuted { get; set; }

        public WarpMode WarpMode { get; set; } = WarpMode.FourCorner;
        public Vector2[] WarpCorners { get; set; }
        public Vector2[] MeshControlPoints { get; set; }
        public int MeshGridWidth { get; set; } = 4;
        public int MeshGridHeight { get; set; } = 4;

        public List<IEffect> Effects { get; } = new(3);

        private readonly int _width;
        private readonly int _height;

        public Layer(string id, int width, int height)
        {
            Id = id;
            _width = width;
            _height = height;

            OutputRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = $"Layer_{id}_Output",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            OutputRT.Create();

            DrawingRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = $"Layer_{id}_Drawing",
                filterMode = FilterMode.Bilinear
            };
            DrawingRT.Create();

            MaskRT = new RenderTexture(width, height, 0, RenderTextureFormat.R8)
            {
                name = $"Layer_{id}_Mask",
                filterMode = FilterMode.Bilinear
            };
            MaskRT.Create();

            // Initialize mask to white (fully revealed)
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = MaskRT;
            GL.Clear(true, true, Color.white);
            RenderTexture.active = prev;

            WarpCorners = new Vector2[] { new(0,0), new(1,0), new(1,1), new(0,1) };
        }

        public void RenderToOutput(IEffectPipeline effectPipeline, Material compositeLayerMat)
        {
            // Composite: media * mask + drawing, then apply effects
            RenderTexture source = OutputRT;

            // Step 1: Composite media + drawing + mask into output RT
            if (MediaSource != null && MediaSource.IsReady)
            {
                compositeLayerMat.SetTexture("_MediaTex", MediaSource.OutputRT);
            }
            else
            {
                compositeLayerMat.SetTexture("_MediaTex", Texture2D.blackTexture);
            }
            compositeLayerMat.SetTexture("_DrawingTex", DrawingRT);
            compositeLayerMat.SetTexture("_MaskTex", MaskRT);
            compositeLayerMat.SetFloat("_Opacity", Opacity);
            Graphics.Blit(null, OutputRT, compositeLayerMat);

            // Step 2: Apply effects chain
            if (Effects.Count > 0)
            {
                var tempRT = RenderTexture.GetTemporary(_width, _height, 0, RenderTextureFormat.ARGB32);
                effectPipeline.ApplyEffects(OutputRT, tempRT, Effects);
                Graphics.Blit(tempRT, OutputRT);
                RenderTexture.ReleaseTemporary(tempRT);
            }
        }

        public void Dispose()
        {
            MediaSource?.Dispose();
            foreach (var effect in Effects)
            {
                if (effect is IDisposable disposable)
                    disposable.Dispose();
            }
            Effects.Clear();

            if (OutputRT != null) { OutputRT.Release(); UnityEngine.Object.Destroy(OutputRT); }
            if (DrawingRT != null) { DrawingRT.Release(); UnityEngine.Object.Destroy(DrawingRT); }
            if (MaskRT != null) { MaskRT.Release(); UnityEngine.Object.Destroy(MaskRT); }
        }
    }
}
