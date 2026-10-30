#if UNITY_EDITOR
using System;
using UnityEngine;
namespace FishingPrototype.Editor
{
    public static class CombatChecks
    {
        public static void Run()
        {
            var go = new GameObject("Combat check");
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            var target = go.AddComponent<HookableBody>();
            target.SetHooked(false);
            if (target.IsThrown) throw new Exception("Uncaptured body must not become a projectile");
            target.SetHooked(true);
            if (!target.IsHooked) throw new Exception("Capture state failed");
            target.SetHooked(false);
            if (target.IsHooked) throw new Exception("Release state failed");
            if (!target.IsThrown) throw new Exception("Released target must retain impact damage");
            target.SetHooked(true);
            if (target.IsThrown) throw new Exception("Recapture must clear thrown state");
            target.ThrowDamageDuration = 0f;
            target.SetHooked(false);
            if (target.IsThrown) throw new Exception("Expired throw must not deal impact damage");
            CombatShapes.DressEnemy(go, true);
            if (go.GetComponentsInChildren<SpriteRenderer>().Length < 8) throw new Exception("Enemy silhouette missing parts");
            CombatShapes.DressShuriken(go);
            if (go.GetComponent<SpriteRenderer>().sprite.texture.GetPixel(0,0).a != 0f || go.GetComponent<SpriteRenderer>().sprite.texture.GetPixel(32,32).a < 0.9f) throw new Exception("Shuriken must have four blades");
            var model = CombatShapes.DressPlayer(go);
            if (!model.Find("모자") || !model.Find("왼쪽 장화") || go.GetComponent<SpriteRenderer>().sprite != null)
                throw new Exception("Player appearance must replace the rectangle with a complete character");
            var bottle = go.AddComponent<PoisonBottle>();
            bottle.Fill();
            if (!bottle.Filled || target.BaseColor != Color.green) throw new Exception("Bottle capture failed");
            target.Configure(HookableBody.BodyKind.Prop, Color.green, 100f, false);
            target.ReceiveDamage(10000f, Vector2.zero);
            if (target.HealthFraction != 1f) throw new Exception("일반 오브젝트는 피해로 파괴되지 않아야 한다");
            target.ThrowDamageDuration = 2f;
            target.SetHooked(true); target.SetHooked(false);
            typeof(HookableBody).GetField("<Body>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, go.GetComponent<Rigidbody2D>());
            target.Body.position = new Vector2(20f, -20f);
            target.Body.linearVelocity = Vector2.down * 10f;
            typeof(HookableBody).GetMethod("FixedUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, null);
            if (target.Body.position.y < -12f || target.Body.linearVelocity != Vector2.zero || target.IsThrown)
                throw new Exception("맵 아래로 떨어진 오브젝트는 투척 상태를 해제하고 복귀해야 한다");
            target.Kind = HookableBody.BodyKind.Enemy;
            var poison = go.AddComponent<PoisonStatus>(); poison.Apply();
            if (Mathf.Abs(poison.Until - Time.time - 2f) > 0.01f) throw new Exception("Poison residual must last two seconds");
            var projectile = new GameObject("Projectile collision check");
            projectile.AddComponent<HookableBody>();
            var projectileCollider = projectile.AddComponent<BoxCollider2D>();
            var star = projectile.AddComponent<Shuriken>();
            typeof(Shuriken).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(star, null);
            star.ConfigureEnemy(go.GetComponent<Collider2D>());
            if (!Physics2D.GetIgnoreCollision(projectileCollider, go.GetComponent<Collider2D>())) throw new Exception("Enemy shuriken must pass through allies");
            star.Reflect();
            if (Physics2D.GetIgnoreCollision(projectileCollider, go.GetComponent<Collider2D>())) throw new Exception("Returned shuriken must hit enemies");
            UnityEngine.Object.DestroyImmediate(projectile);
            for(int i=-10;i<40;i++)
                if (Mathf.Abs(StageDirector.ChooseSpawnX(i,false)-i)<3.5f || Mathf.Abs(StageDirector.ChooseSpawnX(i,true)-i)<5.5f)
                    throw new Exception("Spawn must choose the farther entrance");
            UnityEngine.Object.DestroyImmediate(go);
            Vector2 velocity = new Vector2(10f, 0f);
            Vector2 redirected = CombatAimAssist.RedirectThrow(velocity, new Vector2(4f, 2f), 55f);
            if (Mathf.Abs(redirected.magnitude - velocity.magnitude) > 0.001f || redirected.y <= 0f)
                throw new Exception("Assist must change direction without changing speed");
            if (CombatAimAssist.RedirectThrow(velocity, Vector2.left, 55f) != velocity)
                throw new Exception("Target behind throw must not reverse it");
            if (CombatAimAssist.RedirectThrow(Vector2.right, Vector2.up, 55f) != Vector2.right)
                throw new Exception("Slow release must remain unchanged");
            if (CombatAimAssist.RedirectThrow(velocity, Vector2.zero, 55f) != velocity)
                throw new Exception("Overlapping target must not zero throw velocity");
            var rodObject = new GameObject("Attack restart check");
            var rod = rodObject.AddComponent<FishingRodController>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var stateField = typeof(FishingRodController).GetField("state", flags);
            var startField = typeof(FishingRodController).GetField("whipStarted", flags);
            var startMethod = typeof(FishingRodController).GetMethod("StartWhip", flags);
            foreach (string stateName in new[] { "Whipping", "Returning", "Hooked" })
            {
                stateField.SetValue(rod, Enum.Parse(stateField.FieldType, stateName)); startField.SetValue(rod, -123f);
                for(int i=0;i<10;i++) startMethod.Invoke(rod,null);
                if ((float)startField.GetValue(rod) != -123f || stateField.GetValue(rod).ToString() != stateName)
                    throw new Exception("Repeated clicks must not restart an active action");
            }
            rod.RodVisual = rodObject.transform;
            var tipObject = new GameObject("Recovery check tip");
            var tip = tipObject.AddComponent<Rigidbody2D>();
            typeof(FishingRodController).GetField("tipBody", flags).SetValue(rod, tip);
            var bufferField = typeof(FishingRodController).GetField("bufferedAttackUntil", flags);
            var returnMethod = typeof(FishingRodController).GetMethod("UpdateReturn", flags);
            stateField.SetValue(rod, Enum.Parse(stateField.FieldType, "Returning"));
            tip.position = Vector2.right * 7f;
            bufferField.SetValue(rod, Time.time - 1f);
            returnMethod.Invoke(rod, null);
            if (stateField.GetValue(rod).ToString() != "Returning")
                throw new Exception("Expired input must not start another attack");
            bufferField.SetValue(rod, Time.time + 0.25f);
            returnMethod.Invoke(rod, null);
            if (stateField.GetValue(rod).ToString() != "Whipping" ||
                !float.IsNegativeInfinity((float)bufferField.GetValue(rod)))
                throw new Exception("Queued attack must skip recovery and consume input once");
            UnityEngine.Object.DestroyImmediate(tipObject);
            UnityEngine.Object.DestroyImmediate(rodObject);
            var playerObject = new GameObject("포획 충돌 검사 플레이어");
            playerObject.AddComponent<PlayerController>();
            var playerCollider = playerObject.AddComponent<BoxCollider2D>();
            var heldObject = new GameObject("포획 충돌 검사 물체");
            var heldBody = heldObject.AddComponent<HookableBody>();
            var heldCollider = heldObject.AddComponent<BoxCollider2D>();
            heldBody.SetHooked(true);
            heldBody.SetHooked(true);
            if (!Physics2D.GetIgnoreCollision(heldCollider, playerCollider))
                throw new Exception("잡은 물체는 플레이어와 충돌하면 안 된다");
            heldBody.SetHooked(false);
            if (Physics2D.GetIgnoreCollision(heldCollider, playerCollider))
                throw new Exception("놓은 일반 물체는 원래 충돌 상태로 돌아가야 한다");
            var heldStar = heldObject.AddComponent<Shuriken>();
            typeof(Shuriken).GetMethod("Awake", flags).Invoke(heldStar, null);
            heldBody.SetHooked(true); heldBody.SetHooked(false);
            if (!Physics2D.GetIgnoreCollision(heldCollider, playerCollider))
                throw new Exception("되던진 수리검의 플레이어 충돌 제외는 유지되어야 한다");
            UnityEngine.Object.DestroyImmediate(heldObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
            var gasPlayerObject = new GameObject("독가스 검사 플레이어");
            var gasPlayer = gasPlayerObject.AddComponent<PlayerController>();
            typeof(PlayerController).GetMethod("Awake", flags).Invoke(gasPlayer, null);
            gasPlayer.Body.position = new Vector2(4f, 0f);
            var gasEnemyObject = new GameObject("독가스 검사 적");
            var gasBody = gasEnemyObject.AddComponent<HookableBody>();
            typeof(HookableBody).GetField("<Body>k__BackingField", flags).SetValue(gasBody, gasEnemyObject.GetComponent<Rigidbody2D>());
            var gasAI = gasEnemyObject.AddComponent<TangEnemyAI>();
            gasAI.Player = gasPlayer;
            typeof(TangEnemyAI).GetMethod("Start", flags).Invoke(gasAI, null);
            var gasTick = typeof(TangEnemyAI).GetMethod("FixedUpdate", flags);
            var gasState = typeof(TangEnemyAI).GetField("state", flags);
            var gasUntil = typeof(TangEnemyAI).GetField("until", flags);
            typeof(TangEnemyAI).GetField("cooldown", flags).SetValue(gasAI, Time.time + 100f);
            gasTick.Invoke(gasAI, null);
            if ((int)gasState.GetValue(gasAI) != 1 || Mathf.Abs((float)gasUntil.GetValue(gasAI) - Time.time - 2f) > 0.01f || gasBody.Body.linearVelocity.x <= 0f)
                throw new Exception("독가스는 반경 4에서 수리검 대기와 무관하게 2초 접근을 시작해야 한다");
            gasPlayer.Body.position = new Vector2(-4f, 0f);
            gasTick.Invoke(gasAI, null);
            if (gasBody.Body.linearVelocity.x >= 0f)
                throw new Exception("독가스 접근 중 플레이어 방향을 추적해야 한다");
            gasUntil.SetValue(gasAI, Time.time - 1f);
            gasTick.Invoke(gasAI, null);
            var gasCloud = UnityEngine.Object.FindFirstObjectByType<PoisonCloud>();
            if (!gasCloud || gasCloud.transform.position.x >= gasBody.Body.position.x || gasBody.Body.linearVelocity.x != 0f)
                throw new Exception("접근 후 정지하고 전방에 독가스를 생성해야 한다");
            typeof(PoisonCloud).GetMethod("Start", flags).Invoke(gasCloud, null);
            if (Mathf.Abs((float)typeof(PoisonCloud).GetField("expires", flags).GetValue(gasCloud) - Time.time - 3f) > 0.01f)
                throw new Exception("독가스는 3초 유지되어야 한다");
            gasTick.Invoke(gasAI, null);
            if ((int)gasState.GetValue(gasAI) == 1)
                throw new Exception("독가스 재사용 대기 중 즉시 다시 접근하면 안 된다");
            UnityEngine.Object.DestroyImmediate(gasCloud.gameObject);
            UnityEngine.Object.DestroyImmediate(gasEnemyObject);
            UnityEngine.Object.DestroyImmediate(gasPlayerObject);
            var smokeEnemy = new GameObject("반격 연기 검사 적");
            var smokeTarget = smokeEnemy.AddComponent<HookableBody>();
            smokeEnemy.AddComponent<BoxCollider2D>();
            smokeEnemy.AddComponent<CircleCollider2D>();
            typeof(HookableBody).GetField("<Body>k__BackingField", flags).SetValue(smokeTarget, smokeEnemy.GetComponent<Rigidbody2D>());
            typeof(HookableBody).GetField("visual", flags).SetValue(smokeTarget, smokeEnemy.GetComponent<SpriteRenderer>());
            smokeTarget.Configure(HookableBody.BodyKind.Enemy, Color.white, 100f, false);
            var smokePlayerObject = new GameObject("반격 연기 검사 플레이어");
            smokePlayerObject.AddComponent<PlayerController>();
            smokePlayerObject.AddComponent<BoxCollider2D>();
            var smokeBottleObject = new GameObject("반격 연기 검사 빈 병");
            var smokeBottleBody = smokeBottleObject.AddComponent<HookableBody>();
            smokeBottleBody.Configure(HookableBody.BodyKind.Prop, Color.cyan, 100f, false);
            smokeBottleObject.AddComponent<BoxCollider2D>();
            var smokeBottle = smokeBottleObject.AddComponent<PoisonBottle>();
            smokeBottleBody.SetHooked(true);
            PoisonCloud.Spawn(Vector2.zero, true);
            var counterCloud = UnityEngine.Object.FindFirstObjectByType<PoisonCloud>();
            typeof(PoisonCloud).GetMethod("Start", flags).Invoke(counterCloud, null);
            Physics2D.SyncTransforms();
            var cloudTick = typeof(PoisonCloud).GetMethod("FixedUpdate", flags);
            cloudTick.Invoke(counterCloud, null);
            if (Mathf.Abs(smokeTarget.HealthFraction - 0.92f) > 0.001f)
                throw new Exception("반격 연기는 콜라이더 수와 무관하게 적에게 주기당 8 피해를 줘야 한다");
            cloudTick.Invoke(counterCloud, null);
            if (Mathf.Abs(smokeTarget.HealthFraction - 0.92f) > 0.001f || smokePlayerObject.GetComponent<PoisonStatus>() || smokeBottle.Filled)
                throw new Exception("반격 연기는 주기를 지키고 플레이어 중독이나 병 재충전을 일으키지 않아야 한다");
            typeof(PoisonCloud).GetField("nextTick", flags).SetValue(counterCloud, Time.time - 1f);
            cloudTick.Invoke(counterCloud, null);
            if (Mathf.Abs(smokeTarget.HealthFraction - 0.84f) > 0.001f)
                throw new Exception("다음 피해 주기에 적에게 지속 피해가 적용되어야 한다");
            UnityEngine.Object.DestroyImmediate(counterCloud.gameObject);
            UnityEngine.Object.DestroyImmediate(smokeEnemy);
            UnityEngine.Object.DestroyImmediate(smokeBottleObject);
            UnityEngine.Object.DestroyImmediate(smokePlayerObject);
            Debug.Log("COMBAT CHECKS PASSED");
        }
    }
}
#endif
