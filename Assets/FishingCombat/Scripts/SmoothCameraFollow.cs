using System.Collections;
using UnityEngine;

namespace FishingPrototype
{
    public sealed class SmoothCameraFollow : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("Target")]
        [Header("추적 대상")]
        public Transform target;
        [Header("화면 구도")]
        public Vector3 offset = new Vector3(2.2f, 0.35f, -10f);
        [Min(0.01f)] public float smoothTime = 0.24f;
        private Vector3 velocity;

        private void LateUpdate()
        {
            if (!target) return;
            Vector3 desired = new Vector3(target.position.x + offset.x, offset.y, offset.z);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }
    }
}
