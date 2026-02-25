using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using Maptifier.Core;
using Maptifier.Projects;

namespace Maptifier.UI
{
    /// <summary>
    /// Controls the 5-step onboarding flow. Manages pages via swipe or Next/Back,
    /// updates step indicators, and switches to main UI when complete.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(-500)]
    public class OnboardingController : MonoBehaviour
    {
        private const string OnboardingCompleteKey = "Maptifier_OnboardingComplete";
        private const int TotalSteps = 5;
        private const float TransitionDuration = 0.25f;

        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset _onboardingLayout;
        [SerializeField] private VisualTreeAsset _mainLayout;
        [SerializeField] private StyleSheet _theme;
        [SerializeField] private StyleSheet _onboardingStyles;

        [Header("References")]
        [SerializeField] private MainUIController _mainUIController;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _pagesContainer;
        private Button _skipBtn;
        private Button _nextBtn;
        private Button _backBtn;
        private Button _createProjectBtn;
        private VisualElement _onboardStatusDot;
        private Label _onboardStatusLabel;

        private int _currentStep;
        private float _pageWidth;
        private float _swipeStartX;
        private bool _isTransitioning;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();

            if (PlayerPrefs.GetInt(OnboardingCompleteKey, 0) == 1)
            {
                SwitchToMainUI();
                enabled = false;
                return;
            }

            LoadOnboarding();
            if (_mainUIController != null)
                _mainUIController.enabled = false;
        }

        private void OnEnable()
        {
            if (_document?.rootVisualElement == null) return;
            EventBus.Subscribe<DisplayConnectedEvent>(OnDisplayConnected);
            EventBus.Subscribe<DisplayDisconnectedEvent>(OnDisplayDisconnected);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DisplayConnectedEvent>(OnDisplayConnected);
            EventBus.Unsubscribe<DisplayDisconnectedEvent>(OnDisplayDisconnected);
        }

        private void LoadOnboarding()
        {
            if (_onboardingLayout != null)
                _document.visualTreeAsset = _onboardingLayout;
            if (_theme != null)
                _document.styleSheets.Add(_theme);
            if (_onboardingStyles != null)
                _document.styleSheets.Add(_onboardingStyles);
        }

        private void Start()
        {
            CacheElements();
            SetupPageWidths();
            _currentStep = 0;
            UpdateStepIndicators();
            UpdateNavigationVisibility();
            UpdatePagesPosition();
            WireCallbacks();
        }

        private void Update()
        {
            if (_pagesContainer == null || _root == null) return;
            var w = _root.resolvedStyle.width;
            if (w > 0 && Mathf.Abs(w - _pageWidth) > 0.1f)
            {
                _pageWidth = w;
                SetupPageWidths();
                UpdatePagesPosition();
            }
        }

        private void SetupPageWidths()
        {
            if (_root == null || _pagesContainer == null) return;
            var viewportW = _root.resolvedStyle.width;
            if (viewportW <= 0) return;

            _pageWidth = viewportW;
            _pagesContainer.style.width = viewportW * TotalSteps;

            for (int i = 0; i < TotalSteps; i++)
            {
                var page = _root.Q<VisualElement>($"page-{i}");
                if (page != null)
                    page.style.width = viewportW;
            }
        }

        private void CacheElements()
        {
            _root = _document?.rootVisualElement;
            if (_root == null) return;

            _pagesContainer = _root.Q<VisualElement>("pages-container");
            _skipBtn = _root.Q<Button>("skip-btn");
            _nextBtn = _root.Q<Button>("next-btn");
            _backBtn = _root.Q<Button>("back-btn");
            _createProjectBtn = _root.Q<Button>("create-project-btn");
            _onboardStatusDot = _root.Q<VisualElement>("onboard-status-dot");
            _onboardStatusLabel = _root.Q<Label>("onboard-status-label");

            if (_pagesContainer != null && _pagesContainer.parent != null)
                _pageWidth = _pagesContainer.parent.resolvedStyle.width;
        }

        private void WireCallbacks()
        {
            if (_skipBtn != null)
                _skipBtn.clicked += OnSkipClicked;

            if (_nextBtn != null)
                _nextBtn.clicked += OnNextClicked;

            if (_backBtn != null)
                _backBtn.clicked += OnBackClicked;

            if (_createProjectBtn != null)
                _createProjectBtn.clicked += OnCreateProjectClicked;

            if (_pagesContainer != null)
            {
                _pagesContainer.RegisterCallback<PointerDownEvent>(OnPointerDown);
                _pagesContainer.RegisterCallback<PointerUpEvent>(OnPointerUp);
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            _swipeStartX = evt.position.x;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_isTransitioning) return;

            var delta = evt.position.x - _swipeStartX;
            var threshold = _pageWidth * 0.2f;

            if (delta < -threshold && _currentStep < TotalSteps - 1)
                GoToStep(_currentStep + 1);
            else if (delta > threshold && _currentStep > 0)
                GoToStep(_currentStep - 1);
        }

        private void OnSkipClicked()
        {
            CompleteOnboarding(createProject: false);
        }

        private void OnNextClicked()
        {
            if (_currentStep >= TotalSteps - 1)
            {
                CompleteOnboarding(createProject: false);
                return;
            }
            GoToStep(_currentStep + 1);
        }

        private void OnBackClicked()
        {
            if (_currentStep > 0)
                GoToStep(_currentStep - 1);
        }

        private void OnCreateProjectClicked()
        {
            CompleteOnboarding(createProject: true);
        }

        private void GoToStep(int step)
        {
            if (_isTransitioning || step < 0 || step >= TotalSteps) return;

            _currentStep = step;
            UpdateStepIndicators();
            UpdateNavigationVisibility();
            StartCoroutine(AnimateToStep(step));
        }

        private IEnumerator AnimateToStep(int step)
        {
            _isTransitioning = true;
            var targetX = -step * _pageWidth;

            if (_pagesContainer != null)
            {
                var startTranslate = _pagesContainer.style.translate;
                var startX = startTranslate.value.x.value;
                var elapsed = 0f;

                while (elapsed < TransitionDuration)
                {
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / TransitionDuration);
                    var eased = 1f - (1f - t) * (1f - t);
                    var x = Mathf.Lerp(startX, targetX, eased);
                    _pagesContainer.style.translate = new Translate(x, 0);
                    yield return null;
                }
            }

            UpdatePagesPosition();
            _isTransitioning = false;
        }

        private void UpdatePagesPosition()
        {
            if (_pagesContainer == null) return;

            if (_pageWidth <= 0 && _pagesContainer.parent != null)
                _pageWidth = _pagesContainer.parent.resolvedStyle.width;

            var x = -_currentStep * _pageWidth;
            _pagesContainer.style.translate = new Translate(x, 0);
        }

        private void UpdateStepIndicators()
        {
            const string ActiveClass = "maptifier-step-dot--active";
            for (int i = 0; i < TotalSteps; i++)
            {
                var dot = _root?.Q<VisualElement>($"dot-{i}");
                if (dot == null) continue;

                if (i == _currentStep)
                    dot.AddToClassList(ActiveClass);
                else
                    dot.RemoveFromClassList(ActiveClass);
            }
        }

        private void UpdateNavigationVisibility()
        {
            if (_backBtn != null)
            {
                _backBtn.style.visibility = _currentStep > 0
                    ? Visibility.Visible
                    : Visibility.Hidden;
            }

            if (_nextBtn != null)
            {
                _nextBtn.text = _currentStep >= TotalSteps - 1 ? "Get Started" : "Next";
            }
        }

        private void CompleteOnboarding(bool createProject)
        {
            PlayerPrefs.SetInt(OnboardingCompleteKey, 1);
            PlayerPrefs.Save();

            if (createProject && ServiceLocator.TryGet<IProjectManager>(out var projectManager))
            {
                projectManager.CreateNewProject("Untitled");
            }

            SwitchToMainUI();
        }

        private void SwitchToMainUI()
        {
            if (_mainLayout != null)
                _document.visualTreeAsset = _mainLayout;

            if (_mainUIController != null)
            {
                _mainUIController.enabled = true;
            }

            enabled = false;
        }

        private void OnDisplayConnected(DisplayConnectedEvent evt)
        {
            if (_onboardStatusDot != null)
            {
                _onboardStatusDot.RemoveFromClassList("maptifier-status-dot--disconnected");
                _onboardStatusDot.AddToClassList("maptifier-status-dot--connected");
            }
            if (_onboardStatusLabel != null)
                _onboardStatusLabel.text = $"{evt.Resolution.x}×{evt.Resolution.y}";
        }

        private void OnDisplayDisconnected(DisplayDisconnectedEvent evt)
        {
            if (_onboardStatusDot != null)
            {
                _onboardStatusDot.RemoveFromClassList("maptifier-status-dot--connected");
                _onboardStatusDot.AddToClassList("maptifier-status-dot--disconnected");
            }
            if (_onboardStatusLabel != null)
                _onboardStatusLabel.text = "No display detected";
        }
    }
}
