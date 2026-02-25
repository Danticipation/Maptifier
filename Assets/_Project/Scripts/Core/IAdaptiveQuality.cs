namespace Maptifier.Core
{
    public interface IAdaptiveQuality
    {
        PerformanceTier CurrentTier { get; }
        bool IsPerformanceMode { get; }
        float RTScaleFactor { get; }
        int MaxEffectsForCurrentTier { get; }

        void SetPerformanceMode(bool enabled);
        void UpdateFrameTiming(float frameTimeMs);
    }
}
