using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishingPrototype
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        public static Font KoreanFont => Resources.Load<Font>("KoreanUI");
        private static Sprite whiteSprite;
        private static bool isQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototype()
        {
            if (SceneManager.GetActiveScene().name != "FishingPrototype" || isQuitting || FindFirstObjectByType<PrototypeBootstrap>() != null) return;
            new GameObject("System_FishingCombat").AddComponent<PrototypeBootstrap>().Build();
        }

        private void OnApplicationQuit() => isQuitting = true;

        private void Build()
        {
            Application.targetFrameRate = 120;
            Physics2D.gravity = new Vector2(0f, -24f);

            CreateCamera();
            CreateBackdrop();
            CreateGround();
            GameObject player = CreatePlayer(new Vector2(-8f, -1.15f));
            FishingRodController rod = CreateFishingRod(player);

            CreateHookable("Prop_ThrowingRock", new Vector2(-1f, -1.55f), new Vector2(0.8f, 0.8f),
                new Color(0.42f, 0.47f, 0.52f), HookableBody.BodyKind.Prop, 3.2f, 160f, true, "object.rock");

            GameObject directorObject = new GameObject("System_StageDirector");
            StageDirector director = directorObject.AddComponent<StageDirector>();
            director.player = player.GetComponent<PlayerController>();
            director.rod = rod;

            gameObject.AddComponent<CombatHud>();
        }

        private void Update()
        {
            if ((Keyboard.current?.rKey.wasPressedThisFrame ?? false) || (Keyboard.current?.f9Key.wasPressedThisFrame ?? false))
                Reload();
        }

        private static void Reload()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
#endif
        }

        private static void CreateCamera()
        {
            Camera existing = Camera.main;
            GameObject go = existing ? existing.gameObject : new GameObject("Camera_Main");
            Camera cam = existing ? existing : go.AddComponent<Camera>();
            go.tag = "MainCamera";
            if (!go.GetComponent<AudioListener>()) go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0f, 0.5f, -10f);
            cam.orthographic = true;
            cam.orthographicSize = 5.2f;
            cam.backgroundColor = new Color(0.055f, 0.075f, 0.105f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            go.AddComponent<SmoothCameraFollow>();
            go.AddComponent<CameraShake>();
        }

        private static void CreateBackdrop()
        {
            GameObject far = CreateVisual("Sprite_Backdrop", new Vector2(17f, 1.2f), new Vector2(78f, 8f), new Color(0.08f, 0.12f, 0.17f));
            far.GetComponent<SpriteRenderer>().sortingOrder = -10;
            for (int i = -3; i <= 12; i++)
            {
                GameObject marker = CreateVisual("Sprite_DistanceMarker", new Vector2(i * 4f, -1.85f), new Vector2(0.04f, 0.35f), new Color(0.2f, 0.28f, 0.34f));
                marker.GetComponent<SpriteRenderer>().sortingOrder = -2;
            }
        }

        private static void CreateGround()
        {
            GameObject sea = CreateVisual("Sprite_Sea", new Vector2(17f, -4.15f), new Vector2(78f, 2.15f),
                new Color(0.06f, 0.3f, 0.5f));
            sea.GetComponent<SpriteRenderer>().sortingOrder = -4;

            GameObject seaSurface = CreateVisual("Sprite_SeaSurface", new Vector2(17f, -3.18f), new Vector2(78f, 0.12f),
                new Color(0.2f, 0.72f, 0.9f));
            seaSurface.GetComponent<SpriteRenderer>().sortingOrder = -3;

            GameObject ground = CreateVisual("Environment_Ground", new Vector2(17f, -2.65f), new Vector2(78f, 1.25f), new Color(0.13f, 0.18f, 0.22f));
            ground.AddComponent<BoxCollider2D>();
            ground.AddComponent<GroundSurface>();

            GameObject stripe = CreateVisual("Sprite_GroundEdge", new Vector2(17f, -2f), new Vector2(78f, 0.08f), new Color(0.33f, 0.78f, 0.68f));
            stripe.GetComponent<SpriteRenderer>().sortingOrder = 1;
        }

        private static GameObject CreatePlayer(Vector2 position)
        {
            GameObject player = CreateVisual("Player_Fisher", position, new Vector2(0.85f, 1.55f), new Color(0.2f, 0.72f, 0.95f));
            Rigidbody2D rb = player.AddComponent<Rigidbody2D>();
            rb.mass = 1.2f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            BoxCollider2D col = player.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.9f, 1f);
            PlayerController controller = player.AddComponent<PlayerController>();
            player.AddComponent<PlayerHealth>();
            player.AddComponent<PlayerAppearance>();
            Camera.main.GetComponent<SmoothCameraFollow>().target = player.transform;

            GameObject rod = CreateVisual("Weapon_FishingRod", position, new Vector2(1.25f, 0.09f), new Color(0.93f, 0.74f, 0.31f));
            rod.transform.SetParent(player.transform, false);
            rod.transform.localPosition = new Vector3(0.52f, 0.28f, 0f);
            rod.GetComponent<SpriteRenderer>().sortingOrder = 7;
            controller.rodVisual = rod.transform;
            CombatShapes.DressRod(rod);
            return player;
        }

        private static FishingRodController CreateFishingRod(GameObject player)
        {
            GameObject system = new GameObject("System_FishingRod");
            FishingRodController rod = system.AddComponent<FishingRodController>();
            rod.player = player.GetComponent<PlayerController>();
            rod.rodVisual = player.GetComponent<PlayerController>().rodVisual;
            return rod;
        }

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
