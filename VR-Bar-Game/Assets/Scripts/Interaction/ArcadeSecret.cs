using System.Collections;
using System.Linq;
using BarAudio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BarInteraccion
{
    /// <summary>
    /// El arcade "fuera de servicio" esconde algo: no tiene etiqueta ni contorno al principio, solo el retículo
    /// delata que responde (un ofrecimiento escondido que premia la curiosidad). Cada vez que se lo elige
    /// (tocar, o mirarlo un rato largo) recibe un golpe: se sacude, el cartel tiembla y el zumbido tartamudea.
    /// Al tercer golpe el cartel se cae, la pantalla se prende como un tubo viejo y arranca el modo demo.
    /// </summary>
    public class ArcadeSecret : GazeInteractable
    {
        public Transform cabinet;
        public Transform sign;
        public BarSound hum;
        [Tooltip("Centro de la pantalla (mundo) y normal hacia el salón.")]
        public Vector3 screenCenter;
        public Vector3 screenNormal = Vector3.left;
        public Vector2 screenSize = new Vector2(0.34f, 0.27f);
        public int hitsToFix = 3;
        public float floorY = 0f;

        public bool Fixed { get; private set; }

        int hits;
        bool busy;
        Quaternion cabinetRest;

        void Awake()
        {
            if (cabinet == null) cabinet = transform;
            cabinetRest = cabinet.localRotation;
            screenNormal = screenNormal.sqrMagnitude > 0f ? screenNormal.normalized : Vector3.left;
            if (dwellTime <= 0f) dwellTime = 2.5f; // "mirarlo un rato largo"
            FindScreen();
        }

        /// <summary>
        /// Ubica la pantalla sobre la malla real del modelo (material "MAT_Screen"): centro de su cara frontal,
        /// su inclinación y su tamaño. Si no la encuentra, usa los valores del Inspector.
        /// </summary>
        void FindScreen()
        {
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.sharedMaterials.Any(m => m != null && m.name.ToLowerInvariant().Contains("screen"))) continue;
                Bounds lb = r.localBounds;
                Vector3 e = lb.extents;
                int thin = e.x <= e.y && e.x <= e.z ? 0 : (e.y <= e.z ? 1 : 2);
                Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
                Transform t = r.transform;
                Vector3 n = t.TransformDirection(axes[thin]).normalized;
                if (Vector3.Dot(n, screenNormal) < 0f) n = -n; // la cara que mira al salón
                float half = t.TransformVector(axes[thin] * e[thin]).magnitude;
                float a = t.TransformVector(axes[(thin + 1) % 3] * e[(thin + 1) % 3] * 2f).magnitude;
                float b = t.TransformVector(axes[(thin + 2) % 3] * e[(thin + 2) % 3] * 2f).magnitude;
                screenNormal = n;
                screenCenter = t.TransformPoint(lb.center) + n * half;
                screenSize = new Vector2(Mathf.Max(a, b), Mathf.Min(a, b)) * 0.94f; // un poco adentro del marco
                return;
            }
        }

        protected override bool IsAvailable() => !busy && !Fixed;

        // Secreto: sin contorno hasta el primer golpe.
        protected override void ApplyHighlight(float amount) => base.ApplyHighlight(hits > 0 ? amount : 0f);

        public override void OnSelect(GazePointer pointer)
        {
            if (!IsAvailable()) return;
            StartCoroutine(Hit());
        }

        IEnumerator Hit()
        {
            busy = true;
            hits++;
            if (hum != null) hum.SetVolumeScale(0.15f, 0f);
            Vector3 axis = Vector3.Cross(Vector3.up, screenNormal).normalized; // se hamaca hacia adelante y atrás
            Vector3 axisLocal = cabinet.parent != null ? cabinet.parent.InverseTransformDirection(axis) : axis;
            Quaternion signRest = sign != null ? sign.localRotation : Quaternion.identity;
            yield return Ease.Run(0.5f, t =>
            {
                float a = 2.2f * Mathf.Exp(-t * 6f) * Mathf.Sin(t * 50f);
                cabinet.localRotation = Quaternion.AngleAxis(a, axisLocal) * cabinetRest;
                if (sign != null) sign.localRotation = signRest * Quaternion.Euler(0f, 0f, a * 3f);
            });
            cabinet.localRotation = cabinetRest;
            if (sign != null) sign.localRotation = signRest;
            if (hum != null) hum.SetVolumeScale(1f, 0.4f);

            if (hits >= hitsToFix) yield return Fix();
            busy = false;
        }

        IEnumerator Fix()
        {
            Fixed = true;
            // El cartel se despega y cae al piso.
            if (sign != null)
            {
                Vector3 p0 = sign.position;
                Quaternion r0 = sign.rotation;
                Vector3 p1 = new Vector3(p0.x, floorY + 0.01f, p0.z) + screenNormal * 0.35f;
                Quaternion r1 = Quaternion.AngleAxis(90f, Vector3.Cross(screenNormal, Vector3.up).normalized) * r0 * Quaternion.Euler(0f, 0f, 12f);
                yield return Ease.Run(0.55f, t =>
                {
                    Vector3 p = Vector3.Lerp(p0, p1, t);
                    p.y = Mathf.Lerp(p0.y, p1.y, Ease.InCubic(t)); // cae acelerando
                    sign.SetPositionAndRotation(p, Quaternion.Slerp(r0, r1, Ease.InCubic(t)));
                });
                Vector3 bounce = p1 + Vector3.up * 0.03f;
                yield return Ease.Run(0.12f, t => sign.position = Vector3.Lerp(p1, bounce, Ease.OutCubic(t)));
                yield return Ease.Run(0.12f, t => sign.position = Vector3.Lerp(bounce, p1, Ease.InCubic(t)));
            }
            yield return new WaitForSeconds(0.4f);
            ArcadeScreen.Create(transform, screenCenter + screenNormal * 0.003f, screenNormal, screenSize);
            if (hum != null) hum.SetVolumeScale(0.35f, 2f); // anda: ya no zumba roto
        }
    }

    /// <summary>Pantalla del arcade: se prende como un tubo (línea que se abre) y muestra un Pong que se juega solo.</summary>
    public class ArcadeScreen : MonoBehaviour
    {
        RectTransform root, ball, left, right;
        TextMeshProUGUI insert;
        Image flash;
        Vector2 vel = new Vector2(150f, 95f);
        Vector2 size;
        int scoreL, scoreR;
        TextMeshProUGUI score;
        bool running;

        public static ArcadeScreen Create(Transform parent, Vector3 center, Vector3 normal, Vector2 meters)
        {
            // Sin padre: el modelo del arcade puede traer escala interna (FBX) y la pantalla la heredaría.
            var go = new GameObject("Pantalla arcade", typeof(RectTransform));
            // Mira hacia el salón y respeta la inclinación de la pantalla.
            go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(-normal, Vector3.ProjectOnPlane(Vector3.up, normal)));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var s = go.AddComponent<ArcadeScreen>();
            s.root = (RectTransform)go.transform;
            s.size = new Vector2(meters.x * 1000f, meters.y * 1000f);
            s.root.sizeDelta = s.size;
            s.root.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            s.Build();
            s.StartCoroutine(s.TurnOn());
            return s;
        }

        void Build()
        {
            var bg = Make<Image>("Fondo", Vector2.zero, size);
            bg.color = new Color(0.01f, 0.02f, 0.03f);
            left = Make<Image>("Paleta izq", new Vector2(-size.x * 0.42f, 0f), new Vector2(10f, 50f)).rectTransform;
            right = Make<Image>("Paleta der", new Vector2(size.x * 0.42f, 0f), new Vector2(10f, 50f)).rectTransform;
            ball = Make<Image>("Pelota", Vector2.zero, new Vector2(10f, 10f)).rectTransform;
            for (int i = 0; i < 9; i++)
            {
                var dash = Make<Image>("Red", new Vector2(0f, -size.y * 0.4f + i * size.y * 0.1f), new Vector2(3f, 12f));
                dash.color = new Color(1f, 1f, 1f, 0.35f);
            }
            score = Make<TextMeshProUGUI>("Puntaje", new Vector2(0f, size.y * 0.36f), new Vector2(size.x, 40f));
            score.fontSize = 30; score.alignment = TextAlignmentOptions.Center; score.color = Color.white;
            insert = Make<TextMeshProUGUI>("Insert coin", new Vector2(0f, -size.y * 0.36f), new Vector2(size.x, 36f));
            insert.text = "INSERT COIN"; insert.fontSize = 24; insert.alignment = TextAlignmentOptions.Center;
            insert.color = new Color(1f, 0.85f, 0.3f);
            flash = Make<Image>("Destello", Vector2.zero, size);
            flash.color = new Color(1f, 1f, 1f, 0f);
            foreach (var g in GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            UpdateScore();
        }

        IEnumerator TurnOn()
        {
            // Tubo que arranca: una línea brillante que se abre en vertical.
            Vector3 s = root.localScale;
            flash.color = Color.white;
            yield return Ease.Run(0.12f, t => root.localScale = new Vector3(s.x * Mathf.Lerp(0.2f, 1f, t), s.y * 0.02f, s.z));
            yield return Ease.Run(0.18f, t => root.localScale = new Vector3(s.x, s.y * Mathf.Lerp(0.02f, 1f, Ease.OutCubic(t)), s.z));
            yield return Ease.Run(0.5f, t => flash.color = new Color(1f, 1f, 1f, 1f - t));
            running = true;
        }

        void Update()
        {
            if (!running) return;
            float dt = Time.deltaTime;
            Vector2 p = ball.anchoredPosition + vel * dt;
            float halfH = size.y * 0.5f - 8f, halfW = size.x * 0.42f;
            if (p.y > halfH || p.y < -halfH) { vel.y = -vel.y; p.y = Mathf.Clamp(p.y, -halfH, halfH); }
            // Paletas: siguen la pelota con un poco de retraso (a veces fallan).
            Follow(left, p.y, vel.x < 0f ? 1f : 0.3f, dt);
            Follow(right, p.y, vel.x > 0f ? 1f : 0.3f, dt);
            if (p.x < -halfW + 10f || p.x > halfW - 10f)
            {
                var paddle = p.x < 0f ? left : right;
                if (Mathf.Abs(paddle.anchoredPosition.y - p.y) < 32f)
                {
                    vel.x = -vel.x * 1.04f;
                    vel.y += Random.Range(-40f, 40f);
                    p.x = Mathf.Clamp(p.x, -halfW + 10f, halfW - 10f);
                }
                else if (p.x < -halfW - 20f || p.x > halfW + 20f)
                {
                    if (p.x < 0f) scoreR++; else scoreL++;
                    UpdateScore();
                    p = Vector2.zero;
                    vel = new Vector2((Random.value < 0.5f ? -1f : 1f) * 150f, Random.Range(-100f, 100f));
                }
            }
            ball.anchoredPosition = p;
            insert.alpha = Mathf.Repeat(Time.time, 1.2f) < 0.7f ? 1f : 0f;
        }

        void Follow(RectTransform paddle, float y, float skill, float dt)
        {
            Vector2 pos = paddle.anchoredPosition;
            pos.y = Mathf.MoveTowards(pos.y, y, 120f * skill * dt);
            float lim = size.y * 0.5f - 30f;
            pos.y = Mathf.Clamp(pos.y, -lim, lim);
            paddle.anchoredPosition = pos;
        }

        void UpdateScore() { if (score != null) score.text = $"{scoreL}   {scoreR}"; }

        T Make<T>(string name, Vector2 pos, Vector2 sz) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sz;
            return go.AddComponent<T>();
        }
    }
}
