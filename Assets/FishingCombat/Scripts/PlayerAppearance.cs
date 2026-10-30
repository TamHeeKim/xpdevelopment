using UnityEngine;
namespace FishingPrototype
{
    public sealed class PlayerAppearance : MonoBehaviour
    {
        Transform model, leftBoot, rightBoot;
        PlayerController player;
        SpriteRenderer[] parts;
        Color[] colors;
        float flashUntil, step;
        float facing = 1f;
        void Start()
        {
            player = GetComponent<PlayerController>();
            model = CombatShapes.DressPlayer(gameObject);
            leftBoot = model.Find("왼쪽 장화"); rightBoot = model.Find("오른쪽 장화");
            parts = model.GetComponentsInChildren<SpriteRenderer>(); colors = new Color[parts.Length];
            for(int i=0;i<parts.Length;i++) colors[i]=parts[i].color;
        }
        public void Flash() { flashUntil = Time.time + 0.1f; }
        void LateUpdate()
        {
            if (!model || !player.Body) return;
            model.gameObject.SetActive(player.Body.simulated);
            if (!player.Body.simulated) return;
            float speed = player.Body.linearVelocity.x;
            if (Mathf.Abs(speed) > 0.2f) facing = Mathf.Sign(speed);
            model.localScale = new Vector3(facing,1,1);
            bool walking = player.Grounded && Mathf.Abs(speed) > 0.2f;
            if(walking) step += Mathf.Abs(speed) * Time.deltaTime * 2f;
            float stride = walking ? Mathf.Sin(step)*0.055f : 0;
            leftBoot.localPosition = new Vector3(-.17f,-.41f+Mathf.Max(0,stride),0);
            rightBoot.localPosition = new Vector3(.17f,-.41f+Mathf.Max(0,-stride),0);
            model.localPosition = new Vector3(0,walking?Mathf.Abs(Mathf.Sin(step))*.015f:0,0);
            for(int i=0;i<parts.Length;i++) parts[i].color=Time.time<flashUntil?Color.white:colors[i];
        }
    }
}
