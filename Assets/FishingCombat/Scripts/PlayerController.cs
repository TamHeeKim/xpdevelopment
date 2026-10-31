using UnityEngine.InputSystem;
using UnityEngine;

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
        public bool ControlsLocked { get; set; }
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
            Body.linearVelocity = new Vector2(moveInput * moveSpeed * (GetComponent<PoisonStatus>() && Time.time < GetComponent<PoisonStatus>().until ? 0.6f : 1f), Body.linearVelocity.y);
            if (Time.time - lastJumpTime < 0.12f && IsGrounded())
            {
                Body.linearVelocity = new Vector2(Body.linearVelocity.x, jumpSpeed);
                lastJumpTime = -10f;
            }
        }

        private bool IsGrounded()
        {
            Vector2 origin = (Vector2)transform.position + Vector2.down * 0.72f;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 0.18f);
            foreach (RaycastHit2D hit in hits)
                if (hit.collider != null && hit.rigidbody != Body) return true;
            return false;
        }
    }
}
