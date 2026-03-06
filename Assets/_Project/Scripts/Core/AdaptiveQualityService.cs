using UnityEngine;
using System.Collections.Generic;

namespace Maptifier.Core
{
    public class AdaptiveQualityService : IAdaptiveQuality
    {
        private const string PerformanceModeKey = "Maptifier_PerformanceMode";
        private const float OverBudgetThresholdMs = 14f; // ~70 FPS target (to stay safely above 60)
        private const float UnderBudgetThresholdMs = 10f; // ~100 FPS potential
        private const int OverBudgetFramesToStepDown = 15;
        private const int UnderBudgetFramesToStepUp = 60;

        private PerformanceTier _currentTier = PerformanceTier.Quality;
        private bool _performanceMode;
        private int _overBudgetCount;
        private int _underBudgetCount;

        // Smoothing for RT scale changes
        private float _targetRTScale = 1.0f;
        private float _currentRTScale = 1.0f;

        public PerformanceTier CurrentTier => _currentTier;

        public bool IsPerformanceMode => _performanceMode;

        public float RTScaleFactor => _currentRTScale;

        public int MaxEffectsForCurrentTier => _currentTier switch
        {
            PerformanceTier.Quality => 8,
            PerformanceTier.Balanced => 4,
            PerformanceTier.Performance => 2,
            _ => 8
        };

        public AdaptiveQualityService()
        {
            _performanceMode = PlayerPrefs.GetInt(PerformanceModeKey, 0) == 1;
            if (_performanceMode)
            {
                _currentTier = PerformanceTier.Balanced;
                _targetRTScale = 0.75f;
                _currentRTScale = 0.75f;
            }
        }

        public void SetPerformanceMode(bool enabled)
        {
            if (_performanceMode == enabled) return;

            _performanceMode = enabled;
            PlayerPrefs.SetInt(PerformanceModeKey, enabled ? 1 : 0);
            PlayerPrefs.Save();

            if (enabled)
            {
                SetTier(PerformanceTier.Balanced);
            }
            else
            {
                SetTier(PerformanceTier.Quality);
            }
        }

        public void UpdateFrameTiming(float frameTimeMs)
        {
            // Update smoothed RT scale factor
            if (Mathf.Abs(_currentRTScale - _targetRTScale) > 0.01f)
            {
                _currentRTScale = Mathf.Lerp(_currentRTScale, _targetRTScale, Time.deltaTime * 2.0f);
            }

            if (_performanceMode)
            {
                _overBudgetCount = 0;
                _underBudgetCount = 0;
                return;
            }

            if (frameTimeMs > OverBudgetThresholdMs)
            {
                _overBudgetCount++;
                _underBudgetCount = 0;

                if (_overBudgetCount >= OverBudgetFramesToStepDown)
                {
                    StepDownTier();
                    _overBudgetCount = 0;
                }
            }
            else if (frameTimeMs < UnderBudgetThresholdMs)
            {
                _underBudgetCount++;
                _overBudgetCount = 0;

                if (_underBudgetCount >= UnderBudgetFramesToStepUp)
                {
                    StepUpTier();
                    _underBudgetCount = 0;
                }
            }
            else
            {
                _overBudgetCount = 0;
                _underBudgetCount = 0;
            }
        }

        private void StepDownTier()
        {
            var newTier = _currentTier switch
            {
                PerformanceTier.Quality => PerformanceTier.Balanced,
                PerformanceTier.Balanced => PerformanceTier.Performance,
                _ => _currentTier
            };

            if (newTier != _currentTier)
            {
                SetTier(newTier);
            }
        }

        private void StepUpTier()
        {
            var newTier = _currentTier switch
            {
                PerformanceTier.Performance => PerformanceTier.Balanced,
                PerformanceTier.Balanced => PerformanceTier.Quality,
                _ => _currentTier
            };

            if (newTier != _currentTier)
            {
                SetTier(newTier);
            }
        }

        private void SetTier(PerformanceTier tier)
        {
            if (_currentTier == tier) return;

            _currentTier = tier;
            _targetRTScale = tier switch
            {
                PerformanceTier.Quality => 1.0f,
                PerformanceTier.Balanced => 0.75f,
                PerformanceTier.Performance => 0.5f,
                _ => 1.0f
            };

            // Limit active effects if we step down
            if (ServiceLocator.TryGet<ILayerManager>(out var layerManager))
            {
                EnforceEffectLimits(layerManager.LayerA);
                EnforceEffectLimits(layerManager.LayerB);
            }

            EventBus.Publish(new PerformanceTierChangedEvent(tier));
            Debug.Log($"[AdaptiveQuality] Switched to {tier} tier (Target Scale: {_targetRTScale})");
        }

        private void EnforceEffectLimits(Layers.Layer layer)
        {
            if (layer == null) return;
            int limit = MaxEffectsForCurrentTier;
            if (layer.Effects.Count > limit)
            {
                // Disable effects beyond the limit rather than removing them to preserve user settings
                for (int i = limit; i < layer.Effects.Count; i++)
                {
                    layer.Effects[i].IsEnabled = false;
                }
            }
        }
    }
}
