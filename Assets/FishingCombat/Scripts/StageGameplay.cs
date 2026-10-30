using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FishingPrototype
{
    public sealed class GroundSurface : MonoBehaviour { }

    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        public float MaxHealth = 100f;
        public float Health { get; private set; }

        private Rigidbody2D body;
        private SpriteRenderer visual;
        private Vector2 checkpoint;
        private TextMesh healthText;
        private bool recovering;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            Health = MaxHealth;
            checkpoint = transform.position;
            CreateHud();
        }

        private void CreateHud()
        {
            GameObject go = new GameObject("Player Health HUD");
            go.transform.SetParent(Camera.main.transform, false);
            go.transform.localPosition = new Vector3(4.7f, 4.72f, 10f);
            healthText = go.AddComponent<TextMesh>();
            healthText.font = PrototypeBootstrap.KoreanFont;
            healthText.GetComponent<MeshRenderer>().sharedMaterial = PrototypeBootstrap.KoreanFont.material;
            healthText.GetComponent<MeshRenderer>().enabled = false;
            healthText.anchor = TextAnchor.UpperRight;
            healthText.alignment = TextAlignment.Right;
            healthText.fontSize = 42;
            healthText.characterSize = 0.05f;
            healthText.color = new Color(0.3f, 0.9f, 1f);
            healthText.GetComponent<MeshRenderer>().sortingOrder = 110;
            RefreshHud();
        }

        public void SetCheckpoint(Vector2 point) => checkpoint = point;
        public void Heal() { Health = MaxHealth; RefreshHud(); }

        public void ReceiveDamage(float damage, Vector2 knockback)
        {
            if (recovering) return;
            Health = Mathf.Max(0f, Health - damage);
            body.AddForce(knockback, ForceMode2D.Impulse);
            CameraShake.Pulse(Mathf.Clamp01(damage / 30f));
            GetComponent<PlayerAppearance>()?.Flash();
            StartCoroutine(HitFlash());
            RefreshHud();
            if (Health <= 0f) StartCoroutine(Recover());
        }

        private IEnumerator HitFlash()
        {
            visual.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            visual.color = new Color(0.2f, 0.72f, 0.95f);
        }

        private IEnumerator Recover()
        {
            recovering = true;
            FindFirstObjectByType<FishingRodController>()?.Interrupt();
            GetComponent<PlayerController>().ControlsLocked = true;
            var poison = GetComponent<PoisonStatus>(); if (poison) poison.Until = 0;
            body.simulated = false;
            visual.enabled = false;
            yield return new WaitForSeconds(0.8f);
            transform.position = checkpoint;
            body.simulated = true;
            body.linearVelocity = Vector2.zero;
            Health = MaxHealth;
            visual.enabled = true;
            recovering = false;
            GetComponent<PlayerController>().ControlsLocked = false;
            FindFirstObjectByType<StageDirector>().ResetEncounter();
            RefreshHud();
        }

        private void RefreshHud()
        {
            if (healthText) healthText.text = "체력  " + Mathf.CeilToInt(Health) + " / " + Mathf.CeilToInt(MaxHealth);
        }
    }

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
        public PlayerController Player;
        public FishingRodController Rod;

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
            combatGate = CreateGate("Combat Gate", 10.5f, new Color(0.85f, 0.25f, 0.22f));
            finishGate = CreateGate("Finish Gate", 36.5f, new Color(0.74f, 0.24f, 0.78f));

            for (int i = 0; i < 3; i++)
            {
                var bottle = PrototypeBootstrap.CreateHookable("호리병", new Vector2(i * 12f, -1.5f), new Vector2(0.5f, 0.8f), Color.cyan, HookableBody.BodyKind.Prop, 1f, 500f, false);
                bottle.AddComponent<PoisonBottle>();
            }
            CreateObjectiveHud();
            if (PlayerPrefs.GetInt("TangCheckpoint", 0) == 1)
            {
                waveOneStarted = waveOneCleared = catchReady = true;
                combatGate.SetActive(false);
                Player.transform.position = new Vector2(14f, -1.15f);
                Player.GetComponent<PlayerHealth>().SetCheckpoint(Player.transform.position);
            }
            SetObjective("시작 지점·첫 전투 다음 휴식 구간에서 E: 저장·회복  |  F9: 불러오기");
        }

        private void Update()
        {
            if ((Keyboard.current?.eKey.wasPressedThisFrame ?? false) &&
                (Player.transform.position.x < -5f || (waveOneCleared && Mathf.Abs(Player.transform.position.x - 14f) < 2f)))
            {
                bool second = waveOneCleared && Player.transform.position.x > 10f;
                catchReady = second;
                Vector2 point = new Vector2(second ? 14f : -8f, -1.15f);
                Player.GetComponent<PlayerHealth>().SetCheckpoint(point);
                Player.GetComponent<PlayerHealth>().Heal();
                PlayerPrefs.SetInt("TangCheckpoint", second ? 1 : 0);
                PlayerPrefs.Save();
                SetObjective("저장·회복 완료  |  오른쪽으로 이동하세요");
            }
            if (!waveOneStarted && Player.transform.position.x > -4.5f)
            {
                waveOneStarted = true;
                StartCoroutine(SpawnEncounter(waveOne, false));
            }

            if (waveOneStarted && firstSpawnComplete && !waveOneCleared && AllDefeated(waveOne))
            {
                waveOneCleared = true;
                combatGate.SetActive(false);
                SetObjective("첫 번째 전투 완료  |  오른쪽 휴식 구간에서 E: 저장·회복");
            }

            if (waveOneCleared && !waveTwoStarted && Player.transform.position.x > 20.5f)
            {
                waveTwoStarted = true;
                StartCoroutine(SpawnEncounter(waveTwo, true));
            }

            if (waveTwoStarted && secondSpawnComplete && !stageComplete && AllDefeated(waveTwo))
            {
                stageComplete = true;
                finishGate.SetActive(false);
                SetObjective("구간 완료  |  R: 저장 지점부터 다시 시작");
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
            SetObjective("재도전  |  오른쪽으로 이동하세요");
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
                float x = ChooseSpawnX(Player.transform.position.x, second);
                spawnWarning = PrototypeBootstrap.CreateVisual("적 등장 예고", new Vector2(x, -1.95f), new Vector2(1.5f,0.12f), new Color(1f,0.55f,0.2f));
                SetObjective((second ? "두 번째 전투" : "첫 번째 전투") + " · 적 접근 중  " + (i+1) + "/" + total);
                yield return new WaitForSeconds(1.2f);
                // Re-warn if the player moved onto the spawn point during the telegraph.
                while (Mathf.Abs(Player.transform.position.x-x) < 2.5f)
                {
                    x=ChooseSpawnX(Player.transform.position.x,second);
                    spawnWarning.transform.position=new Vector2(x,-1.95f);
                    yield return new WaitForSeconds(1.2f);
                }
                bool heavy = second ? i%3==2 : i==3;
                wave.Add(SpawnEnemy(heavy?EnemyCombatAI.EnemyType.Bruiser:EnemyCombatAI.EnemyType.Rusher,new Vector2(x,heavy?-.9f:-1.1f)));
                Destroy(spawnWarning); spawnWarning=null;
                SetObjective((second?"두 번째 전투":"첫 번째 전투")+" · 남은 적 "+(LivingCount(wave)+total-i-1));
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
            GameObject enemy = PrototypeBootstrap.CreateHookable(bruiser ? "무거운 적" : "일반 적", position,
                bruiser ? new Vector2(1.35f, 2.15f) : new Vector2(0.85f, 1.65f), color,
                HookableBody.BodyKind.Enemy, bruiser ? 3.4f : 1.15f, bruiser ? 150f : 72f, false);
            enemy.GetComponent<Rigidbody2D>().freezeRotation = true;
            CombatShapes.DressEnemy(enemy, bruiser);
            TangEnemyAI ai = enemy.AddComponent<TangEnemyAI>();
            ai.Player = Player;
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
            GameObject go = new GameObject("Objective HUD");
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
