#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
namespace FishingPrototype.Editor
{
    public static class EnemyCombatChecks
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, flags).Invoke(value, args);
        static void Set(object value, string name, object data) => value.GetType().GetField(name, flags).SetValue(value, data);
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static HookableBody Actor(string name, Transform root, Vector2 position)
        {
            var go = UnityEngine.Object.Instantiate(CombatSceneBuilder.Prefab(name), root);
            go.transform.position = position;
            var body = go.GetComponent<HookableBody>(); Call(body, "Awake");
            body.Body.position = position; body.Body.gravityScale = 0; return body;
        }
        public static void Run()
        {
            var root = new GameObject("Test_EnemyCombat");
            try
            {
                var playerObject = UnityEngine.Object.Instantiate(CombatSceneBuilder.Prefab("Player"), root.transform);
                playerObject.transform.position = new Vector2(1000, 1000);
                var player = playerObject.GetComponent<PlayerController>(); Call(player, "Awake"); player.Body.position = new Vector2(1000, 1000);
                var health = playerObject.GetComponent<PlayerHealth>(); Call(health, "Awake");
                var rod = playerObject.GetComponentInChildren<FishingRodController>(); Call(rod, "Start");
                var gasBody = Actor("PoisonEnemy", root.transform, new Vector2(1013, 1000));
                var gas = gasBody.GetComponent<TangEnemyAI>(); gas.player = player; Call(gas, "Start");
                Call(gas, "FixedUpdate"); Check(gas.State == TangEnemyAI.ActionState.Waiting, "감지 밖에서 대기해야 한다.");
                gasBody.Body.position = new Vector2(1008, 1000); Call(gas, "FixedUpdate");
                Check(gas.State == TangEnemyAI.ActionState.Moving && gasBody.Body.linearVelocity.x < 0, "독가스병이 공격 범위까지 접근해야 한다.");
                gasBody.Body.position = new Vector2(1004, 1000); Call(gas, "FixedUpdate");
                Check(gas.State == TangEnemyAI.ActionState.Windup && gasBody.Body.linearVelocity.x == 0, "독가스 공격 경계에서 즉시 준비해야 한다.");
                player.Body.position = new Vector2(1008, 1000); Call(gas, "FixedUpdate");
                Check(gas.attackPoint.localPosition.x > 0 && gas.transform.Find("Sprite_눈").localPosition.x > 0, "준비 중 반대편으로 이동하면 예고 지점과 외형이 함께 돌아야 한다.");
                player.Body.position = new Vector2(1000, 1000);
                Set(gas, "until", Time.time - 1); Call(gas, "FixedUpdate");
                Check(gas.attackPoint.localPosition.x < 0 && gas.transform.Find("Sprite_눈").localPosition.x < 0, "독가스 분사 직전에도 지점과 외형을 다시 맞춰야 한다.");
                var cloud = (PoisonCloud)typeof(TangEnemyAI).GetField("spray", flags).GetValue(gas);
                Check(cloud && gas.State == TangEnemyAI.ActionState.Spraying, "준비 후 분사를 시작해야 한다.");
                Check(Mathf.Abs(cloud.area.x - gas.gasRange) < 0.01f, "분사 시작부터 전체 gasRange 판정이 있어야 한다.");
                cloud.transform.SetParent(root.transform); Call(cloud, "Start");
                Check(cloud.transform.childCount > 0, "독가스 흐름을 표시할 연기 파츠가 있어야 한다.");
                Vector3 gasPosition = cloud.transform.position, gasScale = cloud.transform.localScale;
                Call(cloud, "Update");
                Check(cloud.transform.position == gasPosition && cloud.transform.localScale == gasScale, "연기 연출이 피해 범위를 움직이거나 늘리면 안 된다.");
                Set(gas, "until", Time.time + gas.sprayDuration * 0.5f); Call(gas, "FixedUpdate");
                Check(Mathf.Abs(cloud.area.x - gas.gasRange) < 0.01f, "분사 중에도 전체 범위를 유지해야 한다.");
                UnityEngine.Object.DestroyImmediate(cloud.gameObject);
                Set(gas, "until", Time.time - 1); Call(gas, "FixedUpdate");
                Check(gas.State == TangEnemyAI.ActionState.Recovery, "분사 후 대기해야 한다.");
                Call(gas, "FixedUpdate"); Check(gas.State == TangEnemyAI.ActionState.Recovery, "대기 중 재공격하면 안 된다.");
                Set(gas, "until", Time.time - 1); Call(gas, "FixedUpdate");
                Check(gas.State == TangEnemyAI.ActionState.Windup, "범위 안이면 다시 준비해야 한다.");
                gasBody.SetHooked(true); Call(gas, "FixedUpdate"); Check(gas.State == TangEnemyAI.ActionState.Waiting, "포획되면 공격을 취소해야 한다.");
                gasBody.SetHooked(false); gasBody.ClearThrow();

                var rangedBody = Actor("ShurikenEnemy", root.transform, new Vector2(1002, 1000));
                var ranged = rangedBody.GetComponent<TangEnemyAI>(); ranged.player = player; Call(ranged, "Start");
                rangedBody.Body.position = new Vector2(1013, 1000); Call(ranged, "FixedUpdate");
                Check(rangedBody.Body.linearVelocity.x < 0, "수리검병은 독가스 감지 거리 밖에서도 거리 조절해야 한다.");
                rangedBody.Body.position = new Vector2(1002, 1000);
                Call(ranged, "FixedUpdate"); Check(rangedBody.Body.linearVelocity.x > 0, "너무 가까우면 물러나야 한다.");
                rangedBody.Body.position = new Vector2(1006, 1000); Call(ranged, "FixedUpdate");
                Check(rangedBody.Body.linearVelocity.x < 0, "너무 멀면 접근해야 한다.");
                foreach (float boundary in new[] { ranged.minKeepDistance, ranged.maxKeepDistance })
                {
                    Set(ranged, "state", TangEnemyAI.ActionState.Moving); rangedBody.Body.position = new Vector2(1000 + boundary, 1000);
                    Call(ranged, "FixedUpdate"); Check(ranged.State == TangEnemyAI.ActionState.Windup, "거리 유지의 양쪽 경계를 포함해야 한다.");
                }
                player.Body.position = new Vector2(1010, 1000);
                Set(ranged, "until", Time.time - 1); Call(ranged, "FixedUpdate");
                var shot = UnityEngine.Object.FindFirstObjectByType<Shuriken>();
                Check(shot && ranged.State == TangEnemyAI.ActionState.Recovery, "수리검 발사 직후 대기해야 한다.");
                Check(ranged.attackPoint.localPosition.x > 0 && ranged.transform.Find("Sprite_눈").localPosition.x > 0
                    && shot.transform.position.x > rangedBody.Body.position.x && shot.GetComponent<Rigidbody2D>().linearVelocity.x > 0,
                    "수리검 발사 순간 바뀐 플레이어 방향으로 외형·발사 지점·속도를 맞춰야 한다.");
                player.Body.position = new Vector2(1000, 1000);
                shot.transform.SetParent(root.transform);
                Check(shot.GetComponent<Rigidbody2D>().gravityScale == 0 && Mathf.Abs(shot.GetComponent<Rigidbody2D>().linearVelocity.magnitude - shot.flySpeed) < 0.01f, "수리검은 무중력 일정 속도로 발사해야 한다.");
                UnityEngine.Object.DestroyImmediate(shot.gameObject);

                var starBody = Actor("Shuriken", root.transform, new Vector2(1001, 1000));
                var star = starBody.GetComponent<Shuriken>(); Call(star, "Awake"); star.Launch(rangedBody, Vector2.left);
                Check(star.CounterAvailable, "반격 범위 안의 적 수리검은 Q 표시가 가능해야 한다.");
                var stateType = typeof(FishingRodController).GetField("state", flags).FieldType;
                Set(rod, "state", Enum.Parse(stateType, "Held"));
                Check(!star.CounterAvailable && !rod.TryCounter(), "기존 포획 중에는 Q 투척을 우선해야 한다.");
                Set(rod, "state", Enum.Parse(stateType, "Idle"));
                starBody.Body.position = new Vector2(1000 + rod.shurikenCounterRange + 0.1f, 1000);
                Check(!star.CounterAvailable, "반격 범위 밖에서는 Q 표시가 없어야 한다.");
                starBody.Body.position = new Vector2(1001, 1000);
                Check(rod.TryCounter() && !rod.TryCounter(), "한 번에 한 수리검만 반격해야 한다.");
                Check(star.IsCountering && !starBody.Body.simulated && !star.CounterAvailable, "반격 모션 중 수리검 충돌과 재입력을 막아야 한다.");
                Set(star, "counterStarted", Time.time - rod.shurikenCounterMotionTime - 1); Call(star, "Update");
                Check(!star.IsCountering && starBody.Body.simulated && !starBody.IsHooked, "모션 종료 후 수리검을 해제해야 한다.");
                Check(Mathf.Abs(starBody.Body.linearVelocity.magnitude - star.flySpeed * 1.5f) < 0.01f && star.HitDamage == star.attackDamage * 2f, "Q 반격 속도와 피해 배율이 일치해야 한다.");
                Check(!Physics2D.GetIgnoreCollision(star.GetComponent<Collider2D>(), gasBody.GetComponent<Collider2D>()), "반격 수리검은 다른 적도 맞혀야 한다.");
                Check(Physics2D.GetIgnoreCollision(star.GetComponent<Collider2D>(), player.GetComponent<Collider2D>()), "반격 수리검이 플레이어를 맞히면 안 된다.");

                var poison = player.GetComponent<PoisonStatus>(); poison.until = 0;
                poison.Apply(3, 7, 0.25f, 0.3f);
                Check(poison.status == PoisonStatus.PlayerStatus.poisoning && Mathf.Abs(poison.SpeedMultiplier - 0.7f) < 0.001f, "중독 상태와 감속 비율을 적용해야 한다.");
                float hp = health.Health; Set(poison, "nextTick", Time.time - 0.5f); Call(poison, "Update");
                Check(health.Health == hp - 21, "밀린 중독 3틱을 누락 없이 합산해야 한다.");
                Call(poison, "Update"); Check(health.Health == hp - 21, "이미 처리한 틱을 중복 적용하면 안 된다.");
                poison.until = 0; poison.Apply(0.875f, 4, 0.25f, 0.3f); hp = health.Health;
                Call(poison, "Tick", Time.time + 2f);
                Check(health.Health == hp - 12, "만료를 넘긴 프레임에도 만료 전 3틱만 적용해야 한다.");
                Call(poison, "Tick", Time.time + 3f); Check(health.Health == hp - 12, "만료 뒤 추가 피해가 있으면 안 된다.");
                poison.until = 0; poison.Apply(3, 2, 0.25f, 0.3f); hp = health.Health;
                Set(poison, "nextTick", Time.time - 0.25f); poison.Apply(3, 5, 0.25f, 0.3f);
                Check(health.Health == hp - 4, "접촉 갱신 전에 밀린 틱은 이전 피해값으로 정산해야 한다.");
                poison.until = Time.time - 1; Check(poison.status == PoisonStatus.PlayerStatus.normal && poison.SpeedMultiplier == 1, "만료 후 정상 속도로 돌아와야 한다.");
                var gasObject = UnityEngine.Object.Instantiate(CombatSceneBuilder.Prefab("EnemyPoison"), root.transform);
                gasObject.transform.position = player.transform.position;
                var contact = gasObject.GetComponent<PoisonCloud>(); Call(contact, "Start"); contact.refreshDurationOnContact = false;
                Physics2D.SyncTransforms(); Call(contact, "FixedUpdate");
                poison.until = Time.time + 0.1f; Call(contact, "FixedUpdate");
                Check(Mathf.Abs(poison.until - Time.time - 0.1f) < 0.01f, "갱신 끔이면 접촉 유지로 시간이 초기화되면 안 된다.");
                contact.refreshDurationOnContact = true; Call(contact, "FixedUpdate");
                Check(Mathf.Abs(poison.until - Time.time - contact.poisonDuration) < 0.01f, "갱신 켬이면 접촉 중 시간을 유지해야 한다.");
                player.Body.position += Vector2.up * 10; Physics2D.SyncTransforms(); Call(contact, "FixedUpdate");
                poison.until = Time.time + 0.1f; contact.refreshDurationOnContact = false;
                player.Body.position = gasObject.transform.position; Physics2D.SyncTransforms(); Call(contact, "FixedUpdate");
                Check(Mathf.Abs(poison.until - Time.time - contact.poisonDuration) < 0.01f, "재접촉하면 중독 시간을 갱신해야 한다.");
                Debug.Log("ENEMY SPEC CHECKS PASSED");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
#endif
