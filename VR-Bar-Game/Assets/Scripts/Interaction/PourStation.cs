using System;
using System.Collections;
using BarAudio;
using UnityEngine;
using UnityEngine.Rendering;

namespace BarInteraccion
{
    /// <summary>
    /// Estación de cerveza: coordina canillas, jarra, chorro y sonido. Hay una sola jarra en juego:
    ///
    ///   Libre ──(mirar una canilla)──▶ Sirviendo ──▶ Llena ──(mirar la jarra)──▶ Entregando ──▶ Libre
    ///
    /// Sirviendo: la jarra entra deslizándose, la manija baja, sale el chorro, la jarra se llena
    /// (líquido + espuma), la manija vuelve con un rebote y la espuma se asienta.
    /// Entregando: la jarra se desliza hacia los clientes, suena la caja y desaparece.
    /// El evento BeerServed queda listo para el sistema de pedidos de la etapa 2.
    /// </summary>
    public class PourStation : MonoBehaviour, IDrinkStation
    {
        public enum Phase { Idle, Pouring, Full, Serving }

        [Header("Jarra")]
        [Tooltip("Modelo de la jarra (beer_mug_glass.glb).")]
        public GameObject mugPrefab;
        [Tooltip("Rotación del modelo para que quede parado (copiada de una jarra de la escena).")]
        public Quaternion mugRotation = Quaternion.identity;
        public Vector3 mugScale = Vector3.one;

        [Header("Materiales")]
        public Material glassMaterial;
        public Material liquidMaterial;
        public Material foamMaterial;

        [Header("Sonido (BarSound de Audio (Bar) → Foley)")]
        public BarSound canillaSound;
        public BarSound vasosSound;
        public BarSound cajaSound;

        [Header("Entrega")]
        [Tooltip("Hacia dónde se desliza la jarra al servirla (hacia los clientes).")]
        public Vector3 serveDirection = Vector3.right;
        public float serveDistance = 0.45f;

        [Header("Tiempos")]
        public float fillDuration = 3.6f;
        [Range(0.5f, 0.95f)] public float liquidLevel = 0.8f;
        [Range(0.02f, 0.3f)] public float foamLevel = 0.12f;

        public Phase State { get; private set; } = Phase.Idle;
        public bool CanPour => State == Phase.Idle && mugPrefab != null;
        public int Served { get; private set; }
        public string CurrentBeer { get; private set; }
        public string CurrentDrink => CurrentBeer;

        /// <summary>Se dispara al entregar una cerveza (nombre de la cerveza).</summary>
        public event Action<string> BeerServed;

        public bool IsReadyToServe => State == Phase.Full;
        public bool IsBusy => State == Phase.Pouring || State == Phase.Serving;
        public event Action<string> DrinkStarted;
        public event Action<string> DrinkServed;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        GameObject mug;
        Transform mugMesh;
        ServeMug serveMug;
        Bounds mugLocalBounds;
        Transform liquid, foam, stream;
        Renderer liquidRenderer, streamRenderer;
        MaterialPropertyBlock block;
        float bottomLocal, heightLocal, radiusLocal;
        float liquidFrac, foamFrac;
        Vector3 mugAnchor;
        float mugGrow = 1f;
        Vector3 serveDir;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (liquidMaterial == null) liquidMaterial = FallbackMaterial(new Color(1f, 0.72f, 0.15f));
            if (foamMaterial == null) foamMaterial = FallbackMaterial(new Color(0.97f, 0.93f, 0.82f));
            serveDir = serveDirection; serveDir.y = 0f;
            serveDir = serveDir.sqrMagnitude > 0f ? serveDir.normalized : Vector3.right;

            stream = MakeCylinder("Chorro", transform, liquidMaterial, out streamRenderer);
            stream.gameObject.SetActive(false);
        }

        // ---------- API ----------

        public void Pour(BeerTap tap)
        {
            if (!CanPour || tap == null) return;
            StartCoroutine(PourRoutine(tap));
        }

        public void Serve()
        {
            if (State != Phase.Full) return;
            StartCoroutine(ServeRoutine());
        }

        // ---------- Secuencias ----------

        IEnumerator PourRoutine(BeerTap tap)
        {
            State = Phase.Pouring;
            CurrentBeer = tap.beerName;
            DrinkStarted?.Invoke(CurrentBeer);
            EnsureMug();
            SetBeerColor(tap.beerColor);
            liquidFrac = 0f; foamFrac = 0f; UpdateLevels();
            mug.SetActive(true);

            Vector3 target = tap.pourTarget != null ? tap.pourTarget.position : tap.transform.position;
            Vector3 spout = tap.spout != null ? tap.spout.position : target + Vector3.up * 0.3f;

            // 1. La jarra entra deslizándose desde el lado del barman.
            Vector3 from = target - serveDir * 0.22f;
            mugGrow = 1f;
            yield return Ease.Run(0.35f, t => PlaceMug(Vector3.Lerp(from, target, Ease.OutCubic(t))));
            if (vasosSound != null) vasosSound.PlayOnceAt(target);

            // 2. Tirar de la manija.
            yield return tap.Pull(0.25f);

            // 3. Chorro + sonido de la canilla.
            if (canillaSound != null) canillaSound.PlayOnceAt(spout);
            stream.gameObject.SetActive(true);
            yield return Ease.Run(0.12f, t => SetStream(spout, Vector3.Lerp(spout, SurfacePoint(), Ease.OutCubic(t))));

            // 4. Llenado: primero líquido, la espuma aparece en la segunda mitad.
            float wobbleSeed = UnityEngine.Random.value * 10f;
            yield return Ease.Run(fillDuration, t =>
            {
                liquidFrac = liquidLevel * Ease.InOutCubic(t) * 0.95f;
                foamFrac = (foamLevel + 0.04f) * Ease.Smooth(0.35f, 1f, t);
                UpdateLevels();
                SetStream(spout, SurfacePoint(), 1f + 0.15f * Mathf.Sin((Time.time + wobbleSeed) * 40f));
            });

            // 5. Soltar la manija; el chorro se corta desde arriba.
            StartCoroutine(tap.Release(0.35f));
            if (canillaSound != null) canillaSound.FadeOut(0.4f);
            yield return Ease.Run(0.15f, t => SetStream(Vector3.Lerp(spout, SurfacePoint(), Ease.InCubic(t)), SurfacePoint()));
            stream.gameObject.SetActive(false);

            // 6. La espuma se asienta.
            float l0 = liquidFrac, f0 = foamFrac;
            yield return Ease.Run(0.7f, t =>
            {
                float e = Ease.OutCubic(t);
                liquidFrac = Mathf.Lerp(l0, liquidLevel, e);
                foamFrac = Mathf.Lerp(f0, foamLevel, e);
                UpdateLevels();
            });

            State = Phase.Full;
            serveMug.Attract = true;
        }

        IEnumerator ServeRoutine()
        {
            State = Phase.Serving;
            serveMug.Attract = false;
            if (cajaSound != null) cajaSound.PlayOnceAt(mugAnchor);

            Vector3 start = mugAnchor;
            Vector3 end = start + serveDir * serveDistance;
            yield return Ease.Run(0.8f, t => PlaceMug(Vector3.Lerp(start, end, Ease.InOutCubic(t))));
            yield return new WaitForSeconds(0.35f);
            yield return Ease.Run(0.25f, t => { mugGrow = 1f - Ease.InCubic(t) * 0.99f; PlaceMug(end); });
            mug.SetActive(false);
            mugGrow = 1f;

            Served++;
            State = Phase.Idle;
            BeerServed?.Invoke(CurrentBeer);
            DrinkServed?.Invoke(CurrentBeer);
        }

        // ---------- Jarra ----------

        void EnsureMug()
        {
            if (mug != null) return;
            mug = Instantiate(mugPrefab, transform);
            mug.name = "Jarra de servicio";
            mug.transform.rotation = mugRotation;
            mug.transform.localScale = mugScale;
            foreach (var c in mug.GetComponentsInChildren<Collider>(true)) Destroy(c);

            var mf = mug.GetComponentInChildren<MeshFilter>(true);
            mugMesh = mf.transform;
            mugLocalBounds = mf.sharedMesh.bounds;

            // Vidrio transparente para que se vea el llenado.
            var glassRenderers = mug.GetComponentsInChildren<MeshRenderer>(true);
            if (glassMaterial != null)
            {
                foreach (var r in glassRenderers)
                {
                    var rf = r.GetComponent<MeshFilter>();
                    int n = rf != null && rf.sharedMesh != null ? Mathf.Max(1, rf.sharedMesh.subMeshCount) : 1;
                    var mats = new Material[n];
                    for (int i = 0; i < n; i++) mats[i] = glassMaterial;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            // El cuerpo de la jarra es un cilindro sobre el eje Y local de la malla (el asa queda hacia un costado).
            Bounds b = mugLocalBounds;
            radiusLocal = Mathf.Min(b.extents.x, b.extents.z) * 0.86f;
            bottomLocal = b.min.y + b.size.y * 0.07f;
            heightLocal = b.size.y * 0.88f;

            liquid = MakeCylinder("Liquido", mugMesh, liquidMaterial, out liquidRenderer);
            foam = MakeCylinder("Espuma", mugMesh, foamMaterial, out _);

            var col = mf.gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = b.center;
            col.size = b.size * 1.15f;

            serveMug = mug.AddComponent<ServeMug>();
            serveMug.station = this;
            serveMug.glassRenderers = glassRenderers;
            serveMug.label = "Servir";
            serveMug.dwellTime = 1f;
            Vector3 top = mugMesh.TransformPoint(new Vector3(0f, b.max.y, 0f));
            serveMug.labelOffset = new Vector3(0f, top.y - mug.transform.position.y + 0.08f, 0f);

            mug.SetActive(false);
        }

        /// <summary>Ubica la jarra con el centro de su base en "anchor" (y aplica mugGrow como escala).</summary>
        void PlaceMug(Vector3 anchor)
        {
            mugAnchor = anchor;
            mug.transform.localScale = mugScale * mugGrow;
            Vector3 bottom = mugMesh.TransformPoint(new Vector3(0f, mugLocalBounds.min.y, 0f));
            mug.transform.position += anchor - bottom;
        }

        void UpdateLevels()
        {
            float lh = heightLocal * liquidFrac;
            float fh = heightLocal * foamFrac;
            SetCylinderLocal(liquid, bottomLocal, lh, radiusLocal);
            SetCylinderLocal(foam, bottomLocal + lh, fh, radiusLocal * 1.02f);
        }

        static void SetCylinderLocal(Transform t, float bottom, float height, float radius)
        {
            bool visible = height > 0.0005f;
            if (t.gameObject.activeSelf != visible) t.gameObject.SetActive(visible);
            if (!visible) return;
            t.localPosition = new Vector3(0f, bottom + height * 0.5f, 0f);
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f); // el cilindro de Unity mide 2 de alto
        }

        Vector3 SurfacePoint()
        {
            float y = bottomLocal + heightLocal * (liquidFrac + foamFrac);
            return mugMesh.TransformPoint(new Vector3(0f, y, 0f));
        }

        void SetStream(Vector3 top, Vector3 bottom, float thickness = 1f)
        {
            float len = Mathf.Max(0.001f, top.y - bottom.y);
            stream.position = new Vector3(top.x, (top.y + bottom.y) * 0.5f, top.z);
            stream.rotation = Quaternion.identity;
            float w = 0.012f * thickness;
            stream.localScale = new Vector3(w, len * 0.5f, w);
        }

        void SetBeerColor(Color c)
        {
            foreach (var r in new[] { liquidRenderer, streamRenderer })
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                block.SetColor(ColorId, c);
                block.SetColor(EmissionId, c * 0.35f); // las luces son Baked: un poco de brillo propio
                r.SetPropertyBlock(block);
            }
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

        static Material FallbackMaterial(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;
            var m = new Material(shader);
            m.SetColor(BaseColorId, c);
            return m;
        }
    }
}
