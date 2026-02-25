using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Maptifier.Core;
using Maptifier.Layers;
using Maptifier.Media;
using Maptifier.Warping;
using Maptifier.Masking;
using Maptifier.Effects;

namespace Maptifier.Projects
{
    /// <summary>
    /// Full production implementation of IProjectManager.
    /// Projects stored in Application.persistentDataPath/Projects/{guid}/
    /// </summary>
    public class ProjectManager : IProjectManager
    {
        private const string ProjectsFolder = "Projects";
        private const string ProjectFileName = "project.json";
        private const string ThumbnailFileName = "thumbnail.png";
        private const string AutosaveFolder = ".autosave";
        private const int MaxAutosaveSlots = 3;
        private const int ThumbnailSize = 256;

        private string _currentProjectId;
        private string _currentProjectName;
        private bool _hasUnsavedChanges;
        private float _autoSaveInterval = -1f;
        private ICoroutineRunner _coroutineRunner;

        public string CurrentProjectId => _currentProjectId;
        public string CurrentProjectName => _currentProjectName;
        public bool HasUnsavedChanges => _hasUnsavedChanges;

        public ProjectManager()
        {
            if (ServiceLocator.TryGet<ICoroutineRunner>(out var runner))
                _coroutineRunner = runner;
        }

        public void MarkDirty()
        {
            _hasUnsavedChanges = true;
        }

        public void SaveProject(string name, Action<bool> onComplete = null)
        {
            try
            {
                var layerManager = ServiceLocator.Get<ILayerManager>();
                var compositeRenderer = ServiceLocator.Get<CompositeRenderer>();
                var mediaImport = ServiceLocator.Get<IMediaImportService>();
                var maskService = ServiceLocator.Get<IMaskService>();

                bool isNewProject = string.IsNullOrEmpty(_currentProjectId);
                string projectId = isNewProject ? Guid.NewGuid().ToString("N") : _currentProjectId;
                string projectDir = GetProjectDirectory(projectId);
                Directory.CreateDirectory(projectDir);

                var data = BuildProjectDataFromLayers(layerManager, projectId, name, projectDir, mediaImport, maskService);

                // Save drawing RTs as PNG
                SaveDrawingTextures(layerManager, data, projectDir);

                // Copy media files to project folder
                CopyMediaToProject(layerManager, data, projectDir);

                // Capture thumbnail from CompositeRenderer
                string thumbnailPath = CaptureThumbnail(compositeRenderer, projectDir);
                data.Modified = DateTime.UtcNow.ToString("o");

                // Write project.json
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(Path.Combine(projectDir, ProjectFileName), json);

                _currentProjectId = projectId;
                _currentProjectName = name;
                _hasUnsavedChanges = false;

                EventBus.Publish(new ProjectSavedEvent(projectId, name));
                onComplete?.Invoke(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProjectManager] SaveProject failed: {ex.Message}\n{ex.StackTrace}");
                onComplete?.Invoke(false);
            }
        }

        public void LoadProject(string projectId, Action<bool> onComplete = null)
        {
            if (_coroutineRunner != null)
            {
                _coroutineRunner.RunCoroutine(LoadProjectCoroutine(projectId, onComplete));
            }
            else
            {
                bool success = false;
                try { success = LoadProjectInternal(projectId); }
                catch (Exception ex) { Debug.LogError($"[ProjectManager] LoadProject failed: {ex.Message}"); }
                onComplete?.Invoke(success);
            }
        }

        private IEnumerator LoadProjectCoroutine(string projectId, Action<bool> onComplete)
        {
            yield return null;
            bool success = false;
            try { success = LoadProjectInternal(projectId); }
            catch (Exception ex) { Debug.LogError($"[ProjectManager] LoadProject failed: {ex.Message}"); }
            onComplete?.Invoke(success);
        }

        private bool LoadProjectInternal(string projectId)
        {
            bool success = false;
            var layerManager = ServiceLocator.Get<ILayerManager>();
            var mediaImport = ServiceLocator.Get<IMediaImportService>();
            var maskService = ServiceLocator.Get<IMaskService>();
            var effectPipeline = ServiceLocator.Get<IEffectPipeline>();

            string projectPath = Path.Combine(GetProjectsRoot(), projectId, ProjectFileName);
            if (!File.Exists(projectPath))
            {
                Debug.LogError($"[ProjectManager] Project file not found: {projectPath}");
                return false;
            }

            string json = File.ReadAllText(projectPath);
            var data = JsonUtility.FromJson<ProjectData>(json);
            if (data == null) return false;

            int width = data.Resolution?.Width ?? 1920;
            int height = data.Resolution?.Height ?? 1080;

            layerManager.Initialize(width, height);
            layerManager.Reinitialize(width, height);

            if (data.Mixer != null)
            {
                layerManager.MixValue = data.Mixer.Crossfade;
                if (!string.IsNullOrEmpty(data.Mixer.BlendMode) &&
                    Enum.TryParse<BlendMode>(data.Mixer.BlendMode, out var blendMode))
                    layerManager.CurrentBlendMode = blendMode;
            }

            var layers = new[] { layerManager.LayerA, layerManager.LayerB };
            for (int i = 0; i < Mathf.Min(data.Layers?.Count ?? 0, 2); i++)
            {
                var layerData = data.Layers[i];
                var layer = layers[i];

                layer.Opacity = layerData.Opacity;
                layer.IsMuted = layerData.Muted;

                if (layerData.Media != null)
                {
                    if (string.Equals(layerData.Media.Type, "text", StringComparison.OrdinalIgnoreCase))
                    {
                        var textConfig = new TextConfig
                        {
                            Content = layerData.Media.TextContent ?? "",
                            FontSize = layerData.Media.TextFontSize > 0 ? layerData.Media.TextFontSize : 72,
                            TextColor = new Color(layerData.Media.TextColorR, layerData.Media.TextColorG,
                                layerData.Media.TextColorB, layerData.Media.TextColorA),
                            BackgroundColor = new Color(layerData.Media.BgColorR, layerData.Media.BgColorG,
                                layerData.Media.BgColorB, layerData.Media.BgColorA),
                            OutlineColor = new Color(layerData.Media.OutlineColorR, layerData.Media.OutlineColorG,
                                layerData.Media.OutlineColorB, layerData.Media.OutlineColorA),
                            OutlineWidth = layerData.Media.OutlineWidth,
                            Alignment = (TextAnchor)layerData.Media.TextAlignment,
                            Style = (FontStyle)layerData.Media.TextStyle,
                            WordWrap = layerData.Media.TextWordWrap
                        };
                        int tw = layerData.Media.RasterWidth > 0 ? layerData.Media.RasterWidth : 1024;
                        int th = layerData.Media.RasterHeight > 0 ? layerData.Media.RasterHeight : 512;
                        layer.MediaSource?.Dispose();
                        layer.MediaSource = mediaImport.LoadText(textConfig, tw, th);
                    }
                    else if (!string.IsNullOrEmpty(layerData.Media.Path))
                    {
                        string mediaPath = ResolveMediaPath(layerData.Media.Path, projectId);
                        if (File.Exists(mediaPath))
                        {
                            MediaType mediaType = string.Equals(layerData.Media.Type, "video", StringComparison.OrdinalIgnoreCase)
                                ? MediaType.Video
                                : string.Equals(layerData.Media.Type, "vector", StringComparison.OrdinalIgnoreCase)
                                    ? MediaType.Vector
                                    : MediaType.Image;
                            int rasterW = layerData.Media.RasterWidth > 0 ? layerData.Media.RasterWidth : 1024;
                            int rasterH = layerData.Media.RasterHeight > 0 ? layerData.Media.RasterHeight : 1024;
                            IMediaSource source = mediaType switch
                            {
                                MediaType.Video => mediaImport.LoadVideo(mediaPath),
                                MediaType.Vector => mediaImport.LoadVector(mediaPath, rasterW, rasterH),
                                _ => mediaImport.LoadImage(mediaPath)
                            };
                            layer.MediaSource?.Dispose();
                            layer.MediaSource = source;
                            source.SetLoop(layerData.Media.Loop);
                            if (mediaType == MediaType.Video)
                                source.Play();
                        }
                    }
                }

                if (layerData.Warp != null)
                {
                    if (string.Equals(layerData.Warp.Type, "meshGrid", StringComparison.OrdinalIgnoreCase) &&
                        layerData.Warp.CornersFlat != null &&
                        layerData.Warp.GridWidth > 0 && layerData.Warp.GridHeight > 0)
                    {
                        int count = layerData.Warp.GridWidth * layerData.Warp.GridHeight;
                        if (layerData.Warp.CornersFlat.Length >= count * 2)
                        {
                            var points = new Vector2[count];
                            for (int j = 0; j < count; j++)
                                points[j] = new Vector2(layerData.Warp.CornersFlat[j * 2], layerData.Warp.CornersFlat[j * 2 + 1]);
                            layer.WarpMode = WarpMode.MeshGrid;
                            layer.MeshGridWidth = layerData.Warp.GridWidth;
                            layer.MeshGridHeight = layerData.Warp.GridHeight;
                            layer.MeshControlPoints = points;
                        }
                    }
                    else if (layerData.Warp.CornersFlat != null && layerData.Warp.CornersFlat.Length >= 8)
                    {
                        layer.WarpMode = WarpMode.FourCorner;
                        layer.WarpCorners = new Vector2[]
                        {
                            new(layerData.Warp.CornersFlat[0], layerData.Warp.CornersFlat[1]),
                            new(layerData.Warp.CornersFlat[2], layerData.Warp.CornersFlat[3]),
                            new(layerData.Warp.CornersFlat[4], layerData.Warp.CornersFlat[5]),
                            new(layerData.Warp.CornersFlat[6], layerData.Warp.CornersFlat[7])
                        };
                    }
                }

                if (layerData.Mask.Polygons != null && layerData.Mask.Polygons.Count > 0)
                    maskService.Deserialize(layerData.Mask, layer.MaskRT);
                else if (layerData.Mask.BrushMaskPng != null && layerData.Mask.BrushMaskPng.Length > 0)
                    maskService.Deserialize(layerData.Mask, layer.MaskRT);
                else
                    maskService.ClearMask(layer.MaskRT);

                if (!string.IsNullOrEmpty(layerData.DrawingTexturePath))
                {
                    string drawPath = ResolvePathInProject(layerData.DrawingTexturePath, projectId);
                    if (File.Exists(drawPath))
                    {
                        var tex = new Texture2D(2, 2);
                        if (tex.LoadImage(File.ReadAllBytes(drawPath)))
                        {
                            var prev = RenderTexture.active;
                            RenderTexture.active = layer.DrawingRT;
                            Graphics.Blit(tex, layer.DrawingRT);
                            RenderTexture.active = prev;
                        }
                        UnityEngine.Object.Destroy(tex);
                    }
                }

                layer.Effects.Clear();
                if (layerData.Effects != null)
                {
                    foreach (var effectData in layerData.Effects)
                    {
                        if (string.IsNullOrEmpty(effectData.EffectId)) continue;
                        var effect = effectPipeline.CreateEffect(effectData.EffectId);
                        if (effect != null)
                        {
                            effect.IsEnabled = effectData.Enabled;
                            if (effectData.Params != null)
                                foreach (var p in effectData.Params)
                                    effect.SetParameter(p.Name, p.Value);
                            layer.Effects.Add(effect);
                        }
                    }
                }
            }

            _currentProjectId = projectId;
            _currentProjectName = data.Name ?? "Untitled";
            _hasUnsavedChanges = false;

            EventBus.Publish(new ProjectLoadedEvent(projectId));
            return true;
        }

        public void DeleteProject(string projectId)
        {
            try
            {
                string projectDir = GetProjectDirectory(projectId);
                if (Directory.Exists(projectDir))
                {
                    Directory.Delete(projectDir, true);
                }
                if (_currentProjectId == projectId)
                {
                    _currentProjectId = null;
                    _currentProjectName = null;
                    _hasUnsavedChanges = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProjectManager] DeleteProject failed: {ex.Message}");
            }
        }

        public List<ProjectMetadata> ListProjects()
        {
            var list = new List<ProjectMetadata>();
            var root = GetProjectsRoot();
            if (!Directory.Exists(root)) return list;

            foreach (var dir in Directory.GetDirectories(root))
            {
                string projectId = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(projectId) || projectId.StartsWith(".")) continue;

                string projectPath = Path.Combine(dir, ProjectFileName);
                if (!File.Exists(projectPath)) continue;

                try
                {
                    string json = File.ReadAllText(projectPath);
                    var data = JsonUtility.FromJson<ProjectData>(json);
                    if (data == null) continue;

                    string thumbPath = Path.Combine(dir, ThumbnailFileName);
                    list.Add(new ProjectMetadata
                    {
                        Id = data.Id ?? projectId,
                        Name = data.Name ?? "Untitled",
                        Created = data.Created ?? "",
                        Modified = data.Modified ?? "",
                        ThumbnailPath = File.Exists(thumbPath) ? thumbPath : null
                    });
                }
                catch
                {
                    // Skip corrupted projects
                }
            }

            list.Sort((a, b) => string.Compare(b.Modified, a.Modified, StringComparison.Ordinal));
            return list;
        }

        public ProjectData CreateNewProject(string name)
        {
            string projectId = Guid.NewGuid().ToString("N");
            string projectDir = GetProjectDirectory(projectId);
            Directory.CreateDirectory(projectDir);

            var data = new ProjectData
            {
                Id = projectId,
                Name = name ?? "Untitled",
                Created = DateTime.UtcNow.ToString("o"),
                Modified = DateTime.UtcNow.ToString("o"),
                Resolution = new ResolutionData { Width = 1920, Height = 1080 },
                Layers = new List<LayerData>
                {
                    new() { Id = "A", Opacity = 1f },
                    new() { Id = "B", Opacity = 1f }
                }
            };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(Path.Combine(projectDir, ProjectFileName), json);

            _currentProjectId = projectId;
            _currentProjectName = data.Name;
            _hasUnsavedChanges = false;

            LoadProjectInternal(projectId);
            return data;
        }

        public void EnableAutoSave(float intervalSeconds = 60f)
        {
            _autoSaveInterval = intervalSeconds;
            if (_coroutineRunner != null)
                _coroutineRunner.RunCoroutine(AutoSaveLoop());
        }

        public void DisableAutoSave()
        {
            _autoSaveInterval = -1f;
        }

        private IEnumerator AutoSaveLoop()
        {
            while (_autoSaveInterval > 0)
            {
                yield return new WaitForSeconds(_autoSaveInterval);
                if (_autoSaveInterval < 0) yield break;
                if (!_hasUnsavedChanges || string.IsNullOrEmpty(_currentProjectId)) continue;

                PerformAutoSave();
            }
        }

        private void PerformAutoSave()
        {
            try
            {
                var layerManager = ServiceLocator.Get<ILayerManager>();
                var compositeRenderer = ServiceLocator.Get<CompositeRenderer>();
                var mediaImport = ServiceLocator.Get<IMediaImportService>();
                var maskService = ServiceLocator.Get<IMaskService>();

                string projectDir = GetProjectDirectory(_currentProjectId);
                string autosaveDir = Path.Combine(projectDir, AutosaveFolder);
                Directory.CreateDirectory(autosaveDir);

                // Rotate autosave slots (0, 1, 2 -> keep last 3)
                for (int i = MaxAutosaveSlots - 1; i >= 1; i--)
                {
                    string src = Path.Combine(autosaveDir, $"project_{i - 1}.json");
                    string dst = Path.Combine(autosaveDir, $"project_{i}.json");
                    if (File.Exists(src))
                    {
                        if (File.Exists(dst)) File.Delete(dst);
                        File.Move(src, dst);
                    }
                }

                var data = BuildProjectDataFromLayers(layerManager, _currentProjectId, _currentProjectName, projectDir, mediaImport, maskService);
                data.Modified = DateTime.UtcNow.ToString("o");

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(Path.Combine(autosaveDir, "project_0.json"), json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProjectManager] AutoSave failed: {ex.Message}");
            }
        }

        private string GetProjectsRoot()
        {
            return Path.Combine(Application.persistentDataPath, ProjectsFolder);
        }

        private string GetProjectDirectory(string projectId)
        {
            return Path.Combine(GetProjectsRoot(), projectId);
        }

        private string ResolvePathInProject(string relativePath, string projectId)
        {
            if (Path.IsPathRooted(relativePath)) return relativePath;
            return Path.Combine(GetProjectDirectory(projectId), relativePath);
        }

        private string ResolveMediaPath(string path, string projectId)
        {
            if (File.Exists(path)) return path;
            return ResolvePathInProject(path, projectId);
        }

        private ProjectData BuildProjectDataFromLayers(
            ILayerManager layerManager,
            string projectId,
            string name,
            string projectDir,
            IMediaImportService mediaImport,
            IMaskService maskService)
        {
            int width = 1920, height = 1080;
            if (ServiceLocator.TryGet<CompositeRenderer>(out var cr) && cr.CompositeRT != null)
            {
                width = cr.CompositeRT.width;
                height = cr.CompositeRT.height;
            }

            string created = DateTime.UtcNow.ToString("o");
            if (!string.IsNullOrEmpty(_currentProjectId) && projectId == _currentProjectId)
            {
                string projectPath = Path.Combine(projectDir, ProjectFileName);
                if (File.Exists(projectPath))
                {
                    try
                    {
                        var existing = JsonUtility.FromJson<ProjectData>(File.ReadAllText(projectPath));
                        if (existing?.Created != null) created = existing.Created;
                    }
                    catch { /* preserve default */ }
                }
            }

            var data = new ProjectData
            {
                Id = projectId,
                Name = name,
                Created = created,
                Resolution = new ResolutionData { Width = width, Height = height },
                Mixer = new MixerData
                {
                    Crossfade = layerManager.MixValue,
                    BlendMode = layerManager.CurrentBlendMode.ToString()
                }
            };

            var layers = new[] { layerManager.LayerA, layerManager.LayerB };
            foreach (var layer in layers)
            {
                var layerData = new LayerData
                {
                    Id = layer.Id,
                    Opacity = layer.Opacity,
                    Muted = layer.IsMuted
                };

                if (layer.MediaSource != null)
                {
                    if (layer.MediaSource.Type == MediaType.Text && layer.MediaSource is TextSource ts)
                    {
                        var cfg = ts.Config;
                        layerData.Media = new MediaReferenceData
                        {
                            Type = "text",
                            TextContent = cfg.Content,
                            TextFontSize = cfg.FontSize,
                            TextColorR = cfg.TextColor.r, TextColorG = cfg.TextColor.g,
                            TextColorB = cfg.TextColor.b, TextColorA = cfg.TextColor.a,
                            BgColorR = cfg.BackgroundColor.r, BgColorG = cfg.BackgroundColor.g,
                            BgColorB = cfg.BackgroundColor.b, BgColorA = cfg.BackgroundColor.a,
                            OutlineColorR = cfg.OutlineColor.r, OutlineColorG = cfg.OutlineColor.g,
                            OutlineColorB = cfg.OutlineColor.b, OutlineColorA = cfg.OutlineColor.a,
                            OutlineWidth = cfg.OutlineWidth,
                            TextAlignment = (int)cfg.Alignment,
                            TextStyle = (int)cfg.Style,
                            TextWordWrap = cfg.WordWrap,
                            RasterWidth = ts.NativeResolution.x,
                            RasterHeight = ts.NativeResolution.y
                        };
                    }
                    else if (!string.IsNullOrEmpty(layer.MediaSource.SourcePath))
                    {
                        layerData.Media = new MediaReferenceData
                        {
                            Type = layer.MediaSource.Type switch
                            {
                                MediaType.Video => "video",
                                MediaType.Vector => "vector",
                                _ => "image"
                            },
                            Path = GetRelativeMediaPath(layer.MediaSource.SourcePath, projectDir),
                            Loop = true
                        };
                        if (layer.MediaSource.Type == MediaType.Vector && layer.MediaSource is VectorSource vs)
                        {
                            layerData.Media.RasterWidth = vs.RasterWidth;
                            layerData.Media.RasterHeight = vs.RasterHeight;
                        }
                    }
                }

                layerData.Warp = new WarpData
                {
                    Type = layer.WarpMode == WarpMode.MeshGrid ? "meshGrid" : "fourCorner",
                    GridWidth = layer.MeshGridWidth,
                    GridHeight = layer.MeshGridHeight
                };
                if (layer.WarpCorners != null && layer.WarpCorners.Length >= 4)
                {
                    layerData.Warp.CornersFlat = new float[8];
                    for (int i = 0; i < 4; i++)
                    {
                        layerData.Warp.CornersFlat[i * 2] = layer.WarpCorners[i].x;
                        layerData.Warp.CornersFlat[i * 2 + 1] = layer.WarpCorners[i].y;
                    }
                }
                else if (layer.MeshControlPoints != null && layer.MeshControlPoints.Length > 0)
                {
                    layerData.Warp.CornersFlat = new float[layer.MeshControlPoints.Length * 2];
                    for (int i = 0; i < layer.MeshControlPoints.Length; i++)
                    {
                        layerData.Warp.CornersFlat[i * 2] = layer.MeshControlPoints[i].x;
                        layerData.Warp.CornersFlat[i * 2 + 1] = layer.MeshControlPoints[i].y;
                    }
                }

                // Serialize mask from layer's MaskRT
                layerData.Mask = SerializeMaskFromRT(layer.MaskRT);

                foreach (var effect in layer.Effects)
                {
                    if (effect == null) continue;
                    var effectData = new EffectData
                    {
                        EffectId = effect.Id,
                        Enabled = effect.IsEnabled
                    };
                    foreach (var kvp in effect.GetParameters())
                        effectData.Params.Add(new EffectParamData { Name = kvp.Key, Value = kvp.Value.Value });
                    layerData.Effects.Add(effectData);
                }

                data.Layers.Add(layerData);
            }

            return data;
        }

        private MaskData SerializeMaskFromRT(RenderTexture maskRT)
        {
            if (maskRT == null) return default;

            var tex = new Texture2D(maskRT.width, maskRT.height, TextureFormat.R8, false);
            var prev = RenderTexture.active;
            RenderTexture.active = maskRT;
            tex.ReadPixels(new Rect(0, 0, maskRT.width, maskRT.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            var png = tex.EncodeToPNG();
            UnityEngine.Object.Destroy(tex);

            return new MaskData
            {
                Mode = MaskMode.Brush,
                Polygons = null,
                BrushMaskPng = png,
                Inverted = false
            };
        }

        private void SaveDrawingTextures(ILayerManager layerManager, ProjectData data, string projectDir)
        {
            var layers = new[] { layerManager.LayerA, layerManager.LayerB };
            for (int i = 0; i < Mathf.Min(data.Layers.Count, 2); i++)
            {
                var layer = layers[i];
                var layerData = data.Layers[i];
                if (layer.DrawingRT == null) continue;

                var tex = new Texture2D(layer.DrawingRT.width, layer.DrawingRT.height, TextureFormat.RGBA32, false);
                var prev = RenderTexture.active;
                RenderTexture.active = layer.DrawingRT;
                tex.ReadPixels(new Rect(0, 0, layer.DrawingRT.width, layer.DrawingRT.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                string fileName = $"drawing_{layer.Id}.png";
                string path = Path.Combine(projectDir, fileName);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);

                layerData.DrawingTexturePath = fileName;
            }
        }

        private void CopyMediaToProject(ILayerManager layerManager, ProjectData data, string projectDir)
        {
            var layers = new[] { layerManager.LayerA, layerManager.LayerB };
            for (int i = 0; i < Mathf.Min(data.Layers.Count, 2); i++)
            {
                var layer = layers[i];
                var layerData = data.Layers[i];
                if (layer.MediaSource == null || layerData.Media == null) continue;

                string srcPath = layer.MediaSource.SourcePath;
                if (string.IsNullOrEmpty(srcPath) || !File.Exists(srcPath)) continue;

                string fileName = Path.GetFileName(srcPath);
                if (string.IsNullOrEmpty(fileName)) fileName = $"media_{layer.Id}_{Path.GetExtension(srcPath) ?? ".png"}";
                string destPath = Path.Combine(projectDir, fileName);

                if (!string.Equals(Path.GetFullPath(srcPath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                    File.Copy(srcPath, destPath, overwrite: true);

                layerData.Media.Path = fileName;
            }
        }

        private string GetRelativeMediaPath(string fullPath, string projectDir)
        {
            try
            {
                var projectUri = new Uri(projectDir + Path.DirectorySeparatorChar);
                var fileUri = new Uri(fullPath);
                var relative = projectUri.MakeRelativeUri(fileUri).ToString();
                return Uri.UnescapeDataString(relative).Replace('/', Path.DirectorySeparatorChar);
            }
            catch
            {
                return Path.GetFileName(fullPath);
            }
        }

        private string CaptureThumbnail(CompositeRenderer compositeRenderer, string projectDir)
        {
            var compositeRT = compositeRenderer?.CompositeRT;
            if (compositeRT == null) return null;

            var thumbRT = RenderTexture.GetTemporary(ThumbnailSize, ThumbnailSize, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(compositeRT, thumbRT);

            var tex = new Texture2D(ThumbnailSize, ThumbnailSize, TextureFormat.RGBA32, false);
            var prev = RenderTexture.active;
            RenderTexture.active = thumbRT;
            tex.ReadPixels(new Rect(0, 0, ThumbnailSize, ThumbnailSize), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(thumbRT);

            string path = Path.Combine(projectDir, ThumbnailFileName);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);

            return path;
        }
    }
}
