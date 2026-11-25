using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BarInteraccion
{
    /// <summary>
    /// Punto de teletransporte (patrón de control del punto de vista: saltos entre lugares fijos).
    /// En Cardboard no hay joystick y el desplazamiento continuo marea, así que el participante
    /// se mueve mirando un aro en el piso y confirmando. El aro "respira" para que se note sin
    /// gritar, se ilumina al mirarlo y muestra el nombre del lugar.
    /// Arriba del aro flota un indicador (con un haz fino hasta el piso) que también se puede mirar:
    /// así el punto se ve y se elige aunque el piso quede tapado (por ejemplo, detrás de la barra).
    /// El objeto apunta (eje Z azul) hacia donde queda mirando el participante al llegar.
    /// </summary>
    public class TeleportPoint : GazeInteractable
    {
        [Tooltip("Al llegar, girar la vista para mirar hacia el eje Z de este punto.")]
        public bool alignRotation = true;

        public Material markerMaterial;
        public float radius = 0.28f;
        public Color idleColor = new Color(0.45f, 0.8f, 1f, 1f);

        [Tooltip("Indicador flotante sobre el aro, visible por encima de muebles y mostradores.")]
        public bool showBeacon = true;
        [Tooltip("Altura del indicador sobre el piso (m).")]
        public float beaconHeight = 1.55f;

        public bool Occupied { get; private set; }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        Transform marker, ring, beacon, beam;
        MeshRenderer markerRenderer, beaconRenderer, beamRenderer;
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

        protected override Vector3 LabelPosition =>
            showBeacon ? transform.position + Vector3.up * (beaconHeight + 0.28f) : base.LabelPosition;

        protected override void ApplyHighlight(float amount)
        {
            if (markerRenderer == null) return;
            float breathe = 0.5f + 0.5f * Mathf.Sin((Time.time + phase) * 2.2f);
            Color c = Color.Lerp(idleColor * Mathf.Lerp(0.45f, 0.75f, breathe), highlightColor * 1.3f, amount);
            c.a = 1f;
            SetColor(markerRenderer, c);
            ring.localScale = Vector3.one * Mathf.Lerp(1f, 1.2f, Ease.OutCubic(amount));

            if (beacon != null)
            {
                Color bc = Color.Lerp(idleColor * 0.85f, highlightColor * 1.3f, amount); bc.a = 1f;
                SetColor(beaconRenderer, bc);
                Color lc = Color.Lerp(idleColor * 0.3f, highlightColor * 0.8f, amount); lc.a = 1f;
                SetColor(beamRenderer, lc);
                float t = Time.time + phase;
                beacon.localPosition = new Vector3(0f, beaconHeight + Mathf.Sin(t * 1.6f) * 0.03f, 0f);
                beacon.localRotation = Quaternion.Euler(0f, t * 45f, 0f);
                beacon.localScale = Vector3.one * Mathf.Lerp(1f, 1.35f, Ease.OutCubic(amount));
            }
        }

        void SetColor(Renderer r, Color c)
        {
            if (r == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            r.SetPropertyBlock(block);
        }

        void BuildMarker()
        {
            if (markerMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null) markerMaterial = new Material(shader);
            }

            // Raíz que se oculta cuando uno está parado en este punto.
            marker = new GameObject("__marcador").transform;
            marker.SetParent(transform, false);

            ring = MakePart("__aro", RingMesh(radius * 0.78f, radius, 48), out markerRenderer);
            ring.localPosition = new Vector3(0f, 0.012f, 0f);

            if (showBeacon)
            {
                beacon = MakePart("__faro", PinMesh(0.075f, 0.07f, 0.11f), out beaconRenderer);
                beacon.localPosition = new Vector3(0f, beaconHeight, 0f);
                var box = beacon.gameObject.AddComponent<BoxCollider>(); // se puede elegir mirándolo
                box.isTrigger = true;
                box.size = new Vector3(0.32f, 0.32f, 0.32f);

                var beamGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                var col = beamGo.GetComponent<Collider>();
                if (col != null) Destroy(col);
                beamGo.name = "__haz";
                beamGo.AddComponent<NoGazeOutline>();
                beam = beamGo.transform;
                beam.SetParent(marker, false);
                float h = Mathf.Max(0.05f, beaconHeight - 0.12f);
                beam.localPosition = new Vector3(0f, h * 0.5f, 0f);
                beam.localScale = new Vector3(0.008f, h * 0.5f, 0.008f);
                beamRenderer = beamGo.GetComponent<MeshRenderer>();
                SetupRenderer(beamRenderer);
            }
            ApplyHighlight(0f);
        }

        Transform MakePart(string name, Mesh mesh, out MeshRenderer renderer)
        {
            var go = new GameObject(name);
            go.AddComponent<NoGazeOutline>();
            go.transform.SetParent(marker, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            SetupRenderer(renderer);
            return go.transform;
        }

        void SetupRenderer(MeshRenderer r)
        {
            r.sharedMaterial = markerMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>Pirámide invertida (punta abajo): "acá", sin palabras. Caras planas, normales hacia afuera.</summary>
        static Mesh PinMesh(float halfWidth, float up, float down)
        {
            var apex = new Vector3(0f, -down, 0f);
            var b = new[]
            {
                new Vector3(-halfWidth, up, -halfWidth), new Vector3(halfWidth, up, -halfWidth),
                new Vector3(halfWidth, up, halfWidth), new Vector3(-halfWidth, up, halfWidth),
            };
            var center = new Vector3(0f, (up - down) * 0.25f, 0f);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int k = 0; k < 4; k++) AddFace(verts, tris, center, apex, b[k], b[(k + 1) % 4]);
            AddFace(verts, tris, center, b[0], b[1], b[2]);
            AddFace(verts, tris, center, b[0], b[2], b[3]);
            var mesh = new Mesh { name = "Indicador teletransporte" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddFace(List<Vector3> verts, List<int> tris, Vector3 center, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            Vector3 outward = (p0 + p1 + p2) / 3f - center;
            // En Unity la cara visible es la de orden horario: Cross(p1-p0, p2-p0) apunta hacia afuera.
            if (Vector3.Dot(n, outward) < 0f) { var tmp = p1; p1 = p2; p2 = tmp; }
            int i = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
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
