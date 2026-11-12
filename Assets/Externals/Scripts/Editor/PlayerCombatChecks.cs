#if UNITY_EDITOR
using System;
using System.Reflection;
using FishingPrototype.Integration;
using UnityEngine;
namespace FishingPrototype.Editor
{
    public static class PlayerCombatChecks
    {
        private const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, flags).Invoke(target, args);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, flags).SetValue(target, value);
        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, flags).GetValue(target);
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static HookableBody Enemy(Transform root, Vector2 position)
        {
            var go = UnityEngine.Object.Instantiate(CombatSceneBuilder.Prefab("PoisonEnemy"), root);
            go.transform.position = position;
            var body = go.GetComponent<HookableBody>(); Call(body, "Awake"); body.Configure(HookableBody.BodyKind.Enemy, Color.white, 100f, false); body.Body.gravityScale = 0;
            return body;
        }
        public static void Run()
        {
            var root = new GameObject("Test_PlayerCombat");
            try
            {
                var playerObject = UnityEngine.Object.Instantiate(CombatSceneBuilder.Prefab("Player"), root.transform);
                var player = playerObject.GetComponent<PlayerController>(); Call(player, "Awake");
                player.transform.position = new Vector3(1000, 1000); player.Body.gravityScale = 0;
                var rod = player.GetComponentInChildren<FishingRodController>(); Call(rod, "Start");
                rod.statDefinition = null; rod.catchRange = 8; rod.attack = 10; rod.attackWidth = 1;
                Vector2 origin = player.transform.position;
                var near = Enemy(root.transform, origin + Vector2.right * 2);
                var far = Enemy(root.transform, origin + new Vector2(5, 1));
                Physics2D.SyncTransforms();
                Check(rod.SelectCatchTarget(far.transform.position) == far, "커서 대상이 가까운 적보다 우선해야 한다.");
                Check(rod.SelectCatchTarget(origin + Vector2.up * 20) == near, "빈 커서는 가장 가까운 적을 선택해야 한다.");
                far.transform.position = origin + Vector2.right * 9; Physics2D.SyncTransforms();
                Check(rod.SelectCatchTarget(far.transform.position) == near, "범위 밖 커서 대상은 제외해야 한다.");
                near.transform.position = origin + new Vector2(2, 2); far.transform.position = origin + new Vector2(4, 2); Physics2D.SyncTransforms();
                Vector2 anchor = rod.rodVisual.TransformPoint(Vector3.right * 0.5f);
                near.transform.position = anchor + Vector2.right * 2; far.transform.position = anchor + new Vector2(4, 3); Physics2D.SyncTransforms();
                Call(rod, "BeginAttack", anchor + Vector2.right * 6);
                Check(Mathf.Approximately(near.Health, 90), "기본공격은 attack 피해를 줘야 한다.");
                Vector3 attackPosition = Get<BoxCollider2D>(rod,"attackCollider").transform.position;
                player.transform.position += Vector3.up;
                Call(rod,"LateUpdate");
                Check(Vector3.Distance(Get<BoxCollider2D>(rod,"attackCollider").transform.position,attackPosition)<0.001f, "플레이어 이동으로 활성 공격 영역이 밀리면 안 된다.");
                player.transform.position -= Vector3.up;
                Call(rod, "TickAttack"); Check(Mathf.Approximately(near.Health, 90), "같은 공격은 같은 적을 중복 타격하면 안 된다.");
                far.transform.position = anchor + Vector2.right * 4; Physics2D.SyncTransforms(); Call(rod, "TickAttack");
                Check(Mathf.Approximately(far.Health, 90), "유지 중 진입한 적도 맞아야 한다.");
                Set(rod, "attackUntil", Time.time - 1f); Call(rod, "TickAttack");
                Check(!Get<BoxCollider2D>(rod, "attackCollider").enabled, "종료된 공격 판정은 꺼져야 한다.");
                rod.BeginPress(near.transform.position); near.statDefinition = null; near.catchHold = 0.5f;
                Set(rod, "pressedAt", Time.time - 0.4f); Set(rod,"castStarted",Time.time-1f); rod.lineSpeed = 10000;
                Call(rod, "TickCapture", true);
                Check(near.IsHooked && rod.HeldName == "", "도달해도 추가 홀드 시간이 필요하다.");
                Call(rod, "TickCapture", false); Check(!near.IsHooked && !near.IsThrown, "포획 취소는 투척이 아니다.");
                rod.BeginPress(near.transform.position); Set(rod, "pressedAt", Time.time - 1f); Set(rod,"castStarted",Time.time-1f);
                Vector2 capturePosition = near.Body.position; near.Body.linearVelocity = new Vector2(2f, 1f);
                Call(rod, "TickCapture", true);
                Check(rod.HeldName != "" && near.Body.position == capturePosition && near.Body.bodyType == RigidbodyType2D.Dynamic && near.Body.linearVelocity == new Vector2(2f,1f), "포획 완료는 위치·관성을 덮어쓰지 않는다.");
                Check(Get<float>(rod,"desiredRopeLength")==4f,"줄 길이의 목표는 catchRange/2다.");
                near.Body.linearVelocity = Vector2.zero;
                Call(rod, "LateUpdate"); Check(!Get<SpriteRenderer>(rod,"tipVisual").enabled, "포획 중 찌는 숨겨야 한다.");
                far.transform.position = origin + Vector2.right * 3; Physics2D.SyncTransforms();
                rod.BeginSlam(); Set(rod, "slamStarted", Time.time - 1f); float heldHp = near.Health, victimHp = far.Health;
                Vector2 beforeSlam = near.Body.position;
                Call(rod, "TickSlam");
                Check(near.Body.position==beforeSlam && near.Health==heldHp,"내려찍기는 충돌 전 순간이동하거나 피해를 주지 않는다.");
                Call(rod,"OnHeldContact",far.GetComponent<Collider2D>());
                Call(rod,"OnHeldContact",far.GetComponent<Collider2D>());
                Check(Mathf.Abs(near.Health - heldHp + 12) < 0.01f && Mathf.Abs(far.Health - victimHp + 11) < 0.01f, "내려찍기 120%/110% 피해가 정확해야 한다.");
                Check(rod.HeldName != "", "생존한 대상은 포획을 유지해야 한다.");
                far.transform.position = origin + Vector2.right * 30; Physics2D.SyncTransforms();
                heldHp = near.Health;
                rod.BeginSlam(); Set(rod, "slamStarted", Time.time - 1f); Call(rod, "TickSlam");
                var floorObject = new GameObject("Test_SlamFloor"); floorObject.transform.SetParent(root.transform);
                var floor = floorObject.AddComponent<BoxCollider2D>();
                Call(rod,"OnHeldContact",floor);
                Check(Mathf.Abs(near.Health - heldHp + 12) < 0.01f && rod.HeldName != "", "다른 적이 없는 내려찍기도 자신에게 120% 피해를 준다.");
                far.transform.position = origin + new Vector2(5, 5); Physics2D.SyncTransforms();
                Check(Vector2.Angle(rod.SelectThrowDirection(Vector2.right), Vector2.right) < 0.01f, "전체 60도 밖 대상은 제외해야 한다.");
                far.transform.position = origin + new Vector2(5, 1); Physics2D.SyncTransforms();
                Check(Vector2.Angle(rod.SelectThrowDirection(Vector2.right), (Vector2)far.transform.position - near.Body.position) < 0.01f, "부채꼴 안 적에게 보정해야 한다.");
                var tie = Enemy(root.transform, origin + new Vector2(2, -0.4f)); Physics2D.SyncTransforms();
                Check(Vector2.Angle(rod.SelectThrowDirection(Vector2.right), (Vector2)tie.transform.position - near.Body.position) < 0.01f, "각도가 같으면 가까운 적이 우선한다.");
                UnityEngine.Object.DestroyImmediate(tie.gameObject);
                rod.ThrowHeld(Vector2.right); Check(near.IsThrown && !near.IsHooked && Mathf.Abs(near.Body.linearVelocity.magnitude - rod.throwSpeed) < 0.01f, "Q 투척은 포획을 해제하고 지정 속도를 적용해야 한다.");
                Call(rod,"LateUpdate"); Check(Get<SpriteRenderer>(rod,"tipVisual").enabled, "투척 후 찌 표시를 복원해야 한다.");
                near.transform.position = origin + Vector2.right * 30; far.transform.position = origin + Vector2.right * 40; Physics2D.SyncTransforms();
                Check(rod.SelectCatchTarget(origin) == null, "범위 안 적이 없으면 타겟팅하지 않는다.");
                var rockObject = UnityEngine.Object.Instantiate(CombatSceneBuilder.Prefab("Rock"), root.transform);
                var rock = rockObject.GetComponent<HookableBody>(); rock.transform.position = origin + Vector2.left * 2; Call(rock, "Awake");
                Physics2D.SyncTransforms(); rod.BeginPress(rock.transform.position); Set(rod,"pressedAt",Time.time-1f); Set(rod,"castStarted",Time.time-1f); Call(rod,"TickCapture",true);
                Check(rod.HeldName != "", "오브젝트도 커서로 포획할 수 있어야 한다.");
                near.transform.position = origin + Vector2.right * 3; Physics2D.SyncTransforms();
                float rockHealth = rock.Health; heldHp = near.Health;
                rod.BeginSlam(); Set(rod,"slamStarted",Time.time-1f); Call(rod,"TickSlam"); Call(rod,"OnHeldContact",near.GetComponent<Collider2D>());
                Check(rock.Health == rockHealth && Mathf.Abs(near.Health-heldHp+11)<0.01f, "오브젝트 내려찍기는 적에게만 110% 피해를 준다.");
                rod.Interrupt(); UnityEngine.Object.DestroyImmediate(rockObject);
                near.Configure(HookableBody.BodyKind.Enemy,Color.white,100,false); near.catchHold=0;
                rod.BeginPress(near.transform.position); Set(rod,"pressedAt",Time.time-1f); Set(rod,"castStarted",Time.time-1f); Call(rod,"TickCapture",true);
                near.Configure(HookableBody.BodyKind.Enemy,Color.white,10,false);
                rod.BeginSlam(); Set(rod,"slamStarted",Time.time-1f); Call(rod,"TickSlam"); Call(rod,"OnHeldContact",floor);
                Check(rod.HeldName=="" && !near.IsHooked, "내려찍기로 사망한 적은 포획을 해제해야 한다.");
                CheckSheetColumns();
                Debug.Log("PLAYER SPEC CHECKS PASSED");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void CheckSheetColumns()
        {
            string csv = CombatSheetData.playerHeader + ",attackWidth,catchRange\nPLAYER,100,17,0,5,0.8,9";
            var value = CombatStatValues.Parse(CombatSheetData.Parse(csv, CombatSheetData.playerHeader)[0], false);
            Check(value.attack == 17 && value.attackWidth == 0.8f && value.catchRange == 9, "새 플레이어 시트 열을 읽어야 한다.");
            string old = CombatSheetData.playerHeader + "\nPLAYER,100,10,0,5";
            Check(CombatStatValues.Parse(CombatSheetData.Parse(old, CombatSheetData.playerHeader)[0], false).catchRange == 0, "기존 시트 호환을 유지해야 한다.");
            string enemy = CombatSheetData.enemyHeader + ",catchHold\nENEMY,1,Tang,Enemy,100,12,0,3,0.7";
            Check(CombatStatValues.Parse(CombatSheetData.Parse(enemy, CombatSheetData.enemyHeader)[0], true).catchHold == 0.7f, "적 추가 홀드 시간을 읽어야 한다.");
        }
    }
}
#endif
