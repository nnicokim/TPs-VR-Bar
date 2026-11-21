using UnityEngine;
using UnityEngine.Rendering;

namespace BarInteraccion
{
    /// <summary>
    /// Punto de teletransporte (patrón de control del punto de vista: saltos entre lugares fijos).
    /// En Cardboard no hay joystick y el desplazamiento continuo marea, así que el participante
    /// se mueve mirando un aro en el piso y confirmando. El aro "respira" para que se note sin
    /// gritar, se ilumina al mirarlo y muestra el nombre del lugar.
    /// El objeto apunta (eje Z azul) hacia donde queda mirando el participante al llegar.
    /// </summary>
    public class TeleportPoint : GazeInteractable
    {
        [Tooltip("Al llegar, girar la vista para mirar hacia el eje Z de este punto.")]
        public bool alignRotation = true;

        public Material markerMaterial;
        public float radius = 0.28f;
        public Color idleColor = new Color(0.45f, 0.8f, 1f, 1f);

        public bool Occupied { get; private set; }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        Transform marker;
        MeshRenderer markerRenderer;
        MaterialPropertyBlock block;
        float phase;

        void Reset()
        {
            labelOffset = new Vector3(0f, 0.45f, 0f);
            dwellTime = 1f;
        }

        void Awake()
        {
            phase = Random.value * 10f;
            if (GetComponent<Collider>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(radius * 3f, 0.08f, radius * 3f);
                box.center = new Vector3(0f, 0.04f, 0f);
            }
            BuildMarker();
        }

        public void SetOccupied(bool value)
        {
            Occupied = value;
            if (marker != null) marker.gameObject.SetActive(!value);
        }

        protected override bool IsAvailable() => !Occupied && GazeTeleporter.Instance != null && !GazeTeleporter.Instance.Busy;

        public override void OnSelect(GazePointer pointer)
        {
            if (GazeTeleporter.Instance != null) GazeTeleporter.Instance.Teleport(this);
        }

        protected override void ApplyHighlight(float amount)
        {
            if (markerRenderer == null) return;
            float breathe = 0.5f + 0.5f * Mathf.Sin((Time.time + phase) * 2.2f);
            Color c = Color.Lerp(idleColor * Mathf.Lerp(0.45f, 0.75f, breathe), highlightColor * 1.3f, amount);
            c.a = 1f;
            if (block == null) block = new MaterialPropertyBlock();
            markerRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            markerRenderer.SetPropertyBlock(block);
            marker.localScale = Vector3.one * Mathf.Lerp(1f, 1.2f, Ease.OutCubic(amount));
        }

        void BuildMarker()
        {
            var go = new GameObject("__aro");
            go.AddComponent<NoGazeOutline>();
            marker = go.transform;
            marker.SetParent(transform, false);
            marker.localPosition = new Vector3(0f, 0.012f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = RingMesh(radius * 0.78f, radius, 48);
            markerRenderer = go.AddComponent<MeshRenderer>();
            if (markerMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null) markerMaterial = new Material(shader);
            }
            markerRenderer.sharedMaterial = markerMaterial;
            markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            markerRenderer.receiveShadows = false;
            markerRenderer.lightProbeUsage = LightProbeUsage.Off;
            ApplyHighlight(0f);
        }

        static Mesh RingMesh(float inner, float outer, int segments)
        {
            var verts = new Vector3[segments * 2];
            var normals = new Vector3[segments * 2];
            var uvs = new Vector2[segments * 2];
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = dir * inner;
                verts[i * 2 + 1] = dir * outer;
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
                uvs[i * 2] = new Vector2((float)i / segments, 0f);
                uvs[i * 2 + 1] = new Vector2((float)i / segments, 1f);
                int n = (i + 1) % segments;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = n * 2; tris[t + 2] = i * 2 + 1;
                tris[t + 3] = i * 2 + 1; tris[t + 4] = n * 2; tris[t + 5] = n * 2 + 1;
            }
            var mesh = new Mesh { name = "Aro teletransporte" };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.45f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.02f, radius);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.02f, transform.position + Vector3.up * 0.02f + transform.forward * 0.6f);
        }
    }
}
