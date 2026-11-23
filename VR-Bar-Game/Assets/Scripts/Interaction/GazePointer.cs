using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Patrón de selección "apuntar con la cabeza" (head-gaze pointing) para Cardboard.
    /// Va en la cámara: lanza un rayo hacia adelante, avisa al IGazeable que se mira (entrar / salir)
    /// y lo elige con un toque de pantalla o mirándolo fijo (dwell). El retículo muestra el estado.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GazePointer : MonoBehaviour
    {
        public static GazePointer Instance { get; private set; }
        public static Material OutlineMaterial { get; private set; }

        [Tooltip("Distancia máxima del rayo (el salón mide ~14 m).")]
        public float maxDistance = 14f;

        public LayerMask layers = Physics.DefaultRaycastLayers;

        [Tooltip("Elegir mirando fijo, además del toque. Útil en visores sin botón.")]
        public bool useDwell = true;

        [Tooltip("Segundos de mirada fija para elegir (si el objeto no define otro).")]
        public float defaultDwellTime = 1.2f;

        [Tooltip("Material del contorno de resaltado (VRBar/GazeOutline).")]
        public Material outlineMaterial;

        public GazeReticle reticle;

        /// <summary>Lo que se está mirando ahora (null si nada utilizable).</summary>
        public IGazeable Current => current;

        /// <summary>Punto donde pega el rayo, o un punto a "idleDistance" si no pega en nada.</summary>
        public Vector3 HitPoint { get; private set; }

        /// <summary>Bloquea la interacción (por ejemplo durante un fundido de teletransporte).</summary>
        public bool Locked { get; set; }

        IGazeable current;
        IGazeable lastSelected; // para no re-elegir por dwell lo mismo mientras se lo sigue mirando
        float dwell;
        bool consumed;

        void Awake()
        {
            Instance = this;
            if (outlineMaterial != null) OutlineMaterial = outlineMaterial;
            if (reticle == null) reticle = GetComponent<GazeReticle>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            Transform t = transform;
            IGazeable target = null;
            IGazeable looked = null;
            float distance = reticle != null ? reticle.idleDistance : 3f;

            if (!Locked && Physics.Raycast(t.position, t.forward, out RaycastHit hit, maxDistance, layers, QueryTriggerInteraction.Collide))
            {
                distance = hit.distance;
                var candidate = hit.collider.GetComponentInParent<IGazeable>();
                if (IsAlive(candidate))
                {
                    looked = candidate;
                    if (candidate.CanInteract) target = candidate;
                }
            }
            HitPoint = t.position + t.forward * distance;
            if (!ReferenceEquals(looked, lastSelected)) lastSelected = null;

            if (!ReferenceEquals(target, current))
            {
                if (IsAlive(current)) current.OnGazeExit(this);
                current = target;
                dwell = 0f;
                consumed = current != null && ReferenceEquals(current, lastSelected);
                if (current != null) current.OnGazeEnter(this);
            }

            float progress = 0f;
            if (current != null)
            {
                float needed = current.DwellTime > 0f ? current.DwellTime : defaultDwellTime;
                bool tapped = GazeInput.SelectPressedThisFrame();
                bool dwelled = false;
                if (useDwell && !consumed)
                {
                    dwell += Time.deltaTime;
                    progress = Mathf.Clamp01(dwell / needed);
                    if (dwell >= needed) dwelled = true;
                }
                if (tapped || dwelled) Select(!tapped);
            }

            if (reticle != null) reticle.SetState(distance, current != null, progress);
        }

        void Select(bool byDwell)
        {
            var target = current;
            SessionLog.Selection(target is Component comp ? comp.name : "?", byDwell);
            consumed = true; // no se vuelve a elegir por dwell hasta mirar otra cosa
            lastSelected = target;
            dwell = 0f;
            if (reticle != null) reticle.Pulse();
            target.OnSelect(this);
        }

        static bool IsAlive(IGazeable g)
        {
            if (g == null) return false;
            if (g is Object o) return o != null; // objeto de Unity destruido
            return true;
        }
    }
}
