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
        void Update()
        {
            if (status != PlayerStatus.poisoning) return;
            if (Time.time >= nextTick)
            {
                nextTick = Time.time + interval;
                GetComponent<PlayerHealth>()?.ReceiveDamage(damage, Vector2.zero);
            }
        }
        public void Apply(float poisonDuration = 2f, float damagePerTick = 3f, float damageTickInterval = 0.5f, float moveSpeedReduction = 0.4f)
        {
            bool wasPoisoned = status == PlayerStatus.poisoning;
            interval = Mathf.Max(0.01f, damageTickInterval);
            if (!wasPoisoned) nextTick = Time.time + interval;
            damage = Mathf.Max(0f, damagePerTick); reduction = Mathf.Clamp01(moveSpeedReduction);
            until = Time.time + Mathf.Max(0f, poisonDuration);
        }
    }
}
