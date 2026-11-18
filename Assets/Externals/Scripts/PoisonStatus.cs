using UnityEngine;
namespace FishingPrototype
{
    public sealed class PoisonStatus : MonoBehaviour
    {
        public enum PlayerStatus { normal, poisoning }
        [UnityEngine.Serialization.FormerlySerializedAs("Until")]
        [Header("독 상태")]
        public float until;
        public PlayerStatus status => Time.time < until ? PlayerStatus.poisoning : PlayerStatus.normal;
        public float SpeedMultiplier => status == PlayerStatus.poisoning ? 1f - reduction : 1f;
        float nextTick, damage, interval, reduction;
        void Update() => Tick(Time.time);
        void Tick(float now)
        {
            if (until <= 0f || interval <= 0f || nextTick > now || nextTick >= until) return;
            int ticks = Mathf.Min(Mathf.FloorToInt((now - nextTick) / interval) + 1,
                Mathf.CeilToInt((until - nextTick) / interval));
            nextTick += ticks * interval;
            // 같은 프레임에 밀린 틱은 합산하되, 만료 이후 피해는 포함하지 않습니다.
            GetComponent<PlayerHealth>()?.ReceiveDamage(damage * ticks, Vector2.zero);
        }
        public void Apply(float poisonDuration = 2f, float damagePerTick = 3f, float damageTickInterval = 0.5f, float moveSpeedReduction = 0.4f)
        {
            Tick(Time.time);
            var player = GetComponent<PlayerController>();
            if (player && player.ControlsLocked) return;
            bool wasPoisoned = status == PlayerStatus.poisoning;
            interval = Mathf.Max(0.01f, damageTickInterval);
            if (!wasPoisoned) nextTick = Time.time + interval;
            damage = Mathf.Max(0f, damagePerTick); reduction = Mathf.Clamp01(moveSpeedReduction);
            until = Time.time + Mathf.Max(0f, poisonDuration);
        }
    }
}
