using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace FishingPrototype
{
    /// <summary>UTF-8 CSV의 텍스트 키를 표시 언어로 조회한다. 번역이 비어 있으면 한국어를 사용한다.</summary>
    public static class CombatText
    {
        public static string Language { get; set; } = "ko";
        private static string loadedLanguage;
        private static Dictionary<string, string> messages;

        public static string Get(string key)
        {
            if (messages == null || loadedLanguage != Language)
            {
                var asset = Resources.Load<TextAsset>("Data_CombatText");
                if (!asset) throw new InvalidOperationException("Data_CombatText.csv 파일이 없습니다.");
                messages = Parse(asset.text, Language);
                loadedLanguage = Language;
            }
            if (!messages.TryGetValue(key, out string value))
                throw new KeyNotFoundException("번역 키가 없습니다: " + key);
            return value;
        }

        public static string Format(string key, params object[] values) =>
            string.Format(CultureInfo.CurrentCulture, Get(key), values);

        // 쉼표, 따옴표 이스케이프, 셀 내부 줄바꿈을 포함한 스프레드시트 CSV를 읽는다.
        public static Dictionary<string, string> Parse(string csv, string language)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false, closedQuote = false;
            csv = csv.TrimStart('\uFEFF');
            for (int i = 0; i < csv.Length; i++)
            {
                char ch = csv[i];
                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; }
                        else { quoted = false; closedQuote = true; }
                    }
                    else cell.Append(ch);
                    continue;
                }
                if (ch == ',' || ch == '\r' || ch == '\n')
                {
                    row.Add(cell.ToString()); cell.Clear(); closedQuote = false;
                    if (ch != ',')
                    {
                        if (row.Count != 1 || row[0].Length != 0) rows.Add(row);
                        row = new List<string>();
                        if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    }
                }
                else if (ch == '"' && cell.Length == 0 && !closedQuote) quoted = true;
                else
                {
                    if (closedQuote || ch == '"') throw new FormatException("CSV 따옴표 형식이 잘못되었습니다.");
                    cell.Append(ch);
                }
            }
            if (quoted) throw new FormatException("CSV 따옴표가 닫히지 않았습니다.");
            if (row.Count > 0 || cell.Length > 0 || closedQuote)
            { row.Add(cell.ToString()); rows.Add(row); }
            if (rows.Count == 0 || rows[0].Count < 2 || rows[0][0] != "key" || rows[0][1] != "ko")
                throw new FormatException("CSV 헤더는 key,ko로 시작해야 합니다.");
            if (new HashSet<string>(rows[0]).Count != rows[0].Count)
                throw new FormatException("CSV 언어 열이 중복되었습니다.");
            int column = rows[0].IndexOf(language);
            if (column < 1) column = 1;
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < rows.Count; i++)
            {
                var values = rows[i];
                if (values.Count != rows[0].Count || string.IsNullOrWhiteSpace(values[0]) || string.IsNullOrWhiteSpace(values[1]))
                    throw new FormatException("CSV 행의 키·한국어·열 수를 확인하세요: " + (i + 1));
                string value = string.IsNullOrEmpty(values[column]) ? values[1] : values[column];
                if (!result.TryAdd(values[0], value)) throw new FormatException("CSV 키가 중복되었습니다: " + values[0]);
            }
            return result;
        }
    }
}
