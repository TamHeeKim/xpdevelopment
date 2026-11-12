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
            PlayerCombatChecks.Run();
            UnityEngine.Object.DestroyImmediate(go);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
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
            EnemyCombatChecks.Run();
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
