using System.Collections.Generic;
using FishingPrototype.Integration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FishingPrototype
{
    public sealed class FishingRodController : MonoBehaviour
    {
        private enum RodState { Idle, Attacking, Extending, Catching, Pulling, Held, Slamming, Throwing }
        [Header("연결")]
        public PlayerController player;
        public Transform rodVisual;
        public CombatStatDefinition statDefinition;
        [SerializeField] private Rigidbody2D tipBody;
        [SerializeField] private CircleCollider2D tipCollider;
        [SerializeField] private LineRenderer line, strikeLine, outline;
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
        [Header("공격 거리와 전체 동작 시간 (임시값)")]
        public float minAttackRange = 2f, maxAttackRange = 10f;
        public float minAttackTime = 0.35f, maxAttackTime = 0.8f;
        public float attackWindup = 0.1f;
        [Header("연결·당기기·포획 위치 (임시값)")]
        public float catchInputTimeout = 2f, holdDistance = 2.5f, holdHeight = 1f;
        public float pullSpeed = 12f, slamRange = 4f, throwRecoveryTime = 0.2f;
        [Header("일반 투척 피해 (기획 확정 전 임시값)")]
        public float thrownSelfDamage = 12f, thrownHitDamage = 11f;
        public float throwArcHeight = 0.7f;
        private bool UsesSheet => statDefinition && statDefinition.Values.revisedCombat;
        public float Attack => statDefinition ? statDefinition.Values.attack : attack;
        public float AttackWidth => statDefinition && statDefinition.Values.attackWidth > 0 ? statDefinition.Values.attackWidth : attackWidth;
        public float CatchRange => statDefinition && statDefinition.Values.catchRange > 0 ? statDefinition.Values.catchRange : catchRange;
        private float HoldDistance => UsesSheet ? statDefinition.Values.holdDistance : holdDistance;
        private float HoldHeight => UsesSheet ? statDefinition.Values.holdHeight : holdHeight;
        private float SlamRange => UsesSheet ? statDefinition.Values.slamRange : slamRange;
        private float CatchTimeout => UsesSheet ? statDefinition.Values.catchInputTimeout : catchInputTimeout;
        public bool CapturePending => state == RodState.Extending || state == RodState.Catching || state == RodState.Pulling;
        public string HeldName => hooked ? hooked.DisplayName : "";
        public string EquipmentName => CombatText.Get("equipment.bobber");
        private RodState state;
        private HookableBody target, hooked, slamTarget;
        private float connectedAt, attackUntil = float.NegativeInfinity, attackStarted, attackTotal, hitAt, slamStarted, slamAttack;
        private bool attackApplied;
        private Vector2 tipPosition;
        private Vector2 aim = Vector2.right, attackStart, attackEnd, slamStart, slamEnd, holdOffset;
        private float castStarted = float.NegativeInfinity, castSide = 1f;
        private RigidbodyType2D savedBodyType;
        private float savedGravity;
        private bool bodyClaimed;
        private SpriteRenderer tipVisual;
        private float rodFacing = 1f;
        private Vector3 rodPosition, rodScale, rodAngles;
        private Vector2 RodAnchor => rodVisual.TransformPoint(Vector3.right * 0.5f);

        private void Start()
        {
            if (!player || !rodVisual || !tipBody)
                throw new System.InvalidOperationException("플레이어 전투 프리팹 연결을 확인하세요.");
            rodPosition = rodVisual.localPosition; rodScale = rodVisual.localScale; rodAngles = rodVisual.localEulerAngles;
            tipVisual = tipBody.GetComponent<SpriteRenderer>();
            if (strikeLine) strikeLine.enabled = false;
            tipCollider.enabled = false;
            tipBody.bodyType = RigidbodyType2D.Kinematic;
            tipBody.simulated = false;
            tipPosition = RodAnchor + aim * idleLength;
        }
        private void Update()
        {
            if (!player.Body || !player.Body.simulated) { Interrupt(); return; }
            UpdateRodVisual();
            Vector2 cursor = Camera.main ? (Vector2)Camera.main.ScreenToWorldPoint(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero) : RodAnchor + aim;
            if ((cursor - player.Body.position).sqrMagnitude > 0.0001f) aim = (cursor - player.Body.position).normalized;
            if (!player.ControlsLocked)
            {
                if (Mouse.current?.leftButton.wasPressedThisFrame ?? false) BeginPress(cursor);
                else if (Mouse.current?.rightButton.wasPressedThisFrame ?? false) BeginCapture(cursor);
            }
            UpdateTargetOutline(state == RodState.Idle && !player.ControlsLocked ? SelectCatchTarget(cursor) : null);
        }
        private void FixedUpdate()
        {
            if (!player.Body || !player.Body.simulated) return;
            if (state == RodState.Attacking) TickAttack();
            else if (CapturePending) TickCapture();
            else if (state == RodState.Held)
            {
                if (!Alive(hooked)) { CancelCapture(); return; }
                MoveHeld(HoldPosition);
            }
            else if (state == RodState.Slamming) TickSlam();
            else if (state == RodState.Throwing) { if (Time.time >= attackUntil) state = RodState.Idle; }
            else tipPosition = Vector2.Lerp(tipPosition, RodAnchor + aim * idleLength, 0.28f);
        }
        private Vector2 HoldPosition => player.Body.position + holdOffset;
        private void MoveHeld(Vector2 position)
        {
            hooked.Body.linearVelocity = Vector2.zero;
            hooked.Body.MovePosition(position);
        }
        private void LateUpdate()
        {
            if (tipBody) tipBody.transform.position = tipPosition;
            if (tipVisual) tipVisual.enabled = !bodyClaimed;
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
            HookableBody best = null;
            float bestDistance = float.PositiveInfinity, bestDepth = float.PositiveInfinity;
            int bestLayer = int.MinValue, bestOrder = int.MinValue;
            var seen = new HashSet<HookableBody>();
            foreach (var col in Physics2D.OverlapCircleAll(player.Body.position, CatchRange))
            {
                var candidate = col.GetComponentInParent<HookableBody>();
                if (!candidate || !seen.Add(candidate) || !Alive(candidate) || !candidate.canCatch || candidate.IsHooked) continue;
                if (Vector2.Distance(player.Body.position, candidate.Body.position) > CatchRange) continue;
                var shape = candidate.GetComponent<Collider2D>();
                if (!shape || !shape.enabled) continue;
                bool inside = shape.OverlapPoint(cursor);
                if (!inside && !(candidate.targetingCollider && candidate.targetingCollider.OverlapPoint(cursor))) continue;
                float distance = inside ? 0 : (shape.ClosestPoint(cursor) - cursor).sqrMagnitude;
                var sprite = candidate.GetComponent<SpriteRenderer>();
                int layer = sprite ? SortingLayer.GetLayerValueFromID(sprite.sortingLayerID) : 0;
                int order = sprite ? sprite.sortingOrder : 0;
                float depth = Camera.main ? Camera.main.WorldToViewportPoint(candidate.transform.position).z : candidate.transform.position.z;
                bool front = layer > bestLayer || (layer == bestLayer && (order > bestOrder || (order == bestOrder && depth < bestDepth)));
                if (distance < bestDistance - 0.00001f || (Mathf.Abs(distance - bestDistance) < 0.00001f && front))
                { best = candidate; bestDistance = distance; bestLayer = layer; bestOrder = order; bestDepth = depth; }
            }
            return best;
        }
        public void BeginPress(Vector2 cursor)
        {
            if (player.ControlsLocked) return;
            if (CapturePending) { CancelCapture(); return; }
            if (state == RodState.Held)
            {
                if (hooked.GetComponent<Shuriken>()) ThrowHeld(cursor - player.Body.position);
                else BeginSlam();
            }
            else if (state == RodState.Idle) BeginAttack(cursor);
        }
        public void BeginCapture(Vector2 cursor)
        {
            if (player.ControlsLocked) return;
            if (state == RodState.Held) { ThrowHeld(cursor - player.Body.position); return; }
            if (state == RodState.Catching) { state = RodState.Pulling; return; }
            if (state != RodState.Idle) return;
            target = SelectCatchTarget(cursor);
            if (!target) return;
            holdOffset = new Vector2((target.Body.position.x >= player.Body.position.x ? 1 : -1) * HoldDistance, HoldHeight);
            castStarted = Time.time;
            state = RodState.Extending;
            if (target.GetComponent<Shuriken>())
            {
                ClaimBody(target); CompleteCapture(); hooked.Body.position = HoldPosition;
            }
        }
        public float AttackTimeForDistance(float distance)
        {
            float minRange = UsesSheet ? statDefinition.Values.minAttackRange : minAttackRange;
            float maxRange = UsesSheet ? statDefinition.Values.maxAttackRange : maxAttackRange;
            float minTime = UsesSheet ? statDefinition.Values.minAttackTime : minAttackTime;
            float maxTime = UsesSheet ? statDefinition.Values.maxAttackTime : maxAttackTime;
            return Mathf.Max(0.02f, Mathf.Lerp(minTime, maxTime, Mathf.InverseLerp(minRange, maxRange, distance)));
        }
        private void BeginAttack(Vector2 cursor)
        {
            attackStart = RodAnchor;
            Vector2 delta = cursor - attackStart;
            Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right * rodFacing;
            float minRange = UsesSheet ? statDefinition.Values.minAttackRange : minAttackRange;
            float maxRange = UsesSheet ? statDefinition.Values.maxAttackRange : maxAttackRange;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Max(0.01f, minRange), Mathf.Max(minRange, maxRange));
            attackEnd = attackStart + direction * distance;
            attackTotal = AttackTimeForDistance(distance); attackStarted = castStarted = Time.time;
            float windup = Mathf.Min(attackWindup, (UsesSheet ? statDefinition.Values.minAttackTime : minAttackTime) * 0.5f);
            hitAt = attackStarted + windup + (attackTotal - windup) * 0.55f;
            attackUntil = attackStarted + attackTotal; attackApplied = false;
            castSide *= -1f; state = RodState.Attacking;
        }
        private void TickAttack()
        {
            float windup = Mathf.Min(attackWindup, (UsesSheet ? statDefinition.Values.minAttackTime : minAttackTime) * 0.5f);
            if (Time.time < hitAt)
            {
                float t = Mathf.Clamp01((Time.time - attackStarted - windup) / Mathf.Max(0.01f, hitAt - attackStarted - windup));
                Vector2 delta = attackEnd - attackStart;
                float angle = Mathf.Lerp(-95f * castSide, 0, Mathf.SmoothStep(0, 1, t)) * Mathf.Deg2Rad;
                Vector2 d = delta.normalized;
                tipPosition = attackStart + new Vector2(d.x * Mathf.Cos(angle) - d.y * Mathf.Sin(angle), d.x * Mathf.Sin(angle) + d.y * Mathf.Cos(angle)) * Mathf.Lerp(0.6f, delta.magnitude, t);
            }
            else
            {
                if (!attackApplied)
                {
                    attackApplied = true;
                    Vector2 delta = attackEnd - attackStart;
                    var hits = new HashSet<HookableBody>();
                    foreach (var col in Physics2D.OverlapCapsuleAll((attackStart + attackEnd) * 0.5f,
                        new Vector2(delta.magnitude + AttackWidth, AttackWidth), CapsuleDirection2D.Horizontal, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg))
                    {
                        if (col.isTrigger) continue;
                        var body = col.GetComponentInParent<HookableBody>();
                        if (Alive(body) && body.kind == HookableBody.BodyKind.Enemy && hits.Add(body))
                        { body.ReceiveDamage(Attack); body.GetComponent<TangEnemyAI>()?.Stun(); }
                    }
                }
                tipPosition = Vector2.Lerp(attackEnd, RodAnchor + aim * idleLength, Mathf.InverseLerp(hitAt, attackUntil, Time.time));
            }
            if (Time.time >= attackUntil) state = RodState.Idle;
        }
        private void TickCapture()
        {
            if (!Alive(target) || !OnScreen(target)) { CancelCapture(); return; }
            if (state == RodState.Extending)
            {
                UpdateCastTip(target.Body.position);
                if (Vector2.Distance(tipPosition, target.Body.position) > 0.02f) return;
                ClaimBody(target); state = RodState.Catching; connectedAt = Time.time;
            }
            if (state == RodState.Catching)
            {
                if (Time.time - connectedAt >= CatchTimeout) CancelCapture();
            }
            else if (state == RodState.Pulling)
            {
                Vector2 next = Vector2.MoveTowards(target.Body.position, HoldPosition, Mathf.Max(0.1f, pullSpeed) * Time.fixedDeltaTime);
                target.Body.MovePosition(next);
                if (Vector2.Distance(next, HoldPosition) < 0.02f) CompleteCapture();
            }
        }
        private static bool OnScreen(HookableBody body)
        {
            if (!Camera.main) return true;
            var p = Camera.main.WorldToViewportPoint(body.Body.position);
            return p.z > 0 && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1;
        }
        private void ClaimBody(HookableBody body)
        {
            savedBodyType = body.Body.bodyType; savedGravity = body.Body.gravityScale; bodyClaimed = true;
            body.SetHooked(true); body.Body.bodyType = RigidbodyType2D.Kinematic;
            body.Body.linearVelocity = Vector2.zero; body.Body.angularVelocity = 0;
        }
        private void CompleteCapture()
        {
            hooked = target; state = RodState.Held;
            if (captureAudio) captureAudio.Play();
        }
        private void CancelCapture()
        {
            if (bodyClaimed && target && target.Body)
            {
                target.Body.bodyType = savedBodyType; target.Body.gravityScale = savedGravity;
                target.SetHooked(false); target.ClearThrow();
            }
            bodyClaimed = false; hooked = target = slamTarget = null; state = RodState.Idle;
        }
        public void OnPhysicalHit() { if (CapturePending) CancelCapture(); }
        public void BeginSlam()
        {
            if (state != RodState.Held || !Alive(hooked) || !player.Grounded || hooked.GetComponent<Shuriken>()) return;
            float range = SlamRange;
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
            float elapsed = Time.time - slamStarted;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, slamDuration));
            Vector2 origin = player.transform.position;
            Vector2 destination = slamEnd - origin;
            float radius = SlamRange;
            Vector2 desired;
            if (t < 0.25f) desired = Vector2.Lerp(slamStart, origin + Vector2.up * radius, t * 4f);
            else
            {
                float progress = (t - 0.25f) / 0.75f;
                float angle = Mathf.Lerp(90f, Mathf.Atan2(destination.y, destination.x) * Mathf.Rad2Deg, progress) * Mathf.Deg2Rad;
                float length = Mathf.Lerp(radius, destination.magnitude, progress);
                desired = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
            }
            MoveHeld(desired);
            if (t < 0.5f) return;
            var shape = hooked.GetComponent<Collider2D>();
            foreach (var col in Physics2D.OverlapBoxAll(desired, shape.bounds.size, 0))
            {
                if (col.isTrigger) continue;
                var victim = col.GetComponentInParent<HookableBody>();
                if (victim && victim != hooked && Alive(victim) && victim.kind == HookableBody.BodyKind.Enemy)
                { FinishSlam(victim); return; }
            }
            // 부채꼴의 착지 시점에 바닥 타격을 확정해 벽·천장 접촉과 구분합니다.
            if (t >= 1f) FinishSlam(null);
        }
        private void FinishSlam(HookableBody victim)
        {
            if (state != RodState.Slamming || !Alive(hooked)) return;
            state = RodState.Held;
            var held = hooked;
            if (Alive(victim)) victim.ReceiveDamage(slamAttack * 1.1f);
            if (held.kind == HookableBody.BodyKind.Enemy) held.ReceiveDamage(slamAttack * 1.2f);
            if (!Alive(held)) { CancelCapture(); return; }
            if (held.kind == HookableBody.BodyKind.Prop && held.breakOnSlam) { CancelCapture(); Destroy(held.gameObject); return; }
            hooked.Body.position = HoldPosition; slamTarget = null;
        }
        private HookableBody SelectThrowTarget(Vector2 direction)
        {
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right * rodFacing;
            HookableBody best = null; float bestAngle = float.PositiveInfinity, bestDistance = float.PositiveInfinity;
            foreach (var candidate in FindObjectsByType<HookableBody>(FindObjectsSortMode.None))
            {
                if (candidate == hooked || !Alive(candidate) || candidate.kind != HookableBody.BodyKind.Enemy) continue;
                Vector2 offset = (Vector2)candidate.transform.position - (Vector2)player.transform.position;
                float distance = offset.magnitude;
                if (distance < 0.001f || distance > (UsesSheet ? statDefinition.Values.throwRange : throwRange)) continue;
                float angle = Vector2.Angle(direction, offset);
                if (angle > (UsesSheet ? statDefinition.Values.throwAngle : throwAngle) * 0.5f + 0.001f) continue;
                if (angle < bestAngle - 0.001f || (Mathf.Abs(angle - bestAngle) <= 0.001f && distance < bestDistance))
                { best = candidate; bestAngle = angle; bestDistance = distance; }
            }
            return best;
        }
        public Vector2 SelectThrowDirection(Vector2 direction)
        {
            var best = SelectThrowTarget(direction);
            Vector2 origin = hooked ? hooked.Body.position : player.Body.position;
            return best ? (best.Body.position - origin).normalized : direction.normalized;
        }
        public void ThrowHeld(Vector2 direction)
        {
            if (state != RodState.Held || !Alive(hooked)) return;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right * rodFacing;
            var best = SelectThrowTarget(direction);
            var released = hooked; var star = released.GetComponent<Shuriken>();
            Vector2 velocity;
            if (star) velocity = SelectThrowDirection(direction) * star.flySpeed * 2f;
            else if (best)
            {
                Vector2 offset = best.Body.position - released.Body.position;
                float gravity = Mathf.Abs(Physics2D.gravity.y * savedGravity);
                float time = Mathf.Max(0.1f, offset.magnitude / Mathf.Max(0.1f, throwSpeed));
                // 가로 속도를 유지하되 위쪽 목표는 낮은 포물선의 정점 높이를 확보합니다.
                if (gravity > 0.01f)
                {
                    float apex = Mathf.Max(0, offset.y) + throwArcHeight;
                    time = Mathf.Sqrt(2 * apex / gravity) + Mathf.Sqrt(2 * Mathf.Max(0, apex - offset.y) / gravity);
                }
                velocity = new Vector2(offset.x / time, offset.y / time + gravity * time * 0.5f);
            }
            else velocity = direction.normalized * throwSpeed;
            released.Body.bodyType = savedBodyType; released.Body.gravityScale = savedGravity;
            released.SetHooked(false);
            released.BeginThrow(thrownSelfDamage, thrownHitDamage);
            released.Body.linearVelocity = velocity;
            if (star) star.OnThrown();
            tipPosition = released.Body.position;
            bodyClaimed = false; hooked = target = null; state = RodState.Throwing;
            attackUntil = Time.time + throwRecoveryTime;
        }
        public void Interrupt()
        {
            CancelCapture(); attackUntil = float.NegativeInfinity;
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
        private void UpdateCastTip(Vector2 destination)
        {
            Vector2 offset = destination - RodAnchor;
            float duration = Mathf.Max(0.42f, offset.magnitude / Mathf.Max(0.1f, lineSpeed));
            float progress = Mathf.Clamp01((Time.time - castStarted) / duration);
            float travel = Mathf.Pow(progress, 1.72f);
            float angle = Mathf.Lerp(-95f * castSide, 0f, Mathf.SmoothStep(0, 1, travel)) * Mathf.Deg2Rad;
            Vector2 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : aim;
            Vector2 rotated = new Vector2(direction.x * Mathf.Cos(angle) - direction.y * Mathf.Sin(angle), direction.x * Mathf.Sin(angle) + direction.y * Mathf.Cos(angle));
            float reach = Mathf.Lerp(Mathf.Min(0.85f, offset.magnitude), offset.magnitude, Mathf.Sin(progress * Mathf.PI * 0.5f));
            tipPosition = RodAnchor + rotated * reach;
        }
        private void DrawLine()
        {
            if (!line || !tipBody || !rodVisual) return;
            Vector2 end = bodyClaimed && target ? (Vector2)target.transform.position : tipPosition;
            Vector2 start = RodAnchor;
            float distance = Vector2.Distance(start, end);
            float slack = 0.1f;
            Vector2 control = (start + end) * 0.5f + Vector2.down * (0.08f + slack * 0.35f);
            if (!bodyClaimed && Time.time - castStarted < 0.42f)
            {
                Vector2 direction = (end - start).normalized;
                Vector2 normal = new Vector2(-direction.y, direction.x) * -castSide;
                float progress = Mathf.Clamp01((Time.time - castStarted) / 0.42f);
                control = Vector2.Lerp(start, end, 0.46f) + normal * distance * Mathf.Lerp(0.72f, 0.06f, Mathf.SmoothStep(0, 1, progress));
            }
            line.positionCount = 24;
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (line.positionCount - 1f), u = 1f - t;
                line.SetPosition(i, u * u * start + 2f * u * t * control + t * t * end);
            }

        }
    }
}
