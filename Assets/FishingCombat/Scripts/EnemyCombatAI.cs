using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FishingPrototype
{
    [RequireComponent(typeof(HookableBody))]
    public sealed class EnemyCombatAI : MonoBehaviour
    {
        public enum EnemyType { Rusher, Bruiser }

        private EnemyType type;
        private PlayerController player;
        private PlayerHealth playerHealth;
        private HookableBody hookable;
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private Color baseColor;
        private float moveSpeed;
        private float attackRange;
        private float damage;
        private float nextAttack;
        private float attackMoment;
        private bool windingUp;

        private void Awake()
        {
            hookable = GetComponent<HookableBody>();
            body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
        }

        public void Configure(EnemyType enemyType, PlayerController target, Color color)
        {
            type = enemyType;
            player = target;
            playerHealth = target.GetComponent<PlayerHealth>();
            baseColor = color;
            moveSpeed = type == EnemyType.Rusher ? 4.2f : 2.35f;
            attackRange = type == EnemyType.Rusher ? 1.25f : 2.15f;
            damage = type == EnemyType.Rusher ? 10f : 19f;
        }

        private void FixedUpdate()
        {
            if (!player || !body.simulated || hookable.IsHooked) return;
            if (hookable.IsThrown) { windingUp = false; return; }

            Vector2 delta = player.Body.position - body.position;
            float direction = Mathf.Sign(delta.x);

            if (windingUp)
            {
                body.linearVelocity = new Vector2(Mathf.MoveTowards(body.linearVelocity.x, 0f, 20f * Time.fixedDeltaTime), body.linearVelocity.y);
                if (Time.time >= attackMoment) PerformAttack(direction);
                return;
            }

            if (Mathf.Abs(body.linearVelocity.x) > moveSpeed * 1.7f || Mathf.Abs(body.linearVelocity.y) > 3f) return;

            if (Mathf.Abs(delta.x) <= attackRange && Mathf.Abs(delta.y) < 1.8f && Time.time >= nextAttack)
            {
                windingUp = true;
                attackMoment = Time.time + (type == EnemyType.Rusher ? 0.22f : 0.5f);
                visual.color = new Color(1f, 0.72f, 0.2f);
                return;
            }

            if (Mathf.Abs(delta.x) < 13f)
            {
                float desired = direction * moveSpeed;
                body.linearVelocity = new Vector2(Mathf.MoveTowards(body.linearVelocity.x, desired, 12f * Time.fixedDeltaTime), body.linearVelocity.y);
            }
        }

        private void PerformAttack(float direction)
        {
            windingUp = false;
            visual.color = baseColor;
            nextAttack = Time.time + (type == EnemyType.Rusher ? 1.05f : 1.65f);
            float currentDistance = Mathf.Abs(player.Body.position.x - body.position.x);
            if (currentDistance <= attackRange + 0.65f)
            {
                Vector2 knockback = new Vector2(direction * (type == EnemyType.Rusher ? 5.5f : 9.5f), type == EnemyType.Rusher ? 2.5f : 4.2f);
                playerHealth.ReceiveDamage(damage, knockback);
            }
            body.AddForce(new Vector2(direction * (type == EnemyType.Rusher ? 2f : 5f), 0f), ForceMode2D.Impulse);
        }
    }
}
