using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BarInteraccion
{
    /// <summary>
    /// Una botella del estante que se puede usar. Al elegirla, la ShotStation la levanta, la lleva
    /// volando sobre el vaso, sirve y la devuelve a su lugar. Recuerda su pose original.
    /// Como las botellas están detrás del barman, el pedido obliga a darse vuelta: usa el espacio de la RV.
    /// Cada botella usable tiene un cartelito en el borde del estante (WHISKY, GIN…), siempre visible:
    /// distingue las que se usan de las decorativas y se puede mirar para elegirla.
    /// </summary>
    public class BottleSource : GazeInteractable
    {
        public ShotStation station;

        [Header("Bebida")]
        public string drinkName = "Whisky";
        public Color liquidColor = new Color(0.75f, 0.42f, 0.12f, 1f);

        [Header("Cartel del estante")]
        public bool showTag = true;
        [Tooltip("Texto del cartel. Vacío: el nombre de la bebida en mayúsculas.")]
        public string tagText = "";
        [Tooltip("Centro del cartel (mundo). Lo calcula el armado: borde del estante, debajo de la botella.")]
        public Vector3 tagPosition;
        [Tooltip("Hacia dónde mira el frente del cartel (hacia el barman).")]
        public Vector3 tagFacing = Vector3.right;
        public Material tagMaterial;
        public Vector2 tagSize = new Vector2(0.095f, 0.04f);

        public Vector3 RestPosition { get; private set; }
        public Quaternion RestRotation { get; private set; }

        /// <summary>Punta del pico, relativa a la pose (en el espacio de rotación de la botella).</summary>
        public Vector3 TipOffset { get; private set; }

        /// <summary>Centro de la botella, relativo a la pose.</summary>
        public Vector3 CenterOffset { get; private set; }

        public float Height { get; private set; }

        void Awake()
        {
            RestPosition = transform.position;
            RestRotation = transform.rotation;
            Bounds b = WorldBounds();
            Height = b.size.y;
            Quaternion inv = Quaternion.Inverse(RestRotation);
            TipOffset = inv * (new Vector3(b.center.x, b.max.y, b.center.z) - RestPosition);
            CenterOffset = inv * (b.center - RestPosition);
            if (showTag) BuildTag();
        }

        void BuildTag()
        {
            Vector3 facing = tagFacing; facing.y = 0f;
            if (facing.sqrMagnitude < 1e-6f) facing = Vector3.right;
            facing.Normalize();
            Vector3 pos = tagPosition != Vector3.zero ? tagPosition : FindShelfEdge(facing);

            var root = new GameObject($"Cartel {drinkName}");
            root.transform.SetParent(transform.parent, false); // no cuelga de la botella: se queda en el estante
            // El frente del cartel (donde está el texto) mira hacia "facing"; su +Z apunta al estante.
            root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-facing, Vector3.up));

            var card = RuntimeMaterials.CreatePrimitive(PrimitiveType.Cube);
            card.name = "Tarjeta";
            card.transform.SetParent(root.transform, false);
            card.transform.localScale = new Vector3(tagSize.x, tagSize.y, 0.004f);
            var r = card.GetComponent<Renderer>();
            if (tagMaterial != null) r.sharedMaterial = tagMaterial;
            else
            {
                // Sin material asignado: tarjeta oscura igual (si no, queda blanca y el texto claro no se lee).
                var block = new MaterialPropertyBlock();
                var dark = new Color(0.09f, 0.13f, 0.11f, 1f);
                block.SetColor("_BaseColor", dark);
                block.SetColor("_Color", dark);
                r.SetPropertyBlock(block);
            }
            r.shadowCastingMode = ShadowCastingMode.Off;
            var col = card.GetComponent<BoxCollider>();
            if (col != null) col.isTrigger = true;
            card.AddComponent<GazeProxy>().target = this;

            var textGo = new GameObject("Texto");
            textGo.transform.SetParent(root.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.0025f); // apenas delante de la tarjeta
            var t = textGo.AddComponent<TextMeshPro>();
            t.text = string.IsNullOrEmpty(tagText) ? drinkName.ToUpperInvariant() : tagText;
            t.fontSize = 0.2f; // ~2 cm de alto (10 pt ≈ 1 m)
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.95f, 0.93f, 0.85f, 1f);
            t.rectTransform.sizeDelta = new Vector2(tagSize.x - 0.008f, tagSize.y);
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.08f;
            t.fontSizeMax = 0.2f;
            var tr = textGo.GetComponent<MeshRenderer>();
            if (tr != null) tr.shadowCastingMode = ShadowCastingMode.Off;
        }

        protected override bool IsAvailable() => station != null && station.CanPour;

        public override void OnSelect(GazePointer pointer)
        {
            if (station != null) station.Pour(this);
        }

        /// <summary>Ubica la botella con la rotación dada de forma que la punta del pico quede en "tip".</summary>
        public void PlaceTipAt(Vector3 tip, Quaternion rotation)
        {
            transform.SetPositionAndRotation(tip - rotation * TipOffset, rotation);
        }

        public Vector3 TipPosition => transform.position + transform.rotation * TipOffset;

        /// <summary>Borde frontal del estante que está debajo de la botella (si no lo encuentra, un poco adelante y abajo).</summary>
        Vector3 FindShelfEdge(Vector3 facing)
        {
            Vector3 fallback = RestPosition + facing * 0.12f + Vector3.down * 0.02f;
            if (!Physics.Raycast(RestPosition + Vector3.up * 0.03f, Vector3.down, out RaycastHit hit, 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return fallback;
            Bounds sb = hit.collider.bounds;
            float edge = Vector3.Dot(sb.center, facing) + Mathf.Abs(Vector3.Dot(sb.extents, facing));
            Vector3 onEdge = RestPosition + facing * (edge - Vector3.Dot(RestPosition, facing) + 0.004f);
            return new Vector3(onEdge.x, sb.center.y - 0.006f, onEdge.z);
        }

        Bounds WorldBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(transform.position, Vector3.one * 0.1f);
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
