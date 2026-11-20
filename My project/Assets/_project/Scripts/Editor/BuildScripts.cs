using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Maptifier.Editor
{
    public static class BuildScripts
    {
        private const string BootScenePath = "Assets/_project/Scenes/MAPTIFIER-SCENE.unity";
        private const string OutputFolder  = "Builds/Android";

        [MenuItem("Maptifier/Build/Android AAB (Release)")]
        public static void BuildAndroidAab()
        {
            // Force the correct scene list so EditorBuildSettings and the build always match
            var bootScene = new EditorBuildSettingsScene(BootScenePath, true);
            EditorBuildSettings.scenes = new[] { bootScene };
            AssetDatabase.SaveAssets();

            if (!File.Exists(BootScenePath))
            {
                Debug.LogError($"Boot scene missing at {BootScenePath}. Aborting.");
                return;
            }

            // Android release settings
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;

            var androidAppId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (IsForbiddenDefaultAndroidAppId(androidAppId))
            {
                var msg = $"Android application identifier '{androidAppId}' is still a Unity default. Set Player Settings > Android package name to a real ID (e.g. com.newsharpe.maptifier) before a release AAB build.";
                Debug.LogError(msg);
                throw new BuildFailedException(msg);
            }

            // Build
            Directory.CreateDirectory(OutputFolder);
            string aabPath = Path.Combine(OutputFolder, $"Maptifier-{PlayerSettings.bundleVersion}.aab");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { BootScenePath },
                locationPathName = aabPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"AAB succeeded: {aabPath} ({summary.totalSize} bytes)");
            else
                Debug.LogError($"AAB failed: {summary.result}");
        }


        private static bool IsForbiddenDefaultAndroidAppId(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
                return true;

            var id = appId.Trim();
            if (id.StartsWith("com.UnityTechnologies.", System.StringComparison.OrdinalIgnoreCase))
                return true;
            if (id.StartsWith("com.DefaultCompany.", System.StringComparison.OrdinalIgnoreCase))
                return true;
            // Unity URP/3D template identifiers, including hyphenated variants
            if (id.IndexOf("urpblank", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (id.IndexOf("urp-blank", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        [MenuItem("Maptifier/Build/Force Boot Scene Only")]
        public static void ForceBootSceneOnly()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("EditorBuildSettings now contains only the Boot scene (enabled).");
        }
    }
}