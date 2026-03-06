using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Maptifier.Core;
using Maptifier.Layers;
using Maptifier.Projects;

namespace Maptifier.UI
{
    /// <summary>
    /// Main UI controller for Maptifier. Loads layout and theme, caches element references,
    /// and wires toolbar, layer drawer, crossfade, blend mode, and display status.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainUIController : MonoBehaviour
    {
        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset _mainLayout;
        [SerializeField] private StyleSheet _theme;

        private UIDocument _document;
        private VisualElement _root;

        // Cached elements
        private VisualElement _displayStatus;
        private Label _displayLabel;
        private Button _settingsBtn;
        private Button _projectsBtn;
        private Button _exportScreenshotBtn;
        private Button _exportVideoBtn;
        private VisualElement _canvasArea;
        private IMGUIContainer _canvasPreview;
        private Slider _crossfadeSlider;
        private VisualElement _layerDrawer;
        private Label _toast;

        private Button _toolSelect;
        private Button _toolWarp;
        private Button _toolMask;
        private Button _toolDraw;
        private Button _toolText;
        private Button _toolEffects;

        private Slider _opacityA;
        private Slider _opacityB;
        private Button _soloA;
        private Button _muteA;
        private Button _soloB;
        private Button _muteB;
        private DropdownField _blendMode;

        private Button _undoBtn;
        private Button _redoBtn;

        private VisualElement _layerACard;
        private VisualElement _layerBCard;

        private VisualElement _projectOverlay;
        private ListView _projectList;
        private Button _projectCloseBtn;
        private Button _projectNewBtn;
        private Button _projectLoadBtn;
        private Button _projectDeleteBtn;

        private ILayerManager _layerManager;
        private ToolType _activeTool = ToolType.Select;
        private bool _drawerOpen;
        private Coroutine _toastCoroutine;

        private ExportService _exportService;

        private const string ActiveClass = "maptifier-toolbar__button--active";
        private const string DrawerOpenClass = "maptifier-drawer--open";
        private const string ToastVisibleClass = "maptifier-toast--visible";
        private const string ConnectedClass = "maptifier-status-dot--connected";
        private const string DisconnectedClass = "maptifier-status-dot--disconnected";

        public IMGUIContainer CanvasPreview => _canvasPreview;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (GetComponent<TextEditorPanel>() == null)
                gameObject.AddComponent<TextEditorPanel>();
            var currentTree = _document?.visualTreeAsset;
            var isOnboarding = currentTree != null && currentTree.name != null &&
                currentTree.name.IndexOf("Onboarding", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isOnboarding && _mainLayout != null)
                _document.visualTreeAsset = _mainLayout;
            if (_theme != null)
                _document.styleSheets.Add(_theme);
        }

        private void OnEnable()
        {
            _document.rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void OnDisable()
        {
            if (_document?.rootVisualElement != null)
                _document.rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            UnsubscribeFromEvents();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            CacheElements();
        }

        private void Start()
        {
            CacheElements();
            ServiceLocator.OnServicesReady += OnServicesReady;
            if (ServiceLocator.IsInitialized)
                OnServicesReady();
        }

        private void OnDestroy()
        {
            ServiceLocator.OnServicesReady -= OnServicesReady;
            UnsubscribeFromEvents();
        }

        private void OnServicesReady()
        {
            if (ServiceLocator.TryGet<ILayerManager>(out _layerManager))
                SyncFromLayerManager();
            if (_exportService == null)
                _exportService = new ExportService();
            WireAll();
            SubscribeToEvents();
        }

        private void CacheElements()
        {
            _root = _document?.rootVisualElement;
            if (_root == null) return;

            _displayStatus = _root.Q<VisualElement>("display-status");
            _displayLabel = _root.Q<Label>("display-label");
            _settingsBtn = _root.Q<Button>("settings-btn");
            _projectsBtn = _root.Q<Button>("projects-btn");
            _exportScreenshotBtn = _root.Q<Button>("export-screenshot-btn");
            _exportVideoBtn = _root.Q<Button>("export-video-btn");
            _canvasArea = _root.Q<VisualElement>("canvas-area");
            _canvasPreview = _root.Q<IMGUIContainer>("canvas-preview");
            _crossfadeSlider = _root.Q<Slider>("crossfade-slider");
            _layerDrawer = _root.Q<VisualElement>("layer-drawer");
            _toast = _root.Q<Label>("toast");

            _toolSelect = _root.Q<Button>("tool-select");
            _toolWarp = _root.Q<Button>("tool-warp");
            _toolMask = _root.Q<Button>("tool-mask");
            _toolDraw = _root.Q<Button>("tool-draw");
            _toolText = _root.Q<Button>("tool-text");
            _toolEffects = _root.Q<Button>("tool-effects");

            _opacityA = _root.Q<Slider>("opacity-a");
            _opacityB = _root.Q<Slider>("opacity-b");
            _soloA = _root.Q<Button>("solo-a");
            _muteA = _root.Q<Button>("mute-a");
            _soloB = _root.Q<Button>("solo-b");
            _muteB = _root.Q<Button>("mute-b");
            _blendMode = _root.Q<DropdownField>("blend-mode");
            _undoBtn = _root.Q<Button>("undo-btn");
            _redoBtn = _root.Q<Button>("redo-btn");
            _layerACard = _root.Q<VisualElement>("layer-a-card");
            _layerBCard = _root.Q<VisualElement>("layer-b-card");
        }

        private void WireAll()
        {
            WireToolbar();
            WireCrossfade();
            WireLayerDrawer();
            WireBlendMode();
            WireLayerControls();
            WireSettings();
            WireExport();
            WireProjects();
            WireUndoRedo();
        }

        private void WireToolbar()
        {
            if (_toolSelect != null) _toolSelect.clicked += () => SetActiveTool(ToolType.Select);
            if (_toolWarp != null) _toolWarp.clicked += () => SetActiveTool(ToolType.Warp);
            if (_toolMask != null) _toolMask.clicked += () => SetActiveTool(ToolType.Mask);
            if (_toolDraw != null) _toolDraw.clicked += () => SetActiveTool(ToolType.Draw);
            if (_toolText != null) _toolText.clicked += () => SetActiveTool(ToolType.Text);
            if (_toolEffects != null) _toolEffects.clicked += () => SetActiveTool(ToolType.Effects);
        }

        private void SetActiveTool(ToolType tool)
        {
            _activeTool = tool;
            EventBus.Publish(new ToolChangedEvent(tool));
            UpdateToolHighlight();

            var textPanel = FindFirstObjectByType<TextEditorPanel>();
            if (tool == ToolType.Text)
                textPanel?.Show();
            else
                textPanel?.Hide();
        }

        private void UpdateToolHighlight()
        {
            void SetActive(Button btn, bool active)
            {
                if (btn == null) return;
                if (active)
                    btn.AddToClassList(ActiveClass);
                else
                    btn.RemoveFromClassList(ActiveClass);
            }

            SetActive(_toolSelect, _activeTool == ToolType.Select);
            SetActive(_toolWarp, _activeTool == ToolType.Warp);
            SetActive(_toolMask, _activeTool == ToolType.Mask);
            SetActive(_toolDraw, _activeTool == ToolType.Draw);
            SetActive(_toolText, _activeTool == ToolType.Text);
            SetActive(_toolEffects, _activeTool == ToolType.Effects);
        }

        private void WireCrossfade()
        {
            if (_crossfadeSlider == null || _layerManager == null) return;

            _crossfadeSlider.value = _layerManager.MixValue;
            _crossfadeSlider.RegisterValueChangedCallback(evt =>
            {
                _layerManager.MixValue = evt.newValue;
            });
        }

        private void WireLayerDrawer()
        {
            if (_layerDrawer == null) return;

            _drawerOpen = false;
            _layerDrawer.RemoveFromClassList(DrawerOpenClass);

            // Add layers toggle button to topbar
            var topbar = _root.Q<VisualElement>(null, "maptifier-topbar");
            if (topbar != null)
            {
                var layersBtn = new Button(() => ToggleDrawer())
                {
                    text = "≡",
                    name = "layers-toggle-btn"
                };
                layersBtn.AddToClassList("maptifier-toolbar__button");
                layersBtn.style.width = 40;
                layersBtn.style.height = 40;
                layersBtn.style.marginRight = 8;
                // Insert before settings button
                var settingsIndex = topbar.IndexOf(_settingsBtn);
                topbar.Insert(settingsIndex >= 0 ? settingsIndex : topbar.childCount, layersBtn);
            }

            // Swipe from right edge: detect pointer events on canvas area
            if (_canvasArea != null)
            {
                var swipeStartX = 0f;
                var screenWidth = Screen.width;

                _canvasArea.RegisterCallback<PointerDownEvent>(evt =>
                {
                    swipeStartX = evt.position.x;
                });

                _canvasArea.RegisterCallback<PointerUpEvent>(evt =>
                {
                    var delta = evt.position.x - swipeStartX;
                    if (swipeStartX >= screenWidth - 32 && delta < -30)
                        OpenDrawer();
                    else if (swipeStartX >= screenWidth - 32 && delta > 30)
                        CloseDrawer();
                });
            }
        }

        private void ToggleDrawer()
        {
            _drawerOpen = !_drawerOpen;
            if (_drawerOpen)
                _layerDrawer?.AddToClassList(DrawerOpenClass);
            else
                _layerDrawer?.RemoveFromClassList(DrawerOpenClass);
        }

        private void OpenDrawer()
        {
            _drawerOpen = true;
            _layerDrawer?.AddToClassList(DrawerOpenClass);
        }

        private void CloseDrawer()
        {
            _drawerOpen = false;
            _layerDrawer?.RemoveFromClassList(DrawerOpenClass);
        }

        private void WireBlendMode()
        {
            if (_blendMode == null || _layerManager == null) return;

            var choices = new List<string> { "Normal", "Additive", "Multiply", "Screen", "Overlay", "Difference" };
            _blendMode.choices = choices;
            _blendMode.index = (int)_layerManager.CurrentBlendMode;
            _blendMode.RegisterValueChangedCallback(evt =>
            {
                var idx = choices.IndexOf(evt.newValue);
                if (idx >= 0 && idx <= (int)BlendMode.Difference)
                    _layerManager.CurrentBlendMode = (BlendMode)idx;
            });
        }

        private void WireLayerControls()
        {
            if (_layerManager == null) return;

            if (_opacityA != null)
            {
                _opacityA.value = _layerManager.LayerA?.Opacity ?? 1f;
                _opacityA.RegisterValueChangedCallback(evt => _layerManager.SetLayerOpacity(0, evt.newValue));
            }
            if (_opacityB != null)
            {
                _opacityB.value = _layerManager.LayerB?.Opacity ?? 1f;
                _opacityB.RegisterValueChangedCallback(evt => _layerManager.SetLayerOpacity(1, evt.newValue));
            }
            if (_soloA != null)
                _soloA.clicked += () => { var next = !(_layerManager.LayerA?.IsSolo ?? false); _layerManager.SetLayerSolo(0, next); UpdateSoloMuteStyle(_soloA, next); };
            if (_muteA != null)
                _muteA.clicked += () => { var next = !(_layerManager.LayerA?.IsMuted ?? false); _layerManager.SetLayerMute(0, next); UpdateSoloMuteStyle(_muteA, next); };
            if (_soloB != null)
                _soloB.clicked += () => { var next = !(_layerManager.LayerB?.IsSolo ?? false); _layerManager.SetLayerSolo(1, next); UpdateSoloMuteStyle(_soloB, next); };
            if (_muteB != null)
                _muteB.clicked += () => { var next = !(_layerManager.LayerB?.IsMuted ?? false); _layerManager.SetLayerMute(1, next); UpdateSoloMuteStyle(_muteB, next); };

            if (_layerACard != null)
            {
                _layerACard.RegisterCallback<ClickEvent>(_ => SelectLayer(0));
            }
            if (_layerBCard != null)
            {
                _layerBCard.RegisterCallback<ClickEvent>(_ => SelectLayer(1));
            }
        }

        private void UpdateSoloMuteStyle(Button btn, bool active)
        {
            if (btn == null) return;
            if (active)
                btn.AddToClassList(ActiveClass);
            else
                btn.RemoveFromClassList(ActiveClass);
        }

        private void WireSettings()
        {
            if (_settingsBtn != null)
                _settingsBtn.clicked += () => SettingsController.Instance?.Show();
        }

        private void WireExport()
        {
            if (_exportScreenshotBtn != null)
                _exportScreenshotBtn.clicked += OnExportScreenshotClicked;
            if (_exportVideoBtn != null)
                _exportVideoBtn.clicked += OnExportVideoClicked;
        }

        private void WireProjects()
        {
            if (_projectsBtn == null || _root == null)
                return;

            EnsureProjectOverlay();
            _projectsBtn.clicked += () =>
            {
                RefreshProjectList();
                ShowProjectOverlay();
            };
        }

        private void WireUndoRedo()
        {
            if (_undoBtn != null)
                _undoBtn.clicked += () => EventBus.Publish(new UndoRequestedEvent());
            if (_redoBtn != null)
                _redoBtn.clicked += () => EventBus.Publish(new RedoRequestedEvent());
        }

        private void SubscribeToEvents()
        {
            EventBus.Subscribe<DisplayConnectedEvent>(OnDisplayConnected);
            EventBus.Subscribe<DisplayDisconnectedEvent>(OnDisplayDisconnected);
            EventBus.Subscribe<ProjectAutoSavedEvent>(OnProjectAutoSaved);
        }

        private void UnsubscribeFromEvents()
        {
            EventBus.Unsubscribe<DisplayConnectedEvent>(OnDisplayConnected);
            EventBus.Unsubscribe<DisplayDisconnectedEvent>(OnDisplayDisconnected);
            EventBus.Unsubscribe<ProjectAutoSavedEvent>(OnProjectAutoSaved);
        }

        private void OnDisplayConnected(DisplayConnectedEvent evt)
        {
            if (_displayStatus != null)
            {
                _displayStatus.RemoveFromClassList(DisconnectedClass);
                _displayStatus.AddToClassList(ConnectedClass);
            }
            if (_displayLabel != null)
                _displayLabel.text = $"{evt.Resolution.x}×{evt.Resolution.y}";
            AnalyticsService.TrackDisplayConnected($"{evt.Resolution.x}x{evt.Resolution.y}");
        }

        private void OnDisplayDisconnected(DisplayDisconnectedEvent evt)
        {
            if (_displayStatus != null)
            {
                _displayStatus.RemoveFromClassList(ConnectedClass);
                _displayStatus.AddToClassList(DisconnectedClass);
            }
            if (_displayLabel != null)
                _displayLabel.text = "No Display";
        }

        private void OnProjectAutoSaved(ProjectAutoSavedEvent evt)
        {
            ShowToast("Project autosaved.");
        }

        private void SyncFromLayerManager()
        {
            if (_layerManager == null) return;
            if (_crossfadeSlider != null) _crossfadeSlider.SetValueWithoutNotify(_layerManager.MixValue);
            if (_opacityA != null) _opacityA.SetValueWithoutNotify(_layerManager.LayerA?.Opacity ?? 1f);
            if (_opacityB != null) _opacityB.SetValueWithoutNotify(_layerManager.LayerB?.Opacity ?? 1f);
            if (_blendMode != null) _blendMode.index = (int)_layerManager.CurrentBlendMode;
        }

        public void ShowToast(string message, float duration = 3f)
        {
            if (_toast == null) return;
            if (_toastCoroutine != null)
                StopCoroutine(_toastCoroutine);

            _toast.text = message;
            _toast.AddToClassList(ToastVisibleClass);

            _toastCoroutine = StartCoroutine(ToastRoutine(duration));
        }

        private IEnumerator ToastRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            _toast?.RemoveFromClassList(ToastVisibleClass);
            _toastCoroutine = null;
        }

        private void OnExportScreenshotClicked()
        {
            if (_exportService == null)
                _exportService = new ExportService();

            if (!ServiceLocator.TryGet<CompositeRenderer>(out var composite) || composite.CompositeRT == null)
            {
                ShowToast("Unable to export: no composite frame.");
                return;
            }

            _exportService.ExportScreenshot(composite.CompositeRT, path =>
            {
                if (string.IsNullOrEmpty(path))
                    ShowToast("Screenshot export failed or was cancelled.");
                else
                    ShowToast("Screenshot exported to gallery.");
            });
        }

        private void OnExportVideoClicked()
        {
            if (_exportService == null)
                _exportService = new ExportService();

            if (!ServiceLocator.TryGet<CompositeRenderer>(out var composite) || composite.CompositeRT == null)
            {
                ShowToast("Unable to export: no composite frame.");
                return;
            }

            const float durationSeconds = 10f;
            const int fps = 30;

            ShowToast("Starting video export...");
            StartCoroutine(_exportService.ExportVideo(
                composite.CompositeRT,
                durationSeconds,
                fps,
                _ => { },
                path =>
                {
                    if (string.IsNullOrEmpty(path))
                        ShowToast("Video export failed or was cancelled.");
                    else
                        ShowToast("Video exported to gallery.");
                },
                () => false));
        }

        private void EnsureProjectOverlay()
        {
            if (_projectOverlay != null || _root == null)
                return;

            _projectOverlay = new VisualElement
            {
                name = "project-overlay"
            };
            _projectOverlay.AddToClassList("maptifier-settings-overlay");
            _projectOverlay.style.display = DisplayStyle.None;
            _projectOverlay.style.pointerEvents = PointerEvents.None;

            var panel = new VisualElement { name = "project-panel" };
            panel.AddToClassList("maptifier-settings-panel");

            var header = new VisualElement();
            header.AddToClassList("maptifier-settings-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 16;
            header.style.paddingRight = 16;
            header.style.paddingTop = 16;
            header.style.paddingBottom = 16;

            var title = new Label("Projects");
            title.AddToClassList("maptifier-label");
            header.Add(title);

            _projectCloseBtn = new Button { text = "✕", name = "project-close-btn" };
            _projectCloseBtn.AddToClassList("maptifier-toolbar__button");
            _projectCloseBtn.style.width = 40;
            _projectCloseBtn.style.height = 40;
            header.Add(_projectCloseBtn);

            panel.Add(header);

            _projectList = new ListView
            {
                name = "project-list",
                selectionType = SelectionType.Single
            };
            _projectList.style.flexGrow = 1;
            panel.Add(_projectList);

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.paddingLeft = 16;
            footer.style.paddingRight = 16;
            footer.style.paddingTop = 12;
            footer.style.paddingBottom = 16;

            _projectNewBtn = new Button { text = "New", name = "project-new-btn" };
            _projectLoadBtn = new Button { text = "Load", name = "project-load-btn" };
            _projectDeleteBtn = new Button { text = "Delete", name = "project-delete-btn" };

            _projectNewBtn.AddToClassList("maptifier-button");
            _projectLoadBtn.AddToClassList("maptifier-button");
            _projectDeleteBtn.AddToClassList("maptifier-button");

            footer.Add(_projectNewBtn);
            footer.Add(_projectLoadBtn);
            footer.Add(_projectDeleteBtn);

            panel.Add(footer);

            _projectOverlay.Add(panel);
            _root.Add(_projectOverlay);

            _projectCloseBtn.clicked += HideProjectOverlay;
            _projectNewBtn.clicked += OnProjectNewClicked;
            _projectLoadBtn.clicked += OnProjectLoadClicked;
            _projectDeleteBtn.clicked += OnProjectDeleteClicked;
        }

        private void ShowProjectOverlay()
        {
            if (_projectOverlay == null) return;
            _projectOverlay.style.display = DisplayStyle.Flex;
            _projectOverlay.style.pointerEvents = PointerEvents.Auto;
        }

        private void HideProjectOverlay()
        {
            if (_projectOverlay == null) return;
            _projectOverlay.style.display = DisplayStyle.None;
            _projectOverlay.style.pointerEvents = PointerEvents.None;
        }

        private void RefreshProjectList()
        {
            if (_projectList == null) return;

            if (!ServiceLocator.TryGet<IProjectManager>(out var projectManager))
            {
                _projectList.itemsSource = null;
                return;
            }

            var projects = projectManager.ListProjects();
            _projectList.itemsSource = projects;
            _projectList.makeItem = () => new Label();
            _projectList.bindItem = (element, i) =>
            {
                if (element is Label label && i >= 0 && i < projects.Count)
                {
                    var p = projects[i];
                    label.text = string.IsNullOrEmpty(p.Name) ? p.Id : p.Name;
                }
            };
        }

        private void OnProjectNewClicked()
        {
            if (!ServiceLocator.TryGet<IProjectManager>(out var projectManager))
            {
                ShowToast("Project service not available.");
                return;
            }

            projectManager.CreateNewProject("Untitled");
            ShowToast("New project created.");
            RefreshProjectList();
        }

        private void OnProjectLoadClicked()
        {
            if (_projectList?.itemsSource == null) return;
            if (!ServiceLocator.TryGet<IProjectManager>(out var projectManager))
            {
                ShowToast("Project service not available.");
                return;
            }

            var index = _projectList.selectedIndex;
            if (index < 0 || index >= _projectList.itemsSource.Count) return;

            var projects = (System.Collections.Generic.List<ProjectMetadata>)_projectList.itemsSource;
            var selected = projects[index];

            if (projectManager.HasUnsavedChanges && !string.IsNullOrEmpty(projectManager.CurrentProjectId))
            {
                projectManager.SaveProject(projectManager.CurrentProjectName ?? "Untitled");
                ShowToast("Current project auto-saved.");
            }

            projectManager.LoadProject(selected.Id, success =>
            {
                if (success)
                {
                    ShowToast($"Loaded project \"{selected.Name}\".");
                    HideProjectOverlay();
                }
                else
                {
                    ShowToast("Failed to load project.");
                }
            });
        }

        private void OnProjectDeleteClicked()
        {
            if (_projectList?.itemsSource == null) return;
            if (!ServiceLocator.TryGet<IProjectManager>(out var projectManager))
            {
                ShowToast("Project service not available.");
                return;
            }

            var index = _projectList.selectedIndex;
            if (index < 0 || index >= _projectList.itemsSource.Count) return;

            var projects = (System.Collections.Generic.List<ProjectMetadata>)_projectList.itemsSource;
            var selected = projects[index];

            projectManager.DeleteProject(selected.Id);
            ShowToast("Project deleted.");
            RefreshProjectList();
        }

        private void SelectLayer(int index)
        {
            if (_layerManager == null) return;
            _layerManager.ActiveLayerIndex = index;
            EventBus.Publish(new LayerSelectedEvent(index));
        }
    }
}
