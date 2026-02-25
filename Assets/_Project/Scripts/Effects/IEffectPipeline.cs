using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maptifier.Effects
{
    public interface IEffect
    {
        string Name { get; }
        string Id { get; }
        bool IsEnabled { get; set; }
        Material EffectMaterial { get; }
        int PassCount { get; }
        void SetParameter(string name, float value);
        float GetParameter(string name);
        Dictionary<string, EffectParameter> GetParameters();
        void UpdateTime(float time);
    }

    public struct EffectParameter
    {
        public string Name;
        public string DisplayName;
        public float Value;
        public float MinValue;
        public float MaxValue;
        public float DefaultValue;
    }

    public interface IEffectPipeline
    {
        int MaxEffectsPerLayer { get; }

        IEffect CreateEffect(string effectId);
        List<string> GetAvailableEffectIds();

        void ApplyEffects(RenderTexture input, RenderTexture output, IReadOnlyList<IEffect> effects);
    }
}
