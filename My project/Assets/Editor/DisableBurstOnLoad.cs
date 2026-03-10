#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Disables Burst compilation on domain reload so the project can load when Burst
/// fails to resolve project assemblies (e.g. Maptifier.Masking). Re-enable via
/// Jobs > Burst > Enable Burst Compilation when the resolution issue is fixed.
/// </summary>
[InitializeOnLoad]
public static class DisableBurstOnLoad
{
    static DisableBurstOnLoad()
    {
        try
        {
            var options = Unity.Burst.BurstCompiler.Options;
            if (options.EnableBurstCompilation)
            {
                options.EnableBurstCompilation = false;
                UnityEngine.Debug.Log("[DisableBurstOnLoad] Burst compilation disabled to avoid assembly resolution errors. Re-enable via Jobs > Burst > Enable Burst Compilation when fixed.");
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[DisableBurstOnLoad] Could not set Burst option: {ex.Message}");
        }
    }
}
#endif
