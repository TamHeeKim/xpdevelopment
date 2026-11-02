using UnityEngine;
using UnityEngine.InputSystem;

namespace FishingPrototype.Integration
{
    /// <summary>Uses an assigned SO; current health belongs to this enemy instance.</summary>
    public sealed class IntegrationEnemy : MonoBehaviour
    {
        [Header("스탯 SO")]
        [SerializeField, Tooltip("적 테이블에서 생성한 스탯 SO를 할당합니다.")]
        private CombatStatDefinition statDefinition;
        // Keep the old ID for migrating scene references through the setup command.
        [SerializeField, HideInInspector] private string enemyId = "ENEMY_CH01_DANGGA_001";

        public CombatStatDefinition StatDefinition => statDefinition;
        public string SourceEnemyId => enemyId;
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public float Attack => statDefinition ? statDefinition.Values.attack : 0;
        public float Defense => statDefinition ? statDefinition.Values.defense : 0;
        public float MoveSpeed => statDefinition ? statDefinition.Values.moveSpeed : 0;
        public bool IsReady { get; private set; }
        public string ConnectionStatus => IsReady ? "Assigned SO loaded" : "Missing enemy stat SO";

        private void OnEnable()
        {
            IsReady = false;
            if (!statDefinition || !statDefinition.IsEnemy || statDefinition.Values.hp <= 0)
            {
                Debug.LogError("[Integration Enemy] Assign an enemy stat SO.", this);
                return;
            }
            statDefinition.Changed += RefreshStats;
            RefreshStats();
        }

        private void OnDisable()
        {
            if (statDefinition) statDefinition.Changed -= RefreshStats;
            IsReady = false;
        }

        private void RefreshStats()
        {
            float damageTaken = IsReady ? MaxHealth - Health : 0;
            bool defeated = IsReady && Health <= 0;
            MaxHealth = statDefinition.Values.hp;
            Health = defeated ? 0 : Mathf.Clamp(MaxHealth - damageTaken, 0, MaxHealth);
            IsReady = true;
            Debug.Log($"[Integration Enemy] SO={statDefinition.name}, id={statDefinition.Values.actorId}, HP={Health}/{MaxHealth}, attack={Attack}, defense={Defense}, moveSpeed={MoveSpeed}", this);
        }

        private void Update()
        {
            if (Keyboard.current?.fKey.wasPressedThisFrame ?? false) ApplyTestDamage();
        }

        public void ApplyTestDamage()
        {
            if (!IsReady) { Debug.LogWarning("[Integration Enemy] " + ConnectionStatus, this); return; }
            float before = Health;
            Health = Mathf.Max(0f, Health - 10f);
            Debug.Log($"[Integration Enemy] {statDefinition.Values.actorId}: HP {before} -> {Health}/{MaxHealth} (F: -10)", this);
        }
    }
}