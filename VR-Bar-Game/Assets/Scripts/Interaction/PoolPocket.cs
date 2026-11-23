using UnityEngine;

namespace BarInteraccion
{
    /// <summary>Tronera: lo que entra en su zona se cae.</summary>
    [RequireComponent(typeof(SphereCollider))]
    public class PoolPocket : MonoBehaviour
    {
        public PoolGame game;

        void OnTriggerEnter(Collider other)
        {
            var ball = other.GetComponent<PoolBall>();
            if (ball != null && game != null) game.OnPocket(ball, transform.position);
        }
    }
}
