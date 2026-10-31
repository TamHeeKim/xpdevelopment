#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using System;
namespace FishingPrototype.Editor
{
    public static class CombatProjectMenu
    {
        const string scenePath = "Assets/FishingCombat/Scenes/FishingPrototype.unity";
        [MenuItem("낚싯대 전투/전투 씬 열기")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(scenePath);
        }
        [MenuItem("낚싯대 전투/검사 실행")]
        public static void Check() => CombatChecks.Run();
        [MenuItem("낚싯대 전투/윈도우 실행 파일 만들기")]
        public static void Build()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { scenePath }, locationPathName = "Builds/FishingCombat/FishingCombat.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Combat build failed");
        }
    }
}
#endif
