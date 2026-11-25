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
        private static HookableBody Actor(string name, Transform root, Vector2 position)
        {
            var go = UnityEngine.Object.Instantiate(CombatSceneBuilder.Prefab(name), root);
            go.transform.position = position;
            var body = go.GetComponent<HookableBody>(); Call(body, "Awake");
            body.statDefinition = null; body.Configure(body.kind, Color.white, 100, false);
            body.Body.position = position;
            return body;
        }
        private static void Position(HookableBody body, Vector2 position)
        { body.transform.position = position; body.Body.position = position; Physics2D.SyncTransforms(); }
        private static void Capture(FishingRodController rod, HookableBody body)
        {
            rod.BeginCapture(body.Body.position);
            Set(rod, "castStarted", Time.time - 10); Call(rod, "TickCapture");
            Check(body.IsHooked && rod.HeldName == "", "첫 입력은 연결만 해야 한다.");
            rod.BeginCapture(Vector2.zero);
            Position(body, rod.player.Body.position + Get<Vector2>(rod, "holdOffset"));
            Call(rod, "TickCapture");
            Check(rod.HeldName != "", "두 번째 입력 후 기본 위치에 도달하면 포획해야 한다.");
        }
        public static void Run()
        {
            var root = new GameObject("Test_PlayerCombat");
            try
            {
                var go = UnityEngine.Object.Instantiate(CombatSceneBuilder.Prefab("Player"), root.transform);
                go.transform.position = new Vector2(1000, 1000);
                var player = go.GetComponent<PlayerController>(); Call(player, "Awake"); player.Body.position = go.transform.position;
                var health = go.GetComponent<PlayerHealth>(); Call(health, "Awake");
                var rod = go.GetComponentInChildren<FishingRodController>(); Call(rod, "Start");
                rod.statDefinition = null; rod.catchRange = 8; rod.attack = 10; rod.attackWidth = 1;
                Vector2 origin = player.Body.position;
                var near = Actor("PoisonEnemy", root.transform, origin + Vector2.right * 2);
                var far = Actor("PoisonEnemy", root.transform, origin + Vector2.right * 5);
                Physics2D.SyncTransforms();
                Check(rod.SelectCatchTarget(far.Body.position) == far, "커서 위치 대상을 선택해야 한다.");
                Check(!rod.SelectCatchTarget(origin + Vector2.up * 20), "빈 커서는 자동으로 가까운 적을 선택하지 않는다.");
                Position(far, origin + Vector2.right * 9);
                Check(!rod.SelectCatchTarget(far.Body.position), "범위 밖 대상은 선택할 수 없다.");
                Position(far, near.Body.position); far.GetComponent<SpriteRenderer>().sortingOrder = 99;
                Check(rod.SelectCatchTarget(near.Body.position) == far, "본체가 겹치면 앞에 그려진 대상을 선택해야 한다.");
                Position(far, origin + new Vector2(5, 4));
                Vector2 anchor = rod.rodVisual.TransformPoint(Vector3.right * 0.5f);
                Position(near, anchor + Vector2.right * 2);
                rod.BeginPress(anchor + Vector2.right * 6);
                Check(near.Health == 100, "준비 중에는 공격 피해가 발생하면 안 된다.");
                Vector2 lockedEnd = Get<Vector2>(rod, "attackEnd");
                rod.BeginPress(anchor + Vector2.left * 6);
                Check(Get<Vector2>(rod, "attackEnd") == lockedEnd, "광클이 진행 중 공격을 초기화하면 안 된다.");
                Set(rod, "hitAt", Time.time - 0.01f); Call(rod, "TickAttack");
                Check(near.Health == 90, "완전 전개 순간에 한 번만 공격해야 한다.");
                Position(far, anchor + Vector2.right * 4); Call(rod, "TickAttack");
                Check(near.Health == 90 && far.Health == 100, "타격 이후 진입한 적은 맞으면 안 된다.");
                Check(rod.AttackTimeForDistance(2) < rod.AttackTimeForDistance(10), "먼 공격은 전체 시간이 길어야 한다.");
                rod.Interrupt();
                rod.BeginPress(anchor); Check(Mathf.Abs(Vector2.Distance(anchor, Get<Vector2>(rod,"attackEnd"))-rod.minAttackRange)<0.001f,"최소 전개 거리 보정");
                rod.Interrupt(); rod.BeginPress(anchor + Vector2.right * 100);
                Check(Mathf.Abs(Vector2.Distance(anchor, Get<Vector2>(rod,"attackEnd"))-rod.maxAttackRange)<0.001f,"최대 전개 거리 보정"); rod.Interrupt();
                Position(near, origin + Vector2.right * 2); Position(far, origin + Vector2.right * 20);
                rod.BeginCapture(near.Body.position); Set(rod,"castStarted",Time.time-10); Call(rod,"TickCapture");
                Set(rod,"connectedAt",Time.time-rod.catchInputTimeout-1); Call(rod,"TickCapture");
                Check(!near.IsHooked && !near.IsThrown, "추가 입력 시간 초과는 투척 없이 취소해야 한다.");
                rod.BeginCapture(near.Body.position); Set(rod,"castStarted",Time.time-10); Call(rod,"TickCapture");
                rod.BeginPress(near.Body.position);
                Check(!near.IsHooked && Get<object>(rod,"state").ToString()=="Idle", "취소 클릭은 동시에 공격하면 안 된다.");
                Capture(rod, near);
                Vector2 offset = Get<Vector2>(rod,"holdOffset"); Set(rod,"rodFacing",-1f);
                Check(Get<Vector2>(rod,"holdOffset")==offset,"방향 전환으로 포획 상대 위치가 반전되면 안 된다.");
                rod.OnPhysicalHit(); Check(near.IsHooked,"완료된 포획은 물리 피격으로 해제되지 않는다.");
                rod.BeginSlam(); Check(Get<object>(rod,"state").ToString()=="Held","공중 내려찍기는 금지다.");
                var floor = new GameObject("Collider_TestFloor"); floor.transform.SetParent(root.transform);
                floor.transform.position = origin + Vector2.down * 0.92f;
                var ground = floor.AddComponent<BoxCollider2D>(); ground.size = new Vector2(20,0.2f); Physics2D.SyncTransforms();
                Position(far, origin + Vector2.right * 3);
                rod.BeginSlam(); Check(Get<object>(rod,"state").ToString()=="Slamming","지상 내려찍기는 가능해야 한다.");
                float heldHp=near.Health, victimHp=far.Health;
                Call(rod,"FinishSlam",far);
                Check(near.Health==heldHp-12 && far.Health==victimHp-11 && near.IsHooked,"내려찍기 배율과 포획 유지");
                Position(far, origin + new Vector2(5,1));
                Vector2 destination = far.Body.position;
                rod.ThrowHeld(Vector2.right);
                Check(near.IsThrown && !near.IsHooked,"투척은 포획을 해제해야 한다.");
                Vector2 v=near.Body.linearVelocity, launch=near.Body.position;
                float flight=(destination.x-launch.x)/v.x;
                float y=launch.y+v.y*flight+0.5f*Physics2D.gravity.y*near.Body.gravityScale*flight*flight;
                Check(Mathf.Abs(y-destination.y)<0.01f,"보정 포물선은 목표 위치에 도달해야 한다.");
                heldHp=near.Health;victimHp=far.Health;
                near.ResolveThrowImpact(far.GetComponent<Collider2D>()); near.ResolveThrowImpact(ground);
                Check(near.Health==heldHp-rod.thrownSelfDamage && far.Health==victimHp-rod.thrownHitDamage,"최초 충돌 이후 중복 피해 금지");
                rod.Interrupt(); Position(near,origin+Vector2.right*20);Position(far,origin+Vector2.right*22);
                var star=Actor("Shuriken",root.transform,origin+Vector2.right*2);var projectile=star.GetComponent<Shuriken>();Call(projectile,"Awake");
                Physics2D.SyncTransforms();rod.BeginCapture(star.Body.position);
                Check(star.IsHooked && rod.HeldName!="","수리검은 첫 우클릭으로 즉시 포획해야 한다.");
                rod.ThrowHeld(Vector2.right);
                Check(Mathf.Abs(star.Body.linearVelocity.magnitude-projectile.flySpeed*2)<0.01f && projectile.HitDamage==projectile.attackDamage*1.5f,"수리검 수동 반격 배율");
                rod.Interrupt(); Position(near,origin+Vector2.right*2);Physics2D.SyncTransforms();rod.BeginCapture(near.Body.position);
                health.ReceiveDamage(1,Vector2.zero);
                Check(!rod.CapturePending,"물리 피격은 진행 중 포획을 취소해야 한다.");
                float hp=health.Health;health.ReceiveDamage(10,Vector2.zero);health.ReceivePoisonDamage(3);
                Check(health.Health==hp-3,"물리 무적 중에도 중독 피해는 적용해야 한다.");
                CheckSheetColumns();
                Debug.Log("PLAYER SPEC CHECKS PASSED");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void CheckSheetColumns()
        {
            string csv=CombatSheetData.playerHeader+",minAttackRange,maxAttackRange,holdDistance,catchInputTimeout\nPLAYER,100,17,0,5,2,9,3,1.2";
            var value=CombatStatValues.Parse(CombatSheetData.Parse(csv,CombatSheetData.playerHeader)[0],false);
            Check(value.revisedCombat && value.holdDistance==3 && value.catchInputTimeout==1.2f,"개정 시트 필드를 가져와야 한다.");
            string old=CombatSheetData.playerHeader+"\nPLAYER,100,10,0,5";
            Check(CombatStatValues.Parse(CombatSheetData.Parse(old,CombatSheetData.playerHeader)[0],false).maxAttackRange==10,"기존 시트에는 명시적 임시 기본값을 적용해야 한다.");
            bool rejected=false;
            try { CombatStatValues.Parse(CombatSheetData.Parse(csv.Replace(",2,9,3,1.2",",9,2,3,1.2"),CombatSheetData.playerHeader)[0],false); }
            catch(FormatException) { rejected=true; }
            Check(rejected,"잘못된 최소·최대 범위는 가져오기 전에 거부해야 한다.");
        }
    }
}
#endif
