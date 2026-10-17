#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.IO;

namespace FishingPrototype.Editor
{
    public static class PrototypeSceneBuilder
    {
        [MenuItem("Tools/Fishing Prototype/Rebuild Play Scene")]
        public static void Build()
        {
            const string folder = "Assets/Scenes";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, folder + "/FishingPrototype.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(folder + "/FishingPrototype.unity", true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Fishing prototype play scene created.");
        }

        [MenuItem("Tools/Fishing Prototype/Build Windows Demo")]
        public static void BuildWindowsDemo()
        {
            Build();
            string requestedPath = Environment.GetEnvironmentVariable("FISHING_DEMO_BUILD_PATH");
            string outputPath = string.IsNullOrWhiteSpace(requestedPath)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Windows", "FishingRodCombatDemo.exe"))
                : requestedPath;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/FishingPrototype.unity" },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Windows demo build failed: " + report.summary.result);
            Debug.Log("Fishing rod combat demo built: " + outputPath);
        }
    }
}
#endif
