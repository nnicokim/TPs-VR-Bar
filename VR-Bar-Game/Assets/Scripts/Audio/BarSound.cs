using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

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

        [Tooltip("Activado: suena solo, cada tanto. Desactivado: solo suena cuando lo dispara una interacción (PlayOnce).")]
        public bool automatic = true;

        [Tooltip("Segundos de silencio entre un sonido y el siguiente (mín, máx). No se usa en loop.")]
        public Vector2 gap = new Vector2(10f, 30f);

        [Tooltip("Volumen de cada disparo (mín, máx), multiplica al volumen del AudioSource.")]
        public Vector2 volume = new Vector2(0.7f, 1f);

        [Tooltip("Pitch de cada disparo (mín, máx). Una variación chica disimula la repetición.")]
        public Vector2 pitch = new Vector2(0.95f, 1.05f);

        /// <summary>Se dispara cada vez que empieza a sonar un clip (para sincronizar animaciones, ej. puertas).</summary>
        public event Action<BarSound> Played;

        /// <summary>Multiplicador de volumen (para los actos: la música sube en la hora pico, la lluvia cuando se abre la puerta).</summary>
        public float VolumeScale { get; private set; } = 1f;

        AudioSource source;
        float baseVolume;
        int lastClip = -1;
        Coroutine fade, scaleFade;
        float clipVolume = 1f;

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
            if (!automatic) yield break; // lo dispara la interacción
            yield return new WaitForSeconds(Random.Range(0f, gap.x));
            while (automatic) // Silence() lo corta: no arranca el clip siguiente
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

        /// <summary>Reproduce un clip al azar (con la misma variación de volumen y pitch). Para sonidos disparados por la interacción.</summary>
        public void PlayOnce()
        {
            if (source == null || clips == null || clips.Length == 0) return;
            if (fade != null) { StopCoroutine(fade); fade = null; }
            AudioClip clip = PickClip();
            if (clip == null) return;
            source.loop = false;
            PlayClip(clip);
        }

        /// <summary>Mueve la fuente a "position" (para que el sonido salga de donde ocurre) y reproduce.</summary>
        public void PlayOnceAt(Vector3 position)
        {
            transform.position = position;
            PlayOnce();
        }

        /// <summary>Apaga del todo: baja el volumen en "seconds" y deja de sonar solo (por ejemplo, al apagar el parlante).</summary>
        public void Silence(float seconds)
        {
            automatic = false;
            FadeOut(seconds);
        }

        /// <summary>Baja el volumen hasta cero en "seconds" y corta (por ejemplo, al cerrar la canilla).</summary>
        public void FadeOut(float seconds)
        {
            if (source == null || !source.isPlaying) return;
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeRoutine(seconds));
        }

        /// <summary>Lleva el multiplicador de volumen a "target" en "seconds" (afecta también al clip que está sonando).</summary>
        public void SetVolumeScale(float target, float seconds)
        {
            if (scaleFade != null) StopCoroutine(scaleFade);
            if (!isActiveAndEnabled || seconds <= 0f) { ApplyScale(target); return; }
            scaleFade = StartCoroutine(ScaleRoutine(target, seconds));
        }

        IEnumerator ScaleRoutine(float target, float seconds)
        {
            float from = VolumeScale;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                ApplyScale(Mathf.Lerp(from, target, t / seconds));
                yield return null;
            }
            ApplyScale(target);
            scaleFade = null;
        }

        void ApplyScale(float scale)
        {
            VolumeScale = scale;
            if (source != null && fade == null) source.volume = baseVolume * clipVolume * VolumeScale;
        }

        IEnumerator FadeRoutine(float seconds)
        {
            float start = source.volume;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                source.volume = Mathf.Lerp(start, 0f, t / seconds);
                yield return null;
            }
            source.Stop();
            source.volume = start;
            fade = null;
        }

        void PlayClip(AudioClip clip)
        {
            source.clip = clip;
            source.pitch = Random.Range(pitch.x, pitch.y);
            clipVolume = Random.Range(volume.x, volume.y);
            source.volume = baseVolume * clipVolume * VolumeScale;
            source.Play();
            Played?.Invoke(this);
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
        void Reset() => ConfigureDefaults();

        /// <summary>Deja el AudioSource y el Steam Audio Source listos para un sonido 3D del bar (lo que hace Reset).</summary>
        public void ConfigureDefaults()
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
