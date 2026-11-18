using System.Collections;
using UnityEngine;
using FishingPrototype.Integration;

namespace FishingPrototype
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("MaxHealth")]
        [Header("플레이어 체력")]
        public float maxHealth = 100f;
        public CombatStatDefinition statDefinition;
        [Header("물리 피격 (임시값)")]
        public float invulnTime = 0.5f, hitLock = 0.15f;
        private float invulnerableUntil;
        public float Health { get; private set; }

        private Rigidbody2D body;
        private SpriteRenderer visual;
        private Color baseColor;
        private Vector2 checkpoint;
        private bool recovering;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            baseColor = visual.color;
            if (statDefinition) maxHealth = statDefinition.Values.hp;
            Health = maxHealth;
            checkpoint = transform.position;
        }

        public void SetCheckpoint(Vector2 point) => checkpoint = point;
        public void Heal() { Health = maxHealth;  }

        public void ReceiveDamage(float damage, Vector2 knockback)
        {
            if (recovering || damage <= 0 || Time.time < invulnerableUntil) return;
            invulnerableUntil = Time.time + (statDefinition && statDefinition.Values.revisedCombat ? statDefinition.Values.invulnTime : invulnTime);
            GetComponent<PlayerController>().LockHit(statDefinition && statDefinition.Values.revisedCombat ? statDefinition.Values.hitLock : hitLock);
            FindFirstObjectByType<FishingRodController>()?.OnPhysicalHit();
            Health = Mathf.Max(0f, Health - damage);
            body.AddForce(knockback, ForceMode2D.Impulse);
            CameraShake.Pulse(Mathf.Clamp01(damage / 30f));
            GetComponent<PlayerAppearance>()?.Flash();
            StartCoroutine(HitFlash());

            if (Health <= 0f) StartCoroutine(Recover());
        }

        public void ReceivePoisonDamage(float damage)
        {
            if (recovering || damage <= 0) return;
            Health = Mathf.Max(0, Health - damage);
            if (Health <= 0) StartCoroutine(Recover());
        }

        private IEnumerator HitFlash()
        {
            visual.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            visual.color = baseColor;
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
            FindFirstObjectByType<StageDirector>()?.ResetEncounter();

        }

    }
}
