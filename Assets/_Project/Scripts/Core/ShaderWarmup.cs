using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maptifier.Core
{
    /// <summary>
    /// Pre-warms shaders during splash/first launch to avoid first-frame compilation stutter.
    /// Uses ShaderVariantCollection and a single-frame render pass through the full pipeline.
    /// </summary>
    public class ShaderWarmup : MonoBehaviour
    {
        private const string ShaderWarmupDoneKey = "Maptifier_ShaderWarmupDone";

        [SerializeField] private ShaderVariantCollection _shaderVariants;

        private static readonly string[] PipelineShaders =
        {
            "Maptifier/Composite",
            "Maptifier/ExternalBlit",
            "Maptifier/LayerComposite",
            "Maptifier/FX/Kaleidoscope",
            "Maptifier/FX/Tunnel",
            "Maptifier/FX/ColorCycle",
            "Maptifier/FX/WaveDistortion",
            "Maptifier/FX/Pixelate",
            "Maptifier/FX/EdgeGlow",
            "Maptifier/FX/ChromaticAberration",
            "Maptifier/FX/DualKawaseBlur",
            "Maptifier/Drawing/BrushStamp",
            "Maptifier/Warp/ProjectiveWarp",
            "Hidden/MaptifierBrushAdd",
            "Hidden/MaptifierBrushEraser"
        };

        public bool IsFirstLaunch => PlayerPrefs.GetInt(ShaderWarmupDoneKey, 0) == 0;

        private void Awake()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            if (_shaderVariants != null)
            {
                try
                {
                    _shaderVariants.WarmUp();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ShaderWarmup] ShaderVariantCollection warmup failed: {ex.Message}");
                }
            }

            WarmUpPipelineShaders();
            sw.Stop();
            Debug.Log($"[ShaderWarmup] Shader warmup completed in {sw.ElapsedMilliseconds}ms");

            PlayerPrefs.SetInt(ShaderWarmupDoneKey, 1);
            PlayerPrefs.Save();
        }

        private void WarmUpPipelineShaders()
        {
            var materials = new List<Material>();
            RenderTexture tempA = null;
            RenderTexture tempB = null;

            try
            {
                tempA = RenderTexture.GetTemporary(16, 16, 0, RenderTextureFormat.ARGB32);
                tempB = RenderTexture.GetTemporary(16, 16, 0, RenderTextureFormat.ARGB32);
                RenderTexture.active = tempA;
                GL.Clear(true, true, Color.black);
                RenderTexture.active = null;

                foreach (var shaderName in PipelineShaders)
                {
                    var shader = Shader.Find(shaderName);
                    if (shader == null) continue;

                    try
                    {
                        var mat = new Material(shader);
                        materials.Add(mat);

                        for (var pass = 0; pass < mat.passCount; pass++)
                        {
                            Graphics.Blit(tempA, tempB, mat, pass);
                            var swap = tempA;
                            tempA = tempB;
                            tempB = swap;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ShaderWarmup] Failed to warm {shaderName}: {ex.Message}");
                    }
                }
            }
            finally
            {
                for (var i = 0; i < materials.Count; i++)
                    Destroy(materials[i]);
                if (tempA != null) RenderTexture.ReleaseTemporary(tempA);
                if (tempB != null) RenderTexture.ReleaseTemporary(tempB);
            }
        }
    }
}
