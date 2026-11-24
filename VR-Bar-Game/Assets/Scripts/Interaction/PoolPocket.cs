using UnityEngine;

namespace BarInteraccion
{
    /// <summary>Boca de una tronera en el plano de juego.</summary>
    public class PoolPocket : MonoBehaviour
    {
        [Min(0.001f)] public float captureRadius = 0.055f;

        public bool TryGetEntry(Vector3 from, Vector3 to, float ballRadius, out float entry)
        {
            Vector3 center = transform.position;
            var offset = new Vector2(from.x - center.x, from.z - center.z);
            var travel = new Vector2(to.x - from.x, to.z - from.z);
            float outside = offset.sqrMagnitude - captureRadius * captureRadius;
            entry = 0f;
            if (outside > 0f)
            {
                float lengthSquared = travel.sqrMagnitude;
                float projection = Vector2.Dot(offset, travel);
                float discriminant = projection * projection - lengthSquared * outside;
                if (projection >= 0f || discriminant < 0f) return false;
                entry = (-projection - Mathf.Sqrt(discriminant)) / lengthSquared;
                if (entry > 1f) return false;
            }
            return Mathf.Abs(Mathf.Lerp(from.y, to.y, entry) - center.y) <= ballRadius;
        }
    }
}
