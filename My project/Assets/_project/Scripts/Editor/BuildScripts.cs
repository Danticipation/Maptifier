using System;
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

        // ---------------------------------------------------------------------------------
        // Release upload-key signing. Values come only from environment variables; passwords
        // are never logged and are cleared from PlayerSettings after every build attempt.
        //
        //   MAPTIFIER_KEYSTORE_PATH  absolute path to the upload keystore
        //                            (optional; default ~/.maptifier/maptifier-upload.keystore)
        //   MAPTIFIER_KEYSTORE_PASS  keystore (store) password
        //   MAPTIFIER_KEY_ALIAS      key alias (e.g. maptifier-upload)
        //   MAPTIFIER_KEY_PASS       key password
        //
        // Create a 25-year upload key once, OUTSIDE the repo (keytool prompts for passwords):
        //   mkdir -p ~/.maptifier && chmod 700 ~/.maptifier
        //   keytool -genkeypair -v -keystore ~/.maptifier/maptifier-upload.keystore \
        //     -alias maptifier-upload -keyalg RSA -keysize 4096 -validity 9125 -storetype PKCS12
        //
        // See docs/SIGNING.md. There is no debug-key fallback for this release method.
        // ---------------------------------------------------------------------------------
        private const string KeystorePathEnv = "MAPTIFIER_KEYSTORE_PATH";
        private const string KeystorePassEnv = "MAPTIFIER_KEYSTORE_PASS";
        private const string KeyAliasEnv     = "MAPTIFIER_KEY_ALIAS";
        private const string KeyPassEnv      = "MAPTIFIER_KEY_PASS";
        private const string DefaultKeystoreFileName = "maptifier-upload.keystore";

        private sealed class ReleaseSigning
        {
            public string KeystorePath;
            public bool PathFromEnv;
            public string KeystorePass;
            public string KeyAlias;
            public string KeyPass;
        }

        [MenuItem("Maptifier/Build/Android AAB (Release)")]
        public static void BuildAndroidAab()
        {
            // Fail fast before touching any settings if release signing is not configured.
            ReleaseSigning signing = ResolveReleaseSigningOrFail();

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

            bool   prevUseCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            string prevKeystoreName      = PlayerSettings.Android.keystoreName;
            string prevKeyAliasName      = PlayerSettings.Android.keyaliasName;

            try
            {
                ApplyReleaseSigning(signing);

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
            finally
            {
                // Drop secrets from the Editor session and restore the prior keystore path/alias
                // so ProjectSettings.asset never keeps credentials or a machine-specific path.
                PlayerSettings.Android.keystorePass      = string.Empty;
                PlayerSettings.Android.keyaliasPass      = string.Empty;
                PlayerSettings.Android.useCustomKeystore = prevUseCustomKeystore;
                PlayerSettings.Android.keystoreName      = prevKeystoreName;
                PlayerSettings.Android.keyaliasName      = prevKeyAliasName;
                signing.KeystorePass = null;
                signing.KeyPass      = null;
                AssetDatabase.SaveAssets();
            }
        }

        private static string DefaultLocalKeystorePath
        {
            get
            {
                string home = Environment.GetEnvironmentVariable("HOME");
                if (string.IsNullOrWhiteSpace(home))
                    home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home ?? string.Empty, ".maptifier", DefaultKeystoreFileName);
            }
        }

        private static string ExpandHome(string path)
        {
            if (string.IsNullOrEmpty(path) || path[0] != '~')
                return path;
            if (path.Length > 1 && path[1] != '/' && path[1] != '\\')
                return path;
            string home = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrWhiteSpace(home))
                home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home ?? string.Empty, path.Length > 2 ? path.Substring(2) : string.Empty);
        }

        /// <summary>
        /// Reads the upload-key settings from env vars and validates them without changing any
        /// PlayerSettings. Throws BuildFailedException naming every missing item. Never logs passwords.
        /// </summary>
        private static ReleaseSigning ResolveReleaseSigningOrFail()
        {
            string envPath   = Environment.GetEnvironmentVariable(KeystorePathEnv);
            bool pathFromEnv = !string.IsNullOrWhiteSpace(envPath);
            string keystorePath = pathFromEnv ? ExpandHome(envPath.Trim()) : DefaultLocalKeystorePath;

            string keystorePass = Environment.GetEnvironmentVariable(KeystorePassEnv);
            string keyAlias     = Environment.GetEnvironmentVariable(KeyAliasEnv);
            string keyPass      = Environment.GetEnvironmentVariable(KeyPassEnv);

            var problems = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(keystorePass)) problems.Add(KeystorePassEnv + " is not set");
            if (string.IsNullOrWhiteSpace(keyAlias))     problems.Add(KeyAliasEnv + " is not set");
            if (string.IsNullOrWhiteSpace(keyPass))      problems.Add(KeyPassEnv + " is not set");
            if (!File.Exists(keystorePath))
            {
                problems.Add(pathFromEnv
                    ? $"keystore not found at {keystorePath} (from {KeystorePathEnv})"
                    : $"keystore not found at default path {keystorePath} ({KeystorePathEnv} is not set)");
            }

            if (problems.Count > 0)
            {
                string msg = "Release signing not configured; refusing to build a release AAB (no debug-key fallback). " +
                             string.Join("; ", problems) +
                             ". See docs/SIGNING.md.";
                Debug.LogError(msg);
                throw new BuildFailedException(msg);
            }

            return new ReleaseSigning
            {
                KeystorePath = Path.GetFullPath(keystorePath),
                PathFromEnv  = pathFromEnv,
                KeystorePass = keystorePass,
                KeyAlias     = keyAlias.Trim(),
                KeyPass      = keyPass
            };
        }

        private static void ApplyReleaseSigning(ReleaseSigning signing)
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName      = signing.KeystorePath;
            PlayerSettings.Android.keystorePass      = signing.KeystorePass;
            PlayerSettings.Android.keyaliasName      = signing.KeyAlias;
            PlayerSettings.Android.keyaliasPass      = signing.KeyPass;

            // Path and alias only; passwords are never logged.
            Debug.Log($"Release keystore: {signing.KeystorePath} " +
                      $"(from {(signing.PathFromEnv ? KeystorePathEnv : "default path")}), alias={signing.KeyAlias}, passwords not logged.");
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