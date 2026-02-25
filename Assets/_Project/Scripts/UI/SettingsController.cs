using UnityEngine;
using UnityEngine.UIElements;
using Maptifier.Core;
using Maptifier.Drawing;

namespace Maptifier.UI
{
    /// <summary>
    /// Manages the settings overlay panel. Display, performance, drawing, and about sections.
    /// All settings persist to PlayerPrefs and apply immediately.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SettingsController : MonoBehaviour
    {
        private const string ResolutionOverrideKey = "Maptifier_ResolutionOverride";
        private const string ForceDisplayModeKey = "Maptifier_ForceDisplayMode";
        private const string PerformanceModeKey = "Maptifier_PerformanceMode";
        private const string DefaultBrushSizeKey = "Maptifier_DefaultBrushSize";
        private const string PressureSensitivityKey = "Maptifier_PressureSensitivity";
        private const string FeedbackUrl = "https://maptifier.app/feedback";

        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset _settingsLayout;
        [SerializeField] private StyleSheet _theme;

        private UIDocument _document;
        private VisualElement _overlay;
        private VisualElement _panel;
        private Button _closeBtn;
        private DropdownField _resolutionDropdown;
        private Toggle _forceDisplayToggle;
        private Toggle _performanceToggle;
        private Slider _brushSizeSlider;
        private Slider _pressureSlider;
        private Label _brushSizeLabel;
        private Label _pressureLabel;
        private Label _versionLabel;
        private Button _licensesBtn;
        private Button _feedbackBtn;

        public static SettingsController Instance { get; private set; }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_settingsLayout != null)
                _document.visualTreeAsset = _settingsLayout;
            if (_theme != null)
                _document.styleSheets.Add(_theme);

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            CacheElements();
            LoadFromPlayerPrefs();
            WireCallbacks();
            Hide();
        }

        private void CacheElements()
        {
            var root = _document?.rootVisualElement;
            if (root == null) return;

            _overlay = root;
            _panel = root.Q<VisualElement>("settings-panel");
            _closeBtn = root.Q<Button>("settings-close-btn");
            _resolutionDropdown = root.Q<DropdownField>("resolution-dropdown");
            _forceDisplayToggle = root.Q<Toggle>("force-display-toggle");
            _performanceToggle = root.Q<Toggle>("performance-mode-toggle");
            _brushSizeSlider = root.Q<Slider>("brush-size-slider");
            _pressureSlider = root.Q<Slider>("pressure-slider");
            _brushSizeLabel = root.Q<Label>("brush-size-label");
            _pressureLabel = root.Q<Label>("pressure-label");
            _versionLabel = root.Q<Label>("version-label");
            _licensesBtn = root.Q<Button>("licenses-btn");
            _feedbackBtn = root.Q<Button>("feedback-btn");
        }

        private void LoadFromPlayerPrefs()
        {
            var resolutionIndex = PlayerPrefs.GetInt(ResolutionOverrideKey, 0);
            var resolutionChoices = new[] { "Auto", "720p", "1080p", "4K" };
            if (_resolutionDropdown != null)
            {
                _resolutionDropdown.choices = new System.Collections.Generic.List<string>(resolutionChoices);
                _resolutionDropdown.index = Mathf.Clamp(resolutionIndex, 0, resolutionChoices.Length - 1);
            }

            if (_forceDisplayToggle != null)
                _forceDisplayToggle.value = PlayerPrefs.GetInt(ForceDisplayModeKey, 0) == 1;

            if (_performanceToggle != null)
                _performanceToggle.value = PlayerPrefs.GetInt(PerformanceModeKey, 0) == 1;

            if (_brushSizeSlider != null)
            {
                var brushSize = PlayerPrefs.GetFloat(DefaultBrushSizeKey, 24f);
                _brushSizeSlider.SetValueWithoutNotify(Mathf.Clamp(brushSize, 1f, 100f));
                UpdateBrushSizeLabel(brushSize);
            }

            if (_pressureSlider != null)
            {
                var pressure = PlayerPrefs.GetFloat(PressureSensitivityKey, 1f);
                _pressureSlider.SetValueWithoutNotify(Mathf.Clamp(pressure, 0f, 2f));
                UpdatePressureLabel(pressure);
            }

            if (_versionLabel != null)
                _versionLabel.text = $"Version {Application.version}";
        }

        private void WireCallbacks()
        {
            if (_closeBtn != null)
                _closeBtn.clicked += Hide;

            if (_overlay != null)
            {
                _overlay.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target == _overlay)
                        Hide();
                });
            }

            if (_resolutionDropdown != null)
            {
                _resolutionDropdown.RegisterValueChangedCallback(evt =>
                {
                    var idx = _resolutionDropdown.index;
                    PlayerPrefs.SetInt(ResolutionOverrideKey, idx);
                    PlayerPrefs.Save();
                });
            }

            if (_forceDisplayToggle != null)
            {
                _forceDisplayToggle.RegisterValueChangedCallback(evt =>
                {
                    PlayerPrefs.SetInt(ForceDisplayModeKey, evt.newValue ? 1 : 0);
                    PlayerPrefs.Save();
                });
            }

            if (_performanceToggle != null)
            {
                _performanceToggle.RegisterValueChangedCallback(evt =>
                {
                    PlayerPrefs.SetInt(PerformanceModeKey, evt.newValue ? 1 : 0);
                    PlayerPrefs.Save();
                    if (ServiceLocator.TryGet<IAdaptiveQuality>(out var adaptive))
                        adaptive.SetPerformanceMode(evt.newValue);
                });
            }

            if (_brushSizeSlider != null)
            {
                _brushSizeSlider.RegisterValueChangedCallback(evt =>
                {
                    var v = evt.newValue;
                    PlayerPrefs.SetFloat(DefaultBrushSizeKey, v);
                    PlayerPrefs.Save();
                    UpdateBrushSizeLabel(v);
                    if (ServiceLocator.TryGet<IDrawingService>(out var drawing))
                        drawing.BrushSize = v;
                });
            }

            if (_pressureSlider != null)
            {
                _pressureSlider.RegisterValueChangedCallback(evt =>
                {
                    var v = evt.newValue;
                    PlayerPrefs.SetFloat(PressureSensitivityKey, v);
                    PlayerPrefs.Save();
                    UpdatePressureLabel(v);
                });
            }

            if (_licensesBtn != null)
                _licensesBtn.clicked += () => { /* TODO: Show licenses modal */ };

            if (_feedbackBtn != null)
                _feedbackBtn.clicked += () => Application.OpenURL(FeedbackUrl);
        }

        private void UpdateBrushSizeLabel(float value)
        {
            if (_brushSizeLabel != null)
                _brushSizeLabel.text = $"Default Brush Size ({Mathf.RoundToInt(value)})";
        }

        private void UpdatePressureLabel(float value)
        {
            if (_pressureLabel != null)
                _pressureLabel.text = $"Pressure Sensitivity ({value:F1})";
        }

        public void Show()
        {
            if (_overlay != null)
            {
                _overlay.style.display = DisplayStyle.Flex;
                _overlay.style.pointerEvents = PointerEvents.Auto;
            }
        }

        public void Hide()
        {
            if (_overlay != null)
            {
                _overlay.style.display = DisplayStyle.None;
                _overlay.style.pointerEvents = PointerEvents.None;
            }
        }
    }
}
