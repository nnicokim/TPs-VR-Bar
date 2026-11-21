using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BarInteraccion
{
    /// <summary>
    /// Base de todo lo que se usa con la mirada. Resuelve los "ofrecimientos" comunes:
    ///  - Resaltado (contorno) cuando se lo mira: dice "esto se puede usar".
    ///  - "Attract": un pulso suave sin mirarlo, para señalar el próximo paso (por ej. la jarra llena).
    ///  - Etiqueta flotante con el nombre, solo mientras se lo mira (no ensucia la escena).
    /// Las subclases deciden qué pasa al elegirlo (OnSelect) y cuándo está disponible (IsAvailable).
    /// </summary>
    public abstract class GazeInteractable : MonoBehaviour, IGazeable
    {
        [Header("Mirada")]
        [Tooltip("Texto que aparece al mirarlo. Vacío: sin etiqueta.")]
        public string label;

        [Tooltip("Dónde aparece la etiqueta, relativo a este objeto (en metros, ejes del mundo).")]
        public Vector3 labelOffset = new Vector3(0f, 0.15f, 0f);

        [Tooltip("Segundos de mirada fija para elegirlo. 0: el valor del GazePointer.")]
        public float dwellTime = 0f;

        [Header("Resaltado")]
        [Tooltip("Qué se contornea. Vacío: este objeto y sus hijos.")]
        public Transform highlightRoot;

        [Tooltip("Grosor del contorno en metros.")]
        public float outlineWidth = 0.004f;

        public Color highlightColor = new Color(1f, 0.78f, 0.35f, 1f);

        public bool IsGazed { get; private set; }

        /// <summary>Pulso suave aunque no se lo mire: "esto es lo próximo".</summary>
        public bool Attract { get; set; }

        public bool CanInteract => isActiveAndEnabled && IsAvailable();
        float IGazeable.DwellTime => dwellTime;

        protected float HighlightAmount { get; private set; }

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int WidthId = Shader.PropertyToID("_Width");

        List<Renderer> outlines;
        MaterialPropertyBlock block;
        TextMeshPro labelText;
        float labelAlpha;

        protected virtual bool IsAvailable() => true;

        public virtual void OnGazeEnter(GazePointer pointer) { IsGazed = true; }
        public virtual void OnGazeExit(GazePointer pointer) { IsGazed = false; }
        public abstract void OnSelect(GazePointer pointer);

        protected virtual void LateUpdate()
        {
            float target = 0f;
            if (IsGazed && CanInteract) target = 1f;
            else if (Attract) target = Mathf.Lerp(0.2f, 0.65f, 0.5f + 0.5f * Mathf.Sin(Time.time * 4f));
            HighlightAmount = Mathf.MoveTowards(HighlightAmount, target, Time.deltaTime * 6f);
            ApplyHighlight(HighlightAmount);
            UpdateLabel();
        }

        protected virtual void OnDisable()
        {
            IsGazed = false;
            HighlightAmount = 0f;
            ApplyHighlight(0f);
            labelAlpha = 0f;
            if (labelText != null) labelText.gameObject.SetActive(false);
        }

        // ---------- Resaltado por contorno (inverted hull) ----------

        /// <summary>Por defecto: contorno. Las subclases pueden cambiarlo (la jarra se tiñe, el marcador brilla).</summary>
        protected virtual void ApplyHighlight(float amount)
        {
            if (amount <= 0.001f)
            {
                if (outlines != null) foreach (var r in outlines) if (r != null) r.enabled = false;
                return;
            }
            if (outlines == null) BuildOutlines();
            if (block == null) block = new MaterialPropertyBlock();
            Color c = highlightColor;
            c *= Mathf.Lerp(0.6f, 1.2f, amount);
            c.a = 1f;
            foreach (var r in outlines)
            {
                if (r == null) continue;
                r.enabled = true;
                r.GetPropertyBlock(block);
                block.SetColor(ColorId, c);
                block.SetFloat(WidthId, outlineWidth * amount);
                r.SetPropertyBlock(block);
            }
        }

        void BuildOutlines()
        {
            outlines = new List<Renderer>();
            Material mat = GazePointer.OutlineMaterial;
            if (mat == null) return; // sin material de contorno: no hay resaltado, pero todo lo demás funciona
            Transform root = highlightRoot != null ? highlightRoot : transform;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.GetComponent<NoGazeOutline>() != null) continue;
                if (mr.GetComponent<TMP_Text>() != null) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var go = new GameObject("__contorno");
                go.AddComponent<NoGazeOutline>();
                go.transform.SetParent(mr.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                var mats = new Material[Mathf.Max(1, mf.sharedMesh.subMeshCount)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.enabled = false;
                outlines.Add(r);
            }
        }

        // ---------- Etiqueta ----------

        protected virtual Vector3 LabelPosition => transform.position + labelOffset;

        void UpdateLabel()
        {
            bool show = IsGazed && CanInteract && !string.IsNullOrEmpty(label);
            labelAlpha = Mathf.MoveTowards(labelAlpha, show ? 1f : 0f, Time.deltaTime * 5f);
            if (labelAlpha <= 0f)
            {
                if (labelText != null && labelText.gameObject.activeSelf) labelText.gameObject.SetActive(false);
                return;
            }
            if (labelText == null) BuildLabel();
            if (labelText == null) return;
            if (!labelText.gameObject.activeSelf) labelText.gameObject.SetActive(true);

            Transform cam = GazePointer.Instance != null ? GazePointer.Instance.transform : (Camera.main != null ? Camera.main.transform : null);
            Vector3 pos = LabelPosition;
            labelText.transform.position = pos;
            if (cam != null)
            {
                Vector3 toLabel = pos - cam.position;
                if (toLabel.sqrMagnitude > 0.0001f) labelText.transform.rotation = Quaternion.LookRotation(toLabel, Vector3.up);
                // Tamaño angular constante (~1,8°): legible de cerca y de lejos. En TextMeshPro 3D, 10 pt ≈ 1 m.
                labelText.fontSize = Mathf.Clamp(toLabel.magnitude * 0.032f, 0.02f, 0.4f) * 10f;
            }
            if (labelText.text != label) labelText.text = label;
            Color c = labelText.color;
            c.a = labelAlpha;
            labelText.color = c;
        }

        void BuildLabel()
        {
            var go = new GameObject("__etiqueta");
            go.AddComponent<NoGazeOutline>();
            go.transform.SetParent(transform, false);
            labelText = go.AddComponent<TextMeshPro>();
            labelText.text = label;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = new Color(1f, 0.95f, 0.85f, 0f);
            labelText.rectTransform.sizeDelta = new Vector2(10f, 2f);
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        }
    }
}
