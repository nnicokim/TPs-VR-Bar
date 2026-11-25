using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BarInteraccion
{
    /// <summary>
    /// Pizarra de pedidos colgada sobre la barra, de frente al barman: interfaz diegética (no hay HUD).
    /// Se arma sola al empezar. El objeto marca el punto de donde cuelga; la pizarra mira hacia -Z local
    /// (el barman la lee desde el lado opuesto a su eje Z azul).
    /// Cuando llega un pedido se balancea como si alguien la hubiera tocado.
    /// </summary>
    public class OrderBoard : MonoBehaviour
    {
        public Material boardMaterial;
        public Material frameMaterial;
        public Vector2 size = new Vector2(0.75f, 0.48f);
        [Tooltip("Distancia entre el punto de donde cuelga y el borde superior de la pizarra.")]
        public float hangDrop = 0.08f;
        [Tooltip("Altura del techo, para las cadenas.")]
        public float ceilingHeight = 3.05f;

        public Color chalk = new Color(0.93f, 0.93f, 0.86f, 1f);
        public Color chalkDone = new Color(0.55f, 0.85f, 0.55f, 1f);
        public Color chalkWarn = new Color(1f, 0.55f, 0.45f, 1f);

        Transform swingRoot;
        TextMeshPro title, list, footer, note;
        Coroutine swing, noteRoutine;
        BoardButton[] choiceButtons;

        void Awake() => Build();

        // ---------- API ----------

        public void SetTitle(string text) { if (title != null) title.text = text; }
        public void SetFooter(string text) { if (footer != null) footer.text = text; }

        /// <summary>Lista de líneas "cliente — bebida". Las marcadas como hechas se ven tachadas en verde.</summary>
        public void SetLines(IList<(string customer, string drink, bool done)> lines, string emptyText)
        {
            if (list == null) return;
            if (lines.Count == 0) { list.text = $"<i>{emptyText}</i>"; return; }
            var sb = new StringBuilder();
            foreach (var l in lines)
            {
                if (l.done) sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(chalkDone)}><s>{l.customer} — {l.drink}</s></color>\n");
                else sb.Append($"{l.customer} — <b>{l.drink}</b>\n");
            }
            list.text = sb.ToString();
        }

        /// <summary>Lista de tareas con casillas: [ ] pendiente, [x] hecha (tachada en verde).</summary>
        public void SetChecklist(IList<(string text, bool done)> items)
        {
            if (list == null) return;
            var sb = new StringBuilder();
            foreach (var it in items)
            {
                if (it.done) sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(chalkDone)}>■ <s>{it.text}</s></color>\n");
                else sb.Append($"□ {it.text}\n");
            }
            list.text = sb.ToString();
        }

        /// <summary>Texto libre en el cuerpo de la pizarra.</summary>
        public void SetBody(string text) { if (list != null) list.text = text; }

        /// <summary>
        /// Dos opciones escritas en la pizarra que se eligen con la mirada. Llama a onChoice(0 o 1) y las borra.
        /// </summary>
        public void ShowChoice(string optionA, string optionB, System.Action<int> onChoice)
        {
            HideChoice();
            float w = size.x, cy = -hangDrop - size.y * 0.5f;
            choiceButtons = new[]
            {
                BoardButton.Create(swingRoot, optionA, new Vector3(-w * 0.24f, cy - size.y * 0.5f + 0.1f, -0.014f), chalk, () => { HideChoice(); onChoice(0); }),
                BoardButton.Create(swingRoot, optionB, new Vector3(w * 0.24f, cy - size.y * 0.5f + 0.1f, -0.014f), chalk, () => { HideChoice(); onChoice(1); }),
            };
        }

        public void HideChoice()
        {
            if (choiceButtons == null) return;
            foreach (var b in choiceButtons) if (b != null) Destroy(b.gameObject);
            choiceButtons = null;
        }

        /// <summary>Mensaje corto abajo de la lista que se borra solo (por ejemplo, un error).</summary>
        public void ShowNote(string text, bool warning, float seconds = 3f)
        {
            if (note == null) return;
            if (noteRoutine != null) StopCoroutine(noteRoutine);
            noteRoutine = StartCoroutine(NoteRoutine(text, warning ? chalkWarn : chalk, seconds));
        }

        /// <summary>Balanceo amortiguado: "llegó algo".</summary>
        public void Swing(float strength = 1f)
        {
            if (swing != null) StopCoroutine(swing);
            swing = StartCoroutine(SwingRoutine(strength));
        }

        // ---------- Animaciones ----------

        IEnumerator SwingRoutine(float strength)
        {
            float amp = 7f * strength, t = 0f, duration = 2.4f;
            while (t < duration)
            {
                float angle = amp * Mathf.Exp(-t * 1.8f) * Mathf.Sin(t * 9f);
                swingRoot.localRotation = Quaternion.Euler(angle, 0f, 0f);
                t += Time.deltaTime;
                yield return null;
            }
            swingRoot.localRotation = Quaternion.identity;
            swing = null;
        }

        IEnumerator NoteRoutine(string text, Color color, float seconds)
        {
            note.text = text;
            yield return Ease.Run(0.2f, t => { var c = color; c.a = t; note.color = c; });
            yield return new WaitForSeconds(seconds);
            yield return Ease.Run(0.6f, t => { var c = color; c.a = 1f - t; note.color = c; });
            note.text = "";
            noteRoutine = null;
        }

        // ---------- Construcción ----------

        void Build()
        {
            swingRoot = new GameObject("Pizarra (balanceo)").transform;
            swingRoot.SetParent(transform, false);

            float w = size.x, h = size.y;
            float cy = -hangDrop - h * 0.5f;

            // Tablero y marco (el frente mira a -Z local).
            Box("Tablero", new Vector3(0f, cy, 0f), new Vector3(w, h, 0.02f), boardMaterial);
            float f = 0.03f;
            Box("Marco sup", new Vector3(0f, cy + h * 0.5f, -0.005f), new Vector3(w + f, f, 0.03f), frameMaterial);
            Box("Marco inf", new Vector3(0f, cy - h * 0.5f, -0.005f), new Vector3(w + f, f, 0.03f), frameMaterial);
            Box("Marco izq", new Vector3(-w * 0.5f, cy, -0.005f), new Vector3(f, h + f, 0.03f), frameMaterial);
            Box("Marco der", new Vector3(w * 0.5f, cy, -0.005f), new Vector3(f, h + f, 0.03f), frameMaterial);

            // Cadenas hasta el techo (fuera del balanceo para que no se doblen).
            float chainLen = Mathf.Max(0.05f, ceilingHeight - transform.position.y);
            foreach (float x in new[] { -w * 0.38f, w * 0.38f })
            {
                var c = Box("Cadena", new Vector3(x, chainLen * 0.5f, 0f), new Vector3(0.008f, chainLen, 0.008f), frameMaterial);
                c.SetParent(transform, true);
                Box("Cadena (tramo)", new Vector3(x, -hangDrop * 0.5f, 0f), new Vector3(0.008f, hangDrop, 0.008f), frameMaterial);
            }

            float z = -0.012f; // apenas delante del tablero
            title = Text("Titulo", new Vector3(0f, cy + h * 0.5f - 0.06f, z), 0.62f, TextAlignmentOptions.Center, w - 0.06f);
            title.text = "PEDIDOS";
            list = Text("Lista", new Vector3(0f, cy + 0.02f, z), 0.42f, TextAlignmentOptions.Top, w - 0.08f, 0.24f);
            note = Text("Nota", new Vector3(0f, cy - h * 0.5f + 0.1f, z), 0.36f, TextAlignmentOptions.Center, w - 0.06f);
            note.text = "";
            footer = Text("Pie", new Vector3(0f, cy - h * 0.5f + 0.045f, z), 0.3f, TextAlignmentOptions.Center, w - 0.06f);
            footer.color = new Color(chalk.r, chalk.g, chalk.b, 0.75f);
        }

        Transform Box(string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = RuntimeMaterials.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(swingRoot, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        TextMeshPro Text(string name, Vector3 localPos, float fontSize, TextAlignmentOptions align, float width, float height = 0.08f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(swingRoot, false);
            go.transform.localPosition = localPos;
            // TextMeshPro se lee mirando en la dirección de su +Z: el lector está del lado -Z (frente) mirando hacia +Z.
            go.transform.localRotation = Quaternion.identity;
            var t = go.AddComponent<TextMeshPro>();
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = chalk;
            t.rectTransform.sizeDelta = new Vector2(width, height);
            t.richText = true;
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
            return t;
        }
    }

    /// <summary>Opción escrita con tiza en la pizarra: se resalta (crece y se pone cálida) al mirarla y se elige.</summary>
    public class BoardButton : GazeInteractable
    {
        TextMeshPro text;
        Color baseColor;
        System.Action onClick;

        public static BoardButton Create(Transform parent, string caption, Vector3 localPos, Color color, System.Action onClick)
        {
            var go = new GameObject($"Opción {caption}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var t = go.AddComponent<TextMeshPro>();
            t.text = $"[ {caption} ]";
            t.fontSize = 0.4f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = color;
            t.rectTransform.sizeDelta = new Vector2(0.32f, 0.07f);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.32f, 0.08f, 0.02f);
            var b = go.AddComponent<BoardButton>();
            b.text = t;
            b.baseColor = color;
            b.onClick = onClick;
            b.dwellTime = 1.2f;
            return b;
        }

        public override void OnSelect(GazePointer pointer) => onClick?.Invoke();

        protected override void ApplyHighlight(float amount)
        {
            if (text == null) return;
            text.color = Color.Lerp(baseColor, highlightColor, amount);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, Ease.OutCubic(amount));
        }
    }
}
