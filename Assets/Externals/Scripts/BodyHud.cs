using UnityEngine;
using UnityEngine.UI;
namespace FishingPrototype
{
    public sealed class BodyHud : MonoBehaviour
    {
        [Header("표시 연결")]
        public HookableBody body;
        public Canvas canvas;
        public Text nameText;
        public Image healthFill;
        [Header("머리 위 표시 위치")]
        public Vector2 offset = new Vector2(0f, 0.3f);
        void LateUpdate()
        {
            canvas.enabled = body.Body && body.Body.simulated;
            canvas.transform.position = body.transform.position + new Vector3(offset.x, body.GetComponent<Collider2D>().bounds.extents.y + offset.y, 0f);
            canvas.transform.rotation = Quaternion.identity;
            nameText.text = body.DisplayName + (body.IsHooked ? CombatText.Get("hud.hooked_suffix") : "");
            healthFill.transform.parent.gameObject.SetActive(body.kind == HookableBody.BodyKind.Enemy);
            healthFill.fillAmount = body.HealthFraction;
        }
    }
}
