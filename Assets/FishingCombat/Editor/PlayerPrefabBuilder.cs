#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishingPrototype.Editor
{
    // 최초 프리팹 이관용. 이미 존재하는 프리팹과 아트는 덮어쓰지 않는다.
    public static class PlayerPrefabBuilder
    {
        public const string playerPath = "Assets/FishingCombat/Prefabs/Prefab_Player.prefab";
        public const string rodPath = "Assets/FishingCombat/Prefabs/Prefab_Rod.prefab";
        private const string artPath = "Assets/FishingCombat/Art/Player";
        private const string scenePath = "Assets/FishingCombat/Scenes/FishingPrototype.unity";

        [MenuItem("낚싯대 전투/플레이어 프리팹 생성 및 씬 배치")]
        public static void CreateAndPlace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이를 종료한 뒤 프리팹을 배치하세요.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/FishingCombat/Prefabs");
            Directory.CreateDirectory(artPath);
            AssetDatabase.Refresh();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(rodPath)) CreateRod(preview);
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(playerPath)) CreatePlayer(preview);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool hasPlayer = false;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<PlayerController>(true)) hasPlayer = true;
            if (!hasPlayer)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(playerPath), scene);
                instance.name = "Player_Fisher";
                instance.transform.position = new Vector3(-8f, -1.15f, 0f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            CombatSceneBuilder.UpgradePlayer();
            AssetDatabase.SaveAssets();
            Check();
        }

        private static void CreateRod(Scene preview)
        {
            var rod = CombatAuthoring.CreateVisual("Weapon_FishingRod", Vector2.zero,
                new Vector2(1.25f, 0.09f), new Color(0.93f, 0.74f, 0.31f));
            SceneManager.MoveGameObjectToScene(rod, preview);
            rod.GetComponent<SpriteRenderer>().sortingOrder = 7;
            CombatShapes.DressRod(rod);
            BakeSprites(rod, "Rod");
            if (!PrefabUtility.SaveAsPrefabAsset(rod, rodPath)) throw new InvalidOperationException("낚싯대 프리팹 저장 실패");
            UnityEngine.Object.DestroyImmediate(rod);
        }

        private static void CreatePlayer(Scene preview)
        {
            var player = CombatAuthoring.CreateVisual("Player_Fisher", Vector2.zero,
                new Vector2(0.85f, 1.55f), new Color(0.2f, 0.72f, 0.95f));
            SceneManager.MoveGameObjectToScene(player, preview);
            var body = player.AddComponent<Rigidbody2D>();
            body.mass = 1.2f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            player.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 1f);
            var controller = player.AddComponent<PlayerController>();
            player.AddComponent<PlayerHealth>();
            var appearance = player.AddComponent<PlayerAppearance>();
            appearance.model = CombatShapes.DressPlayer(player);
            appearance.leftBoot = appearance.model.Find("Sprite_왼쪽장화");
            appearance.rightBoot = appearance.model.Find("Sprite_오른쪽장화");
            BakeSprites(player, "Player");

            var rod = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(rodPath), preview);
            rod.transform.SetParent(player.transform, false);
            rod.transform.localPosition = new Vector3(0.52f, 0.28f, 0f);
            rod.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            controller.rodVisual = rod.transform;
            var system = new GameObject("System_FishingRod");
            system.transform.SetParent(player.transform, false);
            var combat = system.AddComponent<FishingRodController>();
            combat.player = controller;
            combat.rodVisual = rod.transform;
            if (!PrefabUtility.SaveAsPrefabAsset(player, playerPath)) throw new InvalidOperationException("플레이어 프리팹 저장 실패");
            UnityEngine.Object.DestroyImmediate(player);
        }

        public static void BakeSprites(GameObject root, string group)
        {
            var saved = new Dictionary<Sprite, Sprite>();
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var source = renderer.sprite;
                if (!source || EditorUtility.IsPersistent(source)) continue;
                if (!saved.TryGetValue(source, out var sprite))
                {
                    string path = artPath + "/Sprite_" + group + "_" + saved.Count.ToString("00") + ".png";
                    if (!File.Exists(path))
                    {
                        File.WriteAllBytes(path, source.texture.EncodeToPNG());
                        AssetDatabase.ImportAsset(path);
                        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.spritePixelsPerUnit = source.pixelsPerUnit;
                        importer.alphaIsTransparency = true;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.filterMode = source.texture.filterMode;
                        importer.SaveAndReimport();
                    }
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (!sprite) throw new InvalidOperationException("스프라이트 저장 실패: " + path);
                    saved.Add(source, sprite);
                }
                renderer.sprite = sprite;
            }
        }

        [MenuItem("낚싯대 전투/플레이어 프리팹 검사")]
        public static void Check()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            if (!prefab) throw new Exception("플레이어 프리팹이 없습니다.");
            var player = prefab.GetComponent<PlayerController>();
            var appearance = prefab.GetComponent<PlayerAppearance>();
            var rod = prefab.GetComponentInChildren<FishingRodController>(true);
            if (!player || !prefab.GetComponent<PlayerHealth>() || !appearance || !appearance.model ||
                !appearance.leftBoot || !appearance.rightBoot || !rod || rod.player != player || rod.rodVisual != player.rodVisual)
                throw new Exception("플레이어 프리팹 연결이 누락되었습니다.");
            foreach (var behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                if (!behaviour || !MonoScript.FromMonoBehaviour(behaviour)) throw new Exception("저장되지 않은 플레이어 컴포넌트가 있습니다.");
            foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.sprite && !EditorUtility.IsPersistent(renderer.sprite)) throw new Exception("임시 스프라이트가 프리팹에 남았습니다.");
            CheckEditablePose();
            Debug.Log("PLAYER PREFAB CHECKS PASSED");
        }
        private static void CheckEditablePose()
        {
            var root = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var player = root.GetComponent<PlayerController>();
                typeof(PlayerController).GetMethod("Awake", flags).Invoke(player, null);
                var appearance = root.GetComponent<PlayerAppearance>();
                appearance.model.localPosition = new Vector3(0.12f, 0.08f, 0f);
                appearance.model.localScale = new Vector3(1.1f, 0.9f, 1f);
                appearance.leftBoot.localPosition = new Vector3(-0.22f, -0.43f, 0f);
                int count = root.GetComponentsInChildren<SpriteRenderer>(true).Length;
                typeof(PlayerAppearance).GetMethod("Start", flags).Invoke(appearance, null);
                typeof(PlayerAppearance).GetMethod("LateUpdate", flags).Invoke(appearance, null);
                if (root.GetComponentsInChildren<SpriteRenderer>(true).Length != count ||
                    appearance.model.localPosition != new Vector3(0.12f, 0.08f, 0f) ||
                    appearance.model.localScale != new Vector3(1.1f, 0.9f, 1f) ||
                    appearance.leftBoot.localPosition != new Vector3(-0.22f, -0.43f, 0f))
                    throw new Exception("플레이어 외형 초기화가 에디터 설정을 덮어씁니다.");
                var rod = root.GetComponentInChildren<FishingRodController>(true);
                rod.rodVisual.localPosition = new Vector3(0.66f, 0.44f, 0f);
                rod.rodVisual.localRotation = Quaternion.Euler(0f, 0f, 42f);
                typeof(FishingRodController).GetMethod("Start", flags).Invoke(rod, null);
                var update = typeof(FishingRodController).GetMethod("UpdateRodVisual", flags);
                update.Invoke(rod, null);
                if (rod.rodVisual.localPosition != new Vector3(0.66f, 0.44f, 0f) ||
                    Mathf.Abs(Mathf.DeltaAngle(rod.rodVisual.localEulerAngles.z, 42f)) > 0.001f)
                    throw new Exception("낚싯대가 에디터 위치/각도를 덮어씁니다.");
                player.Body.linearVelocity = Vector2.left;
                update.Invoke(rod, null);
                if (rod.rodVisual.localPosition != new Vector3(-0.66f, 0.44f, 0f) ||
                    Mathf.Abs(Mathf.DeltaAngle(rod.rodVisual.localEulerAngles.z, -42f)) > 0.001f)
                    throw new Exception("낚싯대 좌우 반전이 에디터 기준 자세를 유지하지 않습니다.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
#endif
