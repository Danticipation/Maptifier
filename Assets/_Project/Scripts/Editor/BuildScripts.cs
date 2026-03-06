using UnityEditor;

namespace Maptifier.Editor
{
    public static class BuildScripts
    {
        public static void BuildAndroidAab()
        {
            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Boot.unity" },
                locationPathName = "Builds/Android/Maptifier.aab",
                target = BuildTarget.Android,
                options = BuildOptions.CompressWithLz4HC
            };

            BuildPipeline.BuildPlayer(buildPlayerOptions);
        }
    }
}

