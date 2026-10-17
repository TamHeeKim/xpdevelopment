using System.Collections;
using UnityEngine;

namespace FishingPrototype
{
    public sealed class SmoothCameraFollow : MonoBehaviour
    {
        public Transform Target;
        private Vector3 velocity;

        private void LateUpdate()
        {
            if (!Target) return;
            Vector3 desired = new Vector3(Target.position.x + 2.2f, 0.35f, -10f);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.24f);
        }
    }

    public sealed class CameraShake : MonoBehaviour
    {
        private static CameraShake instance;
        private float strength;

        private void Awake() => instance = this;

        public static void Pulse(float amount)
        {
            if (instance) instance.strength = Mathf.Max(instance.strength, amount * 0.18f);
        }

        private void LateUpdate()
        {
            if (strength <= 0.001f) return;
            transform.position += (Vector3)(Random.insideUnitCircle * strength);
            strength = Mathf.MoveTowards(strength, 0f, Time.deltaTime * 1.4f);
        }
    }
}
