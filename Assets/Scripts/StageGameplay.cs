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
            healthText.anchor = TextAnchor.UpperRight;
            healthText.alignment = TextAlignment.Right;
            healthText.fontSize = 42;
            healthText.characterSize = 0.05f;
            healthText.color = new Color(0.3f, 0.9f, 1f);
            healthText.GetComponent<MeshRenderer>().sortingOrder = 110;
            RefreshHud();
        }

        public void SetCheckpoint(Vector2 point) => checkpoint = point;

        public void ReceiveDamage(float damage, Vector2 knockback)
        {
            if (recovering) return;
            Health = Mathf.Max(0f, Health - damage);
            body.AddForce(knockback, ForceMode2D.Impulse);
            CameraShake.Pulse(Mathf.Clamp01(damage / 30f));
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
            body.simulated = false;
            visual.enabled = false;
            yield return new WaitForSeconds(0.8f);
            transform.position = checkpoint;
            body.simulated = true;
            body.linearVelocity = Vector2.zero;
            Health = MaxHealth;
            visual.enabled = true;
            recovering = false;
            RefreshHud();
        }

        private void RefreshHud()
        {
            if (healthText) healthText.text = "HP  " + Mathf.CeilToInt(Health) + " / " + Mathf.CeilToInt(MaxHealth);
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

    public sealed class AnywhereFishingController : MonoBehaviour
    {
        private enum FishingState { Ready, Timing, Cooldown }

        public PlayerController Player;
        public FishingRodController Rod;

        private FishingState state;
        private TextMesh prompt;
        private float startedAt;
        private float cooldownUntil;
        private float messageUntil;

        public void Initialize(PlayerController player, FishingRodController rod)
        {
            Player = player;
            Rod = rod;
            CreateHud();
        }

        private void CreateHud()
        {
            GameObject textObject = new GameObject("Fishing Prompt");
            textObject.transform.SetParent(Camera.main.transform, false);
            textObject.transform.localPosition = new Vector3(0f, -3.65f, 10f);
            prompt = textObject.AddComponent<TextMesh>();
            prompt.anchor = TextAnchor.MiddleCenter;
            prompt.alignment = TextAlignment.Center;
            prompt.fontSize = 42;
            prompt.characterSize = 0.052f;
            prompt.color = new Color(0.45f, 0.9f, 1f);
            prompt.GetComponent<MeshRenderer>().sortingOrder = 125;
        }

        private void Update()
        {
            if (!Player || !Rod) return;

            if (state == FishingState.Cooldown && Time.time >= cooldownUntil) state = FishingState.Ready;

            if (state == FishingState.Ready)
            {
                prompt.text = Time.time < messageUntil
                    ? prompt.text
                    : "SPACE: FISH ANYWHERE WHILE STANDING ON THE PLATFORM";
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    if (!Player.Grounded)
                    {
                        ShowMessage("LAND ON THE PLATFORM BEFORE FISHING");
                        return;
                    }
                    if (!Rod.CanStartFishing)
                    {
                        ShowMessage("FINISH THE CURRENT WHIP OR RELEASE THE HOOK FIRST");
                        return;
                    }
                    state = FishingState.Timing;
                    startedAt = Time.time;
                    SetFishingLock(true);
                }
                return;
            }

            if (state == FishingState.Cooldown)
            {
                if (Time.time >= messageUntil)
                    prompt.text = "CATCH ATTACHED  |  SPACE: RE-FISH ANYWHERE";
                return;
            }

            if (!Player.Grounded)
            {
                CancelFishing("FISHING CANCELLED  |  KEEP YOUR FOOTING");
                return;
            }

            if (Time.time - startedAt > 6f)
            {
                CancelFishing("THE FISH GOT AWAY  |  SPACE TO TRY AGAIN");
                return;
            }

            float marker = Mathf.PingPong((Time.time - startedAt) * 10f, 20f);
            int markerIndex = Mathf.Clamp(Mathf.RoundToInt(marker), 0, 20);
            char[] bar = "-------=====---------".ToCharArray();
            bar[markerIndex] = '|';
            prompt.text = "PRESS SPACE ONCE IN THE CENTER\n[" + new string(bar) + "]";

            if (!Input.GetKeyDown(KeyCode.Space)) return;
            if (marker >= 7f && marker <= 13f)
            {
                CompleteCatch(marker >= 9f && marker <= 11f);
            }
            else
            {
                startedAt = Time.time;
            }
        }

        private void CompleteCatch(bool perfectCenter)
        {
            FishingRodController.CatchType result = perfectCenter
                ? FishingRodController.CatchType.HeavyObject
                : FishingRodController.CatchType.Fish;
            Rod.AttachCatch(result);
            prompt.text = result == FishingRodController.CatchType.HeavyObject
                ? "HEAVY OBJECT CAUGHT!\nGROUND + HOLD LMB = ANCHOR SWING"
                : "FISH CAUGHT!\nFASTER THAN HEAVY, STRONGER THAN EMPTY";
            state = FishingState.Cooldown;
            cooldownUntil = Time.time + 2.8f;
            messageUntil = cooldownUntil;
            SetFishingLock(false);
        }

        private void CancelFishing(string message)
        {
            state = FishingState.Ready;
            ShowMessage(message);
            SetFishingLock(false);
        }

        private void ShowMessage(string message)
        {
            prompt.text = message;
            messageUntil = Time.time + 1.8f;
        }

        private void SetFishingLock(bool locked)
        {
            Player.ControlsLocked = locked;
            Rod.ControlsLocked = locked;
        }

        private void OnDisable()
        {
            if (Player) Player.ControlsLocked = false;
            if (Rod) Rod.ControlsLocked = false;
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
        private AnywhereFishingController fishing;
        private TextMesh objective;
        private bool waveOneStarted;
        private bool waveOneCleared;
        private bool catchReady;
        private bool waveTwoStarted;
        private bool stageComplete;

        private void Start()
        {
            combatGate = CreateGate("Combat Gate", 10.5f, new Color(0.85f, 0.25f, 0.22f));
            finishGate = CreateGate("Finish Gate", 36.5f, new Color(0.74f, 0.24f, 0.78f));
            CreateAnywhereFishing();
            CreateObjectiveHud();
            SetObjective("MOVE RIGHT  >  FIRST COMBAT");
        }

        private void Update()
        {
            if (!waveOneStarted && Player.transform.position.x > -4.5f)
            {
                waveOneStarted = true;
                waveOne.Add(SpawnEnemy(EnemyCombatAI.EnemyType.Rusher, new Vector2(3f, -1.1f)));
                waveOne.Add(SpawnEnemy(EnemyCombatAI.EnemyType.Rusher, new Vector2(6f, -1.1f)));
                waveOne.Add(SpawnEnemy(EnemyCombatAI.EnemyType.Bruiser, new Vector2(8.3f, -0.9f)));
                SetObjective("WAVE 1  |  WHIP, CAPTURE, THROW");
            }

            if (waveOneStarted && !waveOneCleared && AllDefeated(waveOne))
            {
                waveOneCleared = true;
                combatGate.SetActive(false);
                SetObjective("WAVE 1 CLEAR  |  SPACE: FISH ANYWHERE");
            }

            if (waveOneCleared && !catchReady && Rod.HasCatch)
            {
                catchReady = true;
                Player.GetComponent<PlayerHealth>().SetCheckpoint(Player.transform.position);
                SetObjective("CATCH EQUIPPED  |  MOVE RIGHT FOR WAVE 2");
            }

            if (catchReady && !waveTwoStarted && Player.transform.position.x > 20.5f)
            {
                waveTwoStarted = true;
                waveTwo.Add(SpawnEnemy(EnemyCombatAI.EnemyType.Bruiser, new Vector2(25f, -0.9f)));
                waveTwo.Add(SpawnEnemy(EnemyCombatAI.EnemyType.Rusher, new Vector2(28f, -1.1f)));
                waveTwo.Add(SpawnEnemy(EnemyCombatAI.EnemyType.Rusher, new Vector2(31f, -1.1f)));
                waveTwo.Add(SpawnEnemy(EnemyCombatAI.EnemyType.Bruiser, new Vector2(34f, -0.9f)));
                SetObjective("WAVE 2  |  TEST THE CATCHED WEIGHT");
            }

            if (waveTwoStarted && !stageComplete && AllDefeated(waveTwo))
            {
                stageComplete = true;
                finishGate.SetActive(false);
                SetObjective("STAGE CLEAR  |  R TO RESTART AND TRY ANOTHER CATCH");
            }
        }

        private HookableBody SpawnEnemy(EnemyCombatAI.EnemyType type, Vector2 position)
        {
            bool bruiser = type == EnemyCombatAI.EnemyType.Bruiser;
            Color color = bruiser ? new Color(0.62f, 0.22f, 0.75f) : new Color(0.92f, 0.22f, 0.18f);
            GameObject enemy = PrototypeBootstrap.CreateHookable(bruiser ? "Bruiser" : "Rusher", position,
                bruiser ? new Vector2(1.35f, 2.15f) : new Vector2(0.85f, 1.65f), color,
                HookableBody.BodyKind.Enemy, bruiser ? 3.4f : 1.15f, bruiser ? 150f : 72f, false);
            EnemyCombatAI ai = enemy.AddComponent<EnemyCombatAI>();
            ai.Configure(type, Player, color);
            return enemy.GetComponent<HookableBody>();
        }

        private void CreateAnywhereFishing()
        {
            GameObject point = new GameObject("Anywhere Fishing");
            fishing = point.AddComponent<AnywhereFishingController>();
            fishing.Initialize(Player, Rod);
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
