using System.Collections;
using UnityEngine;

namespace BarAudio
{
    /// <summary>
    /// Dispara sonidos sueltos cada cierto tiempo (vasos, canilla, caja, risas, dardos...).
    /// Cada disparo varía clip, volumen, pitch y posición para que no se note la repetición.
    /// Si hay varios AudioSource en el objeto los usa por turnos, así un sonido no le corta la cola al anterior.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class RandomOneShots : MonoBehaviour
    {
        public AudioClip[] clips;

        [Tooltip("Segundos entre disparos (mín, máx).")]
        public Vector2 interval = new Vector2(10f, 30f);

        [Tooltip("Cuántos sonidos seguidos por disparo (mín, máx). Ej: 3 dardos.")]
        public Vector2Int burst = new Vector2Int(1, 1);

        [Tooltip("Separación entre sonidos de una misma ráfaga (mín, máx).")]
        public Vector2 burstGap = new Vector2(0.8f, 2f);

        public Vector2 volume = new Vector2(0.7f, 1f);
        public Vector2 pitch = new Vector2(0.95f, 1.05f);

        [Tooltip("Puntos del mundo desde donde puede sonar (opcional). Vacío = suena desde este objeto.")]
        public Vector3[] worldPoints;

        [Tooltip("Desplazamiento horizontal al azar alrededor del punto (metros).")]
        public float positionJitter = 0f;

        [Tooltip("Apagado = solo suena cuando otro script llama a PlayNow() o PlayBurst().")]
        public bool playAutomatically = true;

        public float maxInitialDelay = 15f;

        AudioSource[] voices;
        int nextVoice;
        Vector3 home;
        int lastClip = -1;

        void Awake()
        {
            voices = GetComponents<AudioSource>();
            foreach (var v in voices) { v.playOnAwake = false; v.loop = false; }
            home = transform.position;
        }

        void OnEnable()
        {
            if (playAutomatically) StartCoroutine(AutoLoop());
        }

        IEnumerator AutoLoop()
        {
            yield return new WaitForSeconds(Random.Range(0f, maxInitialDelay));
            while (true)
            {
                yield return BurstRoutine();
                yield return new WaitForSeconds(Random.Range(interval.x, interval.y));
            }
        }

        /// <summary>Dispara una ráfaga (por ejemplo 3 dardos seguidos).</summary>
        public void PlayBurst()
        {
            if (isActiveAndEnabled) StartCoroutine(BurstRoutine());
        }

        /// <summary>Dispara un solo sonido.</summary>
        public void PlayNow()
        {
            MoveToRandomPoint();
            PlayOne(1f);
        }

        IEnumerator BurstRoutine()
        {
            int n = Random.Range(burst.x, burst.y + 1);
            MoveToRandomPoint();
            for (int i = 0; i < n; i++)
            {
                if (i > 0) yield return new WaitForSeconds(Random.Range(burstGap.x, burstGap.y));
                PlayOne(1f);
            }
        }

        void PlayOne(float volumeScale)
        {
            AudioClip clip = PickClip();
            if (clip == null || voices == null || voices.Length == 0) return;
            AudioSource v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            v.pitch = Random.Range(pitch.x, pitch.y);
            v.PlayOneShot(clip, Random.Range(volume.x, volume.y) * volumeScale);
        }

        AudioClip PickClip()
        {
            if (clips == null || clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];
            int i;
            do { i = Random.Range(0, clips.Length); } while (i == lastClip);
            lastClip = i;
            return clips[i];
        }

        void MoveToRandomPoint()
        {
            Vector3 p = home;
            if (worldPoints != null && worldPoints.Length > 0)
                p = worldPoints[Random.Range(0, worldPoints.Length)];
            if (positionJitter > 0f)
            {
                Vector2 r = Random.insideUnitCircle * positionJitter;
                p += new Vector3(r.x, 0f, r.y);
            }
            transform.position = p;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            if (worldPoints == null) return;
            foreach (var p in worldPoints) Gizmos.DrawWireSphere(p, Mathf.Max(0.1f, positionJitter));
        }
    }
}
