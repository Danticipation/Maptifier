using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Profiling;

namespace Maptifier.Core
{
    /// <summary>
    /// Tracks frame times, GPU timing, draw calls, and GC. Exposes stats for adaptive quality.
    /// Debug overlay (dev builds) toggled via 3-finger tap. Logs CSV for offline analysis.
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        private const int FrameBufferSize = 120;
        private const int MaxLogFiles = 5;
        private const string LogsFolder = "Logs";
        private const string LogPrefix = "perf_";
        private const string LogSuffix = ".csv";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int _touchCount;
        private float _lastTapTime;
        private const float TapWindowSeconds = 0.3f;
#endif

        private readonly float[] _frameTimes = new float[FrameBufferSize];
        private readonly float[] _gpuTimes = new float[FrameBufferSize];
        private int _frameIndex;
        private int _frameCount;
        private FrameTiming[] _frameTimings = new FrameTiming[1];
        private ProfilerRecorder _gcAllocRecorder;
        private ProfilerRecorder _drawCallsRecorder;
        private IAdaptiveQuality _adaptiveQuality;
        private bool _drawCallsRecorderValid;
        private StreamWriter _logWriter;
        private bool _logEnabled;
        private int _logFrameCounter;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _overlayVisible;
#endif

        public float AverageFps { get; private set; }
        public float Low1PercentFps { get; private set; }
        public float MaxFrameTimeMs { get; private set; }
        public float GcAllocKb { get; private set; }
        public float GpuTimeMs { get; private set; }
        public float FrameTimeMs { get; private set; }
        public int DrawCalls { get; private set; }

        private void Awake()
        {
            FrameTimingManager.CaptureFrameTimings();
            _adaptiveQuality = ServiceLocator.TryGet<IAdaptiveQuality>(out var aq) ? aq : null;

            try
            {
                _drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                _drawCallsRecorderValid = _drawCallsRecorder.Valid;
            }
            catch
            {
                _drawCallsRecorder = default;
                _drawCallsRecorderValid = false;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                _gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1,
                    ProfilerRecorderOptions.SumAllSamplesInFrame);
            }
            catch
            {
                _gcAllocRecorder = default;
            }
#endif
        }

        private void OnEnable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame);
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _gcAllocRecorder.Dispose();
#endif
            _drawCallsRecorder.Dispose();
        }

        private void Update()
        {
            var deltaMs = Time.deltaTime * 1000f;
            FrameTimeMs = deltaMs;

            _frameTimes[_frameIndex] = deltaMs;
            _frameCount = Mathf.Min(_frameCount + 1, FrameBufferSize);

            // FrameTimingManager returns data with 4-frame delay
            FrameTimingManager.CaptureFrameTimings();
            var count = FrameTimingManager.GetLatestTimings((uint)_frameTimings.Length, _frameTimings);
            var gpuMs = count > 0 ? (float)_frameTimings[0].gpuFrameTime : 0f;
            _gpuTimes[_frameIndex] = gpuMs;
            GpuTimeMs = gpuMs;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_gcAllocRecorder.Valid && _gcAllocRecorder.Capacity > 0)
            {
                var sample = _gcAllocRecorder.GetSample(0);
                GcAllocKb = sample.Count / 1024f;
            }
            else
            {
                GcAllocKb = 0f;
            }
            DetectThreeFingerTap();
#endif
            DrawCalls = _drawCallsRecorderValid && _drawCallsRecorder.Valid ? (int)_drawCallsRecorder.LastValue : 0;
            _frameIndex = (_frameIndex + 1) % FrameBufferSize;

            ComputeStats();
            _adaptiveQuality?.UpdateFrameTiming(deltaMs);

            if (_logEnabled)
                WriteLogLine(deltaMs, gpuMs);
        }

        private void ComputeStats()
        {
            if (_frameCount == 0)
            {
                AverageFps = 0;
                Low1PercentFps = 0;
                MaxFrameTimeMs = 0;
                return;
            }

            var sorted = new float[_frameCount];
            Array.Copy(_frameTimes, sorted, _frameCount);
            Array.Sort(sorted);

            var sum = 0.0;
            var max = 0f;
            for (var i = 0; i < _frameCount; i++)
            {
                sum += sorted[i];
                if (sorted[i] > max) max = sorted[i];
            }
            MaxFrameTimeMs = max;
            var avgMs = sum / _frameCount;
            AverageFps = avgMs > 0 ? 1000f / (float)avgMs : 0;

            var low1PercentIndex = Mathf.Max(0, (int)(_frameCount * 0.01) - 1);
            var low1PercentMs = sorted[low1PercentIndex];
            Low1PercentFps = low1PercentMs > 0 ? 1000f / low1PercentMs : 0;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void DetectThreeFingerTap()
        {
            if (Input.touchCount >= 3)
            {
                _touchCount = Input.touchCount;
                _lastTapTime = Time.unscaledTime;
            }
            else if (_touchCount >= 3 && Input.touchCount == 0)
            {
                if (Time.unscaledTime - _lastTapTime < TapWindowSeconds)
                {
                    _overlayVisible = !_overlayVisible;
                }
                _touchCount = 0;
            }
        }

        private void OnGUI()
        {
            if (!_overlayVisible) return;

            var w = Screen.width;
            var h = Screen.height;
            var boxW = 220;
            var boxH = 180;
            var x = w - boxW - 20;
            var y = 20;

            GUI.Box(new Rect(x, y, boxW, boxH), "Performance");
            var lineH = 18;
            var ty = y + 24;
            GUI.Label(new Rect(x + 8, ty, boxW - 16, lineH), $"FPS: {AverageFps:F1} (1% low: {Low1PercentFps:F1})");
            ty += lineH;
            GUI.Label(new Rect(x + 8, ty, boxW - 16, lineH), $"Frame: {FrameTimeMs:F2} ms");
            ty += lineH;
            GUI.Label(new Rect(x + 8, ty, boxW - 16, lineH), $"GPU: {GpuTimeMs:F2} ms");
            ty += lineH;
            GUI.Label(new Rect(x + 8, ty, boxW - 16, lineH), $"Draw calls: {DrawCalls}");
            ty += lineH;
            var rtMem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            GUI.Label(new Rect(x + 8, ty, boxW - 16, lineH), $"RT mem: {rtMem} MB");
            ty += lineH;
            GUI.Label(new Rect(x + 8, ty, boxW - 16, lineH), $"GC: {GcAllocKb:F1} KB");
            ty += lineH;
            var tier = _adaptiveQuality?.CurrentTier.ToString() ?? "N/A";
            GUI.Label(new Rect(x + 8, ty, boxW - 16, lineH), $"Quality: {tier}");
        }
#endif

        /// <summary>
        /// Enables CSV logging to Application.persistentDataPath/Logs/
        /// </summary>
        public void EnableLogging(bool enable)
        {
            if (_logEnabled == enable) return;
            _logEnabled = enable;
            if (enable)
                StartLogFile();
            else
                CloseLogFile();
        }

        private void StartLogFile()
        {
            try
            {
                var dir = Path.Combine(Application.persistentDataPath, LogsFolder);
                Directory.CreateDirectory(dir);
                RotateLogFiles(dir);
                var path = Path.Combine(dir, $"{LogPrefix}{DateTime.UtcNow:yyyyMMdd_HHmmss}{LogSuffix}");
                _logWriter = new StreamWriter(path, false) { AutoFlush = true };
                _logWriter.WriteLine("timestamp,frameTimeMs,gpuTimeMs,drawCalls,rtMemoryMB,gcAllocKB,qualityTier");
                _logFrameCounter = 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PerformanceMonitor] Failed to start log: {ex.Message}");
                _logEnabled = false;
            }
        }

        private void RotateLogFiles(string dir)
        {
            try
            {
                var files = Directory.GetFiles(dir, $"{LogPrefix}*{LogSuffix}");
                if (files.Length < MaxLogFiles) return;
                Array.Sort(files);
                for (var i = 0; i < files.Length - MaxLogFiles + 1; i++)
                    File.Delete(files[i]);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PerformanceMonitor] Log rotation failed: {ex.Message}");
            }
        }

        private void WriteLogLine(float frameTimeMs, float gpuTimeMs)
        {
            if (_logWriter == null) return;
            try
            {
                var rtMem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024.0 * 1024.0);
                var tier = _adaptiveQuality?.CurrentTier.ToString() ?? "Unknown";
                var drawCalls = DrawCalls;
                _logWriter.WriteLine($"{DateTime.UtcNow:O},{frameTimeMs:F3},{gpuTimeMs:F3},{drawCalls},{rtMem:F2},{GcAllocKb:F2},{tier}");
                _logFrameCounter++;
                if (_logFrameCounter % 300 == 0)
                    _logWriter.Flush();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PerformanceMonitor] Log write failed: {ex.Message}");
            }
        }

        private void CloseLogFile()
        {
            try
            {
                _logWriter?.Close();
                _logWriter?.Dispose();
            }
            catch { }
            _logWriter = null;
        }

        private void OnDestroy()
        {
            CloseLogFile();
        }
    }
}
