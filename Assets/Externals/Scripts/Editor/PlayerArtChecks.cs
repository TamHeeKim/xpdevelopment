#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FishingPrototype.Editor
{
    public static class PlayerArtChecks
    {
        private static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        [MenuItem("낚싯대 전투/플레이어 아트 연결 검사")]
        public static void Run()
        {
            var player = UnityEngine.Object.Instantiate(CombatSceneBuilder.Prefab("Player"));
            try
            {
                var appearance = player.GetComponent<PlayerAppearance>();
                var body = player.GetComponent<Rigidbody2D>();
                var model = appearance.model;
                if (!model || Mathf.Abs(player.transform.localScale.z) < 0.001f
                    || model.GetComponentsInChildren<SkinnedMeshRenderer>().Length == 0
                    || model.GetComponentsInChildren<Collider>().Length != 0
                    || model.GetComponentsInChildren<Collider2D>().Length != 0)
                    throw new Exception("플레이어 모델/충돌 연결을 확인하세요.");
                Call(player.GetComponent<PlayerController>(), "Awake");
                Call(appearance, "Start");
                var right = model.localRotation;
                body.linearVelocity = Vector2.left;
                Call(appearance, "LateUpdate");
                if (Quaternion.Angle(model.localRotation, Quaternion.Euler(0, 180, 0) * right) > 0.1f)
                    throw new Exception("플레이어 좌향 회전 오류");
                body.linearVelocity = Vector2.zero;
                Call(appearance, "LateUpdate");
                if (Quaternion.Angle(model.localRotation, Quaternion.Euler(0, 180, 0) * right) > 0.1f)
                    throw new Exception("정지 시 바라보는 방향 유지 오류");
                body.simulated = false;
                Call(appearance, "LateUpdate");
                if (model.gameObject.activeSelf) throw new Exception("사망 시 외형 숨김 오류");
                body.simulated = true;
                Call(appearance, "LateUpdate");
                if (!model.gameObject.activeSelf) throw new Exception("부활 시 외형 표시 오류");
                appearance.Flash();
                Call(appearance, "LateUpdate");
                if (model.GetComponentInChildren<SkinnedMeshRenderer>().enabled)
                    throw new Exception("피격 표시 오류");
                Debug.Log("플레이어 아트 연결 검사 통과");
            }
            finally { UnityEngine.Object.DestroyImmediate(player); }
        }
    }
}
#endif
