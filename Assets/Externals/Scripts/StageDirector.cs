using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishingPrototype
{
    public sealed class StageDirector : MonoBehaviour
    {
        [Header("씬 연결")]
        public PlayerController player;
        public FishingRodController rod;
        public Transform mapEntry, mapExit;
        public string nextScene = "Road_02";
        [Header("출현 프리팹")]
        public HookableBody poisonEnemyPrefab, shurikenEnemyPrefab;
        public GameObject warningPrefab;
        [Header("연속 출현 (임시값)")]
        [Min(1)] public int maxLivingEnemies = 4;
        [Min(0.1f)] public float warningTime = 1.2f, spawnInterval = 2f;
        [Min(3f)] public float spawnAheadDistance = 14f;
        [Min(1f)] public float minimumSpawnDistance = 3f;
        [Min(5f)] public float cleanupBehindDistance = 20f;
        public Vector2 warningOffset = new Vector2(0, -0.85f);
        public float heavySpawnOffset = 0.2f;
        public string ObjectiveText { get; private set; } = "";
        public bool IsTransitioning { get; private set; }
        public int MapNumber => mapNumber;
        static int mapNumber = 1;
        static float incomingHealth = -1;
        readonly List<HookableBody> enemies = new List<HookableBody>();
        GameObject spawnWarning;
        int spawnedCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { mapNumber = 1; incomingHealth = -1; }

        void Start()
        {
            if (!player || !rod || !mapEntry || !mapExit || mapExit.position.x <= mapEntry.position.x
                || !poisonEnemyPrefab || !shurikenEnemyPrefab || !warningPrefab
                || !Application.CanStreamedLevelBeLoaded(nextScene))
                throw new System.InvalidOperationException("맵 입구·출구·출현 프리팹과 빌드 목록의 다음 씬을 확인하세요.");
            player.transform.position = mapEntry.position;
            player.Body.position = mapEntry.position;
            player.Body.linearVelocity = Vector2.zero;
            var health = player.GetComponent<PlayerHealth>();
            health.SetCheckpoint(mapEntry.position);
            if (incomingHealth >= 0) { health.RestoreHealth(incomingHealth); incomingHealth = -1; }
            var camera = Camera.main;
            if (camera && camera.TryGetComponent<SmoothCameraFollow>(out var follow))
                camera.transform.position = new Vector3(player.transform.position.x + follow.offset.x, follow.offset.y, follow.offset.z);
            StartCoroutine(SpawnContinuously());
        }

        void Update()
        {
            if (IsTransitioning || !player.Body.simulated) return;
            CleanupEnemies();
            ObjectiveText = CombatText.Format("road.progress", mapNumber, LivingCount());
            if (player.transform.position.x >= mapExit.position.x) GoToNextMap();
        }

        void GoToNextMap()
        {
            if (IsTransitioning || !player.Body.simulated) return;
            IsTransitioning = true;
            StopAllCoroutines();
            if (spawnWarning) Destroy(spawnWarning);
            rod.Interrupt();
            incomingHealth = player.GetComponent<PlayerHealth>().Health;
            mapNumber++;
            player.ControlsLocked = true;
            ObjectiveText = CombatText.Get("road.transition");
            SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
        }

        public void ResetEncounter()
        {
            if (IsTransitioning) return;
            StopAllCoroutines();
            if (spawnWarning) Destroy(spawnWarning);
            foreach (var enemy in enemies) if (enemy) Destroy(enemy.gameObject);
            enemies.Clear();
            foreach (var cloud in FindObjectsByType<PoisonCloud>(FindObjectsSortMode.None)) Destroy(cloud.gameObject);
            foreach (var star in FindObjectsByType<Shuriken>(FindObjectsSortMode.None)) Destroy(star.gameObject);
            spawnedCount = 0;
            StartCoroutine(SpawnContinuously());
        }

        IEnumerator SpawnContinuously()
        {
            yield return new WaitForSeconds(spawnInterval);
            while (!IsTransitioning)
            {
                Vector2 point = ChooseSpawnPoint();
                if (!player.Body.simulated || LivingCount() >= Mathf.Max(1, maxLivingEnemies) || !CanSpawnAt(point))
                { yield return new WaitForSeconds(.25f); continue; }
                spawnWarning = Instantiate(warningPrefab, point + warningOffset, Quaternion.identity);
                yield return new WaitForSeconds(warningTime);
                if (spawnWarning) Destroy(spawnWarning);
                spawnWarning = null;
                // 예고 중 플레이어가 출현 지점을 지나거나 접근하면 새 위치에서 다시 예고한다.
                if (!player.Body.simulated || !CanSpawnAt(point)) continue;
                bool heavy = spawnedCount++ % 2 == 1;
                var enemy = Instantiate(heavy ? shurikenEnemyPrefab : poisonEnemyPrefab,
                    point + Vector2.up * (heavy ? heavySpawnOffset : 0), Quaternion.identity);
                enemy.respawnOnDeath = false;
                enemy.GetComponent<TangEnemyAI>().player = player;
                enemies.Add(enemy);
                foreach (var star in FindObjectsByType<Shuriken>(FindObjectsSortMode.None)) star.ConfigureEnemy(enemy.GetComponent<Collider2D>());
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        Vector2 ChooseSpawnPoint() => new Vector2(
            Mathf.Min(player.transform.position.x + Mathf.Max(spawnAheadDistance, minimumSpawnDistance), mapExit.position.x - 2f), mapEntry.position.y);
        bool CanSpawnAt(Vector2 point) => point.x - player.transform.position.x >= minimumSpawnDistance;
        int LivingCount()
        {
            int count = 0;
            foreach (var enemy in enemies) if (enemy && enemy.Health > 0 && enemy.Body.simulated) count++;
            return count;
        }
        void CleanupEnemies()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                if (!enemy) { enemies.RemoveAt(i); continue; }
                if (!enemy.IsHooked && (enemy.transform.position.y < -12f
                    || (!enemy.IsThrown && enemy.transform.position.x < player.transform.position.x - cleanupBehindDistance)))
                { Destroy(enemy.gameObject); enemies.RemoveAt(i); }
            }
        }
    }
}
