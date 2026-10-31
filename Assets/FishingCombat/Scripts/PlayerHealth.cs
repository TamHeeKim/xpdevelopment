using System.Collections;
using UnityEngine;

namespace FishingPrototype
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("MaxHealth")]
        [Header("플레이어 체력")]
        public float maxHealth = 100f;
        public float Health { get; private set; }

        private Rigidbody2D body;
        private SpriteRenderer visual;
        private Vector2 checkpoint;
        private TextMesh healthText;
        private bool recovering;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            Health = maxHealth;
            checkpoint = transform.position;
        }

        private void Start() => CreateHud();

        private void CreateHud()
        {
            GameObject go = new GameObject("Text_Health");
            go.transform.SetParent(Camera.main.transform, false);
            go.transform.localPosition = new Vector3(4.7f, 4.72f, 10f);
            healthText = go.AddComponent<TextMesh>();
            healthText.font = PrototypeBootstrap.KoreanFont;
            healthText.GetComponent<MeshRenderer>().sharedMaterial = PrototypeBootstrap.KoreanFont.material;
            healthText.GetComponent<MeshRenderer>().enabled = false;
            healthText.anchor = TextAnchor.UpperRight;
            healthText.alignment = TextAlignment.Right;
            healthText.fontSize = 42;
            healthText.characterSize = 0.05f;
            healthText.color = new Color(0.3f, 0.9f, 1f);
            healthText.GetComponent<MeshRenderer>().sortingOrder = 110;
            RefreshHud();
        }

        public void SetCheckpoint(Vector2 point) => checkpoint = point;
        public void Heal() { Health = maxHealth; RefreshHud(); }

        public void ReceiveDamage(float damage, Vector2 knockback)
        {
            if (recovering) return;
            Health = Mathf.Max(0f, Health - damage);
            body.AddForce(knockback, ForceMode2D.Impulse);
            CameraShake.Pulse(Mathf.Clamp01(damage / 30f));
            GetComponent<PlayerAppearance>()?.Flash();
            StartCoroutine(HitFlash());
            RefreshHud();
            if (Health <= 0f) StartCoroutine(Recover());
        }

        private IEnumerator HitFlash()
        {
            visual.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            visual.color = new Color(0.2f, 0.72f, 0.95f);
        }

        private IEnumerator Recover()
        {
            recovering = true;
            FindFirstObjectByType<FishingRodController>()?.Interrupt();
            GetComponent<PlayerController>().ControlsLocked = true;
            var poison = GetComponent<PoisonStatus>(); if (poison) poison.until = 0;
            body.simulated = false;
            visual.enabled = false;
            yield return new WaitForSeconds(0.8f);
            transform.position = checkpoint;
            body.simulated = true;
            body.linearVelocity = Vector2.zero;
            Health = maxHealth;
            visual.enabled = true;
            recovering = false;
            GetComponent<PlayerController>().ControlsLocked = false;
            FindFirstObjectByType<StageDirector>().ResetEncounter();
            RefreshHud();
        }

        private void RefreshHud()
        {
            if (healthText) healthText.text = CombatText.Format("health.value", Mathf.CeilToInt(Health), Mathf.CeilToInt(maxHealth));
        }
    }
}
