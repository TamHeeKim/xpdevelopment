using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FishingPrototype
{
    public sealed class GroundSurface : MonoBehaviour { }

    [RequireComponent(typeof(HookableBody))]
    public sealed class EnemyCombatAI : MonoBehaviour
    {
        public enum EnemyType { Rusher, Bruiser }

        private EnemyType type;
        private PlayerController player;
        private PlayerHealth playerHealth;
        private HookableBody hookable;
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private Color baseColor;
        private float moveSpeed;
        private float attackRange;
        private float damage;
        private float nextAttack;
        private float attackMoment;
        private bool windingUp;

        private void Awake()
        {
            hookable = GetComponent<HookableBody>();
            body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
        }

        public void Configure(EnemyType enemyType, PlayerController target, Color color)
        {
            type = enemyType;
            player = target;
            playerHealth = target.GetComponent<PlayerHealth>();
            baseColor = color;
            moveSpeed = type == EnemyType.Rusher ? 4.2f : 2.35f;
            attackRange = type == EnemyType.Rusher ? 1.25f : 2.15f;
            damage = type == EnemyType.Rusher ? 10f : 19f;
        }

        private void FixedUpdate()
        {
            if (!player || !body.simulated || hookable.IsHooked) return;
            if (hookable.IsThrown) { windingUp = false; return; }

            Vector2 delta = player.Body.position - body.position;
            float direction = Mathf.Sign(delta.x);

            if (windingUp)
            {
                body.linearVelocity = new Vector2(Mathf.MoveTowards(body.linearVelocity.x, 0f, 20f * Time.fixedDeltaTime), body.linearVelocity.y);
                if (Time.time >= attackMoment) PerformAttack(direction);
                return;
            }

            if (Mathf.Abs(body.linearVelocity.x) > moveSpeed * 1.7f || Mathf.Abs(body.linearVelocity.y) > 3f) return;

            if (Mathf.Abs(delta.x) <= attackRange && Mathf.Abs(delta.y) < 1.8f && Time.time >= nextAttack)
            {
                windingUp = true;
                attackMoment = Time.time + (type == EnemyType.Rusher ? 0.22f : 0.5f);
                visual.color = new Color(1f, 0.72f, 0.2f);
                return;
            }

            if (Mathf.Abs(delta.x) < 13f)
            {
                float desired = direction * moveSpeed;
                body.linearVelocity = new Vector2(Mathf.MoveTowards(body.linearVelocity.x, desired, 12f * Time.fixedDeltaTime), body.linearVelocity.y);
            }
        }

        private void PerformAttack(float direction)
        {
            windingUp = false;
            visual.color = baseColor;
            nextAttack = Time.time + (type == EnemyType.Rusher ? 1.05f : 1.65f);
            float currentDistance = Mathf.Abs(player.Body.position.x - body.position.x);
            if (currentDistance <= attackRange + 0.65f)
            {
                Vector2 knockback = new Vector2(direction * (type == EnemyType.Rusher ? 5.5f : 9.5f), type == EnemyType.Rusher ? 2.5f : 4.2f);
                playerHealth.ReceiveDamage(damage, knockback);
            }
            body.AddForce(new Vector2(direction * (type == EnemyType.Rusher ? 2f : 5f), 0f), ForceMode2D.Impulse);
        }
    }

    public sealed class StageDirector : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("Player")]
        [Header("연결")]
        public PlayerController player;
        [UnityEngine.Serialization.FormerlySerializedAs("Rod")]
        public FishingRodController rod;

        private readonly List<HookableBody> waveOne = new List<HookableBody>();
        private readonly List<HookableBody> waveTwo = new List<HookableBody>();
        private GameObject combatGate;
        private GameObject finishGate;

        public string ObjectiveText => objective ? objective.text : "";
        private TextMesh objective;
        private bool waveOneStarted;
        private bool waveOneCleared;
        private bool catchReady;
        private bool waveTwoStarted;
        private bool stageComplete;
        private bool firstSpawnComplete, secondSpawnComplete;
        private GameObject spawnWarning;

        private void Start()
        {
            combatGate = CreateGate("Environment_CombatGate", 10.5f, new Color(0.85f, 0.25f, 0.22f));
            finishGate = CreateGate("Environment_FinishGate", 36.5f, new Color(0.74f, 0.24f, 0.78f));

            for (int i = 0; i < 3; i++)
            {
                var bottle = PrototypeBootstrap.CreateHookable("Prop_PoisonBottle", new Vector2(i * 12f, -1.5f), new Vector2(0.5f, 0.8f), Color.cyan, HookableBody.BodyKind.Prop, 1f, 500f, false, "object.bottle");
                bottle.AddComponent<PoisonBottle>();
            }
            CreateObjectiveHud();
            if (PlayerPrefs.GetInt("TangCheckpoint", 0) == 1)
            {
                waveOneStarted = waveOneCleared = catchReady = true;
                combatGate.SetActive(false);
                player.transform.position = new Vector2(14f, -1.15f);
                player.GetComponent<PlayerHealth>().SetCheckpoint(player.transform.position);
            }
            SetObjective(CombatText.Get("objective.start"));
        }

        private void Update()
        {
            if ((Keyboard.current?.eKey.wasPressedThisFrame ?? false) &&
                (player.transform.position.x < -5f || (waveOneCleared && Mathf.Abs(player.transform.position.x - 14f) < 2f)))
            {
                bool second = waveOneCleared && player.transform.position.x > 10f;
                catchReady = second;
                Vector2 point = new Vector2(second ? 14f : -8f, -1.15f);
                player.GetComponent<PlayerHealth>().SetCheckpoint(point);
                player.GetComponent<PlayerHealth>().Heal();
                PlayerPrefs.SetInt("TangCheckpoint", second ? 1 : 0);
                PlayerPrefs.Save();
                SetObjective(CombatText.Get("objective.saved"));
            }
            if (!waveOneStarted && player.transform.position.x > -4.5f)
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

            if (waveOneCleared && !waveTwoStarted && player.transform.position.x > 20.5f)
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

        public static float ChooseSpawnX(float playerX, bool second)
        {
            float left = second ? 23f : 1.5f, right = second ? 34f : 8.5f;
            return Mathf.Abs(playerX-left) >= Mathf.Abs(playerX-right) ? left : right;
        }
        private IEnumerator SpawnEncounter(List<HookableBody> wave, bool second)
        {
            int total = second ? 6 : 4;
            for (int i = 0; i < total; i++)
            {
                // Keep the arena readable: at most three living enemies at a time.
                while (LivingCount(wave) >= 3) yield return new WaitForSeconds(0.3f);
                float x = ChooseSpawnX(player.transform.position.x, second);
                spawnWarning = PrototypeBootstrap.CreateVisual("Sprite_SpawnWarning", new Vector2(x, -1.95f), new Vector2(1.5f,0.12f), new Color(1f,0.55f,0.2f));
                SetObjective(CombatText.Format("wave.approach_count", second ? CombatText.Get("wave.second") : CombatText.Get("wave.first"), i + 1, total));
                yield return new WaitForSeconds(1.2f);
                // Re-warn if the player moved onto the spawn point during the telegraph.
                while (Mathf.Abs(player.transform.position.x-x) < 2.5f)
                {
                    x=ChooseSpawnX(player.transform.position.x,second);
                    spawnWarning.transform.position=new Vector2(x,-1.95f);
                    yield return new WaitForSeconds(1.2f);
                }
                bool heavy = second ? i%3==2 : i==3;
                wave.Add(SpawnEnemy(heavy?EnemyCombatAI.EnemyType.Bruiser:EnemyCombatAI.EnemyType.Rusher,new Vector2(x,heavy?-.9f:-1.1f)));
                Destroy(spawnWarning); spawnWarning=null;
                SetObjective(CombatText.Format("wave.remaining_count", second ? CombatText.Get("wave.second") : CombatText.Get("wave.first"), LivingCount(wave) + total - i - 1));
                if(i+1<total) yield return new WaitForSeconds(i%2==1?3f:1f);
            }
            if(second) secondSpawnComplete=true; else firstSpawnComplete=true;
        }
        private static int LivingCount(List<HookableBody> wave)
        {
            int count=0; foreach(var enemy in wave) if(enemy && enemy.Body.simulated) count++;
            return count;
        }

        private HookableBody SpawnEnemy(EnemyCombatAI.EnemyType type, Vector2 position)
        {
            bool bruiser = type == EnemyCombatAI.EnemyType.Bruiser;
            Color color = bruiser ? new Color(0.62f, 0.22f, 0.75f) : new Color(0.92f, 0.22f, 0.18f);
            GameObject enemy = PrototypeBootstrap.CreateHookable(bruiser ? "Enemy_Heavy" : "Enemy_Normal", position,
                bruiser ? new Vector2(1.35f, 2.15f) : new Vector2(0.85f, 1.65f), color,
                HookableBody.BodyKind.Enemy, bruiser ? 3.4f : 1.15f, bruiser ? 150f : 72f, false, bruiser ? "enemy.heavy" : "enemy.normal");
            enemy.GetComponent<Rigidbody2D>().freezeRotation = true;
            CombatShapes.DressEnemy(enemy, bruiser);
            TangEnemyAI ai = enemy.AddComponent<TangEnemyAI>();
            ai.player = player;
            foreach (var star in FindObjectsByType<Shuriken>(FindObjectsSortMode.None)) star.ConfigureEnemy(enemy.GetComponent<Collider2D>());
            return enemy.GetComponent<HookableBody>();
        }

        private static GameObject CreateGate(string name, float x, Color color)
        {
            GameObject gate = PrototypeBootstrap.CreateVisual(name, new Vector2(x, 1.3f), new Vector2(0.3f, 6.6f), color);
            gate.GetComponent<SpriteRenderer>().color = new Color(color.r, color.g, color.b, 0.72f);
            gate.AddComponent<BoxCollider2D>();
            return gate;
        }

        private void CreateObjectiveHud()
        {
            GameObject go = new GameObject("Text_Objective");
            go.transform.SetParent(Camera.main.transform, false);
            go.transform.localPosition = new Vector3(0f, 3.85f, 10f);
            objective = go.AddComponent<TextMesh>();
            objective.font = PrototypeBootstrap.KoreanFont;
            objective.GetComponent<MeshRenderer>().sharedMaterial = PrototypeBootstrap.KoreanFont.material;
            objective.GetComponent<MeshRenderer>().enabled = false;
            objective.anchor = TextAnchor.UpperCenter;
            objective.alignment = TextAlignment.Center;
            objective.fontSize = 46;
            objective.characterSize = 0.055f;
            objective.color = new Color(1f, 0.82f, 0.3f);
            objective.GetComponent<MeshRenderer>().sortingOrder = 120;
        }

        private void SetObjective(string message) => objective.text = message;

        private static bool AllDefeated(List<HookableBody> enemies)
        {
            if (enemies.Count == 0) return false;
            foreach (HookableBody enemy in enemies)
                if (enemy) return false;
            return true;
        }
    }
}
