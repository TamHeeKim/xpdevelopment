using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
namespace FishingPrototype
{
    public sealed class CombatHud : MonoBehaviour
    {
        [Header("씬 연결")]
        public PlayerHealth health;
        public PlayerController player;
        public FishingRodController rod;
        public StageDirector stage;
        [Header("텍스트")]
        public Text healthText, poisonText, objectiveText, heldText, controlsText, helpToggleText;
        [Header("게이지와 패널")]
        public Image healthFill, captureFill;
        public GameObject poisonPanel, capturePanel, helpPanel;
        bool help;
        void Update()
        {
            if (Keyboard.current?.hKey.wasPressedThisFrame ?? false) help = !help;
            helpPanel.SetActive(help);
            healthText.text = CombatText.Format("hud.health_value", Mathf.CeilToInt(health.Health), Mathf.CeilToInt(health.maxHealth));
            healthFill.fillAmount = Mathf.Clamp01(health.Health / health.maxHealth);
            healthFill.color = health.Health < 30f ? new Color(1f, 0.35f, 0.3f) : new Color(0.25f, 0.9f, 0.77f);
            var poison = player.GetComponent<PoisonStatus>();
            poisonPanel.SetActive(poison && poison.until > Time.time);
            if (poisonPanel.activeSelf) poisonText.text = CombatText.Format("hud.poison", poison.until - Time.time);
            objectiveText.text = stage.ObjectiveText;
            capturePanel.SetActive(rod.CaptureProgress > 0f);
            captureFill.fillAmount = rod.CaptureProgress;
            heldText.text = rod.HeldName != "" ? CombatText.Format("hud.held", rod.HeldName) : CombatText.Format("hud.equipped", rod.EquipmentName);
            controlsText.text = CombatText.Get(rod.HeldName != "" ? "hud.held_controls" : "hud.attack_controls");
            helpToggleText.text = CombatText.Format("hud.help_toggle", CombatText.Get(help ? "ui.close" : "ui.open"));
        }
    }
}
