using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishingPrototype
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [Header("게임 실행 설정")]
        public int targetFrameRate = 120;
        public Vector2 gravity = new Vector2(0f, -24f);
        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Physics2D.gravity = gravity;
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


    }

}
