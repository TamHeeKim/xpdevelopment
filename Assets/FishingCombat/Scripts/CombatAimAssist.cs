using UnityEngine;
namespace FishingPrototype
{
    public static class CombatAimAssist
    {
        public static bool CanRedirect(Vector2 velocity, Vector2 offset, float angle)
        {
            return velocity.sqrMagnitude >= 4.5f * 4.5f && offset.sqrMagnitude > 0.001f &&
                Vector2.Angle(velocity, offset) <= Mathf.Clamp(angle, 0f, 90f);
        }
        public static Vector2 RedirectThrow(Vector2 velocity, Vector2 offset, float angle)
        {
            return CanRedirect(velocity, offset, angle) ? offset.normalized * velocity.magnitude : velocity;
        }
    }
}
