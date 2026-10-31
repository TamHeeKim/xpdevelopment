#if UNITY_EDITOR
using System;
using UnityEngine;
namespace FishingPrototype.Editor
{
    public static class CombatChecks
    {
        private static void CheckSpawnPoints()
        {
            var root = new GameObject("Test_SpawnPoints");
            try
            {
                var stage = root.AddComponent<StageDirector>();
                var playerObject = new GameObject("Test_Player");
                playerObject.transform.SetParent(root.transform);
                stage.player = playerObject.AddComponent<PlayerController>();
                var points = new Transform[4];
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = new GameObject("Point_" + i).transform;
                    points[i].SetParent(root.transform);
                    points[i].position = new Vector2(i * 13f - 7f, i + 2f);
                }
                stage.firstLeftSpawn = points[0]; stage.firstRightSpawn = points[1];
                stage.secondLeftSpawn = points[2]; stage.secondRightSpawn = points[3];
                var choose = typeof(StageDirector).GetMethod("ChooseSpawnPoint", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                for (int wave = 0; wave < 2; wave++)
                {
                    Vector2 left = points[wave * 2].position, right = points[wave * 2 + 1].position;
                    foreach (float x in new[] { left.x, right.x, (left.x + right.x) / 2f })
                    {
                        stage.player.transform.position = new Vector2(x, 0f);
                        Vector2 expected = Mathf.Abs(x - left.x) >= Mathf.Abs(x - right.x) ? left : right;
                        var actual = (Vector2)choose.Invoke(stage, new object[] { wave == 1 });
                        if (actual != expected) throw new Exception("출현 위치는 편집된 마커 중 먼 쪽을 선택하고 높이를 유지해야 한다.");
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CheckHeldTip()
        {
            var player = UnityEngine.Object.Instantiate(CombatSceneBuilder.Prefab("Player"));
            var prop = UnityEngine.Object.Instantiate(CombatSceneBuilder.Prefab("Rock"));
            try
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var rod = player.GetComponentInChildren<FishingRodController>();
                typeof(FishingRodController).GetMethod("Start", flags).Invoke(rod, null);
                var target = prop.GetComponent<HookableBody>();
                typeof(HookableBody).GetMethod("Awake", flags).Invoke(target, null);
                typeof(FishingRodController).GetMethod("Hook", flags).Invoke(rod, new object[] { target });
                prop.transform.position = new Vector3(3f, 2f, 0f);
                var lateUpdate = typeof(FishingRodController).GetMethod("LateUpdate", flags);
                lateUpdate.Invoke(rod, null);
                var tip = (SpriteRenderer)typeof(FishingRodController).GetField("tipVisual", flags).GetValue(rod);
                var line = (LineRenderer)typeof(FishingRodController).GetField("line", flags).GetValue(rod);
                if (tip.enabled || Vector3.Distance(line.GetPosition(line.positionCount - 1), prop.transform.position) > 0.001f)
                    throw new Exception("포획 중 찌는 숨기고 줄 끝은 물체 표시 위치에 연결해야 한다.");
                rod.Interrupt(); lateUpdate.Invoke(rod, null);
                if (!tip.enabled) throw new Exception("포획 해제 후 찌 표시를 복원해야 한다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(prop); UnityEngine.Object.DestroyImmediate(player); }
        }

        public static void Run()
        {
            var go = new GameObject("Test_Combatcheck");
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
            target.throwDamageDuration = 0f;
            target.SetHooked(false);
            if (target.IsThrown) throw new Exception("Expired throw must not deal impact damage");
            CombatShapes.DressEnemy(go, true);
            if (go.GetComponentsInChildren<SpriteRenderer>().Length < 8) throw new Exception("Enemy silhouette missing parts");
            CombatShapes.DressShuriken(go);
            if (go.GetComponent<SpriteRenderer>().sprite.texture.GetPixel(0,0).a != 0f || go.GetComponent<SpriteRenderer>().sprite.texture.GetPixel(32,32).a < 0.9f) throw new Exception("Shuriken must have four blades");
            var model = CombatShapes.DressPlayer(go);
            if (!model.Find("Sprite_모자") || !model.Find("Sprite_왼쪽장화") || go.GetComponent<SpriteRenderer>().sprite != null)
                throw new Exception("Player appearance must replace the rectangle with a complete character");
            var bottle = go.AddComponent<PoisonBottle>();
            bottle.Fill();
            if (!bottle.Filled || target.baseColor != Color.green) throw new Exception("Bottle capture failed");
            target.Configure(HookableBody.BodyKind.Prop, Color.green, 100f, false);
            target.ReceiveDamage(10000f);
            if (target.HealthFraction != 1f) throw new Exception("일반 오브젝트는 피해로 파괴되지 않아야 한다");
            target.throwDamageDuration = 2f;
            target.SetHooked(true); target.SetHooked(false);
            typeof(HookableBody).GetField("<Body>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, go.GetComponent<Rigidbody2D>());
            target.Body.position = new Vector2(20f, -20f);
            target.Body.linearVelocity = Vector2.down * 10f;
            typeof(HookableBody).GetMethod("FixedUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, null);
            if (target.Body.position.y < -12f || target.Body.linearVelocity != Vector2.zero || target.IsThrown)
                throw new Exception("맵 아래로 떨어진 오브젝트는 투척 상태를 해제하고 복귀해야 한다");
            target.kind = HookableBody.BodyKind.Enemy;
            var poison = go.AddComponent<PoisonStatus>(); poison.Apply();
            if (Mathf.Abs(poison.until - Time.time - 2f) > 0.01f) throw new Exception("Poison residual must last two seconds");
            var projectile = new GameObject("Test_Projectilecollisioncheck");
            projectile.AddComponent<HookableBody>();
            var projectileCollider = projectile.AddComponent<BoxCollider2D>();
            var propObject = new GameObject("Test_ShurikenProp");
            var prop = propObject.AddComponent<HookableBody>(); prop.kind = HookableBody.BodyKind.Prop;
            var propCollider = propObject.AddComponent<BoxCollider2D>();
            var star = projectile.AddComponent<Shuriken>();
            typeof(Shuriken).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(star, null);
            star.ConfigureEnemy(go.GetComponent<Collider2D>());
            if (!Physics2D.GetIgnoreCollision(projectileCollider, go.GetComponent<Collider2D>())) throw new Exception("Enemy shuriken must pass through allies");
            if (!Physics2D.GetIgnoreCollision(projectileCollider, propCollider)) throw new Exception("수리검은 일반 오브젝트를 통과해야 한다.");
            star.Reflect();
            if (!Physics2D.GetIgnoreCollision(projectileCollider, propCollider)) throw new Exception("되던진 수리검도 일반 오브젝트를 통과해야 한다.");
            UnityEngine.Object.DestroyImmediate(propObject);
            if (Physics2D.GetIgnoreCollision(projectileCollider, go.GetComponent<Collider2D>())) throw new Exception("Returned shuriken must hit enemies");
            UnityEngine.Object.DestroyImmediate(projectile);
            CheckSpawnPoints();
            CheckHeldTip();
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
            var rodObject = new GameObject("Test_Attackrestartcheck");
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
            rod.rodVisual = rodObject.transform;
            var tipObject = new GameObject("Test_Recoverychecktip");
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
            var playerObject = new GameObject("Test_포획충돌검사플레이어");
            playerObject.AddComponent<PlayerController>();
            var playerCollider = playerObject.AddComponent<BoxCollider2D>();
            var heldObject = new GameObject("Test_포획충돌검사물체");
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
            var gasPlayerObject = new GameObject("Test_독가스검사플레이어");
            var gasPlayer = gasPlayerObject.AddComponent<PlayerController>();
            typeof(PlayerController).GetMethod("Awake", flags).Invoke(gasPlayer, null);
            gasPlayer.Body.position = new Vector2(4f, 0f);
            var gasEnemyObject = new GameObject("Test_독가스검사적");
            var gasBody = gasEnemyObject.AddComponent<HookableBody>();
            typeof(HookableBody).GetField("<Body>k__BackingField", flags).SetValue(gasBody, gasEnemyObject.GetComponent<Rigidbody2D>());
            var gasAI = gasEnemyObject.AddComponent<TangEnemyAI>();
            gasAI.player = gasPlayer;
            gasAI.poisonCloudPrefab = CombatSceneBuilder.Prefab("EnemyPoison").GetComponent<PoisonCloud>();
            gasAI.shurikenPrefab = CombatSceneBuilder.Prefab("Shuriken").GetComponent<Shuriken>();
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
            var smokeEnemy = new GameObject("Test_반격연기검사적");
            var smokeTarget = smokeEnemy.AddComponent<HookableBody>();
            smokeEnemy.AddComponent<BoxCollider2D>();
            smokeEnemy.AddComponent<CircleCollider2D>();
            typeof(HookableBody).GetField("<Body>k__BackingField", flags).SetValue(smokeTarget, smokeEnemy.GetComponent<Rigidbody2D>());
            typeof(HookableBody).GetField("visual", flags).SetValue(smokeTarget, smokeEnemy.GetComponent<SpriteRenderer>());
            smokeTarget.Configure(HookableBody.BodyKind.Enemy, Color.white, 100f, false);
            var smokePlayerObject = new GameObject("Test_반격연기검사플레이어");
            smokePlayerObject.AddComponent<PlayerController>();
            smokePlayerObject.AddComponent<BoxCollider2D>();
            var smokeBottleObject = new GameObject("Test_반격연기검사빈병");
            var smokeBottleBody = smokeBottleObject.AddComponent<HookableBody>();
            smokeBottleBody.Configure(HookableBody.BodyKind.Prop, Color.cyan, 100f, false);
            smokeBottleObject.AddComponent<BoxCollider2D>();
            var smokeBottle = smokeBottleObject.AddComponent<PoisonBottle>();
            smokeBottleBody.SetHooked(true);
            PoisonCloud.Spawn(CombatSceneBuilder.Prefab("CounterPoison").GetComponent<PoisonCloud>(), Vector2.zero);
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
            var csv = "\uFEFFkey,ko,en\r\nhello,안녕,Hello\r\ncomplex,\"쉼표, 그리고 \"\"인용\"\"\n다음 줄\",\r\n";
            var korean = CombatText.Parse(csv, "ko");
            var english = CombatText.Parse(csv, "en");
            if (english["hello"] != "Hello" || english["complex"] != korean["complex"] ||
                korean["complex"] != "쉼표, 그리고 \"인용\"\n다음 줄")
                throw new Exception("CSV 따옴표·줄바꿈·번역 대체 처리가 잘못되었습니다.");
            foreach (string invalid in new[] { "key,ko\na,가\na,나", "key,ko\na,\"닫히지 않음", "key,ko\na,", "key,ko\na,가,초과" })
            {
                bool rejected = false;
                try { CombatText.Parse(invalid, "ko"); } catch (FormatException) { rejected = true; }
                if (!rejected) throw new Exception("잘못된 CSV를 허용하면 안 됩니다.");
            }
            if (CombatText.Get("player.name") != "낚시꾼" || !CombatText.Get("hud.help_body").Contains("\n"))
                throw new Exception("게임용 CSV를 불러오지 못했습니다.");
            var namedObject = new GameObject("Test_DisplayName");
            var namedBody = namedObject.AddComponent<HookableBody>();
            namedBody.SetDisplayNameKey("object.bottle");
            if (namedObject.name != "Test_DisplayName" || namedBody.DisplayName != "호리병")
                throw new Exception("내부 오브젝트 이름과 표시 이름은 분리되어야 합니다.");
            UnityEngine.Object.DestroyImmediate(namedObject);
            Debug.Log("COMBAT CHECKS PASSED");
        }
    }
}
#endif
