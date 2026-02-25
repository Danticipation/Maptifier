using UnityEngine;
using UnityEngine.UIElements;
using Maptifier.Core;
using Maptifier.Layers;
using Maptifier.Media;

namespace Maptifier.UI
{
    /// <summary>
    /// UI panel for creating and editing text layers. Provides controls for
    /// text content, font size, color, alignment, and outline settings.
    /// Integrates with the layer system — text appears as a standard IMediaSource.
    /// </summary>
    public class TextEditorPanel : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private TextField _contentField;
        private SliderInt _fontSizeSlider;
        private DropdownField _alignmentDropdown;
        private Toggle _outlineToggle;
        private SliderInt _outlineWidthSlider;
        private VisualElement _panel;
        private Button _createButton;
        private Button _closeButton;

        private TextSource _activeTextSource;
        private TextConfig _workingConfig = new();
        private bool _isVisible;

        private static readonly Color[] PresetColors =
        {
            Color.white, Color.black, Color.red, Color.green, Color.blue,
            Color.yellow, Color.cyan, Color.magenta,
            new Color(1f, 0.5f, 0f), new Color(0.5f, 0f, 1f)
        };

        private void OnEnable()
        {
            if (_uiDocument == null)
                _uiDocument = FindFirstObjectByType<UIDocument>();
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            if (root == null) return;
            if (_panel != null && _panel.parent == root) return;

            BuildUI(root);
        }

        private void BuildUI(VisualElement root)
        {
            _panel = new VisualElement();
            _panel.style.position = Position.Absolute;
            _panel.style.left = 0;
            _panel.style.right = 0;
            _panel.style.bottom = 64;
            _panel.style.height = 320;
            _panel.style.backgroundColor = new Color(0.09f, 0.11f, 0.13f, 0.95f);
            _panel.style.paddingTop = 16;
            _panel.style.paddingBottom = 16;
            _panel.style.paddingLeft = 16;
            _panel.style.paddingRight = 16;
            _panel.style.display = DisplayStyle.None;

            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 12;

            var title = new Label("Text Layer");
            title.style.fontSize = 18;
            title.style.color = new Color(0.9f, 0.93f, 0.95f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            _closeButton = new Button(() => Hide()) { text = "✕" };
            _closeButton.style.width = 36;
            _closeButton.style.height = 36;
            _closeButton.style.fontSize = 18;
            _closeButton.style.backgroundColor = Color.clear;
            _closeButton.style.color = new Color(0.55f, 0.58f, 0.62f);
            _closeButton.style.borderTopWidth = 0;
            _closeButton.style.borderBottomWidth = 0;
            _closeButton.style.borderLeftWidth = 0;
            _closeButton.style.borderRightWidth = 0;
            header.Add(_closeButton);
            _panel.Add(header);

            // Text content input
            _contentField = new TextField("Text");
            _contentField.multiline = true;
            _contentField.style.height = 80;
            _contentField.value = _workingConfig.Content;
            _contentField.RegisterValueChangedCallback(evt =>
            {
                _workingConfig.Content = evt.newValue;
                ApplyChanges();
            });
            _panel.Add(_contentField);

            // Font size slider
            _fontSizeSlider = new SliderInt("Font Size", 12, 200);
            _fontSizeSlider.value = _workingConfig.FontSize;
            _fontSizeSlider.RegisterValueChangedCallback(evt =>
            {
                _workingConfig.FontSize = evt.newValue;
                ApplyChanges();
            });
            _panel.Add(_fontSizeSlider);

            // Color presets row
            var colorRow = new VisualElement();
            colorRow.style.flexDirection = FlexDirection.Row;
            colorRow.style.marginTop = 8;
            colorRow.style.marginBottom = 8;

            var colorLabel = new Label("Color");
            colorLabel.style.color = new Color(0.55f, 0.58f, 0.62f);
            colorLabel.style.marginRight = 8;
            colorLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            colorRow.Add(colorLabel);

            foreach (var color in PresetColors)
            {
                var swatch = new Button(() =>
                {
                    _workingConfig.TextColor = color;
                    ApplyChanges();
                });
                swatch.style.width = 28;
                swatch.style.height = 28;
                swatch.style.backgroundColor = color;
                swatch.style.borderTopLeftRadius = 4;
                swatch.style.borderTopRightRadius = 4;
                swatch.style.borderBottomLeftRadius = 4;
                swatch.style.borderBottomRightRadius = 4;
                swatch.style.marginRight = 4;
                swatch.style.borderTopWidth = color == Color.black ? 1 : 0;
                swatch.style.borderBottomWidth = color == Color.black ? 1 : 0;
                swatch.style.borderLeftWidth = color == Color.black ? 1 : 0;
                swatch.style.borderRightWidth = color == Color.black ? 1 : 0;
                swatch.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
                swatch.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
                swatch.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
                swatch.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
                colorRow.Add(swatch);
            }
            _panel.Add(colorRow);

            // Alignment dropdown
            _alignmentDropdown = new DropdownField("Alignment",
                new System.Collections.Generic.List<string> { "Left", "Center", "Right" }, 1);
            _alignmentDropdown.RegisterValueChangedCallback(evt =>
            {
                _workingConfig.Alignment = evt.newValue switch
                {
                    "Left" => TextAnchor.MiddleLeft,
                    "Right" => TextAnchor.MiddleRight,
                    _ => TextAnchor.MiddleCenter
                };
                ApplyChanges();
            });
            _panel.Add(_alignmentDropdown);

            // Outline toggle + width
            _outlineToggle = new Toggle("Outline");
            _outlineToggle.value = _workingConfig.OutlineWidth > 0;
            _outlineToggle.RegisterValueChangedCallback(evt =>
            {
                _workingConfig.OutlineWidth = evt.newValue ? 2f : 0f;
                _outlineWidthSlider.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                ApplyChanges();
            });
            _panel.Add(_outlineToggle);

            _outlineWidthSlider = new SliderInt("Outline Size", 1, 8);
            _outlineWidthSlider.value = 2;
            _outlineWidthSlider.style.display = DisplayStyle.None;
            _outlineWidthSlider.RegisterValueChangedCallback(evt =>
            {
                _workingConfig.OutlineWidth = evt.newValue;
                ApplyChanges();
            });
            _panel.Add(_outlineWidthSlider);

            // Create button (shown when no active text source)
            _createButton = new Button(() => CreateTextLayer())
            {
                text = "Create Text Layer"
            };
            _createButton.style.height = 44;
            _createButton.style.backgroundColor = new Color(0f, 0.83f, 1f);
            _createButton.style.color = Color.black;
            _createButton.style.borderTopLeftRadius = 8;
            _createButton.style.borderTopRightRadius = 8;
            _createButton.style.borderBottomLeftRadius = 8;
            _createButton.style.borderBottomRightRadius = 8;
            _createButton.style.fontSize = 14;
            _createButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _createButton.style.marginTop = 12;
            _panel.Add(_createButton);

            root.Add(_panel);
        }

        public void Show()
        {
            _isVisible = true;
            if (_panel != null)
                _panel.style.display = DisplayStyle.Flex;

            RefreshFromActiveLayer();
        }

        public void Hide()
        {
            _isVisible = false;
            if (_panel != null)
                _panel.style.display = DisplayStyle.None;
        }

        private void RefreshFromActiveLayer()
        {
            if (!ServiceLocator.TryGet<ILayerManager>(out var layerMgr)) return;

            var layer = layerMgr.ActiveLayer;
            if (layer?.MediaSource is TextSource ts)
            {
                _activeTextSource = ts;
                _workingConfig = new TextConfig
                {
                    Content = ts.Config.Content,
                    FontSize = ts.Config.FontSize,
                    TextColor = ts.Config.TextColor,
                    BackgroundColor = ts.Config.BackgroundColor,
                    OutlineColor = ts.Config.OutlineColor,
                    OutlineWidth = ts.Config.OutlineWidth,
                    Alignment = ts.Config.Alignment,
                    Style = ts.Config.Style,
                    WordWrap = ts.Config.WordWrap
                };

                if (_contentField != null) _contentField.value = _workingConfig.Content;
                if (_fontSizeSlider != null) _fontSizeSlider.value = _workingConfig.FontSize;
                if (_createButton != null) _createButton.style.display = DisplayStyle.None;
            }
            else
            {
                _activeTextSource = null;
                _workingConfig = new TextConfig();
                if (_createButton != null) _createButton.style.display = DisplayStyle.Flex;
            }
        }

        private void CreateTextLayer()
        {
            if (!ServiceLocator.TryGet<ILayerManager>(out var layerMgr)) return;

            var textSource = new TextSource(_workingConfig);
            var layer = layerMgr.ActiveLayer;
            if (layer != null)
            {
                layer.MediaSource?.Dispose();
                layer.MediaSource = textSource;
                _activeTextSource = textSource;
                if (_createButton != null)
                    _createButton.style.display = DisplayStyle.None;
            }
        }

        private void ApplyChanges()
        {
            if (_activeTextSource == null) return;
            _activeTextSource.UpdateConfig(_workingConfig);
        }
    }
}
