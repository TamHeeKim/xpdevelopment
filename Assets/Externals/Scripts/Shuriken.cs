using UnityEngine;
namespace FishingPrototype
{
    public sealed class Shuriken : MonoBehaviour
    {
        [Header("수리검 수치")]
        [Min(0)] public float attackDamage = 20f;
        [Min(0.1f)] public float flySpeed = 10f;
        [Header("회전")]
        public float spinSpeed = 720f;
        HookableBody body;
        Collider2D hitbox;
        SpriteRenderer visual;
        float flashUntil;
        bool returned, spent;
        public float HitDamage => attackDamage * (returned ? 1.5f : 1f);
        void Awake()
        {
            body = GetComponent<HookableBody>(); hitbox = GetComponent<Collider2D>(); visual = GetComponent<SpriteRenderer>();
            foreach (var target in FindObjectsByType<HookableBody>(FindObjectsSortMode.None))
            {
                if (target == body) continue;
                if (target.kind == HookableBody.BodyKind.Enemy) ConfigureEnemy(target.GetComponent<Collider2D>());
                else foreach (var collider in target.GetComponentsInChildren<Collider2D>()) Physics2D.IgnoreCollision(hitbox, collider, true);
            }
            foreach (var other in FindObjectsByType<Shuriken>(FindObjectsSortMode.None))
                if (other != this) Physics2D.IgnoreCollision(hitbox, other.GetComponent<Collider2D>(), true);
        }
        public void ConfigureEnemy(Collider2D enemy) { if (enemy) Physics2D.IgnoreCollision(hitbox, enemy, !returned); }
        public void Launch(HookableBody owner, Vector2 direction)
        {
            var flight = GetComponent<Rigidbody2D>();
            flight.gravityScale = 0; flight.linearDamping = 0;
            flight.linearVelocity = direction.normalized * flySpeed;
        }
        public void Reflect()
        {
            if (returned) return;
            returned = true;
            foreach (var target in FindObjectsByType<HookableBody>(FindObjectsSortMode.None))
                if (target.kind == HookableBody.BodyKind.Enemy) ConfigureEnemy(target.GetComponent<Collider2D>());
            var player = FindFirstObjectByType<PlayerController>();
            if (player) Physics2D.IgnoreCollision(hitbox, player.GetComponent<Collider2D>(), true);
        }
        public void OnThrown()
        {
            Reflect(); body.ClearThrow(); body.Body.angularVelocity = spinSpeed;
            visual.color = Color.magenta; flashUntil = Time.time + 0.15f;
        }
        void Start() { body.Body.gravityScale = 0; body.Body.linearDamping = 0; body.Body.angularVelocity = spinSpeed; }
        void Update()
        {
            if (spent) return;
            if (body.IsHooked) return;
            if (visual && Time.time >= flashUntil && returned) visual.color = new Color(0.5f, 0.9f, 1f);
            var camera = Camera.main;
            if (camera)
            {
                Vector3 screen = camera.WorldToViewportPoint(transform.position);
                if (screen.z < 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) Destroy(gameObject);
            }
        }
        void OnCollisionEnter2D(Collision2D hit)
        {
            if (!body || body.IsHooked || spent) return;
            var player = hit.collider.GetComponent<PlayerHealth>();
            var enemy = hit.collider.GetComponent<HookableBody>();
            if (!returned && player) { spent = true; player.ReceiveDamage(HitDamage, new Vector2(Mathf.Sign(body.Body.linearVelocity.x) * 3f, 1f)); Destroy(gameObject); }
            else if (returned && enemy && enemy.kind == HookableBody.BodyKind.Enemy) { spent = true; enemy.ReceiveDamage(HitDamage); Destroy(gameObject); }
            else if (!enemy && !player) { spent = true; Destroy(gameObject); }
        }
    }
}
