using System.Collections;
using UnityEngine;

namespace BarAudio
{
    /// <summary>
    /// Puerta: cada tanto alguien (fuera de cuadro) entra o sale.
    /// Mientras está abierta se escucha más fuerte y más "brillante" lo que hay del otro lado
    /// (la lluvia de la calle o la cocina); cerrada, queda bajo y filtrado (oclusión simple).
    /// Opcionalmente gira la hoja de la puerta alrededor de la bisagra, sincronizado con el sonido.
    /// </summary>
    public class DoorAudio : MonoBehaviour
    {
        [Header("Sonido de la puerta")]
        public AudioSource doorSource;
        public AudioClip[] doorClips;
        [Tooltip("Segundo del clip en que la puerta termina de abrirse.")]
        public float openAt = 0.4f;
        [Tooltip("Segundo del clip en que se escucha el golpe de cierre.")]
        public float closeAt = 3.6f;

        [Header("Lo que hay del otro lado (loop)")]
        public AudioSource beyondSource;
        public float closedVolume = 0.1f;
        public float openVolume = 0.8f;
        public float closedCutoff = 600f;
        public float openCutoff = 9000f;

        [Header("Automático")]
        public bool playAutomatically = true;
        public Vector2 interval = new Vector2(40f, 100f);
        public float maxInitialDelay = 20f;

        [Header("Animación simple (opcional)")]
        public Transform doorLeaf;
        public Vector3 hingeWorldPosition;
        [Tooltip("Ángulo de apertura en grados. Si abre para el lado equivocado, cambiar el signo.")]
        public float openAngle = -75f;
        public float swingSeconds = 0.6f;

        AudioLowPassFilter beyondFilter;
        float currentAngle;
        bool busy;

        void Awake()
        {
            if (beyondSource != null)
            {
                beyondFilter = beyondSource.GetComponent<AudioLowPassFilter>();
                beyondSource.loop = true;
                beyondSource.playOnAwake = false;
            }
        }

        void Start()
        {
            SetOpenAmount(0f);
            if (beyondSource != null && beyondSource.clip != null)
            {
                beyondSource.Play();
                beyondSource.timeSamples = Random.Range(0, beyondSource.clip.samples);
            }
        }

        void OnEnable()
        {
            if (playAutomatically) StartCoroutine(AutoLoop());
        }

        IEnumerator AutoLoop()
        {
            yield return new WaitForSeconds(Random.Range(3f, maxInitialDelay));
            while (true)
            {
                yield return Cycle();
                yield return new WaitForSeconds(Random.Range(interval.x, interval.y));
            }
        }

        /// <summary>Abre y cierra la puerta una vez (por ejemplo cuando entra un personaje).</summary>
        public void OpenAndClose()
        {
            if (!busy && isActiveAndEnabled) StartCoroutine(Cycle());
        }

        IEnumerator Cycle()
        {
            busy = true;
            float startTime = Time.time;
            if (doorSource != null && doorClips != null && doorClips.Length > 0)
            {
                doorSource.pitch = Random.Range(0.97f, 1.03f);
                doorSource.PlayOneShot(doorClips[Random.Range(0, doorClips.Length)]);
            }

            yield return Animate(0f, 1f, Mathf.Max(0.2f, openAt));

            float closeStart = Mathf.Max(openAt, closeAt - swingSeconds);
            while (Time.time - startTime < closeStart) yield return null;

            yield return Animate(1f, 0f, swingSeconds);
            busy = false;
        }

        IEnumerator Animate(float from, float to, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                SetOpenAmount(Mathf.SmoothStep(from, to, t / seconds));
                yield return null;
            }
            SetOpenAmount(to);
        }

        void SetOpenAmount(float k)
        {
            if (beyondSource != null)
            {
                beyondSource.volume = Mathf.Lerp(closedVolume, openVolume, k);
                if (beyondFilter != null) // interpolación exponencial: el oído percibe la frecuencia en escala logarítmica
                    beyondFilter.cutoffFrequency = closedCutoff * Mathf.Pow(openCutoff / closedCutoff, k);
            }
            if (doorLeaf != null)
            {
                float target = openAngle * k;
                doorLeaf.RotateAround(hingeWorldPosition, Vector3.up, target - currentAngle);
                currentAngle = target;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (doorLeaf == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(hingeWorldPosition, 0.06f);
            Gizmos.DrawLine(hingeWorldPosition + Vector3.down, hingeWorldPosition + Vector3.up);
        }
    }
}
