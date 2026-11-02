using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FishingPrototype.Integration
{
    [Serializable]
    public struct CombatStatValues
    {
        public string actorId;
        public float hp, attack, defense, moveSpeed;
        public int chapter;
        public string faction, sourceName;

        public static CombatStatValues Parse(Dictionary<string, string> row, bool enemy)
        {
            var result = new CombatStatValues
            {
                actorId = row[enemy ? "enemyId" : "playerId"],
                hp = Number(row, "hp"), attack = Number(row, "attack"),
                defense = Number(row, "defense"), moveSpeed = Number(row, "moveSpeed"),
                faction = enemy ? row["faction"] : "", sourceName = enemy ? row["name"] : ""
            };
            if (result.hp <= 0) throw new FormatException("hp must be positive: " + result.actorId);
            if (enemy && (!int.TryParse(row["chapter"], NumberStyles.Integer, CultureInfo.InvariantCulture, out result.chapter) || result.chapter < 0))
                throw new FormatException("Invalid chapter: " + result.actorId);
            return result;
        }

        private static float Number(Dictionary<string, string> row, string key)
        {
            if (!float.TryParse(row[key], NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
                || float.IsNaN(number) || float.IsInfinity(number) || number < 0)
                throw new FormatException("Invalid " + key + ": " + row[key]);
            return number;
        }
    }

    [CreateAssetMenu(menuName = "Fishing Combat/Sheet Stats", fileName = "Data_Stats")]
    public sealed class CombatStatDefinition : ScriptableObject
    {
        [Header("시트 원본")]
        [SerializeField, Tooltip("적 테이블이면 활성화합니다. 가져오기 시 자동 설정됩니다.")]
        private bool enemyTable;
        [Header("가져온 스탯")]
        [SerializeField, Tooltip("시트 원본 스탯입니다. 현재 체력은 적 인스턴스에서 관리합니다.")]
        private CombatStatValues values;

        public bool IsEnemy => enemyTable;
        public CombatStatValues Values => values;
        public event Action Changed;

        public bool Apply(CombatStatValues incoming, bool enemy)
        {
            if (enemyTable == enemy && values.Equals(incoming)) return false;
            enemyTable = enemy;
            values = incoming;
            Changed?.Invoke();
            return true;
        }
    }
}
