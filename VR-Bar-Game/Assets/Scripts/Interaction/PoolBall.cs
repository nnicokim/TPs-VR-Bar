using System.Collections;
using UnityEngine;

namespace BarInteraccion
{
    /// <summary>Una bola: cuerpo rígido esférico; avisa los choques y se deja embocar.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class PoolBall : MonoBehaviour
    {
        [Range(0, 15)] public int number;
        [HideInInspector] public PoolGame game;

        public bool IsCue => number == 0;
        public Rigidbody Body { get; private set; }
        public SphereCollider Shape { get; private set; }
        public float CollisionRadius => Shape.radius;
        public bool Pocketed { get; private set; }
        public Vector3 StartPosition { get; private set; }

        Vector3 startScale;
        Quaternion startRotation;
        RigidbodyInterpolation interpolation;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Shape = GetComponent<SphereCollider>();
            interpolation = Body.interpolation;
            StartPosition = transform.position;
            startRotation = transform.rotation;
            startScale = transform.localScale;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (game != null) game.OnBallCollision(collision);
        }

        void Stop()
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

        public void Pocket(Vector3 entry, Vector3 pocketCenter, float feltY)
        {
            Pocketed = true;
            Stop();
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            Shape.enabled = false;
            StartCoroutine(Fall(entry, new Vector3(pocketCenter.x, feltY - 0.12f, pocketCenter.z)));
        }

        IEnumerator Fall(Vector3 entry, Vector3 bottom)
        {
            yield return Ease.Run(0.3f, t =>
            {
                transform.position = Vector3.Lerp(entry, bottom, Ease.InCubic(t));
                transform.localScale = startScale * Mathf.Lerp(1f, 0.7f, t);
            });
            gameObject.SetActive(false);
        }

        public void FinishPocket()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        public void Respawn(Vector3 position)
        {
            StopAllCoroutines();
            Pocketed = false;
            transform.localScale = startScale;
            transform.SetPositionAndRotation(position, startRotation);
            Body.position = position;
            Body.rotation = startRotation;
            Shape.enabled = true;
            gameObject.SetActive(true);
            Body.isKinematic = false;
            Body.interpolation = interpolation;
            Settle();
        }
    }
}
