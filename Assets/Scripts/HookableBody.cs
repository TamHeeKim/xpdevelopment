using System.Collections;
using UnityEngine;

namespace FishingPrototype
{
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    public sealed class HookableBody : MonoBehaviour
    {
        public enum BodyKind { Enemy, Prop }

        public BodyKind Kind;
        public float MaxHealth = 100f;
        public Color BaseColor = Color.white;
        public bool RespawnOnDeath = true;
        public Rigidbody2D Body { get; private set; }
        public bool IsHooked { get; private set; }

        private float health;
        private SpriteRenderer visual;
        private Vector2 spawnPosition;
        private Quaternion spawnRotation;
        private TextMesh label;
        private Coroutine flashRoutine;
        private bool respawning;

        private void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            health = MaxHealth;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            CreateLabel();
        }

        private void CreateLabel()
        {
            GameObject go = new GameObject("Status");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            go.transform.localScale = new Vector3(1f / transform.localScale.x, 1f / transform.localScale.y, 1f);
            label = go.AddComponent<TextMesh>();
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
            IsHooked = value;
            RefreshLabel();
        }

        public void Configure(BodyKind kind, Color color, float maxHealth, bool respawn)
        {
            Kind = kind;
            BaseColor = color;
            MaxHealth = maxHealth;
            health = MaxHealth;
            RespawnOnDeath = respawn;
            if (visual) visual.color = BaseColor;
            RefreshLabel();
        }

        public void ReceiveDamage(float damage, Vector2 hitPoint)
        {
            health -= damage;
            RefreshLabel();
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash());
            if (health <= 0f && !respawning) StartCoroutine(Respawn());
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 4.5f || !IsHooked) return;

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
            visual.color = BaseColor;
            flashRoutine = null;
        }

        private IEnumerator Respawn()
        {
            respawning = true;
            IsHooked = false;
            visual.enabled = false;
            GetComponent<Collider2D>().enabled = false;
            Body.simulated = false;
            if (!RespawnOnDeath)
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
            health = MaxHealth;
            visual.enabled = true;
            GetComponent<Collider2D>().enabled = true;
            respawning = false;
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (!label) return;
            string type = Kind == BodyKind.Enemy ? "ENEMY" : "THROWABLE ROCK";
            label.text = type + "  " + Mathf.CeilToInt(Mathf.Max(0f, health)) + (IsHooked ? "  [HOOKED]" : "");
        }
    }
}
