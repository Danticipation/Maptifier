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