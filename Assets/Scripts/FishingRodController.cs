using System.Collections.Generic;
using UnityEngine;

namespace FishingPrototype
{
    public sealed class FishingRodController : MonoBehaviour
    {
        private enum RodState { Idle, Whipping, Returning, Hooked, GroundAnchored }
        public enum CatchType { None, Fish, HeavyObject }

        public PlayerController Player;
        public Transform RodVisual;

        [Header("Tuning")]
        public float IdleLength = 1.65f;
        public float MaxWhipLength = 7.5f;
        public float SwingForce = 34f;
        public float ReelForce = 42f;
        public float GroundSwingForce = 48f;

        public CatchType CurrentCatch { get; private set; }
        public bool HasCatch => CurrentCatch != CatchType.None;
        public bool ControlsLocked { get; set; }
        public bool CanStartFishing => state == RodState.Idle;

        private RodState state;
        private Rigidbody2D tipBody;
        private CircleCollider2D tipCollider;
        private GameObject weightVisual;
        private GameObject fishVisual;
        private LineRenderer line;
        private LineRenderer strikeLine;
        private HookableBody hooked;
        private Vector2 aim = Vector2.right;
        private float ropeLength = 4f;
        private float whipStarted;
        private float whipProgress;
        private float whipSide = 1f;
        private Vector2 whipAim = Vector2.right;
        private Vector2 tipVelocity;
        private bool heavyWeight;
        private Vector2 groundAnchor;
        private float groundRopeLength;
        private readonly HashSet<int> hitThisWhip = new HashSet<int>();
        private const int LinePoints = 24;
        private const int AttackSamples = 8;
        private const float AttackStart = 0.58f;
        private readonly Vector2[] previousAttackPoints = new Vector2[AttackSamples];
        private readonly Vector2[] currentAttackPoints = new Vector2[AttackSamples];

        private void Start()
        {
            CreateTip();
            CreateLine();
            AttachCatch(CatchType.None);
        }

        private void Update()
        {
            UpdateAim();
            UpdateRodVisual();

            if (ControlsLocked)
            {
                DrawLine();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (state != RodState.Hooked && state != RodState.GroundAnchored) StartWhip();
            }

            if (Input.GetMouseButtonUp(0) && state == RodState.Hooked)
            {
                ReleaseHook();
                state = RodState.Returning;
            }
            else if (Input.GetMouseButtonUp(0) && state == RodState.GroundAnchored)
            {
                ReleaseGroundAnchor();
            }

            DrawLine();
        }

        private void FixedUpdate()
        {
            switch (state)
            {
                case RodState.Idle: UpdateIdle(); break;
                case RodState.Whipping: UpdateWhip(); break;
                case RodState.Returning: UpdateReturn(); break;
                case RodState.Hooked: UpdateHooked(); break;
                case RodState.GroundAnchored: UpdateGroundAnchor(); break;
            }
        }

        private void UpdateAim()
        {
            Camera cam = Camera.main;
            if (!cam) return;
            Vector3 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 delta = (Vector2)mouse - RodAnchor;
            if (delta.sqrMagnitude > 0.02f) aim = delta.normalized;
        }

        private void UpdateRodVisual()
        {
            if (!RodVisual) return;
            Vector2 direction = aim;
            if (state == RodState.Whipping)
            {
                float turn = Mathf.SmoothStep(-38f * whipSide, 24f * whipSide,
                    Mathf.Clamp01(whipProgress / 0.72f));
                direction = Rotate(whipAim, turn);
            }
            RodVisual.right = direction;
            Vector3 scale = RodVisual.localScale;
            scale.y = Mathf.Abs(scale.y) * (direction.x < 0f ? -1f : 1f);
            RodVisual.localScale = scale;
        }

        private Vector2 RodAnchor => (Vector2)Player.transform.position + new Vector2(0f, 0.28f) + aim * 0.55f;

        private void CreateTip()
        {
            GameObject tip = PrototypeBootstrap.CreateVisual("Line End", RodAnchor + aim * IdleLength,
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
            relay.Owner = this;
            Physics2D.IgnoreCollision(tipCollider, Player.GetComponent<Collider2D>());

            weightVisual = PrototypeBootstrap.CreateVisual("Heavy Sinker", Vector2.zero, Vector2.one * 3.1f,
                new Color(0.16f, 0.19f, 0.22f));
            weightVisual.transform.SetParent(tip.transform, false);
            weightVisual.GetComponent<SpriteRenderer>().sortingOrder = 4;

            fishVisual = PrototypeBootstrap.CreateVisual("Caught Fish", Vector2.zero, new Vector2(3.5f, 1.7f),
                new Color(0.25f, 0.78f, 0.94f));
            fishVisual.transform.SetParent(tip.transform, false);
            fishVisual.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            fishVisual.GetComponent<SpriteRenderer>().sortingOrder = 4;
        }

        private void CreateLine()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = LinePoints;
            line.startWidth = 0.055f;
            line.endWidth = 0.035f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(0.82f, 0.91f, 0.96f);
            line.endColor = new Color(0.48f, 0.7f, 0.78f);
            line.sortingOrder = 2;

            GameObject strikeObject = new GameObject("Whip Strike Zone");
            strikeObject.transform.SetParent(transform, false);
            strikeLine = strikeObject.AddComponent<LineRenderer>();
            strikeLine.useWorldSpace = true;
            strikeLine.positionCount = AttackSamples;
            strikeLine.startWidth = 0.045f;
            strikeLine.endWidth = 0.095f;
            strikeLine.material = line.material;
            strikeLine.startColor = new Color(0.74f, 0.88f, 0.9f, 0.7f);
            strikeLine.endColor = new Color(1f, 0.8f, 0.28f, 1f);
            strikeLine.sortingOrder = 3;
            strikeLine.enabled = false;
        }

        public void AttachCatch(CatchType type)
        {
            if (state == RodState.Hooked) ReleaseHook();
            if (state == RodState.GroundAnchored) ReleaseGroundAnchor();
            CurrentCatch = type;
            heavyWeight = type == CatchType.HeavyObject;
            if (weightVisual) weightVisual.SetActive(type == CatchType.HeavyObject);
            if (fishVisual) fishVisual.SetActive(type == CatchType.Fish);
            if (tipBody) tipBody.mass = type == CatchType.HeavyObject ? 3.8f : type == CatchType.Fish ? 1.3f : 0.45f;
            if (tipCollider) tipCollider.radius = type == CatchType.HeavyObject ? 2.15f : type == CatchType.Fish ? 1.55f : 1.1f;
            if (line) line.startWidth = type == CatchType.HeavyObject ? 0.075f : type == CatchType.Fish ? 0.065f : 0.055f;
        }

        private void StartWhip()
        {
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
            tipBody.MovePosition(Vector2.Lerp(tipBody.position, RodAnchor + aim * IdleLength, 0.28f));
        }

        private void UpdateWhip()
        {
            float duration = heavyWeight ? 0.58f : CurrentCatch == CatchType.Fish ? 0.48f : 0.42f;
            whipProgress = Mathf.Clamp01((Time.time - whipStarted) / duration);

            // Slow wind-up followed by a fast expanding arc creates the whip crack.
            float travel = Mathf.Pow(whipProgress, heavyWeight ? 1.45f : 1.72f);
            float angle = Mathf.Lerp(-95f * whipSide, 8f * whipSide, Mathf.SmoothStep(0f, 1f, travel));
            float extension = Mathf.Sin(whipProgress * Mathf.PI * 0.5f);
            float reachScale = heavyWeight ? 0.9f : CurrentCatch == CatchType.Fish ? 0.95f : 1f;
            float reach = Mathf.Lerp(0.85f, MaxWhipLength * reachScale, extension);
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
            Vector2 delta = RodAnchor + aim * 0.55f - tipBody.position;
            tipBody.AddForce(delta * (heavyWeight ? 34f : 48f) * tipBody.mass);
            tipBody.linearVelocity *= heavyWeight ? 0.94f : 0.9f;
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

            if (Input.GetMouseButton(1))
                ropeLength = Mathf.Max(1.25f, ropeLength - 3.8f * Time.fixedDeltaTime);

            Rigidbody2D targetBody = hooked.Body;
            Vector2 anchor = RodAnchor;
            Vector2 desired = anchor + aim * ropeLength;
            Vector2 towardDesired = desired - targetBody.position;
            targetBody.AddForce(towardDesired * SwingForce * targetBody.mass);

            Vector2 fromAnchor = targetBody.position - anchor;
            if (fromAnchor.magnitude > ropeLength)
                targetBody.AddForce(-fromAnchor.normalized * (fromAnchor.magnitude - ropeLength) * 95f * targetBody.mass);

            if (Input.GetMouseButton(1))
                targetBody.AddForce((anchor - targetBody.position).normalized * ReelForce * targetBody.mass);

            Player.Body.AddForce(-towardDesired * 0.08f);
            tipBody.position = targetBody.position;
        }

        private void UpdateGroundAnchor()
        {
            tipBody.position = groundAnchor;
            Vector2 fromAnchor = Player.Body.position - groundAnchor;
            if (fromAnchor.sqrMagnitude < 0.1f) return;

            Vector2 radial = fromAnchor.normalized;
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            float desiredDirection = Mathf.Abs(aim.x) > 0.2f ? Mathf.Sign(aim.x) : 1f;
            if (Mathf.Sign(tangent.x) != desiredDirection) tangent = -tangent;

            float stretch = Mathf.Max(0f, fromAnchor.magnitude - groundRopeLength);
            Player.Body.AddForce(-radial * (38f + stretch * 95f));
            Player.Body.AddForce(tangent * GroundSwingForce);
        }

        public void HandleTipContact(Collider2D other)
        {
            if (state != RodState.Whipping) return;
            HookableBody target = other.GetComponent<HookableBody>();
            if (target) HitTarget(target, 1f);
            else if (heavyWeight && Input.GetMouseButton(0) && other.GetComponent<GroundSurface>())
                PlantGroundAnchor(other);
        }

        private void SweepWhipCurve()
        {
            float radius = heavyWeight ? 0.42f : 0.2f;
            for (int i = 0; i < AttackSamples; i++)
            {
                float ropeT = Mathf.Lerp(AttackStart, 1f, i / (AttackSamples - 1f));
                SweepSegment(previousAttackPoints[i], currentAttackPoints[i], radius, ropeT);
                if (state == RodState.Hooked) break;

                if (i < AttackSamples - 1)
                {
                    float segmentT = Mathf.Lerp(AttackStart, 1f, (i + 0.5f) / (AttackSamples - 1f));
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
            if (!target) return;

            if (hitThisWhip.Add(target.GetInstanceID()))
            {
                float speed = tipVelocity.magnitude;
                float tipStrength = Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(AttackStart, 1f, ropeT));
                float catchDamage = heavyWeight ? 1.65f : CurrentCatch == CatchType.Fish ? 1.25f : 1f;
                float maxDamage = heavyWeight ? 70f : CurrentCatch == CatchType.Fish ? 46f : 34f;
                float damage = Mathf.Clamp((speed - 4f) * 1.15f * tipStrength * catchDamage, 4f, maxDamage);
                target.ReceiveDamage(damage, tipBody.position);
                CameraShake.Pulse(Mathf.Clamp01(damage / 55f));
                Vector2 hitDirection = tipVelocity.sqrMagnitude > 0.1f ? tipVelocity.normalized : whipAim;
                float catchKnockback = heavyWeight ? 0.55f : CurrentCatch == CatchType.Fish ? 0.32f : 0.22f;
                target.Body.AddForce(hitDirection * damage * tipStrength * catchKnockback, ForceMode2D.Impulse);
            }

            // Attacking is forgiving, but capture still requires the very end of the rope.
            if (state == RodState.Whipping && !heavyWeight && ropeT >= 0.88f &&
                Input.GetMouseButton(0) && whipProgress > 0.42f)
                Hook(target);
        }

        private void Hook(HookableBody target)
        {
            hooked = target;
            hooked.SetHooked(true);
            ropeLength = Mathf.Clamp(Vector2.Distance(RodAnchor, target.Body.position), 1.5f, MaxWhipLength);
            target.Body.linearVelocity += tipVelocity * 0.24f;
            state = RodState.Hooked;
            tipBody.linearVelocity = Vector2.zero;
            tipBody.bodyType = RigidbodyType2D.Kinematic;
            tipBody.position = target.Body.position;
        }

        private void ReleaseHook()
        {
            if (hooked)
            {
                tipBody.position = hooked.Body.position;
                tipBody.linearVelocity = hooked.Body.linearVelocity * 0.35f;
                hooked.SetHooked(false);
            }
            hooked = null;
            tipBody.bodyType = RigidbodyType2D.Dynamic;
        }

        private void PlantGroundAnchor(Collider2D ground)
        {
            groundAnchor = ground.ClosestPoint(tipBody.position);
            groundRopeLength = Mathf.Clamp(Vector2.Distance(Player.Body.position, groundAnchor), 2f, MaxWhipLength);
            state = RodState.GroundAnchored;
            Player.PreserveHorizontalMomentum = true;
            tipBody.bodyType = RigidbodyType2D.Kinematic;
            tipBody.position = groundAnchor;

            Vector2 radial = (Player.Body.position - groundAnchor).normalized;
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            float desiredDirection = Mathf.Abs(aim.x) > 0.2f ? Mathf.Sign(aim.x) : 1f;
            if (Mathf.Sign(tangent.x) != desiredDirection) tangent = -tangent;
            Player.Body.linearVelocity = tangent * 8.5f + Vector2.up * 2.6f;
            CameraShake.Pulse(0.35f);
        }

        private void ReleaseGroundAnchor()
        {
            Player.PreserveHorizontalMomentum = false;
            tipBody.bodyType = RigidbodyType2D.Dynamic;
            tipBody.position = groundAnchor;
            tipBody.linearVelocity = Player.Body.linearVelocity * 0.2f;
            state = RodState.Returning;
        }

        private void DrawLine()
        {
            if (!line || !tipBody) return;
            Vector2 start = RodAnchor;
            Vector2 end = state == RodState.Hooked && hooked ? hooked.Body.position :
                state == RodState.GroundAnchored ? groundAnchor : tipBody.position;
            float distance = Vector2.Distance(start, end);
            float slack = state == RodState.Hooked ? Mathf.Max(0f, ropeLength - distance) : 0.1f;
            float sag = 0.08f + slack * 0.35f + (heavyWeight ? 0.12f : 0f);
            Vector2 control = Vector2.Lerp(start, end, 0.5f) + Vector2.down * sag;
            if (state == RodState.Whipping)
                control = GetWhipControl(start, end);
            for (int i = 0; i < LinePoints; i++)
            {
                float t = i / (LinePoints - 1f);
                Vector2 p = QuadraticBezier(start, control, end, t);
                line.SetPosition(i, p);
            }

            strikeLine.enabled = state == RodState.Whipping && whipProgress > 0.24f;
            if (strikeLine.enabled)
            {
                for (int i = 0; i < AttackSamples; i++)
                {
                    float t = Mathf.Lerp(AttackStart, 1f, i / (AttackSamples - 1f));
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
                float t = Mathf.Lerp(AttackStart, 1f, i / (buffer.Length - 1f));
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
        public FishingRodController Owner;
        private void OnTriggerEnter2D(Collider2D other) => Owner.HandleTipContact(other);
    }
}
