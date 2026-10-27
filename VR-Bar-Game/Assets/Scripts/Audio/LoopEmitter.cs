using System.Collections;
using UnityEngine;

namespace BarAudio
{
    /// <summary>
    /// Sonido de ambiente en loop (murmullo, lluvia, neón, heladera...).
    /// Arranca en un punto al azar del clip para que dos fuentes con el mismo archivo
    /// no suenen "en fase" (eso produce un efecto de flanger/peine).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class LoopEmitter : MonoBehaviour
    {
        [Tooltip("Empieza en un punto al azar del loop.")]
        public bool randomStartTime = true;

        [Tooltip("Variación de pitch al iniciar (± este valor). Ayuda a que la misma grabación suene distinta en dos lugares.")]
        [Range(0f, 0.1f)] public float pitchVariation = 0.02f;

        [Tooltip("Fundido de entrada al iniciar la escena (segundos).")]
        public float fadeInSeconds = 3f;

        AudioSource source;
        float baseVolume;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            baseVolume = source.volume;
        }

        void Start()
        {
            if (source.clip == null) return;
            source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            source.volume = fadeInSeconds > 0f ? 0f : baseVolume;
            source.Play();
            if (randomStartTime && source.clip.samples > 0)
                source.timeSamples = Random.Range(0, source.clip.samples);
            if (fadeInSeconds > 0f) StartCoroutine(FadeIn());
        }

        IEnumerator FadeIn()
        {
            float t = 0f;
            while (t < fadeInSeconds)
            {
                t += Time.deltaTime;
                source.volume = Mathf.Lerp(0f, baseVolume, t / fadeInSeconds);
                yield return null;
            }
            source.volume = baseVolume;
        }
    }
}
