using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FishingPrototype.Integration;

namespace FishingPrototype.Editor
{
    public static class IntegrationSceneSetup
    {
        public const string scenePath = "Assets/Externals/Scenes/Integration.unity";

        // Explicit authoring command, never called automatically on editor startup.
        [MenuItem("낚싯대 전투/데이터 테이블/Integration 테스트 적 배치")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(scenePath);
            var integrationEnemy = Object.FindFirstObjectByType<IntegrationEnemy>();
            if (!integrationEnemy)
            {
                var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                enemy.name = "Enemy_TableDummy";
                integrationEnemy = enemy.AddComponent<IntegrationEnemy>();
            }
            var definition = AssetDatabase.LoadAssetAtPath<CombatStatDefinition>(CombatSheetConnection.assetFolder
                + "/Enemy/Data_" + System.Uri.EscapeDataString(integrationEnemy.SourceEnemyId) + ".asset");
            if (!definition) throw new System.InvalidOperationException("Import spreadsheet SOs before assigning the test enemy.");
            var serialized = new SerializedObject(integrationEnemy);
            serialized.FindProperty("statDefinition").objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("낚싯대 전투/데이터 테이블/Integration 테스트 적 배치", true)]
        private static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
