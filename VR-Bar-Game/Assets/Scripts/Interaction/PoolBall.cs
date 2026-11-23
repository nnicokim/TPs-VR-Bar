using System.Collections;
using UnityEngine;

namespace BarInteraccion
{
    /// <summary>Una bola: cuerpo rígido esférico; avisa los choques y se deja embocar.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class PoolBall : MonoBehaviour
    {
        public bool isCue;
        public float radius = 0.0285f;
        [HideInInspector] public PoolGame game;

        public Rigidbody Body { get; private set; }
        public bool Pocketed { get; private set; }
        public Vector3 StartPosition { get; private set; }

        Vector3 startScale;
        Quaternion startRotation;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            StartPosition = transform.position;
            startRotation = transform.rotation;
            startScale = transform.localScale;
        }

        void OnCollisionEnter(Collision c) { if (game != null) game.OnBallCollision(this, c); }

        public void Stop()
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        /// <summary>Frena y deja la bola dormida: no se mueve hasta que otra la golpee.</summary>
        public void Settle()
        {
            Stop();
            Body.Sleep();
        }

        public void Pocket(Vector3 pocketCenter, float feltY)
        {
            Pocketed = true;
            StartCoroutine(Fall(pocketCenter, feltY));
        }

        IEnumerator Fall(Vector3 pocketCenter, float feltY)
        {
            Body.isKinematic = true;
            GetComponent<Collider>().enabled = false;
            Vector3 p0 = transform.position;
            Vector3 p1 = new Vector3(pocketCenter.x, feltY - 0.12f, pocketCenter.z);
            yield return Ease.Run(0.3f, t =>
            {
                transform.position = Vector3.Lerp(p0, p1, Ease.InCubic(t));
                transform.localScale = startScale * Mathf.Lerp(1f, 0.7f, t);
            });
            gameObject.SetActive(false);
        }

        public void Respawn(Vector3 position)
        {
            StopAllCoroutines();
            gameObject.SetActive(true);
            Pocketed = false;
            transform.SetPositionAndRotation(position, startRotation);
            transform.localScale = startScale;
            GetComponent<Collider>().enabled = true;
            Body.isKinematic = false;
            Settle();
        }
    }
}
