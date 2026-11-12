#if UNITY_EDITOR
using UnityEngine;
namespace FishingPrototype.Editor
{
    public static class CombatAuthoring
    {
        public static Font KoreanFont => Resources.Load<Font>("KoreanUI");
        private static Sprite whiteSprite;
        public static GameObject CreateHookable(string name, Vector2 position, Vector2 scale, Color color,
            HookableBody.BodyKind kind, float mass, float maxHealth, bool respawn, string displayNameKey = "object.unknown")
        {
            GameObject body = CreateVisual(name, position, scale, color);
            Rigidbody2D rb = body.AddComponent<Rigidbody2D>();
            rb.mass = mass;
            rb.linearDamping = 0.35f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            BoxCollider2D col = body.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.95f, 0.95f);
            HookableBody hookable = body.AddComponent<HookableBody>();
            hookable.Configure(kind, color, maxHealth, respawn);
            hookable.SetDisplayNameKey(displayNameKey);
            return body;
        }

        public static GameObject CreateVisual(string name, Vector2 position, Vector2 scale, Color color)
        {
            if (whiteSprite == null)
            {
                Texture2D texture = new Texture2D(1, 1) { name = "Texture_WhitePixel" };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            GameObject go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = scale;
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = whiteSprite;
            renderer.color = color;
            return go;
        }
    }
}
#endif
