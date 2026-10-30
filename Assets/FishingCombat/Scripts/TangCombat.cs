using UnityEngine;
namespace FishingPrototype
{
    public sealed class PoisonStatus : MonoBehaviour
    {
        public float Until;
        float nextTick;
        void Update()
        {
            if (Time.time >= Until) return;
            if (Time.time >= nextTick) { nextTick = Time.time + 0.5f; GetComponent<PlayerHealth>().ReceiveDamage(3f, Vector2.zero); }
        }
        public void Apply() { Until = Time.time + 2f; }
    }
    public sealed class PoisonCloud : MonoBehaviour
    {
        float expires, nextTick;
        bool damagesEnemies;
        static Sprite smokeSprite;
        readonly System.Collections.Generic.HashSet<HookableBody> damaged = new System.Collections.Generic.HashSet<HookableBody>();
        public static void Spawn(Vector2 position, bool damagesEnemies = false)
        {
            var go = PrototypeBootstrap.CreateVisual(damagesEnemies ? "반격 독연기" : "독가스", position,
                new Vector2(3f, 2f), new Color(0.3f, 0.85f, 0.2f, 0.35f));
            var cloud = go.AddComponent<PoisonCloud>();
            cloud.damagesEnemies = damagesEnemies;
            if (damagesEnemies) cloud.CreateSmoke();
        }
        void CreateSmoke()
        {
            GetComponent<SpriteRenderer>().enabled = false;
            if (!smokeSprite)
            {
                var texture = new Texture2D(32, 32);
                texture.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    float radius = new Vector2((x + 0.5f) / 16f - 1f, (y + 0.5f) / 16f - 1f).magnitude;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - radius) * 0.8f));
                }
                texture.Apply();
                smokeSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32);
            }
            for (int i = 0; i < 6; i++)
            {
                var puff = new GameObject("보라색 연기");
                puff.transform.SetParent(transform, false);
                var sprite = puff.AddComponent<SpriteRenderer>();
                sprite.sprite = smokeSprite;
                sprite.color = new Color(0.75f, 0.25f, 1f, 0.85f);
                sprite.sortingOrder = 6;
            }
            AnimateSmoke();
        }
        void Update() { if (damagesEnemies) AnimateSmoke(); }
        void AnimateSmoke()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var puff = transform.GetChild(i);
                float phase = Time.time * 1.8f + i * 1.7f;
                puff.localPosition = new Vector3((i % 3 - 1) * 0.24f + Mathf.Sin(phase) * 0.035f,
                    (i / 3 - 0.5f) * 0.3f + Mathf.Cos(phase) * 0.07f, 0f);
                puff.localScale = Vector3.one * (0.72f + Mathf.Sin(phase) * 0.08f);
            }
        }
        void Start() { expires = Time.time + 3f; }
        void FixedUpdate()
        {
            if (Time.time >= expires) { Destroy(gameObject); return; }
            if (damagesEnemies)
            {
                if (Time.time < nextTick) return;
                nextTick = Time.time + 0.5f;
                damaged.Clear();
                foreach (var col in Physics2D.OverlapCircleAll(transform.position, 2f))
                {
                    var target = col.GetComponent<HookableBody>();
                    if (target && target.Kind == HookableBody.BodyKind.Enemy && target.Body.simulated && damaged.Add(target))
                        target.ReceiveDamage(8f, transform.position);
                }
                return;
            }
            foreach (var col in Physics2D.OverlapBoxAll(transform.position, new Vector2(3f, 2f), 0f))
            {
                var bottle = col.GetComponent<PoisonBottle>();
                if (bottle && bottle.GetComponent<HookableBody>().IsHooked && !bottle.Filled)
                { bottle.Fill(); Destroy(gameObject); return; }
                var player = col.GetComponent<PlayerController>();
                if (player) { var status = player.GetComponent<PoisonStatus>(); if (!status) status = player.gameObject.AddComponent<PoisonStatus>(); status.Apply(); }
            }
        }
    }
    public sealed class PoisonBottle : MonoBehaviour
    {
        public bool Filled { get; private set; }
        public void Fill() { Filled = true; GetComponent<HookableBody>().SetDisplayName("독가스 호리병"); GetComponent<HookableBody>().BaseColor = Color.green; GetComponent<SpriteRenderer>().color = Color.green; }
        void OnCollisionEnter2D(Collision2D hit)
        {
            var body = GetComponent<HookableBody>();
            if (!Filled || !body.IsThrown) return;
            Filled = false;
            PoisonCloud.Spawn(transform.position, true);
            Destroy(gameObject);
        }
    }
    public sealed class Shuriken : MonoBehaviour
    {
        HookableBody body;
        float expires;
        bool returned;
        Collider2D hitbox;
        void Awake()
        {
            body = GetComponent<HookableBody>(); hitbox = GetComponent<Collider2D>();
            foreach (var target in FindObjectsByType<HookableBody>(FindObjectsSortMode.None))
                if (target.Kind == HookableBody.BodyKind.Enemy) ConfigureEnemy(target.GetComponent<Collider2D>());
            foreach (var other in FindObjectsByType<Shuriken>(FindObjectsSortMode.None))
                if (other != this) Physics2D.IgnoreCollision(hitbox, other.GetComponent<Collider2D>(), true);
        }
        public void ConfigureEnemy(Collider2D enemy) { Physics2D.IgnoreCollision(hitbox, enemy, !returned); }
        public void Reflect()
        {
            if (returned) return;
            returned = true;
            foreach (var target in FindObjectsByType<HookableBody>(FindObjectsSortMode.None))
                if (target.Kind == HookableBody.BodyKind.Enemy) ConfigureEnemy(target.GetComponent<Collider2D>());
            var player = FindFirstObjectByType<PlayerController>();
            if (player) Physics2D.IgnoreCollision(hitbox, player.GetComponent<Collider2D>(), true);
        }
        void Start() { body = GetComponent<HookableBody>(); expires = Time.time + 8f; }
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
            else if (returned && enemy && enemy.Kind == HookableBody.BodyKind.Enemy) { enemy.ReceiveDamage(30f, transform.position); Destroy(gameObject); }
            else if (!enemy && !player) Destroy(gameObject);
        }
    }
    public sealed class TangEnemyAI : MonoBehaviour
    {
        public PlayerController Player;
        [Header("독가스")]
        [Tooltip("플레이어가 이 반경 안에 들어오면 2초간 접근한 뒤 독가스를 뿌립니다.")]
        public float PoisonTriggerRadius = 4f;
        HookableBody body;
        float until, cooldown, poisonCooldown;
        int state;
        float direction = 1f;
        void Start() { body = GetComponent<HookableBody>(); }
        void FixedUpdate()
        {
            if (!Player || !body.Body.simulated || !Player.Body.simulated) return;
            if (body.IsHooked || body.IsThrown)
            {
                state = 0;
                cooldown = Time.time + 0.8f;
                poisonCooldown = Mathf.Max(poisonCooldown, cooldown);
                return;
            }
            Vector2 delta = Player.Body.position - body.Body.position;
            float distance = Mathf.Abs(delta.x);
            if (Mathf.Abs(delta.x) > 0.05f) direction = Mathf.Sign(delta.x);
            if (state == 1)
            {
                Move(direction * 2f);
                if (Time.time >= until) { Move(0); PoisonCloud.Spawn(body.Body.position + Vector2.right * direction * 1.8f); state = 0; cooldown = poisonCooldown = Time.time + 3f; }
                return;
            }
            if (state == 2)
            {
                Move(0);
                if (Time.time >= until)
                {
                    var go = PrototypeBootstrap.CreateHookable("수리검", body.Body.position + Vector2.right * direction, Vector2.one * 0.55f, Color.yellow, HookableBody.BodyKind.Prop, 0.5f, 1000f, false);
                    CombatShapes.DressShuriken(go);
                    go.GetComponent<Rigidbody2D>().gravityScale = 0;
                    go.GetComponent<Rigidbody2D>().linearVelocity = Vector2.right * direction * 7f;
                    go.AddComponent<Shuriken>();
                    state = 0; cooldown = Time.time + 1.5f;
                    GetComponent<SpriteRenderer>().color = body.BaseColor;
                }
                return;
            }
            if (distance > 12f) { Move(0); return; }
            if (delta.sqrMagnitude <= PoisonTriggerRadius * PoisonTriggerRadius && Time.time >= poisonCooldown)
            {
                state = 1;
                until = Time.time + 2f;
                Move(direction * 2f);
                return;
            }
            if (distance >= 3f && distance <= 5f && Time.time >= cooldown)
            { state = 2; until = Time.time + 0.6f; GetComponent<SpriteRenderer>().color = Color.yellow; Move(0); return; }
            Move(distance > 5f ? direction * 2f : distance < 3f ? -direction * 2f : 0f);
        }
        void Move(float speed) { body.Body.linearVelocity = new Vector2(speed, body.Body.linearVelocity.y); }
    }
}
