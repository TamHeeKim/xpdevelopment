using UnityEngine;
namespace FishingPrototype
{
    public sealed class TangEnemyAI : MonoBehaviour
    {
        public enum EnemyType { [InspectorName("독가스병")] Poison, [InspectorName("수리검병")] Shuriken }
        public enum ActionState { Waiting, Moving, Windup, Spraying, Recovery }
        [Header("적 종류와 연결")]
        public EnemyType enemyType;
        public PlayerController player;
        public Transform attackPoint;
        public Transform attackVisual;
        public Transform model;
        public PoisonCloud poisonCloudPrefab;
        public Shuriken shurikenPrefab;
        [Header("감지와 이동")]
        [Min(0)] public float detectionRange = 12f;
        [Min(0)] public float moveSpeed = 2f;
        [Header("공격 시간")]
        [Min(0)] public float attackWindupTime = 0.6f;
        [Min(0)] public float attackRecoveryTime = 1.5f;
        [Header("독가스 분사")]
        [UnityEngine.Serialization.FormerlySerializedAs("poisonTriggerRadius")]
        [Min(0)] public float attackRange = 4f;
        [Min(0.01f)] public float sprayDuration = 2f;
        [Min(0.01f)] public float gasRange = 4f;
        [Header("수리검 거리 유지 (경계 포함)")]
        [Min(0)] public float minKeepDistance = 3f;
        [Min(0)] public float maxKeepDistance = 5f;
        [Header("피격 및 간격 유지 (임시값)")]
        public float hitStunTime = 0.18f;
        public float separationDistance = 1.2f, separationSpeed = 1.5f;
        private float stunUntil, stunStarted;
        private bool stunned;
        public bool IsStunned => stunned;
        public ActionState State => state;
        HookableBody body;
        SpriteRenderer visual;
        ActionState state;
        float until, direction = 1f;
        bool engaged;
        PoisonCloud spray;
        Vector2 sprayDirection;
        Vector3 visualPosition;
        Quaternion visualRotation, modelRotation;
        SpriteRenderer[] facingParts;
        Vector3[] partPositions;
        bool[] partFlips;
        Vector3 AttackVisualPosition => new Vector3(visualPosition.x * direction, visualPosition.y, visualPosition.z);
        Vector2 AttackOrigin => attackPoint ? body.Body.position + (Vector2)(attackPoint.position - transform.position) : body.Body.position + Vector2.right * direction;

        void Start()
        {
            body = GetComponent<HookableBody>(); visual = GetComponent<SpriteRenderer>();
            if (attackVisual) { visualPosition = attackVisual.localPosition; visualRotation = attackVisual.localRotation; }
            if (model) modelRotation = model.localRotation;
            facingParts = GetComponentsInChildren<SpriteRenderer>();
            partPositions = new Vector3[facingParts.Length]; partFlips = new bool[facingParts.Length];
            for (int i = 0; i < facingParts.Length; i++)
            { partPositions[i] = facingParts[i].transform.localPosition; partFlips[i] = facingParts[i].flipX; }
            if (!player) player = FindFirstObjectByType<PlayerController>();
        }
        void FixedUpdate()
        {
            ApplySheet();
            if (!body || !player || !body.Body.simulated || !player.Body.simulated || body.Health <= 0f)
            { CancelAttack(); return; }
            if (body.IsHooked || body.IsThrown) { CancelAttack(); return; }
            if (stunned)
            {
                Move(0);
                if (Time.time < stunUntil) return;
                until += Time.time - stunStarted;
                if (spray) { spray.ExtendLifetime(Time.time - stunStarted); spray.Paused = false; }
                stunned = false;
            }
            Vector2 delta = player.Body.position - body.Body.position;
            float distance = delta.magnitude;
            if (!engaged)
            {
                if (distance > (enemyType == EnemyType.Poison ? detectionRange : maxKeepDistance)) { Move(0); return; }
                engaged = true;
            }
            if (state == ActionState.Windup)
            {
                FacePlayer();
                Move(0);
                visual.color = Color.Lerp(body.baseColor, Color.yellow, 0.55f + Mathf.Sin(Time.time * 20f) * 0.25f);
                if (attackVisual)
                {
                    float progress = attackWindupTime <= 0 ? 1 : Mathf.Clamp01(1f - (until - Time.time) / attackWindupTime);
                    attackVisual.localPosition = AttackVisualPosition + Vector3.up * Mathf.Sin(progress * Mathf.PI * 0.5f) * 0.2f;
                    attackVisual.localRotation = visualRotation * Quaternion.Euler(0, 0, -50f * direction * progress);
                }
                if (Time.time >= until) Fire();
                return;
            }
            if (state == ActionState.Spraying)
            {
                Move(0);
                if (spray) spray.SetSprayShape(AttackOrigin, sprayDirection, gasRange);
                if (Time.time >= until) { EndSpray(); EnterRecovery(); }
                return;
            }
            if (state == ActionState.Recovery)
            {
                Move(0);
                if (Time.time < until) return;
            }
            FacePlayer();
            float movement = enemyType == EnemyType.Poison ? (distance > attackRange ? direction : 0f)
                : distance < minKeepDistance ? -direction : distance > Mathf.Max(minKeepDistance, maxKeepDistance) ? direction : 0f;
            if (movement != 0f) { state = ActionState.Moving; MoveWithSeparation(movement * (body.statDefinition ? body.statDefinition.Values.moveSpeed : moveSpeed)); return; }
            Move(0); state = ActionState.Windup; until = Time.time + attackWindupTime;
        }
        void ApplySheet()
        {
            if (!body || !body.statDefinition || !body.statDefinition.Values.revisedCombat) return;
            var v = body.statDefinition.Values;
            detectionRange = v.detectionRange; attackRange = v.attackRange;
            attackWindupTime = v.attackWindupTime; attackRecoveryTime = v.attackRecoveryTime;
            sprayDuration = Mathf.Max(0.01f, v.sprayDuration); gasRange = Mathf.Max(0.01f, v.gasRange);
            minKeepDistance = v.minKeepDistance; maxKeepDistance = v.maxKeepDistance; hitStunTime = v.hitStunTime;
        }
        public void Stun()
        {
            if (!body) body = GetComponent<HookableBody>();
            ApplySheet();
            if (stunned || body.Health <= 0 || body.IsHooked || body.IsThrown || hitStunTime <= 0) return;
            stunned = true; stunStarted = Time.time; stunUntil = Time.time + hitStunTime;
            Move(0);
            if (spray) spray.Paused = true;
        }
        public void OnConnected()
        {
            if (!body) body = GetComponent<HookableBody>();
            CancelAttack();
            // 연결 후 취소된 적은 최초 감지 대신 현재 거리로 행동을 다시 판단합니다.
            engaged = true;
        }
        void MoveWithSeparation(float speed)
        {
            float correction = 0;
            var seen = new System.Collections.Generic.HashSet<HookableBody>();
            foreach (var col in Physics2D.OverlapCircleAll(body.Body.position, separationDistance))
            {
                var other = col.GetComponentInParent<HookableBody>();
                if (!other || other == body || !seen.Add(other) || other.kind != HookableBody.BodyKind.Enemy || other.IsHooked || other.IsThrown || other.Health <= 0) continue;
                Vector2 offset = body.Body.position - other.Body.position;
                if (Mathf.Abs(offset.y) > separationDistance * 0.75f) continue;
                float sign = Mathf.Abs(offset.x) > 0.01f ? Mathf.Sign(offset.x) : (body.GetInstanceID() < other.GetInstanceID() ? -1 : 1);
                correction += sign * Mathf.Clamp01(1 - offset.magnitude / Mathf.Max(0.01f, separationDistance));
            }
            Move(speed + Mathf.Clamp(correction, -1, 1) * separationSpeed);
        }
        void FacePlayer()
        {
            float delta = player.Body.position.x - body.Body.position.x;
            if (Mathf.Abs(delta) > 0.01f) direction = Mathf.Sign(delta);
            if (attackPoint)
            {
                Vector3 point = attackPoint.localPosition;
                point.x = Mathf.Abs(point.x) * direction; attackPoint.localPosition = point;
            }
            if (model) model.localRotation = Quaternion.Euler(0, direction < 0 ? 180 : 0, 0) * modelRotation;
            for (int i = 0; i < facingParts.Length; i++)
            {
                var part = facingParts[i]; part.flipX = partFlips[i] ^ (direction < 0f);
                if (part.transform.parent == transform)
                    part.transform.localPosition = new Vector3(partPositions[i].x * direction, partPositions[i].y, partPositions[i].z);
            }
        }
        void Fire()
        {
            FacePlayer();
            visual.color = body.baseColor;
            ResetPose();
            Vector2 aim = (player.Body.position - AttackOrigin).normalized;
            if (aim.sqrMagnitude < 0.01f) aim = Vector2.right * direction;
            if (enemyType == EnemyType.Poison)
            {
                spray = PoisonCloud.Spawn(poisonCloudPrefab, AttackOrigin);
                spray.duration = sprayDuration; sprayDirection = aim;
                spray.SetSprayShape(AttackOrigin, aim, gasRange);
                state = ActionState.Spraying; until = Time.time + sprayDuration;
            }
            else
            {
                var projectile = Instantiate(shurikenPrefab, AttackOrigin, Quaternion.identity);
                projectile.Launch(body, aim);
                EnterRecovery();
            }
        }
        void EnterRecovery() { state = ActionState.Recovery; until = Time.time + attackRecoveryTime; }
        void Move(float speed) { body.Body.linearVelocity = new Vector2(speed, body.Body.linearVelocity.y); }
        void EndSpray() { if (spray) Destroy(spray.gameObject); spray = null; }
        void ResetPose()
        {
            if (attackVisual) { attackVisual.localPosition = AttackVisualPosition; attackVisual.localRotation = visualRotation; }
        }
        void CancelAttack()
        {
            EndSpray(); ResetPose(); stunned = false; state = ActionState.Waiting;
            if (visual && body) visual.color = body.baseColor;
        }
        void OnDisable() { if (body) CancelAttack(); }
    }
}
