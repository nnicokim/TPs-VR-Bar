using System.Collections;
using UnityEngine;

namespace BarAudio
{
    /// <summary>
    /// Un solo componente para todos los sonidos del bar.
    ///
    ///  - Loop (ambientes: lluvia, cocina, neón, heladera, arcade): reproduce el primer clip en loop,
    ///    arrancando en un punto al azar para que dos fuentes con el mismo archivo no suenen "en fase".
    ///  - Sueltos (foley y música): elige un clip al azar (sin repetir el anterior), lo reproduce entero
    ///    y deja un silencio antes del siguiente. Con varias canciones funciona como playlist.
    ///
    /// La espacialización la hace Steam Audio: el AudioSource tiene "Spatialize" activado y el
    /// Steam Audio Source aplica HRTF (binaural), atenuación por distancia y absorción del aire.
    /// Al agregar este componente a un objeto nuevo, los dos quedan configurados solos (ver Reset).
    /// </summary>
    [RequireComponent(typeof(AudioSource), typeof(SteamAudio.SteamAudioSource))]
    public class BarSound : MonoBehaviour
    {
        [Tooltip("Sonidos. En modo loop se usa solo el primero.")]
        public AudioClip[] clips;

        [Tooltip("Activado: suena todo el tiempo (ambiente). Desactivado: un sonido cada tanto.")]
        public bool loop;

        [Tooltip("Segundos de silencio entre un sonido y el siguiente (mín, máx). No se usa en loop.")]
        public Vector2 gap = new Vector2(10f, 30f);

        [Tooltip("Volumen de cada disparo (mín, máx), multiplica al volumen del AudioSource.")]
        public Vector2 volume = new Vector2(0.7f, 1f);

        [Tooltip("Pitch de cada disparo (mín, máx). Una variación chica disimula la repetición.")]
        public Vector2 pitch = new Vector2(0.95f, 1.05f);

        AudioSource source;
        float baseVolume;
        int lastClip = -1;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            baseVolume = source.volume;
        }

        void OnEnable()
        {
            StartCoroutine(Run());
        }

        void OnDisable()
        {
            if (source != null) source.Stop();
        }

        IEnumerator Run()
        {
            // Un frame de espera: deja que el Steam Audio Source termine de inicializarse.
            yield return null;
            if (clips == null || clips.Length == 0) yield break;

            if (loop)
            {
                AudioClip clip = clips[0];
                if (clip == null) yield break;
                source.loop = true;
                PlayClip(clip);
                if (clip.samples > 0) source.timeSamples = Random.Range(0, clip.samples);
                yield break;
            }

            source.loop = false;
            yield return new WaitForSeconds(Random.Range(0f, gap.x));
            while (true)
            {
                AudioClip clip = PickClip();
                float length = 0f;
                if (clip != null)
                {
                    PlayClip(clip);
                    length = clip.length / Mathf.Max(0.1f, source.pitch);
                }
                yield return new WaitForSeconds(length + Random.Range(gap.x, gap.y));
            }
        }

        void PlayClip(AudioClip clip)
        {
            source.clip = clip;
            source.pitch = Random.Range(pitch.x, pitch.y);
            source.volume = baseVolume * Random.Range(volume.x, volume.y);
            source.Play();
        }

        AudioClip PickClip()
        {
            if (clips.Length == 1) return clips[0];
            int i;
            do { i = Random.Range(0, clips.Length); } while (i == lastClip);
            lastClip = i;
            return clips[i];
        }

        // Se llama al agregar el componente en el Editor: deja el AudioSource y el
        // Steam Audio Source listos para un sonido 3D del bar.
        void Reset()
        {
            var s = GetComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialize = true;          // pasa por el spatializer de Steam Audio (Project Settings > Audio)
            s.spatialBlend = 1f;          // 3D
            s.dopplerLevel = 0f;          // nada se mueve rápido en el bar
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 1f;
            s.maxDistance = 15f;

            var steam = GetComponent<SteamAudio.SteamAudioSource>();
            steam.directBinaural = true;  // HRTF
            steam.distanceAttenuation = true;
            steam.distanceAttenuationInput = SteamAudio.DistanceAttenuationInput.CurveDriven; // usa Min/Max Distance del AudioSource
            steam.airAbsorption = true;
        }
    }
}
