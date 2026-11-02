using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using FishingPrototype.Integration;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace FishingPrototype.Editor
{
    public static class IntegrationChecks
    {
        private static bool running;

        [MenuItem("낚싯대 전투/데이터 테이블/Integration F키 검사 (Play)")]
        public static async void Run()
        {
            if (running || !EditorApplication.isPlaying) return;
            var enemy = UnityEngine.Object.FindFirstObjectByType<IntegrationEnemy>();
            if (!enemy || !enemy.IsReady) { Debug.LogError("[Integration Check] Wait for sheet stats to load."); return; }
            running = true;
            bool background = Application.runInBackground;
            Keyboard original = Keyboard.current;
            Keyboard keyboard = null;
            try
            {
                Application.runInBackground = true;
                double pollingDeadline = EditorApplication.timeSinceStartup + 60;
                while (CombatSheetConnection.SuccessfulRefreshCount < 2)
                {
                    if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > pollingDeadline)
                        throw new Exception("Automatic Play mode polling did not complete two successful refreshes.");
                    await Task.Delay(50);
                }
                CombatSheetConnection.pausePolling = true;
                double deadline = EditorApplication.timeSinceStartup + 45;
                while (CombatSheetConnection.IsRefreshing)
                {
                    if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Sheet refresh did not finish.");
                    await Task.Delay(20);
                }
                CheckSoRefresh(enemy);
                float before = enemy.Health;
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                await NextFrames();
                float expected = Mathf.Max(0f, before - 10f);
                if (enemy.Health != expected) throw new Exception($"F input: expected {expected}, got {enemy.Health}.");
                await NextFrames();
                if (enemy.Health != expected) throw new Exception("Held F repeated damage.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                await NextFrames();
                int presses = Mathf.CeilToInt(enemy.Health / 10f) + 1;
                for (int i = 0; i < presses; i++)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                    await NextFrames();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    await NextFrames();
                }
                if (enemy.Health != 0f) throw new Exception("Enemy health must stop at zero.");
                Debug.Log("[Integration Check] PASS: automatic live polling completed twice, SO import and reference preserved, changed stats propagated with damage preserved, unchanged import skipped, invalid data rejected, F deals 10 once per press, HP clamps at zero.");
            }
            catch (Exception exception) { Debug.LogError("[Integration Check] FAIL: " + exception.Message); }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (original != null && original.added) original.MakeCurrent();
                Application.runInBackground = background;
                CombatSheetConnection.pausePolling = false;
                running = false;
            }
        }

        private static void CheckSoRefresh(IntegrationEnemy enemy)
        {
            var players = new List<CombatStatValues>();
            var enemies = new List<CombatStatValues>();
            foreach (string guid in AssetDatabase.FindAssets("t:CombatStatDefinition", new[] { CombatSheetConnection.assetFolder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<CombatStatDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                (asset.IsEnemy ? enemies : players).Add(asset.Values);
            }
            string playerCsv = Csv(players, false);
            string originalCsv = Csv(enemies, true);
            var definition = enemy.StatDefinition;
            string originalGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(definition));
            try
            {
                if (CombatSheetConnection.ImportCsv(playerCsv, originalCsv) != 0)
                    throw new Exception("Unchanged SO import must be skipped.");
                enemy.ApplyTestDamage();
                float damagedHealth = enemy.Health;
                float max = enemy.MaxHealth;
                int index = enemies.FindIndex(row => row.actorId == definition.Values.actorId);
                var changed = enemies[index];
                changed.hp += 20; changed.attack += 3;
                enemies[index] = changed;
                string changedCsv = Csv(enemies, true);
                if (CombatSheetConnection.ImportCsv(playerCsv, changedCsv) != 1 || enemy.MaxHealth != max + 20
                    || enemy.Health != damagedHealth + 20 || enemy.Attack != changed.attack || enemy.StatDefinition != definition
                    || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(definition)) != originalGuid)
                    throw new Exception("SO update must propagate stats and preserve damage and asset reference.");
                if (CombatSheetConnection.ImportCsv(playerCsv, changedCsv) != 0)
                    throw new Exception("Repeated import changed an unchanged SO.");
                enemies[index] = new CombatStatValues { actorId = changed.actorId, hp = float.NaN };
                bool rejected = false;
                try { CombatSheetConnection.ImportCsv(playerCsv, Csv(enemies, true)); }
                catch (FormatException) { rejected = true; }
                if (!rejected || enemy.MaxHealth != changed.hp) throw new Exception("Invalid data must preserve previous SO values.");
            }
            finally { CombatSheetConnection.ImportCsv(playerCsv, originalCsv); }
        }

        private static string Csv(List<CombatStatValues> rows, bool enemy)
        {
            var lines = new List<string> { enemy ? CombatSheetData.enemyHeader : CombatSheetData.playerHeader };
            foreach (var row in rows)
            {
                var cells = new List<string> { row.actorId };
                if (enemy) cells.AddRange(new[] { row.chapter.ToString(CultureInfo.InvariantCulture), row.faction, row.sourceName });
                foreach (float number in new[] { row.hp, row.attack, row.defense, row.moveSpeed })
                    cells.Add(number.ToString("R", CultureInfo.InvariantCulture));
                lines.Add(string.Join(",", cells.ConvertAll(cell => "\"" + (cell ?? "").Replace("\"", "\"\"") + "\"")));
            }
            return string.Join("\n", lines);
        }

        private static async Task NextFrames()
        {
            int start = Time.frameCount;
            double deadline = EditorApplication.timeSinceStartup + 10;
            while (Time.frameCount < start + 3)
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                    throw new Exception("Play mode stopped or frames did not advance.");
                await Task.Delay(20);
            }
        }
    }
}
