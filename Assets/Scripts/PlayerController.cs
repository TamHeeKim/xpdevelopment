using UnityEngine;

namespace FishingPrototype
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        public Transform RodVisual;
        public float MoveSpeed = 7f;
        public float JumpSpeed = 10.5f;
        public Rigidbody2D Body { get; private set; }
        public bool PreserveHorizontalMomentum { get; set; }
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
            moveInput = Input.GetAxisRaw("Horizontal");
            if (Input.GetKeyDown(KeyCode.W)) lastJumpTime = Time.time;
        }

        private void FixedUpdate()
        {
            if (ControlsLocked) return;
            if (!PreserveHorizontalMomentum)
                Body.linearVelocity = new Vector2(moveInput * MoveSpeed, Body.linearVelocity.y);
            else
                Body.AddForce(Vector2.right * moveInput * 4f);
            if (Time.time - lastJumpTime < 0.12f && IsGrounded())
            {
                Body.linearVelocity = new Vector2(Body.linearVelocity.x, JumpSpeed);
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
