using UnityEngine;
using UnityEngine.InputSystem;
namespace FishingPrototype
{
    public sealed class CombatHud : MonoBehaviour
    {
        PlayerHealth health;
        PlayerController player;
        FishingRodController rod;
        StageDirector stage;
        HookableBody[] bodies = new HookableBody[0];
        float refresh;
        bool help;
        GUIStyle text, small, title;
        readonly Color panel = new Color(0.035f, 0.065f, 0.085f, 0.94f);
        readonly Color accent = new Color(0.25f, 0.9f, 0.77f);
        void Start()
        {
            health = FindFirstObjectByType<PlayerHealth>(); player = health.GetComponent<PlayerController>();
            rod = FindFirstObjectByType<FishingRodController>(); stage = FindFirstObjectByType<StageDirector>();
        }
        void Update()
        {
            if (Keyboard.current?.hKey.wasPressedThisFrame ?? false) help = !help;
            if (Time.time >= refresh) { refresh = Time.time + 0.4f; bodies = FindObjectsByType<HookableBody>(FindObjectsSortMode.None); }
        }
        void Box(Rect rect, Color color)
        {
            Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        void Label(Rect rect, string value, GUIStyle style) { GUI.Label(rect, value, style); }
        void OnGUI()
        {
            if (!health || !rod) return;
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { font = PrototypeBootstrap.KoreanFont, fontSize = 18, wordWrap = true };
                text.normal.textColor = new Color(0.9f, 0.96f, 0.97f);
                small = new GUIStyle(text) { fontSize = 14 }; title = new GUIStyle(text) { fontSize = 23, fontStyle = FontStyle.Bold };
            }
            Matrix4x4 old = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            Box(new Rect(24,24,300,100),panel); Box(new Rect(24,24,4,100),accent);
            Label(new Rect(42,34,170,30),CombatText.Get("player.name"),title);
            Label(new Rect(220,40,90,24),Mathf.CeilToInt(health.Health)+" / "+Mathf.CeilToInt(health.maxHealth),small);
            Box(new Rect(42,76,264,12),new Color(0.15f,0.22f,0.25f));
            Box(new Rect(42,76,264*Mathf.Clamp01(health.Health/health.maxHealth),12),health.Health<30?new Color(1f,0.35f,0.3f):accent);
            Label(new Rect(42,96,264,22),CombatText.Get("hud.tip"),small);
            var poison = player.GetComponent<PoisonStatus>();
            if (poison && poison.until > Time.time)
            {
                Box(new Rect(24,132,300,36),new Color(0.15f,0.24f,0.09f,0.95f));
                Label(new Rect(38,138,285,25),CombatText.Format("hud.poison", poison.until - Time.time),small);
            }
            Box(new Rect(width-414,24,390,92),panel);
            Label(new Rect(width-396,34,350,25),CombatText.Get("hud.objective"),small);
            Label(new Rect(width-396,60,350,48),stage?stage.ObjectiveText:CombatText.Get("hud.ready"),text);
            foreach (var body in bodies)
            {
                if (!body || !body.Body || !body.Body.simulated || !Camera.main) continue;
                Vector3 pos = Camera.main.WorldToScreenPoint(body.transform.position + Vector3.up * (body.GetComponent<Collider2D>().bounds.extents.y + 0.3f));
                if (pos.z <= 0 || pos.x < 0 || pos.x > Screen.width) continue;
                float x=pos.x/scale, y=(Screen.height-pos.y)/scale;
                Box(new Rect(x-65,y-28,130,34),panel);
                Label(new Rect(x-59,y-26,120,24),body.DisplayName+(body.IsHooked?CombatText.Get("hud.hooked_suffix"):""),small);
                if(body.kind==HookableBody.BodyKind.Enemy)
                { Box(new Rect(x-65,y+6,130,4),new Color(0.2f,0.1f,0.13f)); Box(new Rect(x-65,y+6,130*body.HealthFraction,4),new Color(1f,0.43f,0.36f)); }
            }
            if (rod.CaptureProgress > 0)
            {
                Box(new Rect(width/2-150,height/2+75,300,55),panel);
                Label(new Rect(width/2-136,height/2+82,270,24),CombatText.Get("hud.capture_heavy"),small);
                Box(new Rect(width/2-136,height/2+113,272*rod.CaptureProgress,5),accent);
            }
            Box(new Rect(24,height-100,320,76),panel);
            Label(new Rect(40,height-90,290,27),rod.HeldName!=""?CombatText.Format("hud.held", rod.HeldName):CombatText.Format("hud.equipped", rod.EquipmentName),text);
            Label(new Rect(40,height-59,290,26),rod.HeldName!=""?CombatText.Get("hud.held_controls"):CombatText.Get("hud.attack_controls"),small);
            Label(new Rect(width-235,height-57,215,30),CombatText.Format("hud.help_toggle", help ? CombatText.Get("ui.close") : CombatText.Get("ui.open")),text);
            if(help)
            {
                Rect card = new Rect(width/2-250,170,500,300); Box(card,panel); Box(new Rect(card.x,card.y,500,3),accent);
                Label(new Rect(card.x+24,card.y+22,450,35),CombatText.Get("hud.help_title"),title);
                Label(new Rect(card.x+24,card.y+75,450,220),CombatText.Get("hud.help_body"),text);
            }
            GUI.matrix = old;
        }
    }
}
