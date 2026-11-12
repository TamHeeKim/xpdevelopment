using UnityEngine;
namespace FishingPrototype
{
    public sealed class PoisonBottle : MonoBehaviour
    {
        [Header("반격 효과 프리팹")]
        public PoisonCloud counterCloudPrefab;
        public bool Filled { get; private set; }
        public void Fill() { Filled = true; GetComponent<HookableBody>().SetDisplayNameKey("object.poison_bottle"); GetComponent<HookableBody>().baseColor = Color.green; GetComponent<SpriteRenderer>().color = Color.green; }
        void OnCollisionEnter2D(Collision2D hit)
        {
            var body = GetComponent<HookableBody>();
            if (!Filled || !body.IsThrown) return;
            Filled = false;
            PoisonCloud.Spawn(counterCloudPrefab, transform.position);
            Destroy(gameObject);
        }
    }
}
