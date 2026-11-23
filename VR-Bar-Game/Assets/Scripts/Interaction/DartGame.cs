using System.Collections;
using System.Collections.Generic;
using BarAudio;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BarInteraccion
{
    /// <summary>
    /// Dardos (va en el tablero). Patrón de selección "apuntar con la cabeza": se tira hacia donde se mira,
    /// con un desvío aleatorio (la puntería humana). Se usan los dardos reales de la mesa: 3 por ronda,
    /// alternando azules y rojos. El puntaje sigue el tablero reglamentario (sectores, doble, triple, bull)
    /// y se anota en una pizarrita al lado. Después de 3 tiros, mirar el tablero de nuevo junta los dardos.
    /// Solo se puede tirar de cerca (desde el punto "Dardos").
    /// </summary>
    public class DartGame : GazeInteractable
    {
        [Header("Dardos (3 azules + 3 rojos, de la mesa)")]
        public Transform[] darts;
        [Tooltip("Punta y cola del dardo en coordenadas locales del modelo (las calcula el armado).")]
        public Vector3 tipLocal = new Vector3(0f, 0f, 0.08f);
        public Vector3 backLocal = new Vector3(0f, 0f, -0.08f);
        public BarSound hitSound;

        [Header("Tablero")]
        [Tooltip("Centro de la cara del tablero (mundo).")]
        public Vector3 boardCenter;
        [Tooltip("Normal de la cara, hacia el salón.")]
        public Vector3 boardNormal = Vector3.left;
        [Tooltip("Radio del anillo doble exterior (170 mm en un tablero reglamentario).")]
        public float scoringRadius = 0.17f;
        [Tooltip("Giro del tablero (grados) si el 20 no queda arriba.")]
        public float rotationOffset = 0f;

        [Header("Juego")]
        public float maxThrowDistance = 4f;
        [Tooltip("Desvío del tiro (desvío estándar, m).")]
        public float spread = 0.035f;

        [Header("Pizarrita")]
        public Vector3 scorePanelPosition;
        public Material panelMaterial;

        public int BestRound { get; private set; }

        static readonly int[] Numbers = { 20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5 };

        Vector3[] restPos;
        Quaternion[] restRot;
        int set, thrown, round = 1, roundScore;
        readonly List<string> marks = new List<string>();
        bool busy;
        TextMeshPro panelText;

        void Awake()
        {
            int n = darts != null ? darts.Length : 0;
            restPos = new Vector3[n];
            restRot = new Quaternion[n];
            for (int i = 0; i < n; i++) { restPos[i] = darts[i].position; restRot[i] = darts[i].rotation; }
            boardNormal = boardNormal.sqrMagnitude > 0f ? boardNormal.normalized : Vector3.left;
            label = "Tirar";
            BuildPanel();
            UpdatePanel();
        }

        protected override bool IsAvailable()
        {
            if (busy || darts == null || darts.Length < 3) return false;
            var cam = GazePointer.Instance != null ? GazePointer.Instance.transform : null;
            return cam != null && Vector3.Distance(cam.position, boardCenter) <= maxThrowDistance;
        }

        public override void OnSelect(GazePointer pointer)
        {
            if (!IsAvailable()) return;
            if (thrown < 3) StartCoroutine(Throw(pointer.HitPoint));
            else StartCoroutine(Collect());
        }

        // ---------- Tiro ----------

        IEnumerator Throw(Vector3 aim)
        {
            busy = true;
            Transform cam = GazePointer.Instance.transform;
            Vector3 n = boardNormal;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
            Vector3 right = Vector3.Cross(up, n).normalized;

            // Punto apuntado sobre el plano del tablero + desvío (gaussiano).
            aim -= n * Vector3.Dot(aim - boardCenter, n);
            Vector3 target = aim + right * Gauss() * spread + up * Gauss() * spread;
            Vector3 stuck = target - n * 0.012f; // la punta entra un poco

            int index = set * 3 + thrown;
            Transform dart = darts[Mathf.Min(index, darts.Length - 1)];
            Vector3 start = cam.position + cam.right * 0.16f - Vector3.up * 0.12f + Flat(cam.forward) * 0.3f;
            Vector3 control = (start + stuck) * 0.5f + Vector3.up * 0.12f;

            // Aparece "en la mano" y vuela.
            float duration = Mathf.Clamp(Vector3.Distance(start, stuck) * 0.13f, 0.18f, 0.45f);
            yield return Ease.Run(duration, t =>
            {
                Vector3 p = Bezier(start, control, stuck, t);
                Vector3 dir = BezierTangent(start, control, stuck, t);
                PlaceTip(dart, p, dir);
            });

            if (hitSound != null) hitSound.PlayOnceAt(target);
            int points = Score(target, out string mark);
            roundScore += points;
            marks.Add(mark);
            thrown++;
            UpdatePanel();

            // Tiembla clavado.
            Vector3 finalDir = BezierTangent(start, control, stuck, 1f);
            Vector3 wobbleAxis = Vector3.Cross(finalDir, Vector3.up).normalized;
            yield return Ease.Run(0.45f, t =>
            {
                float a = 7f * Mathf.Exp(-t * 5f) * Mathf.Sin(t * 45f);
                PlaceTip(dart, stuck, Quaternion.AngleAxis(a, wobbleAxis) * finalDir);
            });

            if (thrown == 3)
            {
                BestRound = Mathf.Max(BestRound, roundScore);
                UpdatePanel();
                label = "Juntar los dardos";
            }
            busy = false;
        }

        IEnumerator Collect()
        {
            busy = true;
            for (int i = 0; i < 3; i++) StartCoroutine(Return(set * 3 + i, i * 0.12f));
            yield return new WaitForSeconds(0.36f + 0.75f);
            set = 1 - set;
            thrown = 0;
            roundScore = 0;
            marks.Clear();
            round++;
            label = "Tirar";
            UpdatePanel();
            busy = false;
        }

        IEnumerator Return(int index, float delay)
        {
            if (index >= darts.Length) yield break;
            yield return new WaitForSeconds(delay);
            Transform dart = darts[index];
            Vector3 p0 = dart.position;
            Quaternion r0 = dart.rotation;
            Vector3 pulled = p0 + boardNormal * 0.05f;
            yield return Ease.Run(0.12f, t => dart.position = Vector3.Lerp(p0, pulled, t)); // sale del tablero
            Vector3 control = (pulled + restPos[index]) * 0.5f + Vector3.up * 0.25f;
            yield return Ease.Run(0.6f, t =>
            {
                float e = Ease.InOutCubic(t);
                dart.SetPositionAndRotation(Bezier(pulled, control, restPos[index], e), Quaternion.Slerp(r0, restRot[index], e));
            });
        }

        /// <summary>Ubica el dardo con la punta en "tip" apuntando hacia "dir".</summary>
        void PlaceTip(Transform dart, Vector3 tip, Vector3 dir)
        {
            Vector3 axisLocal = (tipLocal - backLocal).normalized;
            if (dir.sqrMagnitude < 1e-8f) dir = -boardNormal;
            dart.rotation = Quaternion.FromToRotation(axisLocal, dir.normalized);
            dart.position += tip - dart.TransformPoint(tipLocal);
        }

        // ---------- Puntaje ----------

        int Score(Vector3 p, out string mark)
        {
            Vector3 n = boardNormal;
            Vector3 d = p - boardCenter; d -= n * Vector3.Dot(d, n);
            float r = d.magnitude / Mathf.Max(0.01f, scoringRadius) * 170f; // en "mm" de un tablero reglamentario
            if (r > 170f) { mark = "afuera"; return 0; }
            if (r <= 6.35f) { mark = "BULL"; return 50; }
            if (r <= 15.9f) { mark = "25"; return 25; }
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, -n).normalized; // derecha de quien mira el tablero
            float ang = Mathf.Atan2(Vector3.Dot(d, up), Vector3.Dot(d, right)) * Mathf.Rad2Deg - rotationOffset;
            float fromTop = Mathf.Repeat(90f - ang + 9f, 360f); // grados en sentido horario desde arriba
            int num = Numbers[(int)(fromTop / 18f) % 20];
            if (r >= 99f && r <= 107f) { mark = $"T{num}"; return num * 3; }
            if (r >= 162f) { mark = $"D{num}"; return num * 2; }
            mark = num.ToString();
            return num;
        }

        // ---------- Pizarrita ----------

        void BuildPanel()
        {
            if (scorePanelPosition == Vector3.zero) return;
            var root = new GameObject("Pizarrita dardos").transform;
            root.SetParent(transform.parent, true);
            root.SetPositionAndRotation(scorePanelPosition, Quaternion.LookRotation(-boardNormal, Vector3.up));
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Tablero";
            Destroy(plate.GetComponent<Collider>());
            plate.transform.SetParent(root, false);
            plate.transform.localScale = new Vector3(0.42f, 0.3f, 0.015f);
            var r = plate.GetComponent<Renderer>();
            if (panelMaterial != null) r.sharedMaterial = panelMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;

            var textGo = new GameObject("Texto");
            textGo.transform.SetParent(root, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            panelText = textGo.AddComponent<TextMeshPro>();
            panelText.fontSize = 0.3f;
            panelText.alignment = TextAlignmentOptions.Center;
            panelText.color = new Color(0.93f, 0.93f, 0.86f);
            panelText.rectTransform.sizeDelta = new Vector2(0.38f, 0.26f);
            panelText.richText = true;
        }

        void UpdatePanel()
        {
            if (panelText == null) return;
            string tiros = marks.Count > 0 ? string.Join(" · ", marks) : "—";
            panelText.text = $"<b>DARDOS</b>\nRonda {round}: {tiros}\n<size=140%><b>{roundScore}</b></size>\n<size=80%>Mejor ronda: {BestRound}</size>";
        }

        // ---------- Utilidades ----------

        static float Gauss()
        {
            float u1 = 1f - Random.value, u2 = Random.value;
            return Mathf.Clamp(Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2), -2.5f, 2.5f);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, float t) => 2f * (1f - t) * (b - a) + 2f * t * (c - b);
    }
}
