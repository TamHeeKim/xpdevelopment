using UnityEngine;
namespace FishingPrototype
{
    public sealed class Shuriken : MonoBehaviour
    {
        [Header("수리검 수치")]
        [UnityEngine.Serialization.FormerlySerializedAs("attack")]
        [Min(0)] public float damage = 12f;
        [Min(0), Tooltip("Q 반격 피해의 기준값. 반격 시 2배를 적용합니다.")]
        public float attackDamage = 12f;
        [Min(0.1f)] public float flySpeed = 7f;
        [Header("회전")]
        public float spinSpeed = 720f;
        HookableBody body, shooter;
        Collider2D hitbox;
        SpriteRenderer visual;
        PlayerController counterPlayer;
        FishingRodController rod;
        float counterStarted, counterDuration, flashUntil;
        bool returned, counterShot, spent;
        Vector2 returnDirection;
        Vector3 normalScale;
        public bool IsCountering => counterPlayer;
        public bool CounterAvailable => !spent && !returned && body && body.Body && body.Body.simulated && !body.IsHooked && !IsCountering
            && rod && rod.CanCounter && Vector2.Distance(body.Body.position, rod.player.Body.position) <= rod.shurikenCounterRange;
        public float HitDamage => returned ? attackDamage * (counterShot ? 2f : 1.5f) : damage;
        void Awake()
        {
            body = GetComponent<HookableBody>(); hitbox = GetComponent<Collider2D>(); visual = GetComponent<SpriteRenderer>();
            normalScale = transform.localScale;
            rod = FindFirstObjectByType<FishingRodController>();
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
            shooter = owner; returnDirection = -direction.normalized;
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
        public bool BeginCounter(PlayerController player, float motionTime)
        {
            if (!CounterAvailable) return false;
            counterPlayer = player; counterStarted = Time.time; counterDuration = Mathf.Max(0, motionTime);
            if (body.Body.linearVelocity.sqrMagnitude > 0.01f) returnDirection = -body.Body.linearVelocity.normalized;
            body.SetHooked(true); body.Body.linearVelocity = Vector2.zero; body.Body.simulated = false;
            visual.color = Color.cyan;
            return true;
        }
        void Start() { body.Body.gravityScale = 0; body.Body.linearDamping = 0; body.Body.angularVelocity = spinSpeed; }
        void Update()
        {
            if (spent) return;
            if (IsCountering)
            {
                if (counterPlayer.ControlsLocked || !counterPlayer.Body.simulated) { CancelCounter(); return; }
                float t = counterDuration <= 0 ? 1 : Mathf.Clamp01((Time.time - counterStarted) / counterDuration);
                Vector2 offset = new Vector2(Mathf.Cos(t * Mathf.PI), Mathf.Sin(t * Mathf.PI)) * 0.8f;
                transform.position = counterPlayer.transform.position + (Vector3)offset;
                transform.localScale = normalScale * (1f + Mathf.Sin(t * Mathf.PI) * 0.35f);
                if (t >= 1f) ReleaseCounter();
                return;
            }
            if (body.IsHooked) return;
            if (visual && Time.time >= flashUntil && returned) visual.color = new Color(0.5f, 0.9f, 1f);
            var camera = Camera.main;
            if (camera)
            {
                Vector3 screen = camera.WorldToViewportPoint(transform.position);
                if (screen.z < 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) Destroy(gameObject);
            }
        }
        void ReleaseCounter()
        {
            HookableBody target = shooter && shooter.Body.simulated && shooter.Health > 0f ? shooter : null;
            if (!target)
            {
                float nearest = float.PositiveInfinity;
                foreach (var enemy in FindObjectsByType<HookableBody>(FindObjectsSortMode.None))
                {
                    if (enemy.kind != HookableBody.BodyKind.Enemy || !enemy.Body.simulated || enemy.Health <= 0) continue;
                    float distance = ((Vector2)enemy.transform.position - (Vector2)transform.position).sqrMagnitude;
                    if (distance < nearest) { target = enemy; nearest = distance; }
                }
            }
            Vector2 direction = target ? ((Vector2)target.transform.position - (Vector2)transform.position).normalized : returnDirection;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
            var playerCollider = counterPlayer.GetComponent<Collider2D>();
            counterPlayer = null; counterShot = true; transform.localScale = normalScale;
            body.Body.position = transform.position; body.Body.simulated = true; body.SetHooked(false); body.ClearThrow();
            if (playerCollider) Physics2D.IgnoreCollision(hitbox, playerCollider, true);
            foreach (var enemy in FindObjectsByType<HookableBody>(FindObjectsSortMode.None))
                if (enemy.kind == HookableBody.BodyKind.Enemy) ConfigureEnemy(enemy.GetComponent<Collider2D>());
            body.Body.linearVelocity = direction * flySpeed * 1.5f;
            body.Body.angularVelocity = spinSpeed;
            visual.color = Color.magenta; flashUntil = Time.time + 0.15f;
        }
        public void CancelCounter()
        {
            if (!IsCountering) return;
            counterPlayer = null; body.SetHooked(false); spent = true; Destroy(gameObject);
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
