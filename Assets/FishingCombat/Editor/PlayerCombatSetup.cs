#if UNITY_EDITOR
using FishingPrototype.Integration;
using UnityEditor;
using UnityEngine;

namespace FishingPrototype.Editor
{
    public static class PlayerCombatSetup
    {
        [MenuItem("낚싯대 전투/플레이어 새 기획 프리팹 연결")]
        public static void UpgradePrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("플레이를 종료한 뒤 프리팹을 연결하세요.");
            UpgradePlayer();
            foreach (string name in new[] { "NormalEnemy", "HeavyEnemy" })
            {
                string path = "Assets/FishingCombat/Prefabs/Prefab_" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var body = root.GetComponent<HookableBody>();
                    body.statDefinition = AssetDatabase.LoadAssetAtPath<CombatStatDefinition>("Assets/Externals/Data/Stats/Enemy/Data_ENEMY_CH01_DANGGA_001.asset");
                    if (!body.targetingCollider)
                    {
                        var area = new GameObject("Collider_CatchTarget"); area.transform.SetParent(root.transform, false);
                        var circle = area.AddComponent<CircleCollider2D>(); circle.isTrigger = true;
                        var bounds = root.GetComponent<Collider2D>().bounds;
                        circle.radius = (bounds.extents.magnitude + 0.15f) / Mathf.Max(Mathf.Abs(root.transform.lossyScale.x), Mathf.Abs(root.transform.lossyScale.y));
                        body.targetingCollider = circle;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }
        private static void UpgradePlayer()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.playerPath);
            try
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                var rod = root.GetComponentInChildren<FishingRodController>();
                rod.statDefinition = AssetDatabase.LoadAssetAtPath<CombatStatDefinition>("Assets/Externals/Data/Stats/Player/Data_PLAYER_001.asset");
                var data = new SerializedObject(rod);
                if (!data.FindProperty("attackCollider").objectReferenceValue)
                {
                    var area = new GameObject("Collider_BasicAttack"); area.transform.SetParent(rod.transform, false);
                    var body = area.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
                    var collider = area.AddComponent<BoxCollider2D>(); collider.isTrigger = true; collider.enabled = false;
                    data.FindProperty("attackCollider").objectReferenceValue = collider;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabBuilder.playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
#endif
