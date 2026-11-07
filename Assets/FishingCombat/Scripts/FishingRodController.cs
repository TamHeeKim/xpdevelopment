using System.Collections.Generic;
using FishingPrototype.Integration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FishingPrototype
{
    public sealed class FishingRodController : MonoBehaviour
    {
        private enum RodState { Idle, Extending, Catching, Held, Slamming }
        [Header("연결")]
        public PlayerController player;
        public Transform rodVisual;
        public CombatStatDefinition statDefinition;
        [SerializeField] private Rigidbody2D tipBody;
        [SerializeField] private CircleCollider2D tipCollider;
        [SerializeField] private LineRenderer line, strikeLine, outline;
        [SerializeField] private BoxCollider2D attackCollider;
        public AudioSource captureAudio;
        [Header("시트 열이 없을 때 기본값")]
        [Min(0)] public float attack = 10f;
        [Min(0.01f)] public float attackWidth = 0.6f;
        [Min(0.1f)] public float catchRange = 7.5f;
        [Range(0, 360)] public float throwAngle = 60f;
        [Min(0)] public float throwRange = 10f;
        [Header("동작 속도")]
        [Min(0.1f)] public float lineSpeed = 24f;
        [Min(0.1f)] public float throwSpeed = 12f;
        [Min(0.05f)] public float slamDuration = 0.4f;
        public float idleLength = 1.65f;
        private const float baseHoldTime = 0.3f, attackDuration = 0.5f;
        public float Attack => statDefinition ? statDefinition.Values.attack : attack;
        public float AttackWidth => statDefinition && statDefinition.Values.attackWidth > 0 ? statDefinition.Values.attackWidth : attackWidth;
        public float CatchRange => statDefinition && statDefinition.Values.catchRange > 0 ? statDefinition.Values.catchRange : catchRange;
        public float CaptureProgress => target && (state == RodState.Extending || state == RodState.Catching)
            ? Mathf.Clamp01((Time.time - pressedAt) / (baseHoldTime + target.CatchHold)) : 0f;
        public string HeldName => hooked ? hooked.DisplayName : "";
        public string EquipmentName => CombatText.Get("equipment.bobber");
        private RodState state;
        private HookableBody target, hooked, slamTarget;
        private float pressedAt, attackUntil = float.NegativeInfinity, slamStarted, slamAttack;
        private Vector2 tipPosition;
        private Vector2 aim = Vector2.right, attackStart, attackEnd, holdDirection, slamStart, slamEnd;
        private readonly HashSet<HookableBody> attackHits = new HashSet<HookableBody>();
        private RigidbodyType2D savedBodyType;
        private bool bodyClaimed;
        private SpriteRenderer tipVisual;
        private float rodFacing = 1f;
        private Vector3 rodPosition, rodScale, rodAngles;
        private Vector2 RodAnchor => rodVisual.TransformPoint(Vector3.right * 0.5f);

        private void Start()
        {
            if (!player || !rodVisual || !tipBody || !attackCollider)
                throw new System.InvalidOperationException("플레이어 전투 프리팹 연결을 확인하세요.");
            rodPosition = rodVisual.localPosition; rodScale = rodVisual.localScale; rodAngles = rodVisual.localEulerAngles;
            tipVisual = tipBody.GetComponent<SpriteRenderer>();
            tipCollider.enabled = false;
            tipBody.bodyType = RigidbodyType2D.Kinematic;
            tipBody.simulated = false;
            tipPosition = RodAnchor + aim * idleLength;
            attackCollider.enabled = false;
        }
        private void Update()
        {
            if (!player.Body || !player.Body.simulated || player.ControlsLocked) { Interrupt(); return; }
            UpdateRodVisual();
            Vector2 cursor = Camera.main ? (Vector2)Camera.main.ScreenToWorldPoint(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero) : RodAnchor + aim;
            if ((cursor - (Vector2)player.transform.position).sqrMagnitude > 0.0001f) aim = (cursor - (Vector2)player.transform.position).normalized;
            if (Mouse.current?.leftButton.wasPressedThisFrame ?? false) BeginPress(cursor);
            if ((Mouse.current?.leftButton.wasReleasedThisFrame ?? false) && (state == RodState.Extending || state == RodState.Catching)) CancelCapture();
            if (Keyboard.current?.eKey.wasPressedThisFrame ?? false) BeginSlam();
            if (Keyboard.current?.qKey.wasPressedThisFrame ?? false) ThrowHeld(aim);
            UpdateTargetOutline(state == RodState.Idle ? SelectCatchTarget(cursor) : target);
        }
        private void FixedUpdate()
        {
            if (!player.Body || !player.Body.simulated || player.ControlsLocked) return;
            TickAttack();
            if (state == RodState.Extending || state == RodState.Catching)
                TickCapture(Mouse.current?.leftButton.isPressed ?? false);
            else if (state == RodState.Held)
            {
                if (!Alive(hooked)) { CancelCapture(); return; }
                hooked.Body.position = (Vector2)player.transform.position + holdDirection * (CatchRange * 0.5f);
            }
            else if (state == RodState.Slamming) TickSlam();
            else tipPosition = Time.time < attackUntil ? attackEnd : RodAnchor + aim * idleLength;
        }
        private void LateUpdate()
        {
            if (tipBody) tipBody.transform.position = tipPosition;
            if (tipVisual) tipVisual.enabled = !bodyClaimed;
            if (attackCollider && attackCollider.enabled) PositionAttackCollider();
            DrawLine();
        }
        private void UpdateRodVisual()
        {
            if (Mathf.Abs(player.Body.linearVelocity.x) > 0.2f) rodFacing = Mathf.Sign(player.Body.linearVelocity.x);
            rodVisual.localPosition = new Vector3(rodPosition.x * rodFacing, rodPosition.y, rodPosition.z);
            rodVisual.localRotation = Quaternion.Euler(rodAngles.x, rodAngles.y, rodAngles.z * rodFacing);
            rodVisual.localScale = new Vector3(rodScale.x * rodFacing, rodScale.y, rodScale.z);
        }
        private static bool Alive(HookableBody body) => body && body.Body && body.Body.simulated && (body.kind != HookableBody.BodyKind.Enemy || body.Health > 0f);

        public HookableBody SelectCatchTarget(Vector2 cursor)
        {
            HookableBody hovered = null, nearest = null, prop = null;
            float hoveredDistance = float.PositiveInfinity, nearestDistance = float.PositiveInfinity, propDistance = float.PositiveInfinity;
            foreach (var candidate in FindObjectsByType<HookableBody>(FindObjectsSortMode.None))
            {
                if (!Alive(candidate) || candidate.IsHooked) continue;
                float distance = Vector2.Distance(player.transform.position, candidate.transform.position);
                if (distance > CatchRange) continue;
                if (candidate.kind == HookableBody.BodyKind.Enemy)
                {
                    if (distance < nearestDistance) { nearest = candidate; nearestDistance = distance; }
                    if (candidate.targetingCollider && candidate.targetingCollider.OverlapPoint(cursor) && distance < hoveredDistance)
                    { hovered = candidate; hoveredDistance = distance; }
                }
                else if (candidate.GetComponent<Collider2D>().OverlapPoint(cursor) && distance < propDistance)
                { prop = candidate; propDistance = distance; }
            }
            return hovered ? hovered : prop ? prop : nearest;
        }
        public void BeginPress(Vector2 cursor)
        {
            if (state != RodState.Idle) return;
            if (Time.time >= attackUntil) BeginAttack(cursor);
            target = SelectCatchTarget(cursor);
            if (!target) return;
            pressedAt = Time.time;
            tipPosition = RodAnchor;
            state = RodState.Extending;
        }
        private void BeginAttack(Vector2 cursor)
        {
            attackStart = RodAnchor; attackEnd = cursor; attackUntil = Time.time + attackDuration; attackHits.Clear();
            Vector2 delta = attackEnd - attackStart;
            PositionAttackCollider();
            Vector3 scale = attackCollider.transform.lossyScale;
            attackCollider.size = new Vector2(Mathf.Max(0.001f, delta.magnitude) / Mathf.Abs(scale.x), AttackWidth / Mathf.Abs(scale.y));
            attackCollider.enabled = true;
            TickAttack();
        }
        private void PositionAttackCollider()
        {
            Vector2 delta = attackEnd - attackStart;
            attackCollider.transform.SetPositionAndRotation((attackStart + attackEnd) * 0.5f, Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
        }
        private void TickAttack()
        {
            if (Time.time >= attackUntil) { attackCollider.enabled = false; return; }
            Vector2 delta = attackEnd - attackStart;
            foreach (var col in Physics2D.OverlapBoxAll((attackStart + attackEnd) * 0.5f, new Vector2(Mathf.Max(0.001f, delta.magnitude), AttackWidth), Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg))
            {
                if (col.isTrigger) continue;
                var body = col.GetComponent<HookableBody>();
                if (Alive(body) && body.kind == HookableBody.BodyKind.Enemy && attackHits.Add(body)) body.ReceiveDamage(Attack);
            }
        }
        private void TickCapture(bool held)
        {
            if (!held || !Alive(target) || Vector2.Distance(player.transform.position, target.transform.position) > CatchRange)
            { CancelCapture(); return; }
            if (state == RodState.Extending)
            {
                tipPosition = Vector2.MoveTowards(tipPosition, target.Body.position, lineSpeed * Time.fixedDeltaTime);
                if (Vector2.Distance(tipPosition, target.Body.position) > 0.02f) return;
                ClaimBody(target); state = RodState.Catching;
            }
            if (Time.time - pressedAt >= baseHoldTime + target.CatchHold) CompleteCapture();
        }
        private void ClaimBody(HookableBody body)
        {
            savedBodyType = body.Body.bodyType; bodyClaimed = true;
            body.SetHooked(true); body.Body.linearVelocity = Vector2.zero; body.Body.angularVelocity = 0;
            body.Body.bodyType = RigidbodyType2D.Kinematic;
        }
        private void CompleteCapture()
        {
            hooked = target;
            holdDirection = ((Vector2)hooked.transform.position - (Vector2)player.transform.position).normalized;
            if (holdDirection.sqrMagnitude < 0.001f) holdDirection = Vector2.right * rodFacing;
            RestoreHoldDistance(); state = RodState.Held;
            if (captureAudio) captureAudio.Play();
        }
        private void RestoreHoldDistance()
        {
            hooked.Body.position = (Vector2)player.transform.position + holdDirection * (CatchRange * 0.5f);
            hooked.Body.linearVelocity = Vector2.zero;
        }
        private void CancelCapture()
        {
            if (bodyClaimed && target)
            {
                target.Body.bodyType = savedBodyType;
                target.SetHooked(false); target.ClearThrow();
            }
            bodyClaimed = false; hooked = target = slamTarget = null; state = RodState.Idle;
        }
        public void BeginSlam()
        {
            if (state != RodState.Held || !Alive(hooked)) return;
            float range = CatchRange * 2f / 3f;
            slamTarget = null; float closest = range;
            foreach (var candidate in FindObjectsByType<HookableBody>(FindObjectsSortMode.None))
            {
                if (candidate == hooked || !Alive(candidate) || candidate.kind != HookableBody.BodyKind.Enemy) continue;
                float distance = Vector2.Distance(player.transform.position, candidate.transform.position);
                if (distance <= closest) { slamTarget = candidate; closest = distance; }
            }
            slamStart = hooked.Body.position;
            slamEnd = slamTarget ? slamTarget.Body.position : (Vector2)player.transform.position + Vector2.right * rodFacing * range;
            if (!slamTarget)
            {
                foreach (var hit in Physics2D.RaycastAll(slamEnd + Vector2.up * range, Vector2.down, range * 2f + 5f))
                {
                    if (hit.collider.isTrigger || hit.collider.GetComponent<HookableBody>() || hit.collider.GetComponent<PlayerController>()) continue;
                    slamEnd = hit.point + Vector2.up * hooked.GetComponent<Collider2D>().bounds.extents.y; break;
                }
            }
            slamStarted = Time.time; slamAttack = Attack; state = RodState.Slamming;
        }
        private void TickSlam()
        {
            if (!Alive(hooked)) { CancelCapture(); return; }
            float t = Mathf.Clamp01((Time.time - slamStarted) / Mathf.Max(0.05f, slamDuration));
            Vector2 origin = player.transform.position;
            Vector2 destination = slamEnd - origin;
            float radius = CatchRange * 2f / 3f;
            if (t < 0.25f) hooked.Body.position = Vector2.Lerp(slamStart, origin + Vector2.up * radius, t * 4f);
            else
            {
                float progress = (t - 0.25f) / 0.75f;
                float angle = Mathf.Lerp(90f, Mathf.Atan2(destination.y, destination.x) * Mathf.Rad2Deg, progress) * Mathf.Deg2Rad;
                float length = Mathf.Lerp(radius, destination.magnitude, progress);
                hooked.Body.position = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
            }
            if (t >= 1f) FinishSlam();
        }
        private void FinishSlam()
        {
            var held = hooked;
            var victims = new HashSet<HookableBody>();
            var hitbox = held.GetComponent<Collider2D>();
            foreach (var col in Physics2D.OverlapBoxAll(held.Body.position, hitbox.bounds.size + Vector3.one * 0.15f, 0))
            {
                if (col.isTrigger) continue;
                var victim = col.GetComponent<HookableBody>();
                if (victim != held && Alive(victim) && victim.kind == HookableBody.BodyKind.Enemy && victims.Add(victim)) victim.ReceiveDamage(slamAttack * 1.1f);
            }
            if (held.kind == HookableBody.BodyKind.Enemy) held.ReceiveDamage(slamAttack * 1.2f);
            if (!Alive(held)) { CancelCapture(); return; }
            RestoreHoldDistance(); state = RodState.Held; slamTarget = null;
        }
        public Vector2 SelectThrowDirection(Vector2 direction)
        {
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right * rodFacing;
            HookableBody best = null; float bestAngle = float.PositiveInfinity, bestDistance = float.PositiveInfinity;
            foreach (var candidate in FindObjectsByType<HookableBody>(FindObjectsSortMode.None))
            {
                if (candidate == hooked || !Alive(candidate) || candidate.kind != HookableBody.BodyKind.Enemy) continue;
                Vector2 offset = (Vector2)candidate.transform.position - (Vector2)player.transform.position;
                float distance = offset.magnitude;
                if (distance < 0.001f || distance > throwRange) continue;
                float angle = Vector2.Angle(direction, offset);
                if (angle > throwAngle * 0.5f + 0.001f) continue;
                if (angle < bestAngle - 0.001f || (Mathf.Abs(angle - bestAngle) <= 0.001f && distance < bestDistance))
                { best = candidate; bestAngle = angle; bestDistance = distance; }
            }
            if (!best) return direction;
            Vector2 launchPosition = hooked ? hooked.Body.position : (Vector2)player.transform.position;
            Vector2 toTarget = (Vector2)best.transform.position - launchPosition;
            return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : direction;
        }
        public void ThrowHeld(Vector2 direction)
        {
            if (state != RodState.Held || !Alive(hooked)) return;
            Vector2 velocity = SelectThrowDirection(direction);
            var released = hooked; var star = released.GetComponent<Shuriken>();
            released.Body.bodyType = savedBodyType; released.SetHooked(false);
            released.Body.linearVelocity = velocity * (star ? star.flySpeed * 2f : throwSpeed);
            bodyClaimed = false; hooked = target = null; state = RodState.Idle;
        }
        public void Interrupt()
        {
            CancelCapture(); attackUntil = float.NegativeInfinity;
            if (attackCollider) attackCollider.enabled = false;
        }
        private void OnDisable() { Interrupt(); }
        private void UpdateTargetOutline(HookableBody body)
        {
            outline.enabled = body;
            if (!body) return;
            Bounds b = body.targetingCollider ? body.targetingCollider.bounds : body.GetComponent<Collider2D>().bounds;
            outline.positionCount = 5;
            outline.SetPositions(new[] { new Vector3(b.min.x,b.min.y), new Vector3(b.min.x,b.max.y), new Vector3(b.max.x,b.max.y), new Vector3(b.max.x,b.min.y), new Vector3(b.min.x,b.min.y) });
        }
        private void DrawLine()
        {
            if (!line || !tipBody || !rodVisual) return;
            Vector2 end = bodyClaimed && target ? (Vector2)target.transform.position : tipPosition;
            line.positionCount = 2; line.SetPosition(0, RodAnchor); line.SetPosition(1, end);
            strikeLine.enabled = Time.time < attackUntil;
            if (strikeLine.enabled)
            {
                strikeLine.positionCount = 2; strikeLine.startWidth = strikeLine.endWidth = AttackWidth;
                strikeLine.SetPosition(0, attackStart); strikeLine.SetPosition(1, attackEnd);
            }
        }
    }
}
