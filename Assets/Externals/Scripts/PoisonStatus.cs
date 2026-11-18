using UnityEngine;
namespace FishingPrototype
{
    public sealed class PoisonStatus : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("Until")]
        [Header("독 상태")]
        public float until;
        float nextTick;
        void Update()
        {
            if (Time.time >= until) return;
            if (Time.time >= nextTick) { nextTick = Time.time + 0.5f; GetComponent<PlayerHealth>().ReceiveDamage(3f, Vector2.zero); }
        }
        public void Apply() { until = Time.time + 2f; }
    }
}
