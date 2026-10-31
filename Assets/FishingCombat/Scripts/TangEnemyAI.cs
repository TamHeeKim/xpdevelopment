using UnityEngine;
namespace FishingPrototype
{
    public sealed class TangEnemyAI : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("Player")]
        [Header("연결")]
        public PlayerController player;
        [Header("독가스")]
        [Tooltip("플레이어가 이 반경 안에 들어오면 2초간 접근한 뒤 독가스를 뿌립니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("PoisonTriggerRadius")]
        public float poisonTriggerRadius = 4f;
        [Header("공격 프리팹")]
        public PoisonCloud poisonCloudPrefab;
        public Shuriken shurikenPrefab;
        HookableBody body;
        float until, cooldown, poisonCooldown;
        int state;
        float direction = 1f;
        void Start() { body = GetComponent<HookableBody>(); if (!player) player = FindFirstObjectByType<PlayerController>(); }
        void FixedUpdate()
        {
            if (!player || !body.Body.simulated || !player.Body.simulated) return;
            if (body.IsHooked || body.IsThrown)
            {
                state = 0;
                cooldown = Time.time + 0.8f;
                poisonCooldown = Mathf.Max(poisonCooldown, cooldown);
                return;
            }
            Vector2 delta = player.Body.position - body.Body.position;
            float distance = Mathf.Abs(delta.x);
            if (Mathf.Abs(delta.x) > 0.05f) direction = Mathf.Sign(delta.x);
            if (state == 1)
            {
                Move(direction * 2f);
                if (Time.time >= until) { Move(0); PoisonCloud.Spawn(poisonCloudPrefab, body.Body.position + Vector2.right * direction * 1.8f); state = 0; cooldown = poisonCooldown = Time.time + 3f; }
                return;
            }
            if (state == 2)
            {
                Move(0);
                if (Time.time >= until)
                {
                    var projectile = Instantiate(shurikenPrefab, body.Body.position + Vector2.right * direction, Quaternion.identity);
                    projectile.GetComponent<Rigidbody2D>().linearVelocity = Vector2.right * direction * 7f;
                    state = 0; cooldown = Time.time + 1.5f;
                    GetComponent<SpriteRenderer>().color = body.baseColor;
                }
                return;
            }
            if (distance > 12f) { Move(0); return; }
            if (delta.sqrMagnitude <= poisonTriggerRadius * poisonTriggerRadius && Time.time >= poisonCooldown)
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
