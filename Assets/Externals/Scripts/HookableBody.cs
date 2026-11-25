using FishingPrototype.Integration;
using System.Collections;
using UnityEngine;

namespace FishingPrototype
{
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    public sealed class HookableBody : MonoBehaviour
    {
        public enum BodyKind { Enemy, Prop }

        [UnityEngine.Serialization.FormerlySerializedAs("Kind")]

        [Header("대상 설정")]
        public BodyKind kind;
        [UnityEngine.Serialization.FormerlySerializedAs("MaxHealth")]
        public float maxHealth = 100f;
        [UnityEngine.Serialization.FormerlySerializedAs("BaseColor")]
        public Color baseColor = Color.white;
        [UnityEngine.Serialization.FormerlySerializedAs("RespawnOnDeath")]
        public bool respawnOnDeath = true;
        [Header("포획 설정")]
        public CombatStatDefinition statDefinition;
        public bool canCatch = true;
        [Tooltip("해제하면 플레이어의 이동만 통과시키고 지형·적 충돌은 유지합니다.")]
        public bool blocksPlayerMovement = true;
        public bool breakOnSlam, breakOnThrow;
        public CircleCollider2D targetingCollider;
        public float Health => health;
        public Rigidbody2D Body { get; private set; }
        public bool IsHooked { get; private set; }
        private bool thrown, impactPending;
        private float selfImpactDamage, victimImpactDamage, savedDamping;
        public bool IsThrown => !respawning && thrown;

        [Header("표시 이름")]
        [SerializeField] private string displayNameKey = "object.unknown";
        public string DisplayName => CombatText.Get(displayNameKey);

        public float HealthFraction => Mathf.Clamp01(health / Mathf.Max(1f, maxHealth));
        private float health;
        private SpriteRenderer visual;
        private Vector2 spawnPosition;
        private Quaternion spawnRotation;
        private Coroutine flashRoutine;
        private bool respawning;
        private Collider2D hookedPlayerCollider;
        private bool playerCollisionWasIgnored;

        private void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            baseColor = visual.color;
            if (kind == BodyKind.Enemy && statDefinition) maxHealth = statDefinition.Values.hp;
            health = maxHealth;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void Start()
        {
            if (blocksPlayerMovement) return;
            var player = FindFirstObjectByType<PlayerController>();
            if (player) Physics2D.IgnoreCollision(GetComponent<Collider2D>(), player.GetComponent<Collider2D>(), true);
        }

        public void SetHooked(bool value)
        {
            if (value)
            {
                ClearThrow();
                GetComponent<TangEnemyAI>()?.OnConnected();
            }
            if (value) GetComponent<Shuriken>()?.Reflect();
            var hitbox = GetComponent<Collider2D>();
            if (value && !IsHooked && hitbox)
            {
                var player = FindFirstObjectByType<PlayerController>();
                hookedPlayerCollider = player ? player.GetComponent<Collider2D>() : null;
                if (hookedPlayerCollider)
                {
                    playerCollisionWasIgnored = Physics2D.GetIgnoreCollision(hitbox, hookedPlayerCollider);
                    Physics2D.IgnoreCollision(hitbox, hookedPlayerCollider, true);
                }
            }
            else if (!value && hookedPlayerCollider)
            {
                if (hitbox) Physics2D.IgnoreCollision(hitbox, hookedPlayerCollider, playerCollisionWasIgnored);
                hookedPlayerCollider = null;
            }
            IsHooked = value;
        }

        public void Configure(BodyKind kind, Color color, float maxHealth, bool respawn)
        {
            this.kind = kind;
            baseColor = color;
            this.maxHealth = maxHealth;
            health = this.maxHealth;
            respawnOnDeath = respawn;
            if (visual) visual.color = baseColor;
        }

        public void ReceiveDamage(float damage)
        {
            if (kind == BodyKind.Prop || respawning || damage <= 0f) return;
            health -= damage;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash());
            if (health <= 0f && !respawning) StartCoroutine(Respawn());
        }

        public void BeginThrow(float selfDamage, float victimDamage)
        {
            if (!thrown) savedDamping = Body.linearDamping;
            Body.linearDamping = 0;
            thrown = impactPending = true;
            selfImpactDamage = Mathf.Max(0, selfDamage); victimImpactDamage = Mathf.Max(0, victimDamage);
        }
        public void ClearThrow() { if (thrown && Body) Body.linearDamping = savedDamping; thrown = impactPending = false; }
        public void ResolveThrowImpact(Collider2D collider)
        {
            if (!IsThrown || !impactPending || IsHooked || GetComponent<Shuriken>() || collider.isTrigger || collider.GetComponent<PlayerController>()) return;
            var partner = collider.GetComponentInParent<HookableBody>();
            if (partner && (partner == this || partner.kind != BodyKind.Enemy)) return;
            impactPending = false;
            // 충돌 콜백 순서와 관계없이 병 효과는 투척이 끝나기 전에 한 번만 발동합니다.
            GetComponent<PoisonBottle>()?.Burst();
            ReceiveDamage(selfImpactDamage);
            if (partner) partner.ReceiveDamage(victimImpactDamage);
            if (kind == BodyKind.Prop && breakOnThrow) Destroy(gameObject);
        }

        private void FixedUpdate()
        {
            if (IsThrown && !impactPending)
            {
                foreach (var hit in Physics2D.RaycastAll(Body.position, Vector2.down, GetComponent<Collider2D>().bounds.extents.y + 0.15f))
                    if (!hit.collider.isTrigger && hit.rigidbody != Body && hit.normal.y > 0.5f) { ClearThrow(); break; }
            }
            if (kind != BodyKind.Prop || IsHooked || GetComponent<Shuriken>() || Body.position.y >= -12f) return;
            Body.position = spawnPosition;
            Body.rotation = spawnRotation.eulerAngles.z;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            ClearThrow();
        }

        private void OnCollisionEnter2D(Collision2D collision) => ResolveThrowImpact(collision.collider);

        private IEnumerator Flash()
        {
            visual.color = Color.white;
            var meshes = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var mesh in meshes) mesh.enabled = false;
            yield return new WaitForSeconds(0.08f);
            visual.color = baseColor;
            foreach (var mesh in meshes) mesh.enabled = !respawning && health > 0;
            flashRoutine = null;
        }

        private IEnumerator Respawn()
        {
            respawning = true;
            ClearThrow();
            SetHooked(false);
            foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            GetComponent<Collider2D>().enabled = false;
            Body.simulated = false;
            if (!respawnOnDeath)
            {
                yield return new WaitForSeconds(0.45f);
                Destroy(gameObject);
                yield break;
            }
            yield return new WaitForSeconds(1.1f);
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            Body.simulated = true;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            health = maxHealth;
            foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = true;
            GetComponent<Collider2D>().enabled = true;
            respawning = false;
        }

        public void SetDisplayNameKey(string key) { displayNameKey = key; }

    }
}
