using System;
using System.Collections;
using System.Collections.Generic;
using BarAudio;
using UnityEngine;
using UnityEngine.Rendering;

namespace BarInteraccion
{
    /// <summary>
    /// Estación de tragos: vaso corto con hielo sobre la barra y botellas del estante.
    ///
    ///   Libre ──(mirar una botella)──▶ Sirviendo ──▶ Listo ──(mirar el vaso)──▶ Entregando ──▶ Libre
    ///
    /// Sirviendo: el vaso entra deslizándose (suena el hielo), la botella se levanta del estante,
    /// vuela en arco hasta el vaso, se inclina y sirve, y vuelve a su lugar (tintineo de botellas).
    /// El vaso se arma por código (perfil torneado) con el material de vidrio de la jarra.
    /// </summary>
    public class ShotStation : MonoBehaviour, IDrinkStation
    {
        public enum Phase { Idle, Pouring, Full, Serving }

        [Header("Lugar")]
        [Tooltip("Punto sobre la barra donde se apoya el vaso.")]
        public Transform glassSpot;
        [Tooltip("Hacia dónde se desliza el vaso al servirlo (hacia los clientes).")]
        public Vector3 serveDirection = Vector3.right;
        public float serveDistance = 0.45f;

        [Header("Materiales")]
        public Material glassMaterial;
        public Material liquidMaterial;

        [Header("Sonido")]
        public BarSound abrirBotellaSound;
        public BarSound hieloSound;
        public BarSound botellasSound;
        public BarSound cajaSound;

        [Header("Tiempos")]
        public float flightDuration = 0.8f;
        public float pourDuration = 1.8f;
        [Range(0.2f, 0.9f)] public float fillLevel = 0.55f;
        [Tooltip("Inclinación de la botella al servir (grados).")]
        public float tiltAngle = 118f;

        public Phase State { get; private set; } = Phase.Idle;
        public bool CanPour => State == Phase.Idle && glassSpot != null;
        public bool IsReadyToServe => State == Phase.Full;
        public bool IsBusy => State == Phase.Pouring || State == Phase.Serving;
        public string CurrentDrink { get; private set; }
        public event Action<string> DrinkStarted;
        public event Action<string> DrinkServed;

        const float GlassRadius = 0.042f, GlassHeight = 0.095f, Wall = 0.004f, Base = 0.012f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        Transform glass, liquid, stream;
        Renderer liquidRenderer, streamRenderer;
        ServeMug serveGlass;
        MaterialPropertyBlock block;
        float liquidFrac;
        float grow = 1f;
        Vector3 anchor, serveDir;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            serveDir = serveDirection; serveDir.y = 0f;
            serveDir = serveDir.sqrMagnitude > 0f ? serveDir.normalized : Vector3.right;
            if (liquidMaterial == null) liquidMaterial = FallbackMaterial();
            BuildGlass();
            stream = MakeCylinder("Chorro trago", transform, liquidMaterial, out streamRenderer);
            stream.gameObject.SetActive(false);
        }

        public void Pour(BottleSource bottle)
        {
            if (!CanPour || bottle == null) return;
            StartCoroutine(PourRoutine(bottle));
        }

        public void Serve()
        {
            if (State != Phase.Full) return;
            StartCoroutine(ServeRoutine());
        }

        // ---------- Secuencias ----------

        IEnumerator PourRoutine(BottleSource bottle)
        {
            State = Phase.Pouring;
            CurrentDrink = bottle.drinkName;
            DrinkStarted?.Invoke(CurrentDrink);
            SetLiquidColor(bottle.liquidColor);
            liquidFrac = 0f; UpdateLevel();

            // 1. Vaso con hielo.
            Vector3 target = glassSpot.position;
            glass.gameObject.SetActive(true);
            grow = 1f;
            Vector3 from = target - serveDir * 0.22f;
            if (hieloSound != null) hieloSound.PlayOnceAt(target);
            yield return Ease.Run(0.35f, t => PlaceGlass(Vector3.Lerp(from, target, Ease.OutCubic(t))));

            // 2. La botella se levanta del estante.
            if (abrirBotellaSound != null) abrirBotellaSound.PlayOnceAt(bottle.RestPosition);
            Vector3 restTip = bottle.TipPosition;
            yield return Ease.Run(0.25f, t => bottle.PlaceTipAt(restTip + Vector3.up * 0.08f * Ease.OutCubic(t), bottle.RestRotation));

            // 3. Vuela en arco hasta quedar inclinada sobre el vaso.
            Vector3 rim = GlassTop();
            Vector3 pourTip = rim + Vector3.up * 0.05f;
            Vector3 axis = serveDir; // se inclina "de costado" desde el punto de vista del barman
            Quaternion pourRot = Quaternion.AngleAxis(tiltAngle, axis) * bottle.RestRotation;
            Vector3 startTip = restTip + Vector3.up * 0.08f;
            Vector3 control = (startTip + pourTip) * 0.5f + Vector3.up * 0.25f;
            yield return Ease.Run(flightDuration, t =>
            {
                float e = Ease.InOutCubic(t);
                Vector3 p = Bezier(startTip, control, pourTip, e);
                bottle.PlaceTipAt(p, Quaternion.Slerp(bottle.RestRotation, pourRot, Ease.Smooth(0.3f, 1f, t)));
            });

            // 4. Sirve.
            stream.gameObject.SetActive(true);
            yield return Ease.Run(0.1f, t => SetStream(pourTip, Vector3.Lerp(pourTip, SurfacePoint(), t)));
            yield return Ease.Run(pourDuration, t =>
            {
                liquidFrac = fillLevel * Ease.OutCubic(t);
                UpdateLevel();
                SetStream(pourTip, SurfacePoint(), 1f + 0.2f * Mathf.Sin(Time.time * 45f));
            });
            yield return Ease.Run(0.12f, t => SetStream(Vector3.Lerp(pourTip, SurfacePoint(), t), SurfacePoint()));
            stream.gameObject.SetActive(false);

            // 5. Vuelve al estante.
            yield return Ease.Run(flightDuration, t =>
            {
                float e = Ease.InOutCubic(t);
                Vector3 p = Bezier(pourTip, control, startTip, e);
                bottle.PlaceTipAt(p, Quaternion.Slerp(pourRot, bottle.RestRotation, Ease.Smooth(0f, 0.7f, t)));
            });
            yield return Ease.Run(0.2f, t => bottle.PlaceTipAt(Vector3.Lerp(startTip, restTip, Ease.OutCubic(t)), bottle.RestRotation));
            bottle.transform.SetPositionAndRotation(bottle.RestPosition, bottle.RestRotation);
            if (botellasSound != null) botellasSound.PlayOnceAt(bottle.RestPosition);

            State = Phase.Full;
            serveGlass.Attract = true;
        }

        IEnumerator ServeRoutine()
        {
            State = Phase.Serving;
            serveGlass.Attract = false;
            if (cajaSound != null) cajaSound.PlayOnceAt(anchor);
            Vector3 start = anchor, end = start + serveDir * serveDistance;
            yield return Ease.Run(0.8f, t => PlaceGlass(Vector3.Lerp(start, end, Ease.InOutCubic(t))));
            yield return new WaitForSeconds(0.35f);
            yield return Ease.Run(0.25f, t => { grow = 1f - Ease.InCubic(t) * 0.99f; PlaceGlass(end); });
            glass.gameObject.SetActive(false);
            grow = 1f;
            State = Phase.Idle;
            DrinkServed?.Invoke(CurrentDrink);
        }

        // ---------- Vaso ----------

        void BuildGlass()
        {
            var go = new GameObject("Vaso de trago");
            glass = go.transform;
            glass.SetParent(transform, false);

            var body = new GameObject("Vidrio");
            body.transform.SetParent(glass, false);
            body.AddComponent<MeshFilter>().sharedMesh = TumblerMesh(32);
            var r = body.AddComponent<MeshRenderer>();
            r.sharedMaterial = glassMaterial != null ? glassMaterial : liquidMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;

            liquid = MakeCylinder("Liquido", glass, liquidMaterial, out liquidRenderer);

            // Hielo: tres cubos con el mismo vidrio.
            var rnd = new System.Random(7);
            for (int i = 0; i < 3; i++)
            {
                var ice = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ice.name = "Hielo";
                var c = ice.GetComponent<Collider>();
                if (c != null) Destroy(c);
                ice.AddComponent<NoGazeOutline>();
                ice.transform.SetParent(glass, false);
                float a = i * 2.1f;
                ice.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.014f, Base + 0.012f + i * 0.012f, Mathf.Sin(a) * 0.014f);
                ice.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 40f, (float)rnd.NextDouble() * 90f, (float)rnd.NextDouble() * 40f);
                ice.transform.localScale = Vector3.one * 0.021f;
                var ir = ice.GetComponent<Renderer>();
                ir.sharedMaterial = glassMaterial != null ? glassMaterial : liquidMaterial;
                ir.shadowCastingMode = ShadowCastingMode.Off;
            }

            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, GlassHeight * 0.5f, 0f);
            col.size = new Vector3(GlassRadius * 3f, GlassHeight * 1.4f, GlassRadius * 3f);

            serveGlass = go.AddComponent<ServeMug>();
            serveGlass.station = this;
            serveGlass.glassRenderers = new[] { r };
            serveGlass.label = "Servir";
            serveGlass.dwellTime = 1f;
            serveGlass.labelOffset = new Vector3(0f, GlassHeight + 0.08f, 0f);

            go.SetActive(false);
        }

        void PlaceGlass(Vector3 position)
        {
            anchor = position;
            glass.position = position; // el origen del vaso es el centro de la base
            glass.rotation = Quaternion.identity;
            glass.localScale = Vector3.one * grow;
        }

        void UpdateLevel()
        {
            float maxH = GlassHeight - Base - 0.006f;
            float h = maxH * liquidFrac;
            bool visible = h > 0.0005f;
            if (liquid.gameObject.activeSelf != visible) liquid.gameObject.SetActive(visible);
            if (!visible) return;
            float r = GlassRadius - Wall - 0.002f;
            liquid.localPosition = new Vector3(0f, Base + h * 0.5f, 0f);
            liquid.localRotation = Quaternion.identity;
            liquid.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);
        }

        Vector3 SurfacePoint() => glass.TransformPoint(new Vector3(0f, Base + (GlassHeight - Base - 0.006f) * liquidFrac, 0f));
        Vector3 GlassTop() => glass.TransformPoint(new Vector3(0f, GlassHeight, 0f));

        void SetStream(Vector3 top, Vector3 bottom, float thickness = 1f)
        {
            float len = Mathf.Max(0.001f, top.y - bottom.y);
            stream.position = new Vector3(top.x, (top.y + bottom.y) * 0.5f, top.z);
            stream.rotation = Quaternion.identity;
            float w = 0.007f * thickness;
            stream.localScale = new Vector3(w, len * 0.5f, w);
        }

        void SetLiquidColor(Color c)
        {
            foreach (var r in new[] { liquidRenderer, streamRenderer })
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                block.SetColor(ColorId, c);
                block.SetColor(EmissionId, c * 0.35f);
                r.SetPropertyBlock(block);
            }
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        /// <summary>Vaso corto torneado: pared con espesor, borde y base gruesa. Origen en el centro de la base.</summary>
        static Mesh TumblerMesh(int segments)
        {
            // Perfil (radio, altura) de afuera hacia adentro, pasando por el borde.
            var profile = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(GlassRadius - 0.002f, 0f),
                new Vector2(GlassRadius - 0.003f, GlassHeight),           // pared exterior (levemente cónica)
                new Vector2(GlassRadius - 0.003f - Wall, GlassHeight),    // borde
                new Vector2(GlassRadius - 0.002f - Wall, Base),           // pared interior
                new Vector2(0f, Base),                                    // fondo interior
            };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int p = 0; p < profile.Length - 1; p++)
            {
                Vector2 a = profile[p], b = profile[p + 1];
                int start = verts.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float ang = s * Mathf.PI * 2f / segments;
                    float cx = Mathf.Cos(ang), cz = Mathf.Sin(ang);
                    verts.Add(new Vector3(a.x * cx, a.y, a.x * cz));
                    verts.Add(new Vector3(b.x * cx, b.y, b.x * cz));
                }
                for (int s = 0; s < segments; s++)
                {
                    int i0 = start + s * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                    // Orden elegido para que la cara visible (horaria en Unity) quede hacia afuera del vidrio.
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i1); tris.Add(i3); tris.Add(i2);
                }
            }
            var mesh = new Mesh { name = "Vaso de trago" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Transform MakeCylinder(string name, Transform parent, Material mat, out Renderer renderer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.AddComponent<NoGazeOutline>();
            go.transform.SetParent(parent, false);
            renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        static Material FallbackMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            return shader != null ? new Material(shader) : null;
        }
    }
}
