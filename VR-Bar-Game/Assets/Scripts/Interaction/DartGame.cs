using System.Collections;
using System.Collections.Generic;
using BarAudio;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BarInteraccion
{
    /// <summary>
    /// Dardos (va en el tablero). Patrón de selección "apuntar con la cabeza": se tira hacia donde se mira,
    /// con un desvío aleatorio (la puntería humana). Se usan los dardos reales de la mesa: 3 por ronda,
    /// alternando azules y rojos. El puntaje sigue el tablero reglamentario (sectores, doble, triple, bull)
    /// y se anota en una pizarrita al lado. Después de 3 tiros, mirar el tablero de nuevo junta los dardos.
    /// Solo se puede tirar de cerca (desde el punto "Dardos").
    /// Como desde 2,4 m un dardo clavado casi no se ve, después de cada tiro aparece una vista de cerca:
    /// un diagrama del tablero (dibujado por código, como en las transmisiones) con los dardos de la ronda,
    /// el último destacado, y la jugada. Además un anillo destella en el impacto.
    /// (Sin segunda cámara: en URP + XR una cámara extra a textura ensucia el render del visor y es cara en celular.)
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

        [Header("Vista de cerca")]
        public bool showCloseUp = true;
        public float closeUpSeconds = 3f;

        [Tooltip("Color de los dardos anteriores en la vista de cerca (no existe en el tablero: contrasta).")]
        public Color dartColor = new Color(0f, 0.9f, 1f, 1f);      // cian
        [Tooltip("Color del último dardo (late).")]
        public Color lastDartColor = new Color(1f, 0.15f, 0.85f, 1f); // magenta

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
        Canvas closeCanvas;
        RectTransform diagram;
        readonly List<Vector2> roundHits = new List<Vector2>(); // posiciones de la ronda en "mm" de tablero
        readonly List<Image> hitDots = new List<Image>();
        static Texture2D boardTexture;
        TextMeshProUGUI closeCaption;
        Coroutine closeRoutine;
        static Sprite ringSprite, dotSprite;

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

        /// <summary>La etiqueta ("Tirar") va arriba del borde del tablero y separada de la pared (si no, se corta contra ella).</summary>
        protected override Vector3 LabelPosition =>
            boardCenter + boardNormal * 0.15f + Vector3.up * (scoringRadius * 225.5f / 170f + 0.16f);

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
            roundHits.Add(BoardMm(target));
            roundScore += points;
            marks.Add(mark);
            thrown++;
            UpdatePanel();
            StartCoroutine(HitMarker(target));
            if (showCloseUp)
            {
                string caption = points > 0 ? $"{mark}  ·  +{points}" : "Afuera";
                if (thrown == 3) caption += $"\n<size=75%>Ronda: {roundScore}</size>";
                if (closeRoutine != null) StopCoroutine(closeRoutine);
                closeRoutine = StartCoroutine(CloseUp(caption));
            }

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
            roundHits.Clear();
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

        // ---------- Vista de cerca ----------

        IEnumerator CloseUp(string caption)
        {
            if (closeCanvas == null) BuildCloseUp();
            Transform cam = GazePointer.Instance != null ? GazePointer.Instance.transform : null;
            if (cam == null) yield break;

            // Panel entre el participante y el tablero, corrido hacia un costado para no taparlo.
            Vector3 toBoard = boardCenter - cam.position;
            Vector3 flat = toBoard; flat.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, flat.normalized);
            Vector3 pos = cam.position + toBoard * 0.5f + side * 0.45f + Vector3.up * 0.05f;
            closeCanvas.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - cam.position, Vector3.up));
            closeCaption.text = caption;
            UpdateDots();

            closeCanvas.gameObject.SetActive(true);
            Transform panel = closeCanvas.transform;
            Vector3 full = Vector3.one * 0.001f;
            yield return Ease.Run(0.25f, t => panel.localScale = full * Mathf.Lerp(0.6f, 1f, Ease.OutBack(t)));
            // El último dardo late.
            Transform last = hitDots.Count > 0 && roundHits.Count > 0 ? hitDots[roundHits.Count - 1].transform.parent : null;
            yield return Ease.Run(closeUpSeconds, t =>
            {
                if (last != null) last.localScale = Vector3.one * 1.25f * (1f + 0.3f * Mathf.Abs(Mathf.Sin(t * closeUpSeconds * 5f)));
            });
            yield return Ease.Run(0.2f, t => panel.localScale = full * Mathf.Lerp(1f, 0.6f, t));
            closeCanvas.gameObject.SetActive(false);
            closeRoutine = null;
        }

        void BuildCloseUp()
        {
            var go = new GameObject("Vista de cerca (dardos)", typeof(RectTransform));
            closeCanvas = go.AddComponent<Canvas>();
            closeCanvas.renderMode = RenderMode.WorldSpace;
            closeCanvas.sortingOrder = 300;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(400f, 470f);
            rt.localScale = Vector3.one * 0.001f; // 40 cm de ancho
            var frame = UiChild<Image>(rt, "Marco", Vector2.zero, new Vector2(400f, 470f));
            frame.color = new Color(0.06f, 0.08f, 0.07f, 0.95f);

            // Diagrama del tablero: 380 px = 2 x 205 "mm" (hasta los números).
            var img = UiChild<RawImage>(rt, "Tablero", new Vector2(0f, 30f), new Vector2(380f, 380f));
            if (boardTexture == null) boardTexture = BoardTexture(256);
            img.texture = boardTexture;
            diagram = img.rectTransform;
            for (int i = 0; i < 20; i++)
            {
                float ang = (90f - i * 18f) * Mathf.Deg2Rad;
                var num = UiChild<TextMeshProUGUI>(diagram, $"N{Numbers[i]}", new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 188f * MmToPx, new Vector2(40f, 24f));
                num.text = Numbers[i].ToString();
                num.fontSize = 16; num.alignment = TextAlignmentOptions.Center;
                num.color = new Color(0.95f, 0.93f, 0.85f);
            }
            for (int i = 0; i < 3; i++)
            {
                // Punto de color con borde negro: se lee sobre los sectores claros y sobre los oscuros.
                var border = UiChild<Image>(diagram, $"Dardo {i + 1}", Vector2.zero, new Vector2(22f, 22f));
                border.sprite = RingSprite(0f);
                border.color = Color.black;
                var fill = UiChild<Image>(border.rectTransform, "Color", Vector2.zero, new Vector2(15f, 15f));
                fill.sprite = RingSprite(0f);
                border.gameObject.SetActive(false);
                hitDots.Add(fill);
            }

            closeCaption = UiChild<TextMeshProUGUI>(rt, "Jugada", new Vector2(0f, -195f), new Vector2(380f, 70f));
            closeCaption.fontSize = 40;
            closeCaption.fontStyle = FontStyles.Bold;
            closeCaption.alignment = TextAlignmentOptions.Center;
            closeCaption.color = new Color(1f, 0.85f, 0.4f);
            closeCaption.richText = true;
            foreach (var g in go.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            go.SetActive(false);
        }

        const float MmToPx = 190f / 205f;

        void UpdateDots()
        {
            for (int i = 0; i < hitDots.Count; i++)
            {
                var dot = (RectTransform)hitDots[i].transform.parent; // el borde negro lleva la posición
                bool on = i < roundHits.Count;
                dot.gameObject.SetActive(on);
                if (!on) continue;
                Vector2 mm = roundHits[i];
                if (mm.magnitude > 200f) mm = mm.normalized * 200f; // afuera: en el borde
                dot.anchoredPosition = mm * MmToPx;
                bool last = i == roundHits.Count - 1;
                dot.localScale = Vector3.one * (last ? 1.25f : 1f);
                hitDots[i].color = last ? lastDartColor : dartColor;
                dot.SetAsLastSibling();
            }
        }

        /// <summary>Posición del impacto en "mm" de un tablero reglamentario, con el 20 arriba.</summary>
        Vector2 BoardMm(Vector3 p)
        {
            Vector3 n = boardNormal;
            Vector3 d = p - boardCenter; d -= n * Vector3.Dot(d, n);
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, -n).normalized;
            float k = 170f / Mathf.Max(0.01f, scoringRadius);
            Vector2 v = new Vector2(Vector3.Dot(d, right), Vector3.Dot(d, up)) * k;
            float a = -rotationOffset * Mathf.Deg2Rad; // mismo giro que el puntaje
            return new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));
        }

        /// <summary>Tablero reglamentario dibujado en una textura (sectores, triple, doble, 25 y bull).</summary>
        static Texture2D BoardTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            Color32 black = new Color32(25, 25, 25, 255), cream = new Color32(235, 220, 180, 255);
            Color32 red = new Color32(200, 40, 40, 255), green = new Color32(30, 140, 60, 255), clear = new Color32(0, 0, 0, 0);
            Color32 wire = new Color32(170, 170, 170, 255);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half * 205f, dy = (y + 0.5f - half) / half * 205f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 c;
                    if (r > 170f) c = r < 172f ? wire : clear;
                    else if (r <= 6.35f) c = red;
                    else if (r <= 15.9f) c = green;
                    else
                    {
                        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        int idx = (int)(Mathf.Repeat(90f - ang + 9f, 360f) / 18f) % 20;
                        bool even = idx % 2 == 0;
                        bool ring = (r >= 99f && r <= 107f) || r >= 162f;
                        c = ring ? (even ? red : green) : (even ? black : cream);
                    }
                    px[y * size + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Sprite RingSprite(float inner)
        {
            if (inner <= 0f) { if (dotSprite == null) dotSprite = GazeReticle.CircleSprite(0f); return dotSprite; }
            if (ringSprite == null) ringSprite = GazeReticle.CircleSprite(inner);
            return ringSprite;
        }

        /// <summary>Anillo que destella donde se clavó el dardo.</summary>
        IEnumerator HitMarker(Vector3 point)
        {
            var go = new GameObject("Impacto", typeof(RectTransform));
            go.transform.SetPositionAndRotation(point + boardNormal * 0.004f, Quaternion.LookRotation(-boardNormal, Vector3.up));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(100f, 100f);
            var img = UiChild<Image>(rt, "Anillo", Vector2.zero, new Vector2(100f, 100f));
            img.sprite = RingSprite(0.72f);
            img.raycastTarget = false;
            Color c = lastDartColor;
            yield return Ease.Run(1.4f, t =>
            {
                rt.localScale = Vector3.one * 0.0006f * Mathf.Lerp(0.6f, 1.4f, Ease.OutCubic(t)); // 4 → 8 cm
                img.color = new Color(c.r, c.g, c.b, 1f - t);
            });
            Destroy(go);
        }

        static T UiChild<T>(RectTransform parent, string name, Vector2 pos, Vector2 size) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go.AddComponent<T>();
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
