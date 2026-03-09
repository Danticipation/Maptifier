using System.Collections.Generic;
using UnityEngine;
using Maptifier.Core;

namespace Maptifier.Effects
{
    public class EffectPipeline : IEffectPipeline
    {
        public int MaxEffectsPerLayer => 3;

        private readonly IRenderTexturePool _rtPool;

        private static readonly string[] EffectIds =
        {
            KaleidoscopeEffect.EffectId,
            TunnelEffect.EffectId,
            ColorCycleEffect.EffectId,
            WaveDistortionEffect.EffectId,
            PixelateEffect.EffectId,
            EdgeGlowEffect.EffectId,
            ChromaticAberrationEffect.EffectId,
            BlurEffect.EffectId
        };

        public EffectPipeline()
        {
            _rtPool = ServiceLocator.Get<IRenderTexturePool>();
        }

        public IEffect CreateEffect(string effectId)
        {
            return effectId switch
            {
                KaleidoscopeEffect.EffectId => new KaleidoscopeEffect(),
                TunnelEffect.EffectId => new TunnelEffect(),
                ColorCycleEffect.EffectId => new ColorCycleEffect(),
                WaveDistortionEffect.EffectId => new WaveDistortionEffect(),
                PixelateEffect.EffectId => new PixelateEffect(),
                EdgeGlowEffect.EffectId => new EdgeGlowEffect(),
                ChromaticAberrationEffect.EffectId => new ChromaticAberrationEffect(),
                BlurEffect.EffectId => new BlurEffect(),
                _ => null
            };
        }

        public List<string> GetAvailableEffectIds()
        {
            return new List<string>(EffectIds);
        }

        public void ApplyEffects(RenderTexture input, RenderTexture output, IReadOnlyList<IEffect> effects)
        {
            if (effects == null || effects.Count == 0)
            {
                Graphics.Blit(input, output);
                return;
            }

            int enabledCount = 0;
            for (int i = 0; i < effects.Count && i < MaxEffectsPerLayer; i++)
            {
                if (effects[i] != null && effects[i].IsEnabled)
                    enabledCount++;
            }

            if (enabledCount == 0)
            {
                Graphics.Blit(input, output);
                return;
            }

            int width = input.width;
            int height = input.height;
            var format = input.format;

            RenderTexture tempA = _rtPool.Get(width, height, format);
            RenderTexture tempB = _rtPool.Get(width, height, format);

            try
            {
                RenderTexture src = input;
                RenderTexture dst = tempA;
                int applied = 0;

                for (int i = 0; i < effects.Count && applied < MaxEffectsPerLayer; i++)
                {
                    var effect = effects[i];
                    if (effect == null || !effect.IsEnabled || effect.EffectMaterial == null)
                        continue;

                    for (int pass = 0; pass < effect.PassCount; pass++)
                    {
                        Graphics.Blit(src, dst, effect.EffectMaterial, pass);
                        var swap = src;
                        src = dst;
                        dst = swap;
                    }
                    applied++;
                }

                if (src != output)
                    Graphics.Blit(src, output);
            }
            finally
            {
                _rtPool.Release(tempA);
                _rtPool.Release(tempB);
            }
        }
    }
}
