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
        private Vector3[] smokePositions, smokeScales;
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
            foreach (var col in Physics2D.OverlapBoxAll(transform.position, area, 0f))
            {
                var bottle = col.GetComponent<PoisonBottle>();
                if (bottle && bottle.GetComponent<HookableBody>().IsHooked && !bottle.Filled)
                { bottle.Fill(); Destroy(gameObject); return; }
                var player = col.GetComponent<PlayerController>();
                if (player) { var status = player.GetComponent<PoisonStatus>(); if (status) status.Apply(); }
            }
        }
    }
}
