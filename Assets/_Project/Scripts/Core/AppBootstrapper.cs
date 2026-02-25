using System.Collections;
using UnityEngine;
using Maptifier.Display;
using Maptifier.Input;
using Maptifier.Layers;
using Maptifier.Media;
using Maptifier.Drawing;
using Maptifier.Effects;
using Maptifier.Masking;
using Maptifier.Warping;
using Maptifier.Projects;

namespace Maptifier.Core
{
    /// <summary>
    /// Application entry point. Creates and registers all services
    /// with the ServiceLocator in the correct initialization order.
    /// Attach to a GameObject in the boot scene.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class AppBootstrapper : MonoBehaviour, ICoroutineRunner
    {
        [Header("Configuration")]
        [SerializeField] private AppConfig _appConfig;

        private void Awake()
        {
            if (ServiceLocator.IsInitialized)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (_appConfig != null)
                ServiceLocator.Register<AppConfig>(_appConfig);
            RegisterServices();
            EnsurePhase7Components();
            ServiceLocator.MarkReady();
        }

        private void EnsurePhase7Components()
        {
            if (GetComponent<GlobalExceptionHandler>() == null)
                gameObject.AddComponent<GlobalExceptionHandler>();
            if (GetComponent<ANRWatchdog>() == null)
                gameObject.AddComponent<ANRWatchdog>();
            if (GetComponent<PerformanceMonitor>() == null)
                gameObject.AddComponent<PerformanceMonitor>();
            if (GetComponent<ShaderWarmup>() == null)
                gameObject.AddComponent<ShaderWarmup>();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (GetComponent<ThermalMonitor>() == null)
                gameObject.AddComponent<ThermalMonitor>();
#endif
        }

        private void RegisterServices()
        {
            var rtPool = new RenderTexturePool();
            ServiceLocator.Register<IRenderTexturePool>(rtPool);

            var compositeRenderer = new CompositeRenderer(rtPool);
            ServiceLocator.Register<CompositeRenderer>(compositeRenderer);

#if UNITY_ANDROID && !UNITY_EDITOR
            ServiceLocator.Register<IDisplayService>(new AndroidDisplayService());
#else
            ServiceLocator.Register<IDisplayService>(new EditorDisplayService());
#endif

            ServiceLocator.Register<IInputService>(new InputService());
            ServiceLocator.Register<ICoroutineRunner>(this);
            ServiceLocator.Register<IMediaImportService>(new MediaImportService());
            ServiceLocator.Register<IWarpService>(new WarpService());
            ServiceLocator.Register<IMaskService>(new MaskService());
            ServiceLocator.Register<IDrawingService>(new DrawingService());
            ServiceLocator.Register<IEffectPipeline>(new EffectPipeline());
            ServiceLocator.Register<ILayerManager>(new LayerManager());
            ServiceLocator.Register<IProjectManager>(new ProjectManager());
            ServiceLocator.Register<IAdaptiveQuality>(new AdaptiveQualityService());
        }

        public void RunCoroutine(IEnumerator coroutine)
        {
            StartCoroutine(coroutine);
        }

        private void OnDestroy()
        {
            ServiceLocator.Reset();
        }
    }
}
