using UnityEngine;
namespace FishingPrototype
{
    public sealed class PlayerAppearance : MonoBehaviour
    {
        [Header("프리팹 외형 연결")]
        public Transform model;
        public Transform leftBoot;
        public Transform rightBoot;
        PlayerController player;
        SpriteRenderer[] parts;
        Color[] colors;
        Vector3 modelPosition, modelScale, leftBootPosition, rightBootPosition;
        float flashUntil, step;
        float facing = 1f;
        void Start()
        {
            player = GetComponent<PlayerController>();
            if (!model || !leftBoot || !rightBoot)
            {
                Debug.LogError("플레이어 프리팹의 외형과 양쪽 장화 연결을 확인하세요.", this);
                enabled = false;
                return;
            }
            modelPosition = model.localPosition;
            modelScale = model.localScale;
            leftBootPosition = leftBoot.localPosition;
            rightBootPosition = rightBoot.localPosition;
            parts = model.GetComponentsInChildren<SpriteRenderer>();
            colors = new Color[parts.Length];
            for (int i = 0; i < parts.Length; i++) colors[i] = parts[i].color;
        }
        public void Flash() { flashUntil = Time.time + 0.1f; }
        void LateUpdate()
        {
            if (!model || !player.Body) return;
            model.gameObject.SetActive(player.Body.simulated);
            if (!player.Body.simulated) return;
            float speed = player.Body.linearVelocity.x;
            if (Mathf.Abs(speed) > 0.2f) facing = Mathf.Sign(speed);
            model.localScale = new Vector3(modelScale.x * facing, modelScale.y, modelScale.z);
            bool walking = player.Grounded && Mathf.Abs(speed) > 0.2f;
            if (walking) step += Mathf.Abs(speed) * Time.deltaTime * 2f;
            float stride = walking ? Mathf.Sin(step) * 0.055f : 0f;
            leftBoot.localPosition = leftBootPosition + Vector3.up * Mathf.Max(0f, stride);
            rightBoot.localPosition = rightBootPosition + Vector3.up * Mathf.Max(0f, -stride);
            model.localPosition = modelPosition + Vector3.up * (walking ? Mathf.Abs(Mathf.Sin(step)) * 0.015f : 0f);
            for (int i = 0; i < parts.Length; i++) parts[i].color = Time.time < flashUntil ? Color.white : colors[i];
        }
    }
}
