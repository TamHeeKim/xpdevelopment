using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine;

namespace FishingPrototype
{
    public sealed class FishingRodController : MonoBehaviour
    {
        private enum RodState { Idle, Whipping, Returning, Hooked }

        [UnityEngine.Serialization.FormerlySerializedAs("Player")]

        [Header("연결")]
        public PlayerController player;
        [UnityEngine.Serialization.FormerlySerializedAs("RodVisual")]
        public Transform rodVisual;

        [Header("기본 조정")]
        [UnityEngine.Serialization.FormerlySerializedAs("IdleLength")]
        public float idleLength = 1.65f;
        [UnityEngine.Serialization.FormerlySerializedAs("MaxWhipLength")]
        public float maxWhipLength = 7.5f;
        [UnityEngine.Serialization.FormerlySerializedAs("SwingForce")]
        public float swingForce = 27f;
        [UnityEngine.Serialization.FormerlySerializedAs("ReelForce")]
        public float reelForce = 42f;

        [Header("대상 보정")]
        [UnityEngine.Serialization.FormerlySerializedAs("AssistRadius")]
        public float assistRadius = 7f;
        [UnityEngine.Serialization.FormerlySerializedAs("ThrowAssistAngle")]
        public float throwAssistAngle = 55f;
        [UnityEngine.Serialization.FormerlySerializedAs("SlamGestureSpeed")]
        public float slamGestureSpeed = 0.8f;
        [UnityEngine.Serialization.FormerlySerializedAs("SlamAssistDuration")]
        public float slamAssistDuration = 0.35f;
        private HookableBody slamTarget;
        private float slamUntil, nextSlam;

        public bool ControlsLocked { get; set; }

        private HookableBody charging;
        private float charge;
        private LineRenderer outline;
        public float CaptureProgress => charging ? Mathf.Clamp01(charge / 0.8f) : 0f;
        public string HeldName => hooked ? hooked.DisplayName : "";
        public string EquipmentName => CombatText.Get("equipment.bobber");
        [UnityEngine.Serialization.FormerlySerializedAs("CaptureHoldTime")]
        [Header("포획")]
        public float captureHoldTime = 0.18f;
        private float heldSince = float.PositiveInfinity;
        private float bufferedAttackUntil = float.NegativeInfinity;
        private HookableBody captureCandidate;
        private float candidateUntil;
        private RodState state;
        private Rigidbody2D tipBody;
        private CircleCollider2D tipCollider;
        private LineRenderer line;
        private LineRenderer strikeLine;
        private HookableBody hooked;
        private Vector2 aim = Vector2.right;
        private float rodFacing = 1f;
        private float ropeLength = 4f;
        private float whipStarted;
        private float whipProgress;
        private float whipSide = 1f;
        private Vector2 whipAim = Vector2.right;
        private Vector2 tipVelocity;
        private readonly HashSet<int> hitThisWhip = new HashSet<int>();
        private const int linePoints = 24;
        private const int attackSamples = linePoints;
        private const float attackStart = 0f;
        private readonly Vector2[] previousAttackPoints = new Vector2[attackSamples];
        private readonly Vector2[] currentAttackPoints = new Vector2[attackSamples];

        private void Start()
        {
            CreateTip();
            CreateLine();
            var marker = new GameObject("Line_AimOutline"); marker.transform.SetParent(transform);
            outline = marker.AddComponent<LineRenderer>(); outline.material = line.material;
            outline.positionCount = 5; outline.startWidth = outline.endWidth = 0.04f;
            outline.startColor = outline.endColor = Color.cyan;
        }

        private void Update()
        {
            UpdateRodVisual();
            UpdateAim();
            var cursor = Camera.main.ScreenToWorldPoint(Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero);
            var hover = Physics2D.OverlapPoint(cursor);
            var hovered = hover ? hover.GetComponent<HookableBody>() : null;
            outline.enabled = hovered && hovered.kind == HookableBody.BodyKind.Enemy;
            if (outline.enabled)
            {
                var b = hover.bounds; float pad = 0.08f;
                outline.SetPosition(0, new Vector3(b.min.x-pad,b.min.y-pad)); outline.SetPosition(1,new Vector3(b.min.x-pad,b.max.y+pad));
                outline.SetPosition(2,new Vector3(b.max.x+pad,b.max.y+pad)); outline.SetPosition(3,new Vector3(b.max.x+pad,b.min.y-pad)); outline.SetPosition(4,outline.GetPosition(0));
            }
            if (charging)
            {
                if (!(Mouse.current?.leftButton.isPressed ?? false) || !charging.Body.simulated || Vector2.Distance(RodAnchor, charging.Body.position) > maxWhipLength) { charging = null; charge = 0; state = RodState.Returning; tipBody.bodyType = RigidbodyType2D.Dynamic; }
                else { charge += Time.deltaTime; if (charge >= 0.8f) { var target = charging; charging = null; Hook(target); } }
            }

            if (ControlsLocked)
            {
                bufferedAttackUntil = float.NegativeInfinity;
                captureCandidate = null;
                heldSince = float.PositiveInfinity;
                DrawLine();
                return;
            }

            if ((Mouse.current?.leftButton.wasPressedThisFrame ?? false))
            {
                heldSince = Time.time;
                if (state == RodState.Idle && !charging) StartWhip();
                else if ((state == RodState.Whipping || state == RodState.Returning) && !charging)
                    bufferedAttackUntil = Time.time + 0.25f;
            }

            if (Mouse.current?.leftButton.wasReleasedThisFrame ?? false)
            {
                heldSince = float.PositiveInfinity;
                captureCandidate = null;
            }
            if ((Mouse.current?.leftButton.wasReleasedThisFrame ?? false) && state == RodState.Hooked)
            {
                ReleaseHook(true);
                state = RodState.Returning;
            }

            if (state == RodState.Hooked && hooked && hooked.Body.simulated &&
                !(Mouse.current?.rightButton.isPressed ?? false) && Time.time >= nextSlam && Time.deltaTime > 0f)
            {
                Vector2 movement = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
                if (-movement.y / Mathf.Max(1f, Screen.height) / Time.deltaTime >= slamGestureSpeed &&
                    -movement.y > Mathf.Abs(movement.x))
                {
                    slamTarget = FindAssistTarget(true);
                    if (slamTarget)
                    {
                        slamUntil = Time.time + slamAssistDuration;
                        nextSlam = Time.time + 0.65f;
                    }
                }
            }
            DrawLine();
        }

        private void FixedUpdate()
        {
            if (ControlsLocked) return;
            if (state == RodState.Idle && Time.time <= bufferedAttackUntil) StartWhip();
            if (captureCandidate && (state == RodState.Whipping || state == RodState.Returning) && !charging)
            {
                if (!captureCandidate.Body.simulated || Time.time > candidateUntil ||
                    Vector2.Distance(RodAnchor, captureCandidate.Body.position) > maxWhipLength ||
                    !HasClearPath(RodAnchor, captureCandidate.Body.position)) captureCandidate = null;
                else if ((Mouse.current?.leftButton.isPressed ?? false) && Time.time - heldSince >= captureHoldTime)
                {
                    var target = captureCandidate; captureCandidate = null;
                    bufferedAttackUntil = float.NegativeInfinity;
                    if (target.kind == HookableBody.BodyKind.Enemy && target.Body.mass >= 3f) { charging = target; charge = 0; }
                    else Hook(target);
                }
            }
            if (charging) { tipBody.position = charging.Body.position; return; }
            switch (state)
            {
                case RodState.Idle: UpdateIdle(); break;
                case RodState.Whipping: UpdateWhip(); break;
                case RodState.Returning: UpdateReturn(); break;
                case RodState.Hooked: UpdateHooked(); break;
            }
        }

        private void UpdateAim()
        {
            Camera cam = Camera.main;
            if (!cam) return;
            Vector3 mouse = cam.ScreenToWorldPoint((Vector3)(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero));
            Vector2 delta = (Vector2)mouse - RodAnchor;
            if (delta.sqrMagnitude > 0.02f) aim = delta.normalized;
        }

        private void UpdateRodVisual()
        {
            if (!rodVisual) return;
            if (Mathf.Abs(player.Body.linearVelocity.x) > 0.2f) rodFacing = Mathf.Sign(player.Body.linearVelocity.x);
            rodVisual.localPosition = new Vector3(0.52f * rodFacing, 0.28f, 0f);
            rodVisual.localRotation = Quaternion.Euler(0f, 0f, 35f * rodFacing);
            Vector3 scale = rodVisual.localScale;
            scale.x = Mathf.Abs(scale.x) * rodFacing;
            scale.y = Mathf.Abs(scale.y);
            rodVisual.localScale = scale;
        }

        private Vector2 RodAnchor => rodVisual ? (Vector2)rodVisual.TransformPoint(Vector3.right * 0.5f) : (Vector2)player.transform.position;

        private void CreateTip()
        {
            GameObject tip = PrototypeBootstrap.CreateVisual("Sprite_LineEnd", RodAnchor + aim * idleLength,
                Vector2.one * 0.18f, new Color(0.95f, 0.85f, 0.38f));
            tip.transform.SetParent(transform);
            tip.GetComponent<SpriteRenderer>().sortingOrder = 5;
            tipBody = tip.AddComponent<Rigidbody2D>();
            tipBody.bodyType = RigidbodyType2D.Kinematic;
            tipBody.gravityScale = 0.25f;
            tipBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            tipCollider = tip.AddComponent<CircleCollider2D>();
            tipCollider.isTrigger = true;
            tipCollider.radius = 1.1f;
            TipContact relay = tip.AddComponent<TipContact>();
            relay.owner = this;
            Physics2D.IgnoreCollision(tipCollider, player.GetComponent<Collider2D>());

        }

        private void CreateLine()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = linePoints;
            line.startWidth = 0.055f;
            line.endWidth = 0.035f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(0.82f, 0.91f, 0.96f);
            line.endColor = new Color(0.48f, 0.7f, 0.78f);
            line.sortingOrder = 2;

            GameObject strikeObject = new GameObject("Line_StrikeZone");
            strikeObject.transform.SetParent(transform, false);
            strikeLine = strikeObject.AddComponent<LineRenderer>();
            strikeLine.useWorldSpace = true;
            strikeLine.positionCount = attackSamples;
            strikeLine.startWidth = 0.045f;
            strikeLine.endWidth = 0.095f;
            strikeLine.material = line.material;
            strikeLine.startColor = new Color(0.74f, 0.88f, 0.9f, 0.7f);
            strikeLine.endColor = new Color(1f, 0.8f, 0.28f, 1f);
            strikeLine.sortingOrder = 3;
            strikeLine.enabled = false;
        }

        private void StartWhip()
        {
            // Repeated presses may queue an attack, but never rewind an active swing.
            if (state != RodState.Idle || charging) return;
            bufferedAttackUntil = float.NegativeInfinity;
            captureCandidate = null;
            state = RodState.Whipping;
            whipStarted = Time.time;
            whipProgress = 0f;
            whipAim = aim;
            whipSide *= -1f;
            hitThisWhip.Clear();
            tipBody.bodyType = RigidbodyType2D.Kinematic;
            tipBody.position = RodAnchor + Rotate(whipAim, -95f * whipSide) * 0.85f;
            tipVelocity = Vector2.zero;
            tipBody.angularVelocity = 0f;
            BuildAttackPoints(tipBody.position, previousAttackPoints);
        }

        private void UpdateIdle()
        {
            tipBody.bodyType = RigidbodyType2D.Kinematic;
            tipBody.MovePosition(Vector2.Lerp(tipBody.position, RodAnchor + aim * idleLength, 0.28f));
        }

        private void UpdateWhip()
        {
            float duration = 0.42f;
            whipProgress = Mathf.Clamp01((Time.time - whipStarted) / duration);

            // Slow wind-up followed by a fast expanding arc creates the whip crack.
            float travel = Mathf.Pow(whipProgress, 1.72f);
            float angle = Mathf.Lerp(-95f * whipSide, 8f * whipSide, Mathf.SmoothStep(0f, 1f, travel));
            float extension = Mathf.Sin(whipProgress * Mathf.PI * 0.5f);
            float reachScale = 1f;
            float reach = Mathf.Lerp(0.85f, maxWhipLength * reachScale, extension);
            Vector2 previous = tipBody.position;
            Vector2 next = RodAnchor + Rotate(whipAim, angle) * reach;
            tipVelocity = (next - previous) / Mathf.Max(Time.fixedDeltaTime, 0.001f);
            tipBody.MovePosition(next);

            BuildAttackPoints(next, currentAttackPoints);
            if (whipProgress > 0.3f)
                SweepWhipCurve();
            CopyAttackPoints(currentAttackPoints, previousAttackPoints);

            if (whipProgress >= 1f && state == RodState.Whipping)
            {
                state = RodState.Returning;
                tipBody.bodyType = RigidbodyType2D.Dynamic;
                tipBody.linearVelocity = tipVelocity * 0.22f;
            }
        }

        private void UpdateReturn()
        {
            // 휘두르기는 끝까지 실행하고, 다음 공격은 회수를 기다리지 않는다.
            if (Time.time <= bufferedAttackUntil)
            {
                state = RodState.Idle;
                StartWhip();
                return;
            }
            Vector2 delta = RodAnchor + aim * 0.55f - tipBody.position;
            tipBody.AddForce(delta * 48f * tipBody.mass);
            tipBody.linearVelocity *= 0.9f;
            if (delta.magnitude < 0.65f)
            {
                tipBody.linearVelocity = Vector2.zero;
                state = RodState.Idle;
            }
        }

        private void UpdateHooked()
        {
            if (!hooked || !hooked.Body.simulated)
            {
                ReleaseHook();
                state = RodState.Returning;
                tipBody.bodyType = RigidbodyType2D.Dynamic;
                return;
            }

            if ((Mouse.current?.rightButton.isPressed ?? false))
                ropeLength = Mathf.Max(1.25f, ropeLength - 3.8f * Time.fixedDeltaTime);

            if (!(Mouse.current?.rightButton.isPressed ?? false)) ropeLength = Mathf.Min(maxWhipLength, ropeLength + 3.8f * Time.fixedDeltaTime);
            Rigidbody2D targetBody = hooked.Body;
            Vector2 anchor = RodAnchor;
            Vector2 desired = anchor + aim * ropeLength;
            if ((Mouse.current?.rightButton.isPressed ?? false)) slamTarget = null;
            if (slamTarget && slamTarget.Body.simulated && Time.time < slamUntil &&
                Vector2.Distance(anchor, slamTarget.Body.position) <= Mathf.Min(assistRadius, ropeLength) &&
                HasClearPath(targetBody.position, slamTarget.Body.position))
            {
                // Aim at the upper surface; the existing physical collision deals damage to both bodies.
                Bounds bounds = slamTarget.GetComponent<Collider2D>().bounds;
                desired = new Vector2(bounds.center.x, bounds.max.y - 0.15f);
            }
            else slamTarget = null;
            Vector2 towardDesired = desired - targetBody.position;
            targetBody.AddForce(towardDesired * swingForce * targetBody.mass);

            Vector2 fromAnchor = targetBody.position - anchor;
            if (fromAnchor.magnitude > ropeLength)
                targetBody.AddForce(-fromAnchor.normalized * (fromAnchor.magnitude - ropeLength) * 95f * targetBody.mass);

            if ((Mouse.current?.rightButton.isPressed ?? false))
                targetBody.AddForce((anchor - targetBody.position).normalized * reelForce * targetBody.mass);

            player.Body.AddForce(-towardDesired * 0.08f);
            tipBody.position = targetBody.position;
        }

        public void HandleTipContact(Collider2D other)
        {
            if (state != RodState.Whipping) return;
            HookableBody target = other.GetComponent<HookableBody>();
            if (target) HitTarget(target, 1f);

        }

        private void SweepWhipCurve()
        {
            float radius = 0.2f;
            for (int i = 0; i < attackSamples; i++)
            {
                float ropeT = Mathf.Lerp(attackStart, 1f, i / (attackSamples - 1f));
                SweepSegment(previousAttackPoints[i], currentAttackPoints[i], radius, ropeT);
                if (state == RodState.Hooked) break;

                if (i < attackSamples - 1)
                {
                    float segmentT = Mathf.Lerp(attackStart, 1f, (i + 0.5f) / (attackSamples - 1f));
                    SweepSegment(currentAttackPoints[i], currentAttackPoints[i + 1], radius, segmentT);
                    if (state == RodState.Hooked) break;
                }
            }
        }

        private void SweepSegment(Vector2 start, Vector2 end, float radius, float ropeT)
        {
            Vector2 delta = end - start;
            if (delta.sqrMagnitude < 0.0001f)
            {
                Collider2D[] overlaps = Physics2D.OverlapCircleAll(end, radius);
                foreach (Collider2D overlap in overlaps)
                {
                    HookableBody body = overlap.GetComponent<HookableBody>();
                    if (body) HitTarget(body, ropeT);
                }
                return;
            }

            RaycastHit2D[] hits = Physics2D.CircleCastAll(start, radius, delta.normalized, delta.magnitude);
            foreach (RaycastHit2D hit in hits)
            {
                HookableBody body = hit.collider.GetComponent<HookableBody>();
                if (body) HitTarget(body, ropeT);
                if (state == RodState.Hooked) break;
            }
        }

        private void HitTarget(HookableBody target, float ropeT)
        {
            if (!target || !target.Body.simulated || state != RodState.Whipping || charging) return;

            if (hitThisWhip.Add(target.GetInstanceID()))
            {
                float speed = tipVelocity.magnitude;
                float tipStrength = Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(attackStart, 1f, ropeT));
                float catchDamage = 1f;
                float maxDamage = 34f;
                float damage = Mathf.Clamp((speed - 4f) * 1.15f * tipStrength * catchDamage, 4f, maxDamage);
                target.ReceiveDamage(damage, tipBody.position);
                CameraShake.Pulse(Mathf.Clamp01(damage / 55f));
                Vector2 hitDirection = tipVelocity.sqrMagnitude > 0.1f ? tipVelocity.normalized : whipAim;
                float catchKnockback = 0.22f;
                target.Body.AddForce(hitDirection * damage * tipStrength * catchKnockback, ForceMode2D.Impulse);
            }

            // Any actual strike-zone contact can lead into capture during a continuous hold.
            if (target.Body.simulated && (Mouse.current?.leftButton.isPressed ?? false) && !captureCandidate)
            {
                captureCandidate = target;
                candidateUntil = Time.time + 0.3f;
            }
        }

        private void Hook(HookableBody target)
        {
            hooked = target;
            hooked.SetHooked(true);
            CameraShake.Pulse(0.15f);
            var sound = AudioClip.Create("Audio_Capture", 2205, 1, 22050, false);
            var samples = new float[2205];
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * 0.18f) * 0.1f * (1f - i / 2205f);
            sound.SetData(samples, 0); AudioSource.PlayClipAtPoint(sound, Camera.main.transform.position); Destroy(sound, 1f);
            ropeLength = Mathf.Clamp(Vector2.Distance(RodAnchor, target.Body.position), 1.5f, maxWhipLength);
            target.Body.linearVelocity += tipVelocity * 0.24f;
            state = RodState.Hooked;
            tipBody.linearVelocity = Vector2.zero;
            tipBody.bodyType = RigidbodyType2D.Kinematic;
            tipBody.position = target.Body.position;
        }

        private void ReleaseHook(bool assist = false)
        {
            if (hooked)
            {
                if (assist && hooked.Body.simulated)
                {
                    HookableBody target = FindAssistTarget(false);
                    if (target)
                        hooked.Body.linearVelocity = CombatAimAssist.RedirectThrow(hooked.Body.linearVelocity,
                            target.Body.position - hooked.Body.position, throwAssistAngle);
                }
                tipBody.position = hooked.Body.position;
                tipBody.linearVelocity = hooked.Body.linearVelocity * 0.35f;
                hooked.SetHooked(false);
            }
            hooked = null;
            slamTarget = null;
            tipBody.bodyType = RigidbodyType2D.Dynamic;
        }

        private HookableBody FindAssistTarget(bool forSlam)
        {
            if (!hooked) return null;
            HookableBody best = null;
            float bestScore = float.PositiveInfinity;
            Vector2 origin = hooked.Body.position;
            Vector2 velocity = hooked.Body.linearVelocity;
            foreach (var col in Physics2D.OverlapCircleAll(player.Body.position, assistRadius))
            {
                HookableBody candidate = col.GetComponent<HookableBody>();
                if (!candidate || candidate == hooked || candidate.kind != HookableBody.BodyKind.Enemy ||
                    !candidate.Body.simulated || candidate.IsHooked) continue;
                Vector2 offset = candidate.Body.position - origin;
                if (forSlam)
                {
                    if (offset.y >= -0.25f || Vector2.Distance(RodAnchor, candidate.Body.position) > ropeLength) continue;
                }
                else if (!CombatAimAssist.CanRedirect(velocity, offset, throwAssistAngle)) continue;
                if (!HasClearPath(origin, candidate.Body.position)) continue;
                float score = forSlam ? offset.sqrMagnitude : Vector2.Angle(velocity, offset);
                if (score < bestScore || (score == bestScore && best && candidate.GetInstanceID() < best.GetInstanceID()))
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        private bool HasClearPath(Vector2 from, Vector2 to)
        {
            foreach (var hit in Physics2D.LinecastAll(from, to))
                if (!hit.collider.isTrigger && hit.rigidbody != player.Body &&
                    !hit.collider.GetComponent<HookableBody>()) return false;
            return true;
        }

        public void Interrupt()
        {
            captureCandidate = null;
            bufferedAttackUntil = float.NegativeInfinity;
            heldSince = float.PositiveInfinity;
            if (charging) { charging = null; charge = 0; state = RodState.Returning; tipBody.bodyType = RigidbodyType2D.Dynamic; }
            if (state == RodState.Hooked) { ReleaseHook(); state = RodState.Returning; }
        }
        private void DrawLine()
        {
            if (!line || !tipBody) return;
            Vector2 start = RodAnchor;
            Vector2 end = state == RodState.Hooked && hooked ? hooked.Body.position :
                tipBody.position;
            float distance = Vector2.Distance(start, end);
            float slack = state == RodState.Hooked ? Mathf.Max(0f, ropeLength - distance) : 0.1f;
            float sag = 0.08f + slack * 0.35f;
            Vector2 control = Vector2.Lerp(start, end, 0.5f) + Vector2.down * sag;
            if (state == RodState.Whipping)
                control = GetWhipControl(start, end);
            for (int i = 0; i < linePoints; i++)
            {
                float t = i / (linePoints - 1f);
                Vector2 p = QuadraticBezier(start, control, end, t);
                line.SetPosition(i, p);
            }

            strikeLine.enabled = state == RodState.Whipping && whipProgress > 0.24f;
            if (strikeLine.enabled)
            {
                for (int i = 0; i < attackSamples; i++)
                {
                    float t = Mathf.Lerp(attackStart, 1f, i / (attackSamples - 1f));
                    strikeLine.SetPosition(i, QuadraticBezier(start, control, end, t));
                }
            }
        }

        private void BuildAttackPoints(Vector2 end, Vector2[] buffer)
        {
            Vector2 start = RodAnchor;
            Vector2 control = GetWhipControl(start, end);
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = Mathf.Lerp(attackStart, 1f, i / (buffer.Length - 1f));
                buffer[i] = QuadraticBezier(start, control, end, t);
            }
        }

        private Vector2 GetWhipControl(Vector2 start, Vector2 end)
        {
            float distance = Vector2.Distance(start, end);
            Vector2 normal = new Vector2(-whipAim.y, whipAim.x) * -whipSide;
            float curve = distance * Mathf.Lerp(0.72f, 0.06f, Mathf.SmoothStep(0f, 1f, whipProgress));
            return Vector2.Lerp(start, end, 0.46f) + normal * curve;
        }

        private static void CopyAttackPoints(Vector2[] source, Vector2[] destination)
        {
            for (int i = 0; i < source.Length; i++) destination[i] = source[i];
        }

        private static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
        }
    }

    public sealed class TipContact : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("Owner")]
        public FishingRodController owner;
        private void OnTriggerEnter2D(Collider2D other) => owner.HandleTipContact(other);
    }
}
