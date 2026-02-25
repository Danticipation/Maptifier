using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maptifier.Effects
{
    public abstract class BaseEffect : IEffect
    {
        public abstract string Name { get; }
        public abstract string Id { get; }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        protected Material _material;
        protected readonly Dictionary<string, EffectParameter> _parameters = new();

        private bool _disposed;

        public Material EffectMaterial => _material;

        public virtual int PassCount => 1;

        protected BaseEffect(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[{GetType().Name}] Shader not found: {shaderName}");
                return;
            }
            _material = new Material(shader);
        }

        protected void AddParameter(string name, string displayName, float min, float max, float defaultValue)
        {
            _parameters[name] = new EffectParameter
            {
                Name = name,
                DisplayName = displayName,
                Value = defaultValue,
                MinValue = min,
                MaxValue = max,
                DefaultValue = defaultValue
            };
            if (_material != null && _material.HasProperty(name))
                _material.SetFloat(name, defaultValue);
        }

        public void SetParameter(string name, float value)
        {
            if (!_parameters.TryGetValue(name, out var param))
                return;

            value = Mathf.Clamp(value, param.MinValue, param.MaxValue);
            param.Value = value;
            _parameters[name] = param;

            if (_material != null && _material.HasProperty(name))
                _material.SetFloat(name, value);
        }

        public float GetParameter(string name)
        {
            return _parameters.TryGetValue(name, out var param) ? param.Value : 0f;
        }

        public Dictionary<string, EffectParameter> GetParameters()
        {
            return new Dictionary<string, EffectParameter>(_parameters);
        }

        public void UpdateTime(float time)
        {
            if (_material != null && _material.HasProperty("_TimeParams"))
            {
                float s = Mathf.Sin(time);
                float c = Mathf.Cos(time);
                _material.SetVector("_TimeParams", new Vector4(time, s, c, 0f));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_material != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_material);
                else
                    UnityEngine.Object.DestroyImmediate(_material);
                _material = null;
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
