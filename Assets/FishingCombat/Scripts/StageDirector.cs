using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FishingPrototype
{
    public sealed class StageDirector : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("Player")]
        [Header("연결")]
        public PlayerController player;
        [UnityEngine.Serialization.FormerlySerializedAs("Rod")]
        public FishingRodController rod;

        private readonly List<HookableBody> waveOne = new List<HookableBody>();
        private readonly List<HookableBody> waveTwo = new List<HookableBody>();
        [Header("씬 연결")]
        public GameObject combatGate;
        public GameObject finishGate;
        public Transform startCheckpoint, middleCheckpoint;
        public Transform firstTrigger, secondTrigger;
        public Transform firstLeftSpawn, firstRightSpawn, secondLeftSpawn, secondRightSpawn;
        [Header("출현 프리팹")]
        public HookableBody normalEnemyPrefab, heavyEnemyPrefab;
        public GameObject warningPrefab;
        [Header("출현 설정")]
        public int firstWaveCount = 4, secondWaveCount = 6, maxLivingEnemies = 3;
        public float warningTime = 1.2f, spawnInterval = 1f, groupInterval = 3f;
        public Vector2 warningOffset = new Vector2(0f, -0.85f);
        public float heavySpawnOffset = 0.2f;
        public string ObjectiveText { get; private set; } = "";
        private bool waveOneStarted;
        private bool waveOneCleared;
        private bool catchReady;
        private bool waveTwoStarted;
        private bool stageComplete;
        private bool firstSpawnComplete, secondSpawnComplete;
        private GameObject spawnWarning;

        private void Start()
        {
            if (PlayerPrefs.GetInt("TangCheckpoint", 0) == 1)
            {
                waveOneStarted = waveOneCleared = catchReady = true;
                combatGate.SetActive(false);
                player.transform.position = middleCheckpoint.position;
                player.GetComponent<PlayerHealth>().SetCheckpoint(player.transform.position);
            }
            SetObjective(CombatText.Get("objective.start"));
        }

        private void Update()
        {
            if ((Keyboard.current?.eKey.wasPressedThisFrame ?? false) &&
                (player.transform.position.x < startCheckpoint.position.x + 3f || (waveOneCleared && Mathf.Abs(player.transform.position.x - middleCheckpoint.position.x) < 2f)))
            {
                bool second = waveOneCleared && player.transform.position.x > combatGate.transform.position.x;
                catchReady = second;
                Vector2 point = second ? middleCheckpoint.position : startCheckpoint.position;
                player.GetComponent<PlayerHealth>().SetCheckpoint(point);
                player.GetComponent<PlayerHealth>().Heal();
                PlayerPrefs.SetInt("TangCheckpoint", second ? 1 : 0);
                PlayerPrefs.Save();
                SetObjective(CombatText.Get("objective.saved"));
            }
            if (!waveOneStarted && player.transform.position.x > firstTrigger.position.x)
            {
                waveOneStarted = true;
                StartCoroutine(SpawnEncounter(waveOne, false));
            }

            if (waveOneStarted && firstSpawnComplete && !waveOneCleared && AllDefeated(waveOne))
            {
                waveOneCleared = true;
                combatGate.SetActive(false);
                SetObjective(CombatText.Get("objective.first_clear"));
            }

            if (waveOneCleared && !waveTwoStarted && player.transform.position.x > secondTrigger.position.x)
            {
                waveTwoStarted = true;
                StartCoroutine(SpawnEncounter(waveTwo, true));
            }

            if (waveTwoStarted && secondSpawnComplete && !stageComplete && AllDefeated(waveTwo))
            {
                stageComplete = true;
                finishGate.SetActive(false);
                SetObjective(CombatText.Get("objective.complete"));
            }
        }

        public void ResetEncounter()
        {
            StopAllCoroutines();
            if (spawnWarning) Destroy(spawnWarning);
            firstSpawnComplete = secondSpawnComplete = false;
            foreach (var enemy in waveOne) if (enemy) Destroy(enemy.gameObject);
            foreach (var enemy in waveTwo) if (enemy) Destroy(enemy.gameObject);
            waveOne.Clear(); waveTwo.Clear();
            foreach (var cloud in FindObjectsByType<PoisonCloud>(FindObjectsSortMode.None)) Destroy(cloud.gameObject);
            foreach (var star in FindObjectsByType<Shuriken>(FindObjectsSortMode.None)) Destroy(star.gameObject);
            waveTwoStarted = stageComplete = false; finishGate.SetActive(true);
            if (!catchReady) { waveOneStarted = waveOneCleared = false; combatGate.SetActive(true); }
            SetObjective(CombatText.Get("objective.retry"));
        }

        private IEnumerator SpawnEncounter(List<HookableBody> wave, bool second)
        {
            int total = second ? secondWaveCount : firstWaveCount;
            for (int i = 0; i < total; i++)
            {
                // Keep the arena readable: at most three living enemies at a time.
                while (LivingCount(wave) >= maxLivingEnemies) yield return new WaitForSeconds(0.3f);
                Vector2 spawnPoint = ChooseSpawnPoint(second);
                float x = spawnPoint.x;
                spawnWarning = Instantiate(warningPrefab, spawnPoint + warningOffset, Quaternion.identity);
                SetObjective(CombatText.Format("wave.approach_count", second ? CombatText.Get("wave.second") : CombatText.Get("wave.first"), i + 1, total));
                yield return new WaitForSeconds(warningTime);
                // Re-warn if the player moved onto the spawn point during the telegraph.
                while (Mathf.Abs(player.transform.position.x-x) < 2.5f)
                {
                    spawnPoint = ChooseSpawnPoint(second); x = spawnPoint.x;
                    spawnWarning.transform.position = spawnPoint + warningOffset;
                    yield return new WaitForSeconds(warningTime);
                }
                bool heavy = second ? i%3==2 : i==3;
                wave.Add(SpawnEnemy(heavy,spawnPoint + Vector2.up * (heavy ? heavySpawnOffset : 0f)));
                Destroy(spawnWarning); spawnWarning=null;
                SetObjective(CombatText.Format("wave.remaining_count", second ? CombatText.Get("wave.second") : CombatText.Get("wave.first"), LivingCount(wave) + total - i - 1));
                if(i+1<total) yield return new WaitForSeconds(i%2==1?groupInterval:spawnInterval);
            }
            if(second) secondSpawnComplete=true; else firstSpawnComplete=true;
        }
        private static int LivingCount(List<HookableBody> wave)
        {
            int count=0; foreach(var enemy in wave) if(enemy && enemy.Body.simulated) count++;
            return count;
        }

        private HookableBody SpawnEnemy(bool heavy, Vector2 position)
        {
            var enemy = Instantiate(heavy ? heavyEnemyPrefab : normalEnemyPrefab, position, Quaternion.identity);
            enemy.GetComponent<TangEnemyAI>().player = player;
            foreach (var star in FindObjectsByType<Shuriken>(FindObjectsSortMode.None)) star.ConfigureEnemy(enemy.GetComponent<Collider2D>());
            return enemy;
        }

        private Vector2 ChooseSpawnPoint(bool second)
        {
            Transform left = second ? secondLeftSpawn : firstLeftSpawn;
            Transform right = second ? secondRightSpawn : firstRightSpawn;
            return Mathf.Abs(player.transform.position.x - left.position.x) >= Mathf.Abs(player.transform.position.x - right.position.x)
                ? left.position : right.position;
        }
        private void SetObjective(string message) => ObjectiveText = message;

        private static bool AllDefeated(List<HookableBody> enemies)
        {
            if (enemies.Count == 0) return false;
            foreach (HookableBody enemy in enemies)
                if (enemy) return false;
            return true;
        }
    }
}
