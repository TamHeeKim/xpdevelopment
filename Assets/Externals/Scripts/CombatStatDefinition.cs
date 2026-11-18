using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FishingPrototype.Integration
{
    [Serializable]
    public struct CombatStatValues
    {
        [Header("공통 수치")]
        public string actorId;
        public float hp, attack, defense, moveSpeed;
        public float attackWidth, catchRange;
        [HideInInspector] public bool revisedCombat;
        [Header("플레이어 기본 채찍")]
        public float minAttackRange, maxAttackRange, minAttackTime, maxAttackTime;
        [Header("플레이어 포획과 기술")]
        public float catchInputTimeout, holdDistance, holdHeight, slamRange, throwAngle, throwRange;
        [Header("이동과 피격")]
        public float jumpPower, invulnTime, hitLock, hitStunTime;
        [Header("적 행동")]
        public float detectionRange, attackRange, attackWindupTime, attackRecoveryTime, sprayDuration, gasRange;
        public float minKeepDistance, maxKeepDistance;
        public int chapter;
        public string faction, sourceName;

        public static CombatStatValues Parse(Dictionary<string, string> row, bool enemy)
        {
            var result = new CombatStatValues
            {
                actorId = row[enemy ? "enemyId" : "playerId"],
                hp = Number(row, "hp"), attack = Number(row, "attack"),
                defense = Number(row, "defense"), moveSpeed = Number(row, "moveSpeed"),
                attackWidth = row.ContainsKey("attackWidth") ? Number(row, "attackWidth") : 0f,
                catchRange = row.ContainsKey("catchRange") ? Number(row, "catchRange") : 0f,
                revisedCombat = true,
                minAttackRange = Optional(row, "minAttackRange", 2f), maxAttackRange = Optional(row, "maxAttackRange", 10f),
                minAttackTime = Optional(row, "minAttackTime", 0.35f), maxAttackTime = Optional(row, "maxAttackTime", 0.8f),
                catchInputTimeout = Optional(row, "catchInputTimeout", 2f),
                holdDistance = Optional(row, "holdDistance", 2.5f), holdHeight = Optional(row, "holdHeight", 1f),
                slamRange = Optional(row, "slamRange", 4f), throwAngle = Optional(row, "throwAngle", 60f), throwRange = Optional(row, "throwRange", 10f),
                jumpPower = Optional(row, "jumpPower", 10.5f), invulnTime = Optional(row, "invulnTime", 0.5f), hitLock = Optional(row, "hitLock", 0.15f),
                hitStunTime = Optional(row, "hitStunTime", 0.18f), detectionRange = Optional(row, "detectionRange", 12f),
                attackRange = Optional(row, "attackRange", 4f), attackWindupTime = Optional(row, "attackWindupTime", 0.6f),
                attackRecoveryTime = Optional(row, "attackRecoveryTime", 1.5f), sprayDuration = Optional(row, "sprayDuration", 2f), gasRange = Optional(row, "gasRange", 4f),
                minKeepDistance = Optional(row, "minKeepDistance", 3f), maxKeepDistance = Optional(row, "maxKeepDistance", 5f),
                faction = enemy ? row["faction"] : "", sourceName = enemy ? row["name"] : ""
            };
            if (result.hp <= 0) throw new FormatException("hp must be positive: " + result.actorId);
            if (enemy && (!int.TryParse(row["chapter"], NumberStyles.Integer, CultureInfo.InvariantCulture, out result.chapter) || result.chapter < 0))
                throw new FormatException("Invalid chapter: " + result.actorId);
            if (result.maxAttackRange <= result.minAttackRange || result.minAttackTime <= 0 || result.maxAttackTime < result.minAttackTime
                || result.maxKeepDistance < result.minKeepDistance || result.throwAngle > 360)
                throw new FormatException("Invalid combat ranges/times: " + result.actorId);
            return result;
        }

        private static float Optional(Dictionary<string, string> row, string key, float fallback) =>
            row.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value) ? Number(row, key) : fallback;

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
