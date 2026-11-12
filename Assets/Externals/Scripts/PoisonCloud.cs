using UnityEngine;
namespace FishingPrototype
{
    public sealed class PoisonCloud : MonoBehaviour
    {
        float expires, nextTick;
        [Header("독가스 효과")]
        public bool damagesEnemies;
        public float duration = 3f;
        public float damage = 8f;
        public float tickInterval = 0.5f;
        public float radius = 2f;
        public Vector2 area = new Vector2(3f, 2f);
        [Header("플레이어 중독")]
        [Min(0)] public float poisonDuration = 2f;
        [Min(0)] public float damagePerTick = 3f;
        [Min(0.01f)] public float damageTickInterval = 0.5f;
        [Range(0, 1), Tooltip("기본 이동속도 감소 비율. 0.4는 40% 감소입니다.")]
        public float moveSpeedReduction = 0.4f;
        public bool refreshDurationOnContact = true;
        private readonly System.Collections.Generic.HashSet<PoisonStatus> touching = new System.Collections.Generic.HashSet<PoisonStatus>();
        private readonly System.Collections.Generic.HashSet<PoisonStatus> previous = new System.Collections.Generic.HashSet<PoisonStatus>();
        private Vector3[] smokePositions, smokeScales;
        public void SetSprayShape(Vector2 origin, Vector2 direction, float length)
        {
            length = Mathf.Max(0.01f, length);
            area.x = length;
            transform.SetPositionAndRotation(origin + direction * length * 0.5f, Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
            transform.localScale = new Vector3(length, area.y, 1f);
        }
        readonly System.Collections.Generic.HashSet<HookableBody> damaged = new System.Collections.Generic.HashSet<HookableBody>();
        public static PoisonCloud Spawn(PoisonCloud prefab, Vector2 position)
        {
            if (!prefab) throw new System.InvalidOperationException("독가스 프리팹이 연결되지 않았습니다.");
            return Instantiate(prefab, position, Quaternion.identity);
        }
        void Update() { if (damagesEnemies) AnimateSmoke(); }
        void AnimateSmoke()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var puff = transform.GetChild(i);
                float phase = Time.time * 1.8f + i * 1.7f;
                puff.localPosition = smokePositions[i] + new Vector3(Mathf.Sin(phase) * 0.035f, Mathf.Cos(phase) * 0.07f, 0f);
                puff.localScale = smokeScales[i] * (1f + Mathf.Sin(phase) * 0.1f);
            }
        }
        void Start()
        {
            expires = Time.time + duration;
            smokePositions = new Vector3[transform.childCount];
            smokeScales = new Vector3[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            { smokePositions[i] = transform.GetChild(i).localPosition; smokeScales[i] = transform.GetChild(i).localScale; }
        }
        void FixedUpdate()
        {
            if (Time.time >= expires) { Destroy(gameObject); return; }
            if (damagesEnemies)
            {
                if (Time.time < nextTick) return;
                nextTick = Time.time + tickInterval;
                damaged.Clear();
                foreach (var col in Physics2D.OverlapCircleAll(transform.position, radius))
                {
                    var target = col.GetComponent<HookableBody>();
                    if (target && target.kind == HookableBody.BodyKind.Enemy && target.Body.simulated && damaged.Add(target))
                        target.ReceiveDamage(damage);
                }
                return;
            }
            touching.Clear();
            foreach (var col in Physics2D.OverlapBoxAll(transform.position, area, transform.eulerAngles.z))
            {
                var bottle = col.GetComponent<PoisonBottle>();
                if (bottle && bottle.GetComponent<HookableBody>().IsHooked && !bottle.Filled)
                { bottle.Fill(); Destroy(gameObject); return; }
                var player = col.GetComponent<PlayerController>();
                if (player && !player.ControlsLocked && player.Body.simulated)
                {
                    var status = player.GetComponent<PoisonStatus>();
                    if (status && touching.Add(status) && (refreshDurationOnContact || !previous.Contains(status)))
                        status.Apply(poisonDuration, damagePerTick, damageTickInterval, moveSpeedReduction);
                }
            }
            previous.Clear(); previous.UnionWith(touching);
        }
    }
}
