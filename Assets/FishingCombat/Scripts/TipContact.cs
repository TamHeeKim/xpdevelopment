using UnityEngine;
namespace FishingPrototype
{
    public sealed class TipContact : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("Owner")]
        public FishingRodController owner;
        private void OnTriggerEnter2D(Collider2D other) => owner.HandleTipContact(other);
    }
}
