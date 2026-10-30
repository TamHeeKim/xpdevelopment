using System.Collections;
using System.Collections.Generic;
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
        public Rigidbody2D Body { get; private set; }
        public bool IsHooked { get; private set; }
        [UnityEngine.Serialization.FormerlySerializedAs("ThrowDamageDuration")]
        [Header("투척 피해")]
        public float throwDamageDuration = 2f;
        private float thrownUntil = float.NegativeInfinity;
        public bool IsThrown => !respawning && Time.time < thrownUntil;
        private readonly Dictionary<int, float> impactTimes = new Dictionary<int, float>();

        [Header("표시 이름")]
        [SerializeField] private string displayNameKey = "object.unknown";
        public string DisplayName => CombatText.Get(displayNameKey);

        public float HealthFraction => Mathf.Clamp01(health / Mathf.Max(1f, maxHealth));
        private float health;
        private SpriteRenderer visual;
        private Vector2 spawnPosition;
        private Quaternion spawnRotation;
        private TextMesh label;
        private Coroutine flashRoutine;
        private bool respawning;
        private Collider2D hookedPlayerCollider;
        private bool playerCollisionWasIgnored;

        private void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            health = maxHealth;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            CreateLabel();
        }

        private void CreateLabel()
        {
            GameObject go = new GameObject("Text_Status");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            go.transform.localScale = new Vector3(1f / transform.localScale.x, 1f / transform.localScale.y, 1f);
            label = go.AddComponent<TextMesh>();
            label.font = PrototypeBootstrap.KoreanFont;
            label.GetComponent<MeshRenderer>().sharedMaterial = PrototypeBootstrap.KoreanFont.material;
            label.GetComponent<MeshRenderer>().enabled = false;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 42;
            label.characterSize = 0.065f;
            label.color = new Color(0.92f, 0.96f, 1f);
            label.GetComponent<MeshRenderer>().sortingOrder = 10;
            RefreshLabel();
        }

        public void SetHooked(bool value)
        {
            if (value) { thrownUntil = float.NegativeInfinity; impactTimes.Clear(); }
            else if (IsHooked && !respawning) thrownUntil = Time.time + throwDamageDuration;
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
            RefreshLabel();
        }

        public void Configure(BodyKind kind, Color color, float maxHealth, bool respawn)
        {
            this.kind = kind;
            baseColor = color;
            this.maxHealth = maxHealth;
            health = this.maxHealth;
            respawnOnDeath = respawn;
            if (visual) visual.color = baseColor;
            RefreshLabel();
        }

        public void ReceiveDamage(float damage, Vector2 hitPoint)
        {
            if (kind == BodyKind.Prop || respawning || damage <= 0f) return;
            health -= damage;
            RefreshLabel();
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash());
            if (health <= 0f && !respawning) StartCoroutine(Respawn());
        }

        private void FixedUpdate()
        {
            if (kind != BodyKind.Prop || IsHooked || GetComponent<Shuriken>() || Body.position.y >= -12f) return;
            Body.position = spawnPosition;
            Body.rotation = spawnRotation.eulerAngles.z;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            thrownUntil = float.NegativeInfinity;
            impactTimes.Clear();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 4.5f || (!IsHooked && !IsThrown) || respawning) return;
            if (collision.collider.GetComponent<PlayerController>()) return;
            HookableBody partner = collision.collider.GetComponent<HookableBody>();
            int key = partner ? partner.GetInstanceID() : collision.collider.GetInstanceID();
            if (impactTimes.TryGetValue(key, out float last) && Time.time - last < 0.1f) return;
            impactTimes[key] = Time.time;
            // Both collision callbacks describe one impact, so record it on both bodies before damage.
            if (partner) partner.impactTimes[GetInstanceID()] = Time.time;

            float damage = (speed - 4.5f) * Body.mass * 2.4f;
            ReceiveDamage(damage, collision.GetContact(0).point);
            CameraShake.Pulse(Mathf.Clamp01(damage / 45f));

            HookableBody other = collision.collider.GetComponent<HookableBody>();
            if (other && other != this)
                other.ReceiveDamage(damage * 0.75f, collision.GetContact(0).point);
        }

        private IEnumerator Flash()
        {
            visual.color = Color.white;
            yield return new WaitForSeconds(0.08f);
            visual.color = baseColor;
            flashRoutine = null;
        }

        private IEnumerator Respawn()
        {
            respawning = true;
            thrownUntil = float.NegativeInfinity;
            impactTimes.Clear();
            SetHooked(false);
            foreach (var sprite in GetComponentsInChildren<SpriteRenderer>()) sprite.enabled = false;
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
            foreach (var sprite in GetComponentsInChildren<SpriteRenderer>()) sprite.enabled = true;
            GetComponent<Collider2D>().enabled = true;
            respawning = false;
            RefreshLabel();
        }

        public void SetDisplayNameKey(string key) { displayNameKey = key; RefreshLabel(); }

        private void RefreshLabel()
        {
            if (!label) return;
            string type = DisplayName;
            label.text = type + "  " + Mathf.CeilToInt(Mathf.Max(0f, health)) + (IsHooked ? CombatText.Get("status.hooked_suffix") : "");
        }
    }
}
