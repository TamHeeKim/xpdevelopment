using UnityEngine;
namespace FishingPrototype
{
    public sealed class Shuriken : MonoBehaviour
    {
        [Header("회전")]
        public float spinSpeed = 720f;
        HookableBody body;
        float expires;
        bool returned;
        Collider2D hitbox;
        void Awake()
        {
            body = GetComponent<HookableBody>(); hitbox = GetComponent<Collider2D>();
            foreach (var target in FindObjectsByType<HookableBody>(FindObjectsSortMode.None))
            {
                if (target == body) continue;
                if (target.kind == HookableBody.BodyKind.Enemy) ConfigureEnemy(target.GetComponent<Collider2D>());
                else foreach (var collider in target.GetComponentsInChildren<Collider2D>())
                    Physics2D.IgnoreCollision(hitbox, collider, true);
            }
            foreach (var other in FindObjectsByType<Shuriken>(FindObjectsSortMode.None))
                if (other != this) Physics2D.IgnoreCollision(hitbox, other.GetComponent<Collider2D>(), true);
        }
        public void ConfigureEnemy(Collider2D enemy) { Physics2D.IgnoreCollision(hitbox, enemy, !returned); }
        public void Reflect()
        {
            if (returned) return;
            returned = true;
            foreach (var target in FindObjectsByType<HookableBody>(FindObjectsSortMode.None))
                if (target.kind == HookableBody.BodyKind.Enemy) ConfigureEnemy(target.GetComponent<Collider2D>());
            var player = FindFirstObjectByType<PlayerController>();
            if (player) Physics2D.IgnoreCollision(hitbox, player.GetComponent<Collider2D>(), true);
        }
        void Start() { expires = Time.time + 8f; body.Body.angularVelocity = spinSpeed; }
        void Update()
        {
            if (body.IsHooked) { Reflect(); expires = Time.time + 8f; }
            if (Time.time > expires) Destroy(gameObject);
        }
        void OnCollisionEnter2D(Collision2D hit)
        {
            if (!body || body.IsHooked) return;
            var player = hit.collider.GetComponent<PlayerHealth>();
            var enemy = hit.collider.GetComponent<HookableBody>();
            if (!returned && player) { player.ReceiveDamage(12f, new Vector2(Mathf.Sign(body.Body.linearVelocity.x) * 3f, 1f)); Destroy(gameObject); }
            else if (returned && enemy && enemy.kind == HookableBody.BodyKind.Enemy) { enemy.ReceiveDamage(30f); Destroy(gameObject); }
            else if (!enemy && !player) Destroy(gameObject);
        }
    }
}
