using UnityEngine;

namespace Maptifier.Core
{
    [CreateAssetMenu(fileName = "AppConfig", menuName = "Maptifier/App Config")]
    public class AppConfig : ScriptableObject
    {
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private int maxEffectsPerLayer = 3;
        [SerializeField] private int maxUndoSteps = 20;
        [SerializeField] private int thumbnailSize = 256;
        [SerializeField] private float autoSaveIntervalSeconds = 60f;
        [SerializeField] private Vector2Int defaultCompositeResolution = new(1920, 1080);
        [SerializeField] private bool enableAnalytics = true;
        [SerializeField] private string projectsFolderName = "Projects";
        [SerializeField] private string feedbackUrl = "https://github.com/maptifier/maptifier/issues";

        public int TargetFrameRate => targetFrameRate;
        public int MaxEffectsPerLayer => maxEffectsPerLayer;
        public int MaxUndoSteps => maxUndoSteps;
        public int ThumbnailSize => thumbnailSize;
        public float AutoSaveIntervalSeconds => autoSaveIntervalSeconds;
        public Vector2Int DefaultCompositeResolution => defaultCompositeResolution;
        public bool EnableAnalytics => enableAnalytics;
        public string ProjectsFolderName => projectsFolderName;
        public string FeedbackUrl => feedbackUrl;
    }
}
