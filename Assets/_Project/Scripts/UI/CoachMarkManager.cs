using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using Maptifier.Core;

namespace Maptifier.UI
{
    /// <summary>
    /// Contextual tooltip system. Shows floating tooltips on first use of each tool.
    /// Tracks which tips have been shown via PlayerPrefs bitmask.
    /// </summary>
    public class CoachMarkManager : MonoBehaviour
    {
        private const string CoachMarksShownKey = "Maptifier_CoachMarksShown";
        private const float AutoDismissSeconds = 4f;

        public enum ToolTip
        {
            Warp = 0,
            Mask = 1,
            Draw = 2,
            Effects = 3
        }

        private static readonly (ToolTip Tip, string Message)[] TipDefinitions =
        {
            (ToolTip.Warp, "Drag corners to fit your surface"),
            (ToolTip.Mask, "Paint to reveal or hide"),
            (ToolTip.Draw, "Draw with pressure-sensitive strokes"),
            (ToolTip.Effects, "Add real-time visual effects")
        };

        [Header("References")]
        [SerializeField] private UIDocument _mainDocument;
        [SerializeField] private StyleSheet _theme;

        private VisualElement _root;
        private VisualElement _tooltipContainer;
        private Label _tooltipLabel;
        private VisualElement _tooltipArrow;
        private Coroutine _dismissCoroutine;
        private Button _currentToolButton;

        private void Start()
        {
            if (_mainDocument == null)
                _mainDocument = FindObjectOfType<UIDocument>();

            if (_mainDocument?.rootVisualElement == null) return;

            _root = _mainDocument.rootVisualElement;
            CreateTooltipUI();
            SubscribeToToolChanges();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ToolChangedEvent>(OnToolChanged);
        }

        private void CreateTooltipUI()
        {
            _tooltipContainer = new VisualElement
            {
                name = "coach-mark-tooltip",
                pickingMode = PickingMode.Ignore
            };
            _tooltipContainer.style.position = Position.Absolute;
            _tooltipContainer.style.bottom = 100;
            _tooltipContainer.style.left = 0;
            _tooltipContainer.style.right = 0;
            _tooltipContainer.style.alignItems = Align.Center;
            _tooltipContainer.style.display = DisplayStyle.None;
            _tooltipContainer.style.zIndex = 100;

            var bubble = new VisualElement();
            bubble.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.09f, 0.11f, 0.13f, 0.95f));
            bubble.style.paddingLeft = 16;
            bubble.style.paddingRight = 16;
            bubble.style.paddingTop = 10;
            bubble.style.paddingBottom = 10;
            bubble.style.borderTopLeftRadius = 8;
            bubble.style.borderTopRightRadius = 8;
            bubble.style.borderBottomLeftRadius = 8;
            bubble.style.borderBottomRightRadius = 8;
            bubble.style.maxWidth = 280;

            _tooltipArrow = new VisualElement();
            _tooltipArrow.style.width = 12;
            _tooltipArrow.style.height = 12;
            _tooltipArrow.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.09f, 0.11f, 0.13f, 0.95f));
            _tooltipArrow.style.position = Position.Absolute;
            _tooltipArrow.style.bottom = -6;
            _tooltipArrow.style.left = Length.Percent(50);
            _tooltipArrow.style.translate = new Translate(Length.Percent(-50), 0);
            _tooltipArrow.style.rotate = new Rotate(45);

            _tooltipLabel = new Label
            {
                style =
                {
                    color = new StyleColor(new UnityEngine.Color(0.9f, 0.93f, 0.95f)),
                    fontSize = 14,
                    whiteSpace = WhiteSpace.Normal
                }
            };

            bubble.Add(_tooltipArrow);
            bubble.Add(_tooltipLabel);
            _tooltipContainer.Add(bubble);
            _root.Add(_tooltipContainer);

            if (_theme != null)
                _tooltipContainer.styleSheets.Add(_theme);
        }

        private void SubscribeToToolChanges()
        {
            EventBus.Subscribe<ToolChangedEvent>(OnToolChanged);
        }

        private void OnToolChanged(ToolChangedEvent evt)
        {
            var tip = evt.Tool switch
            {
                ToolType.Warp => ToolTip.Warp,
                ToolType.Mask => ToolTip.Mask,
                ToolType.Draw => ToolTip.Draw,
                ToolType.Effects => ToolTip.Effects,
                _ => (ToolTip?)null
            };

            if (tip.HasValue && !HasShownTip(tip.Value))
            {
                ShowTip(tip.Value);
            }
        }

        private bool HasShownTip(ToolTip tip)
        {
            var mask = PlayerPrefs.GetInt(CoachMarksShownKey, 0);
            return (mask & (1 << (int)tip)) != 0;
        }

        private void MarkTipShown(ToolTip tip)
        {
            var mask = PlayerPrefs.GetInt(CoachMarksShownKey, 0);
            mask |= 1 << (int)tip;
            PlayerPrefs.SetInt(CoachMarksShownKey, mask);
            PlayerPrefs.Save();
        }

        private void ShowTip(ToolTip tip)
        {
            string message = null;
            foreach (var (t, m) in TipDefinitions)
            {
                if (t == tip)
                {
                    message = m;
                    break;
                }
            }

            if (string.IsNullOrEmpty(message) || _tooltipContainer == null) return;

            if (_dismissCoroutine != null)
            {
                StopCoroutine(_dismissCoroutine);
                _dismissCoroutine = null;
            }

            MarkTipShown(tip);

            _tooltipLabel.text = message;
            _tooltipContainer.style.display = DisplayStyle.Flex;

            _currentToolButton = GetToolButton(tip);
            if (_currentToolButton != null)
            {
                _currentToolButton.RegisterCallback<GeometryChangedEvent>(PositionNearButton);
                PositionNearButton(default);
            }

            _dismissCoroutine = StartCoroutine(DismissAfterDelay());
        }

        private Button GetToolButton(ToolTip tip)
        {
            var name = tip switch
            {
                ToolTip.Warp => "tool-warp",
                ToolTip.Mask => "tool-mask",
                ToolTip.Draw => "tool-draw",
                ToolTip.Effects => "tool-effects",
                _ => null
            };
            return _root?.Q<Button>(name);
        }

        private void PositionNearButton(GeometryChangedEvent evt)
        {
            if (_tooltipContainer == null || _tooltipContainer.style.display == DisplayStyle.None)
                return;

            var toolbar = _root?.Q<VisualElement>("toolbar");
            if (toolbar == null) return;

            var toolbarWorldBound = toolbar.worldBound;
            _tooltipContainer.style.bottom = toolbarWorldBound.height + 24;
        }

        private IEnumerator DismissAfterDelay()
        {
            yield return new WaitForSeconds(AutoDismissSeconds);
            HideTip();
            _dismissCoroutine = null;
        }

        private void HideTip()
        {
            if (_currentToolButton != null)
            {
                _currentToolButton.UnregisterCallback<GeometryChangedEvent>(PositionNearButton);
                _currentToolButton = null;
            }
            if (_tooltipContainer != null)
                _tooltipContainer.style.display = DisplayStyle.None;
        }
    }
}
