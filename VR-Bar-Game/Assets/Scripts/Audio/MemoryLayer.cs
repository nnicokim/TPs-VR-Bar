using UnityEngine;
using UnityEngine.Audio;

namespace BarAudio
{
    [System.Serializable]
    public class MemoryTarget
    {
        public string label = "Juego";
        public Transform target;

        [Tooltip("Tamaño del blanco para la mirada (multiplica el tamaño del objeto). Más chico = hay que mirarlo más directo.")]
        [Range(0.1f, 2f)] public float gazeSize = 0.6f;

        [Tooltip("Si el participante se acerca a menos de esta distancia (m) del objeto también se activa (para cuando haya movimiento).")]
        public float proximity = 1.2f;

        [Tooltip("Sonidos del juego que 'vuelven' mientras se recuerda (opcional).")]
        public RandomOneShots echo;
        public Vector2 echoInterval = new Vector2(4f, 8f);

        [HideInInspector] public Bounds bounds;
        [HideInInspector] public float dwell;
        [HideInInspector] public float nextEcho;
    }

    /// <summary>
    /// Capa de RECUERDO (sonido extradiegético).
    /// Si el participante mira un juego (pool, dardos, arcade) un par de segundos, el bar "se aleja":
    /// el mixer pasa al snapshot "Recuerdo" (baja el volumen y filtra los agudos del grupo Bar)
    /// y entra una música que no sale de ningún lugar del bar (2D, sin espacializar).
    /// Al dejar de mirar, todo vuelve. No hay guion: depende solo de dónde mira la persona.
    /// </summary>
    public class MemoryLayer : MonoBehaviour
    {
        [Tooltip("Cabeza del participante (Main Camera). Si queda vacío usa Camera.main.")]
        public Transform head;

        public AudioMixer mixer;
        public string normalSnapshot = "Normal";
        public string memorySnapshot = "Recuerdo";

        [Tooltip("Música extradiegética (fuente 2D en el grupo Recuerdo).")]
        public AudioSource memoryMusic;
        [Range(0f, 1f)] public float memoryMusicVolume = 0.8f;

        public MemoryTarget[] targets;

        [Tooltip("Segundos mirando un juego antes de que empiece el recuerdo.")]
        public float dwellSeconds = 2f;
        public float fadeInSeconds = 4f;
        public float fadeOutSeconds = 3f;
        public float maxGazeDistance = 14f;

        [Header("Solo lectura")]
        [Range(0f, 1f)] public float weight;
        public string focused;

        AudioMixerSnapshot[] snapshots;
        readonly float[] weights = new float[2];
        float lastApplied = -1f;
        float silentTime;
        bool musicStarted;

        void Start()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (mixer != null)
            {
                var a = mixer.FindSnapshot(normalSnapshot);
                var b = mixer.FindSnapshot(memorySnapshot);
                if (a != null && b != null) snapshots = new[] { a, b };
                else Debug.LogWarning("[MemoryLayer] No encontré los snapshots del mixer.");
            }
            if (targets != null)
                foreach (var t in targets) t.bounds = ComputeBounds(t.target);
            if (memoryMusic != null)
            {
                memoryMusic.loop = true;
                memoryMusic.playOnAwake = false;
                memoryMusic.volume = 0f;
            }
            Apply(0f, 0f);
        }

        void Update()
        {
            if (head == null || targets == null) return;
            float dt = Time.deltaTime;
            Vector3 eye = head.position;
            Vector3 fwd = head.forward;

            float goal = 0f;
            MemoryTarget best = null;
            foreach (var t in targets)
            {
                if (t.target == null) continue;
                Vector3 to = t.bounds.center - eye;
                float dist = to.magnitude;
                float size = t.bounds.extents.magnitude;
                bool near = dist - size < t.proximity;
                float halfAngle = Mathf.Atan2(size * t.gazeSize, Mathf.Max(dist, 0.01f)) * Mathf.Rad2Deg;
                bool looking = dist < maxGazeDistance && Vector3.Angle(fwd, to) < halfAngle;

                t.dwell = looking ? t.dwell + dt : 0f;
                if (near || t.dwell >= dwellSeconds)
                {
                    goal = 1f;
                    if (best == null || t.dwell > best.dwell) best = t;
                }
            }

            float rate = goal > weight ? 1f / Mathf.Max(0.01f, fadeInSeconds) : 1f / Mathf.Max(0.01f, fadeOutSeconds);
            weight = Mathf.MoveTowards(weight, goal, rate * dt);
            if (best != null) focused = best.label;
            else if (weight <= 0f) focused = "";

            Apply(weight, dt);

            if (best != null && best.echo != null && weight > 0.6f && Time.time >= best.nextEcho)
            {
                best.echo.PlayBurst();
                best.nextEcho = Time.time + Random.Range(best.echoInterval.x, best.echoInterval.y);
            }
        }

        void Apply(float w, float dt)
        {
            float s = w * w * (3f - 2f * w); // smoothstep: entrada y salida suaves

            if (snapshots != null && Mathf.Abs(s - lastApplied) > 0.002f)
            {
                weights[0] = 1f - s;
                weights[1] = s;
                mixer.TransitionToSnapshots(snapshots, weights, 0.05f);
                lastApplied = s;
            }

            if (memoryMusic == null) return;
            memoryMusic.volume = s * memoryMusicVolume;
            if (s > 0.001f)
            {
                silentTime = 0f;
                if (!memoryMusic.isPlaying)
                {
                    // Optimización: la música del recuerdo solo se decodifica mientras se escucha,
                    // y al volver retoma desde donde quedó (como un recuerdo que sigue).
                    if (musicStarted) memoryMusic.UnPause();
                    else { memoryMusic.Play(); musicStarted = true; }
                }
            }
            else
            {
                silentTime += dt;
                if (memoryMusic.isPlaying && silentTime > 1f) memoryMusic.Pause();
            }
        }

        static Bounds ComputeBounds(Transform t)
        {
            if (t == null) return new Bounds(Vector3.zero, Vector3.one * 0.5f);
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.5f);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        void OnDrawGizmosSelected()
        {
            if (targets == null) return;
            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.9f);
            foreach (var t in targets)
            {
                if (t.target == null) continue;
                Bounds b = Application.isPlaying ? t.bounds : ComputeBounds(t.target);
                Gizmos.DrawWireCube(b.center, b.size);
                Gizmos.DrawWireSphere(b.center, b.extents.magnitude * t.gazeSize);
            }
        }
    }
}
