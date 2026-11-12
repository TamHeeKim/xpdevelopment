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
        public ActionState State => state;
        HookableBody body;
        SpriteRenderer visual;
        ActionState state;
        float until, direction = 1f;
        bool engaged;
        PoisonCloud spray;
        Vector2 sprayDirection;
        Vector3 visualPosition;
        Quaternion visualRotation;
        SpriteRenderer[] facingParts;
        Vector3[] partPositions;
        bool[] partFlips;
        Vector3 AttackVisualPosition => new Vector3(visualPosition.x * direction, visualPosition.y, visualPosition.z);
        Vector2 AttackOrigin => attackPoint ? body.Body.position + (Vector2)(attackPoint.position - transform.position) : body.Body.position + Vector2.right * direction;

        void Start()
        {
            body = GetComponent<HookableBody>(); visual = GetComponent<SpriteRenderer>();
            if (attackVisual) { visualPosition = attackVisual.localPosition; visualRotation = attackVisual.localRotation; }
            facingParts = GetComponentsInChildren<SpriteRenderer>();
            partPositions = new Vector3[facingParts.Length]; partFlips = new bool[facingParts.Length];
            for (int i = 0; i < facingParts.Length; i++)
            { partPositions[i] = facingParts[i].transform.localPosition; partFlips[i] = facingParts[i].flipX; }
            if (!player) player = FindFirstObjectByType<PlayerController>();
        }
        void FixedUpdate()
        {
            if (!body || !player || !body.Body.simulated || !player.Body.simulated || player.ControlsLocked || body.Health <= 0f)
            { CancelAttack(); return; }
            if (body.IsHooked || body.IsThrown) { CancelAttack(); return; }
            Vector2 delta = player.Body.position - body.Body.position;
            float distance = delta.magnitude;
            if (!engaged)
            {
                if (enemyType == EnemyType.Poison && distance > detectionRange) { Move(0); return; }
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
            if (movement != 0f) { state = ActionState.Moving; Move(movement * (body.statDefinition ? body.statDefinition.Values.moveSpeed : moveSpeed)); return; }
            Move(0); state = ActionState.Windup; until = Time.time + attackWindupTime;
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
            EndSpray(); ResetPose(); engaged = false; state = ActionState.Waiting;
            if (visual && body) visual.color = body.baseColor;
        }
        void OnDisable() { if (body) CancelAttack(); }
    }
}
