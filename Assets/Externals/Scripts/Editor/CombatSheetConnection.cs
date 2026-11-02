using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FishingPrototype.Integration;
using UnityEditor;
using UnityEngine;

namespace FishingPrototype.Editor
{
    /// <summary>Editor-only spreadsheet to persistent SO import and Play mode polling.</summary>
    [InitializeOnLoad]
    public static class CombatSheetConnection
    {
        public const string assetFolder = "Assets/Externals/Data/Stats";
        private const double refreshSeconds = 5;
        private static bool downloading;
        private static bool connected;
        internal static bool pausePolling;
        internal static bool IsRefreshing => downloading;
        internal static int SuccessfulRefreshCount { get; private set; }
        private static double nextRefresh;
        private static CancellationTokenSource cancellation = new CancellationTokenSource();

        static CombatSheetConnection()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload += () => cancellation.Cancel();
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                connected = false;
                SuccessfulRefreshCount = 0;
                nextRefresh = 0;
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                cancellation.Cancel();
                cancellation = new CancellationTokenSource();
            }
        }

        private static void Tick()
        {
            if (pausePolling || !EditorApplication.isPlaying || EditorApplication.isCompiling || downloading
                || EditorApplication.timeSinceStartup < nextRefresh) return;
            Download();
        }

        [MenuItem("낚싯대 전투/데이터 테이블/원본 시트 열기")]
        public static void OpenSheet() => Application.OpenURL(
            "https://docs.google.com/spreadsheets/d/" + CombatSheetData.spreadsheetId + "/edit");

        [MenuItem("낚싯대 전투/데이터 테이블/시트에서 SO 가져오기")]
        public static async void Download()
        {
            if (downloading) return;
            downloading = true;
            CancellationToken token = cancellation.Token;
            if (!connected) Debug.Log("[Sheet Connection] Connecting; Play mode polling interval: 5 seconds.");
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
                {
                    string nonce = "&refresh=" + DateTime.UtcNow.Ticks;
                    var responses = await Task.WhenAll(
                        client.GetAsync(CombatSheetData.ExportUrl(8201) + nonce, token),
                        client.GetAsync(CombatSheetData.ExportUrl(8202) + nonce, token));
                    using (var player = responses[0])
                    using (var enemy = responses[1])
                    {
                        player.EnsureSuccessStatusCode(); enemy.EnsureSuccessStatusCode();
                        string playerCsv = await player.Content.ReadAsStringAsync();
                        string enemyCsv = await enemy.Content.ReadAsStringAsync();
                        token.ThrowIfCancellationRequested();
                        int changed = ImportCsv(playerCsv, enemyCsv);
                        if (!connected && changed == 0)
                        {
                            CombatSheetData.LogStats("Player", CombatSheetData.Parse(playerCsv, CombatSheetData.playerHeader));
                            CombatSheetData.LogStats("Enemy", CombatSheetData.Parse(enemyCsv, CombatSheetData.enemyHeader));
                        }
                        if (!connected || changed > 0)
                            Debug.Log($"[Sheet Connection] Connected; {changed} SO asset(s) updated. Watching for spreadsheet changes.");
                        connected = true;
                        SuccessfulRefreshCount++;
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                connected = false;
                Debug.LogWarning("[Sheet Connection] Refresh failed; keeping previous SO stats and retrying: " + exception.Message);
            }
            finally
            {
                downloading = false;
                nextRefresh = EditorApplication.timeSinceStartup + refreshSeconds;
            }
        }

        public static int ImportCsv(string playerCsv, string enemyCsv)
        {
            var players = CombatSheetData.Parse(playerCsv, CombatSheetData.playerHeader);
            var enemies = CombatSheetData.Parse(enemyCsv, CombatSheetData.enemyHeader);
            // Validate both complete tables before changing any SO.
            var playerValues = players.ConvertAll(row => CombatStatValues.Parse(row, false));
            var enemyValues = enemies.ConvertAll(row => CombatStatValues.Parse(row, true));
            int changed = ImportRows(playerValues, false) + ImportRows(enemyValues, true);
            if (changed > 0) AssetDatabase.SaveAssets();
            return changed;
        }

        private static int ImportRows(List<CombatStatValues> rows, bool enemy)
        {
            string folder = assetFolder + (enemy ? "/Enemy" : "/Player");
            EnsureFolder(folder);
            int changed = 0;
            foreach (var row in rows)
            {
                string path = folder + "/Data_" + Uri.EscapeDataString(row.actorId) + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<CombatStatDefinition>(path);
                bool created = !asset;
                if (created) asset = ScriptableObject.CreateInstance<CombatStatDefinition>();
                if (!asset.Apply(row, enemy)) continue;
                if (created) AssetDatabase.CreateAsset(asset, path);
                EditorUtility.SetDirty(asset);
                changed++;
                Debug.Log($"[Sheet Stats] SO={asset.name}, id={row.actorId}, hp={row.hp}, attack={row.attack}, defense={row.defense}, moveSpeed={row.moveSpeed}, chapter={row.chapter}, faction={row.faction}, name={row.sourceName}", asset);
            }
            return changed;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            string parent = path.Substring(0, split);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }
    }
}
