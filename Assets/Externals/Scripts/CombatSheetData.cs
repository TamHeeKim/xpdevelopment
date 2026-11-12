using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FishingPrototype.Integration
{
    public static class CombatSheetData
    {
        public const string spreadsheetId = "1TyjqkiLVNg_shUpMejZh3StgU6b0RJoVzFeIXk8NIrs";
        public const string playerHeader = "playerId,hp,attack,defense,moveSpeed";
        public const string enemyHeader = "enemyId,chapter,faction,name,hp,attack,defense,moveSpeed";

        public static string ExportUrl(int sheetId) =>
            "https://docs.google.com/spreadsheets/d/" + spreadsheetId + "/export?format=csv&gid=" + sheetId;

        public static List<Dictionary<string, string>> Parse(string csv, string expectedHeader)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            csv = csv.TrimStart('\uFEFF');
            for (int i = 0; i < csv.Length; i++)
            {
                char ch = csv[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (!quoted && (ch == ',' || ch == '\r' || ch == '\n'))
                {
                    row.Add(cell.ToString()); cell.Clear();
                    if (ch != ',')
                    {
                        if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                        rows.Add(row); row = new List<string>();
                    }
                }
                else cell.Append(ch);
            }
            if (quoted) throw new FormatException("Unclosed CSV quote.");
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            string[] required = expectedHeader.Split(',');
            if (rows.Count == 0 || rows[0].Count < required.Length) throw new FormatException("Missing sheet headers.");
            for (int i = 0; i < required.Length; i++)
                if (rows[0][i] != required[i]) throw new FormatException("Unexpected sheet header: " + expectedHeader);
            while (rows[0].Count > required.Length && rows[0][rows[0].Count - 1] == "") rows[0].RemoveAt(rows[0].Count - 1);
            string[] headers = rows[0].ToArray();
            var names = new HashSet<string>();
            for (int i = 0; i < headers.Length; i++)
            {
                if (!names.Add(headers[i])) throw new FormatException("Duplicate sheet column: " + headers[i]);
                if (string.IsNullOrWhiteSpace(headers[i])) throw new FormatException("Empty sheet column.");
            }
            var result = new List<Dictionary<string, string>>();
            var ids = new HashSet<string>();
            for (int i = 1; i < rows.Count; i++)
            {
                if (rows[i].TrueForAll(string.IsNullOrWhiteSpace)) continue;
                if (rows[i].Count < headers.Length || string.IsNullOrWhiteSpace(rows[i][0]))
                    throw new FormatException("Incomplete CSV row: " + (i + 1));
                if (!ids.Add(rows[i][0])) throw new FormatException("Duplicate table ID: " + rows[i][0]);
                var values = new Dictionary<string, string>();
                for (int j = 0; j < headers.Length; j++) values.Add(headers[j], rows[i][j]);
                result.Add(values);
            }
            if (result.Count == 0) throw new FormatException("Sheet contains no stat rows.");
            return result;
        }

        public static void LogStats(string table, List<Dictionary<string, string>> rows)
        {
            foreach (var row in rows)
            {
                var values = new List<string>();
                foreach (var pair in row) values.Add(pair.Key + "=" + pair.Value);
                Debug.Log("[Sheet Stats] " + table + ": " + string.Join(", ", values));
            }
        }
    }
}
