using UnityEngine;

namespace Maptifier.Core
{
    public class AdaptiveQualityService : IAdaptiveQuality
    {
        private const string PerformanceModeKey = "Maptifier_PerformanceMode";
        private const float OverBudgetThresholdMs = 14f;
        private const float UnderBudgetThresholdMs = 10f;
        private const int OverBudgetFramesToStepDown = 10;
        private const int UnderBudgetFramesToStepUp = 30;

        private PerformanceTier _currentTier = PerformanceTier.Quality;
        private bool _performanceMode;
        private int _overBudgetCount;
        private int _underBudgetCount;

        public PerformanceTier CurrentTier => _currentTier;

        public bool IsPerformanceMode => _performanceMode;

        public float RTScaleFactor => _currentTier switch
        {
            PerformanceTier.Quality => 1.0f,
            PerformanceTier.Balanced => 0.75f,
            PerformanceTier.Performance => 0.5f,
            _ => 1.0f
        };

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
            EventBus.Publish(new PerformanceTierChangedEvent(tier));
        }
    }
}
