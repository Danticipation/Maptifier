using NUnit.Framework;
using UnityEngine;
using Maptifier.Masking;

namespace Maptifier.Tests
{
    public class MaskServiceSerializationTests
    {
        [Test]
        public void SerializeAndDeserialize_BrushMask_RoundTrips()
        {
            var maskService = new MaskService();
            var rt = maskService.GetMaskRT(128, 128);

            // Draw a simple brush stroke
            var center = new Vector2(0.5f, 0.5f);
            maskService.SetMode(MaskMode.Brush);
            maskService.BeginBrushStroke(rt, center, 0.1f, 1f, eraser: false);
            maskService.EndBrushStroke();

            var data = maskService.Serialize();
            Assert.IsNotNull(data.BrushMaskPng);
            Assert.Greater(data.BrushMaskPng.Length, 0);

            // Deserialize into a new RT and ensure we wrote something non-white
            var rt2 = new RenderTexture(128, 128, 0, RenderTextureFormat.R8);
            rt2.Create();

            maskService.Deserialize(data, rt2);

            var tex = new Texture2D(128, 128, TextureFormat.R8, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt2;
            tex.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            var centerPixel = tex.GetPixel(64, 64);
            Object.DestroyImmediate(tex);
            rt2.Release();

            Assert.Less(centerPixel.r, 0.99f);
        }
    }
}

