using UnityEngine.InputSystem;
using UnityEngine;
using FishingPrototype.Integration;

namespace FishingPrototype
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("MoveSpeed")]
        [Header("이동")]
        public float moveSpeed = 7f;
        [UnityEngine.Serialization.FormerlySerializedAs("JumpSpeed")]
        public float jumpSpeed = 10.5f;
        public Rigidbody2D Body { get; private set; }
        public CombatStatDefinition statDefinition;
        private bool controlsLocked;
        private float hitLockedUntil;
        public bool ControlsLocked { get => controlsLocked || Time.time < hitLockedUntil; set => controlsLocked = value; }
        public void LockHit(float duration) { hitLockedUntil = Time.time + Mathf.Max(0, duration); moveInput = 0; lastJumpTime = -10; }
        public bool Grounded => IsGrounded();

        private float moveInput;
        private float lastJumpTime = -10f;

        private void Awake() => Body = GetComponent<Rigidbody2D>();

        private void Update()
        {
            if (ControlsLocked)
            {
                moveInput = 0f;
                lastJumpTime = -10f;
                return;
            }
            moveInput = (((Keyboard.current?.dKey.isPressed ?? false) || (Keyboard.current?.rightArrowKey.isPressed ?? false)) ? 1f : 0f) - (((Keyboard.current?.aKey.isPressed ?? false) || (Keyboard.current?.leftArrowKey.isPressed ?? false)) ? 1f : 0f);
            if ((Keyboard.current?.wKey.wasPressedThisFrame ?? false)) lastJumpTime = Time.time;
        }

        private void FixedUpdate()
        {
            if (ControlsLocked) return;
            var poison = GetComponent<PoisonStatus>();
            Body.linearVelocity = new Vector2(moveInput * (statDefinition ? statDefinition.Values.moveSpeed : moveSpeed) * (poison ? poison.SpeedMultiplier : 1f), Body.linearVelocity.y);
            if (Time.time - lastJumpTime < 0.12f && IsGrounded())
            {
                Body.linearVelocity = new Vector2(Body.linearVelocity.x, statDefinition && statDefinition.Values.revisedCombat ? statDefinition.Values.jumpPower : jumpSpeed);
                lastJumpTime = -10f;
            }
        }

        private bool IsGrounded()
        {
            Vector2 origin = (Vector2)transform.position + Vector2.down * 0.72f;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 0.18f);
            foreach (RaycastHit2D hit in hits)
                if (hit.collider != null && !hit.collider.isTrigger && hit.rigidbody != Body) return true;
            return false;
        }
    }
}
