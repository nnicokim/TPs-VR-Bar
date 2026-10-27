using UnityEngine;

namespace BarAudio
{
    /// <summary>
    /// Para cuando los juegos sean jugables (bolas de pool, dardos, vasos que se agarran):
    /// reproduce un sonido al chocar, con volumen según la velocidad del golpe.
    /// Necesita un Collider y un Rigidbody (en este objeto o en el otro).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CollisionSound : MonoBehaviour
    {
        public AudioClip[] clips;
        [Tooltip("Velocidad relativa (m/s) a partir de la cual suena, y a la que suena al máximo.")]
        public float minSpeed = 0.15f;
        public float maxSpeed = 4f;
        [Tooltip("Tiempo mínimo entre sonidos para no saturar con micro-rebotes.")]
        public float minInterval = 0.04f;
        public Vector2 pitch = new Vector2(0.94f, 1.06f);

        AudioSource source;
        float last;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
        }

        void OnCollisionEnter(Collision c)
        {
            if (clips == null || clips.Length == 0) return;
            float v = c.relativeVelocity.magnitude;
            if (v < minSpeed || Time.time - last < minInterval) return;
            last = Time.time;
            float k = Mathf.InverseLerp(minSpeed, maxSpeed, v);
            source.pitch = Random.Range(pitch.x, pitch.y);
            source.PlayOneShot(clips[Random.Range(0, clips.Length)], k * k);
        }
    }
}
