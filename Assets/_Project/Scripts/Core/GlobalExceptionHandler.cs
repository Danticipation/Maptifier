using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace Maptifier.Core
{
    /// <summary>
    /// Catches unhandled exceptions, logs to Crashlytics (if available), shows recovery dialog,
    /// and detects crash loops to offer app data reset.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public class GlobalExceptionHandler : MonoBehaviour
    {
        private const int CrashLoopThreshold = 3;
        private const float CrashLoopWindowSeconds = 60f;
        [SerializeField] private AppConfig _appConfig;

        private readonly List<float> _exceptionTimestamps = new(8);
        private string _lastExceptionMessage;
        private string _lastExceptionStack;
        private UIDocument _recoveryDocument;
        private VisualElement _recoveryRoot;
        private bool _dialogVisible;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += HandleLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type != LogType.Exception) return;

            _lastExceptionMessage = logString;
            _lastExceptionStack = stackTrace ?? string.Empty;

            // Log to Crashlytics if available (Firebase Crashlytics Unity SDK)
            LogToCrashlytics(logString, stackTrace);

            // Track for crash loop detection
            _exceptionTimestamps.Add(Time.realtimeSinceStartup);
            PruneOldExceptions();

            if (_dialogVisible) return;

            ShowRecoveryDialog();
        }

        private void PruneOldExceptions()
        {
            var cutoff = Time.realtimeSinceStartup - CrashLoopWindowSeconds;
            for (var i = _exceptionTimestamps.Count - 1; i >= 0; i--)
            {
                if (_exceptionTimestamps[i] < cutoff)
                    _exceptionTimestamps.RemoveAt(i);
            }
        }

        private void LogToCrashlytics(string message, string stackTrace)
        {
#if MAPTIFIER_CRASHLYTICS
            try
            {
                Firebase.Crashlytics.Crashlytics.Log($"{message}\n{stackTrace}");
                Firebase.Crashlytics.Crashlytics.LogException(new Exception(message));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GlobalExceptionHandler] Crashlytics log failed: {ex.Message}");
            }
#else
            // No-op when Crashlytics not linked; still log to console
            Debug.LogError($"[GlobalExceptionHandler] Unhandled exception: {message}\n{stackTrace}");
#endif
        }

        private void ShowRecoveryDialog()
        {
            _dialogVisible = true;

            var root = GetOrCreateRecoveryRoot();
            if (root != null)
            {
                ShowRecoveryDialogUIToolkit(root);
            }
            else
            {
                // Fallback: use OnGUI if UI Toolkit root not ready
                enabled = true;
            }
        }

        private void ShowRecoveryDialogUIToolkit(VisualElement root)
        {
            root.Clear();
            root.style.display = DisplayStyle.Flex;

            var panel = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                    backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f),
                    justifyContent = Justify.Center,
                    alignItems = Align.Center
                }
            };

            var card = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f),
                    paddingLeft = 24, paddingRight = 24, paddingTop = 24, paddingBottom = 24,
                    borderRadius = 8,
                    minWidth = 280,
                    maxWidth = 400
                }
            };

            var title = new Label("Something went wrong")
            {
                style =
                {
                    fontSize = 20,
                    color = Color.white,
                    marginBottom = 12,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };

            var message = new Label("An unexpected error occurred. You can retry or report the issue.")
            {
                style =
                {
                    fontSize = 14,
                    color = new Color(0.9f, 0.9f, 0.9f, 1f),
                    marginBottom = 20,
                    whiteSpace = WhiteSpace.Normal
                }
            };

            var buttonRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd,
                    gap = 12
                }
            };

            var retryBtn = new Button(OnRetry) { text = "Retry" };
            retryBtn.style.paddingLeft = 16;
            retryBtn.style.paddingRight = 16;
            retryBtn.style.paddingTop = 8;
            retryBtn.style.paddingBottom = 8;

            var reportBtn = new Button(OnReport) { text = "Report" };
            reportBtn.style.paddingLeft = 16;
            reportBtn.style.paddingRight = 16;
            reportBtn.style.paddingTop = 8;
            reportBtn.style.paddingBottom = 8;

            buttonRow.Add(retryBtn);
            buttonRow.Add(reportBtn);

            card.Add(title);
            card.Add(message);
            card.Add(buttonRow);

            // Crash loop: offer reset
            if (_exceptionTimestamps.Count >= CrashLoopThreshold)
            {
                var resetLabel = new Label("Multiple errors detected. You may want to reset app data.")
                {
                    style =
                    {
                        fontSize = 12,
                        color = new Color(1f, 0.8f, 0.4f, 1f),
                        marginTop = 12,
                        marginBottom = 8
                    }
                };
                var resetBtn = new Button(OnResetAppData) { text = "Reset app data" };
                resetBtn.style.paddingLeft = 16;
                resetBtn.style.paddingRight = 16;
                resetBtn.style.paddingTop = 8;
                resetBtn.style.paddingBottom = 8;
                card.Add(resetLabel);
                card.Add(resetBtn);
            }

            panel.Add(card);
            root.Add(panel);
        }

        private void OnGUI()
        {
            if (!_dialogVisible || _recoveryRoot != null) return;

            // Fallback when UI Toolkit not ready
            var rect = new Rect(Screen.width * 0.5f - 150, Screen.height * 0.5f - 80, 300, 160);
            GUI.Box(rect, "Something went wrong");
            GUI.Label(new Rect(rect.x + 20, rect.y + 40, rect.width - 40, 40),
                "An unexpected error occurred. You can retry or report the issue.");
            if (GUI.Button(new Rect(rect.x + rect.width - 100, rect.y + 100, 80, 30), "Retry"))
                OnRetry();
            if (GUI.Button(new Rect(rect.x + rect.width - 190, rect.y + 100, 80, 30), "Report"))
                OnReport();
        }

        private VisualElement GetOrCreateRecoveryRoot()
        {
            if (_recoveryRoot != null) return _recoveryRoot;

            _recoveryDocument = GetComponent<UIDocument>();
            if (_recoveryDocument == null)
                _recoveryDocument = gameObject.AddComponent<UIDocument>();

            _recoveryDocument.sortingOrder = 32767; // Top-most
            _recoveryRoot = _recoveryDocument.rootVisualElement;
            if (_recoveryRoot != null)
            {
                _recoveryRoot.style.position = Position.Absolute;
                _recoveryRoot.style.left = _recoveryRoot.style.right = _recoveryRoot.style.top = _recoveryRoot.style.bottom = 0f;
                _recoveryRoot.pickingMode = PickingMode.Position;
            }

            return _recoveryRoot;
        }

        private void OnRetry()
        {
            _dialogVisible = false;
            if (_recoveryRoot != null)
            {
                _recoveryRoot.Clear();
                _recoveryRoot.style.display = DisplayStyle.None;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnReport()
        {
            var report = $"{_lastExceptionMessage}\n\n{_lastExceptionStack}";
            GUIUtility.systemCopyBuffer = report;

            var feedbackUrl = GetFeedbackUrl();
            Application.OpenURL(feedbackUrl);
        }

        private string GetFeedbackUrl()
        {
            var config = _appConfig ?? (ServiceLocator.TryGet<AppConfig>(out var c) ? c : null);
            return config != null && !string.IsNullOrEmpty(config.FeedbackUrl)
                ? config.FeedbackUrl
                : "https://github.com/maptifier/maptifier/issues";
        }

        private void OnResetAppData()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            if (System.IO.Directory.Exists(Application.persistentDataPath))
            {
                try
                {
                    foreach (var file in System.IO.Directory.GetFiles(Application.persistentDataPath))
                        System.IO.File.Delete(file);
                    foreach (var dir in System.IO.Directory.GetDirectories(Application.persistentDataPath))
                        System.IO.Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GlobalExceptionHandler] Reset cleanup failed: {ex.Message}");
                }
            }

            OnRetry();
        }
    }
}
