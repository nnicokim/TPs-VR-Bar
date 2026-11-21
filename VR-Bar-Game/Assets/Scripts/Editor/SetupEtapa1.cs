using System.Collections.Generic;
using System.Linq;
using System.Text;
using BarAudio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BarInteraccion.EditorTools
{
    /// <summary>
    /// Menú "VR Bar → Armar Etapa 1": agrega a la escena abierta todo lo de la etapa 1
    /// (mirada, retículo, teletransporte y canillas de cerveza). Se puede correr de nuevo:
    /// borra lo que armó antes y lo vuelve a armar. Todo queda en un solo paso de Deshacer (Ctrl+Z).
    /// Al terminar escribe en la Consola un resumen de lo que hizo.
    /// </summary>
    public static class SetupEtapa1
    {
        const string Prefix = "[Etapa1] ";
        const string RootName = "Interaccion (Etapa 1)";
        const string MatFolder = "Assets/Materials/Interaccion";
        const string GenFolder = "Assets/Models/Tap/Generado";
        const string TapPath = "Assets/Models/Tap/beer_tap.glb";
        const string MugPath = "Assets/Models/Mugs and can/beer_mug_glass.glb";
        const string PintPath = "Assets/Models/Mugs and can/draft_asahi_beer_glass_lowpoly_remesh.glb";

        static readonly (string name, Color color)[] Beers =
        {
            ("Rubia", new Color(1f, 0.74f, 0.18f)),
            ("Roja", new Color(0.62f, 0.18f, 0.07f)),
            ("Negra", new Color(0.09f, 0.05f, 0.03f)),
            ("IPA", new Color(0.93f, 0.55f, 0.12f)),
        };

        struct Mats { public Material outline, beer, foam, glass, marker; }

        class Handle
        {
            public GameObject tower;
            public MeshFilter body;
            public Mesh mesh;          // mitad de la malla de manijas
            public Vector3 basePoint;  // base de la manija (pivote)
            public Vector3 topPoint;
            public Vector3 front;      // hacia el barman
            public Vector3 spout;
            public MeshFilter source;  // malla original con las dos manijas
        }

        [MenuItem("VR Bar/Armar Etapa 1 (mirada, teletransporte y canilla)")]
        public static void Build()
        {
            var log = new StringBuilder("<b>VR Bar · Etapa 1</b>\n");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Armar Etapa 1");
            int group = Undo.GetCurrentGroup();

            Cleanup(log);
            Mats mats = EnsureMaterials(log);

            // ---------- Cámara: puntero de mirada, retículo y fundido ----------
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam == null) { Debug.LogError("VR Bar: no encontré la cámara."); return; }
            var reticle = GetOrAdd<GazeReticle>(cam.gameObject);
            var pointer = GetOrAdd<GazePointer>(cam.gameObject);
            pointer.outlineMaterial = mats.outline;
            pointer.reticle = reticle;
            var fader = GetOrAdd<ScreenFader>(cam.gameObject);
            Transform origin = cam.transform.root;
            var teleporter = GetOrAdd<GazeTeleporter>(origin.gameObject);
            teleporter.cameraTransform = cam.transform;
            teleporter.fader = fader;
            log.AppendLine($"• Cámara '{cam.name}': GazePointer + GazeReticle + ScreenFader. Teletransporte en '{origin.name}'.");

            // ---------- Raíz ----------
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Armar Etapa 1");
            var stationGo = new GameObject("Estacion de cerveza");
            Undo.RegisterCreatedObjectUndo(stationGo, "Armar Etapa 1");
            stationGo.transform.SetParent(root.transform, false);
            var station = stationGo.AddComponent<PourStation>();
            station.liquidMaterial = mats.beer;
            station.foamMaterial = mats.foam;
            station.glassMaterial = mats.glass;

            // ---------- Canillas ----------
            Physics.SyncTransforms();
            var handles = BuildHandles(log);
            Vector3 front = handles.Count > 0 ? handles[0].front : Vector3.left;
            Vector3 lateral = Vector3.Cross(Vector3.up, front).normalized;
            handles = handles.OrderBy(h => Vector3.Dot(h.basePoint, lateral)).ToList();
            var spouts = new List<Vector3>();
            for (int i = 0; i < handles.Count; i++)
            {
                var beer = Beers[i % Beers.Length];
                var tap = CreateTap(handles[i], i, beer.name, beer.color, station);
                spouts.Add(tap.spout.position);
                log.AppendLine($"• Canilla '{beer.name}' en '{handles[i].tower.name}' (boca {Fmt(tap.spout.position)}, jarra apoyada a y={tap.pourTarget.position.y:0.00}).");
            }
            if (handles.Count == 0) log.AppendLine("<color=orange>• No encontré canillas (beer_tap.glb) en la escena.</color>");

            // Vasos decorativos que quedaban justo debajo de una boca: se ocultan (la jarra de servicio ocupa ese lugar).
            foreach (var go in FindInstances(MugPath).Concat(FindInstances(PintPath)))
            {
                if (!go.activeInHierarchy) continue;
                Bounds b = WorldBounds(go);
                foreach (var s in spouts)
                {
                    Vector2 d = new Vector2(b.center.x - s.x, b.center.z - s.z);
                    if (d.magnitude < 0.08f)
                    {
                        Undo.RecordObject(go, "Armar Etapa 1");
                        go.SetActive(false);
                        log.AppendLine($"• Oculté el vaso decorativo '{go.name}' (estaba debajo de una canilla).");
                        break;
                    }
                }
            }

            // Jarra: modelo y rotación "parada" copiada de una jarra de la escena.
            station.mugPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MugPath);
            var sceneMug = FindInstances(MugPath).OrderBy(g => spouts.Count > 0 ? Vector3.Distance(g.transform.position, spouts[0]) : 0f).FirstOrDefault();
            if (sceneMug != null)
            {
                station.mugRotation = sceneMug.transform.rotation;
                station.mugScale = sceneMug.transform.lossyScale;
            }
            else log.AppendLine("<color=orange>• No hay ninguna beer_mug_glass en la escena para copiar su rotación: revisá 'Mug Rotation' en la Estación.</color>");
            station.serveDirection = -front;

            // Sonidos: la canilla deja de sonar sola (ahora la usa el participante).
            foreach (var bs in Object.FindObjectsByType<BarSound>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = bs.gameObject.name.ToLowerInvariant();
                if (n.Contains("canilla"))
                {
                    Undo.RecordObject(bs, "Armar Etapa 1");
                    bs.automatic = false;
                    station.canillaSound = bs;
                    log.AppendLine($"• Sonido '{bs.gameObject.name}': ahora lo dispara la interacción (automatic = false).");
                }
                else if (n.Contains("vasos")) station.vasosSound = bs;
                else if (n.Contains("caja")) station.cajaSound = bs;
            }

            // ---------- Teletransporte ----------
            BuildTeleportPoints(root.transform, cam, handles, front, mats, log);

            // ---------- Light probes en la barra ----------
            BuildLightProbes(root.transform, log);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            log.AppendLine("Listo. Guardá la escena (Ctrl+S) y dale Play. En el Editor: clic derecho + mouse para mirar, clic izquierdo o Espacio para elegir.");
            Debug.Log(log.ToString());
        }

        // =====================================================================
        // Canillas
        // =====================================================================

        static List<Handle> BuildHandles(StringBuilder log)
        {
            var result = new List<Handle>();
            var splitCache = new Dictionary<Mesh, Mesh[]>();
            foreach (var tower in FindInstances(TapPath))
            {
                ClearBatchingStatic(tower);
                var filters = tower.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null && !f.name.StartsWith(Prefix)).ToList();
                MeshFilter handleMf = filters.FirstOrDefault(f =>
                {
                    var r = f.GetComponent<Renderer>();
                    return r != null && r.sharedMaterials.Any(m => m != null && m.name.ToLowerInvariant().Contains("wood"));
                }) ?? filters.FirstOrDefault(f => f.name == "Object_6");
                if (handleMf == null) { log.AppendLine($"<color=orange>• '{tower.name}': no encontré la malla de las manijas.</color>"); continue; }
                MeshFilter body = filters.Where(f => f != handleMf).OrderByDescending(f => Volume(f.sharedMesh.bounds)).FirstOrDefault();

                if (!splitCache.TryGetValue(handleMf.sharedMesh, out Mesh[] halves))
                {
                    halves = SplitMesh(handleMf.sharedMesh);
                    splitCache[handleMf.sharedMesh] = halves;
                }

                var towerHandles = new List<Handle>();
                foreach (var half in halves)
                {
                    var world = half.vertices.Select(v => handleMf.transform.TransformPoint(v)).ToArray();
                    float minY = world.Min(p => p.y), maxY = world.Max(p => p.y), range = maxY - minY;
                    towerHandles.Add(new Handle
                    {
                        tower = tower,
                        body = body,
                        mesh = half,
                        source = handleMf,
                        basePoint = Average(world.Where(p => p.y <= minY + range * 0.15f)),
                        topPoint = Average(world.Where(p => p.y >= maxY - range * 0.15f)),
                    });
                }

                // Frente de la torre: desde el centro del cuerpo hacia las manijas (lado del barman).
                Vector3 handlesCenter = Average(towerHandles.Select(h => h.basePoint));
                Vector3 bodyCenter = body != null ? body.GetComponent<Renderer>().bounds.center : tower.transform.position;
                Vector3 front = handlesCenter - bodyCenter; front.y = 0f;
                front = front.sqrMagnitude > 1e-6f ? front.normalized : Vector3.left;
                float scale = Mathf.Abs(tower.transform.lossyScale.x);
                Vector3[] bodyWorld = body != null ? body.sharedMesh.vertices.Select(v => body.transform.TransformPoint(v)).ToArray() : new Vector3[0];
                foreach (var h in towerHandles)
                {
                    h.front = front;
                    h.spout = FindSpout(h.basePoint, front, bodyWorld, scale);
                }

                // Las manijas originales (una sola malla para las dos) se ocultan: las reemplazan las mitades móviles.
                var handleRenderer = handleMf.GetComponent<Renderer>();
                Undo.RecordObject(handleRenderer, "Armar Etapa 1");
                handleRenderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(handleRenderer);

                result.AddRange(towerHandles);
            }
            return result;
        }

        static BeerTap CreateTap(Handle h, int index, string beerName, Color beerColor, PourStation station)
        {
            Transform tower = h.tower.transform;

            // Pivote en la base de la manija (lo que rota).
            var pivot = new GameObject($"{Prefix}Canilla {beerName}");
            Undo.RegisterCreatedObjectUndo(pivot, "Armar Etapa 1");
            pivot.transform.SetParent(tower, true);
            pivot.transform.SetPositionAndRotation(h.basePoint, Quaternion.identity);

            // Malla de la manija: mismo lugar que la original, colgada del pivote.
            var meshGo = new GameObject($"{Prefix}Manija");
            Undo.RegisterCreatedObjectUndo(meshGo, "Armar Etapa 1");
            meshGo.transform.SetParent(h.source.transform.parent, false);
            meshGo.transform.localPosition = h.source.transform.localPosition;
            meshGo.transform.localRotation = h.source.transform.localRotation;
            meshGo.transform.localScale = h.source.transform.localScale;
            meshGo.transform.SetParent(pivot.transform, true);
            meshGo.AddComponent<MeshFilter>().sharedMesh = h.mesh;
            var mr = meshGo.AddComponent<MeshRenderer>();
            mr.sharedMaterials = h.source.GetComponent<Renderer>().sharedMaterials.Take(Mathf.Max(1, h.mesh.subMeshCount)).ToArray();
            var col = meshGo.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = h.mesh.bounds.center;
            col.size = h.mesh.bounds.size * 1.5f; // más grande que la manija: más fácil de apuntar con la cabeza

            // Boca y apoyo de la jarra (no rotan con la manija).
            var spout = new GameObject($"{Prefix}Boca {beerName}");
            Undo.RegisterCreatedObjectUndo(spout, "Armar Etapa 1");
            spout.transform.SetParent(tower, true);
            spout.transform.position = h.spout;

            var target = new GameObject($"{Prefix}Apoyo jarra {beerName}");
            Undo.RegisterCreatedObjectUndo(target, "Armar Etapa 1");
            target.transform.SetParent(tower, true);
            target.transform.position = new Vector3(h.spout.x, CounterHeightBelow(h.spout, h.basePoint.y - 0.45f), h.spout.z);

            // Eje y sentido de giro: la parte de arriba de la manija va hacia el barman.
            Vector3 worldAxis = Vector3.Cross(Vector3.up, h.front).normalized;
            Vector3 v = h.topPoint - h.basePoint;
            float sign = Vector3.Dot(Quaternion.AngleAxis(40f, worldAxis) * v - v, h.front) >= 0f ? 1f : -1f;

            var tap = pivot.AddComponent<BeerTap>();
            tap.station = station;
            tap.beerName = beerName;
            tap.beerColor = beerColor;
            tap.label = beerName;
            tap.labelOffset = new Vector3(0f, (h.topPoint.y - h.basePoint.y) + 0.07f, 0f);
            tap.dwellTime = 1f;
            tap.outlineWidth = 0.003f;
            tap.highlightRoot = meshGo.transform;
            tap.lever = pivot.transform;
            tap.leverAxis = pivot.transform.parent.InverseTransformDirection(worldAxis).normalized;
            tap.pullAngle = 40f * sign;
            tap.spout = spout.transform;
            tap.pourTarget = target.transform;
            return tap;
        }

        /// <summary>Busca la punta de la boca: el vértice más bajo del cuerpo justo debajo de la manija.</summary>
        static Vector3 FindSpout(Vector3 basePoint, Vector3 front, Vector3[] bodyWorld, float scale)
        {
            Vector3 axis = basePoint + front * 0.012f * scale;
            var near = bodyWorld.Where(p =>
            {
                float dx = p.x - axis.x, dz = p.z - axis.z;
                return dx * dx + dz * dz < (0.03f * scale) * (0.03f * scale)
                       && p.y < basePoint.y - 0.005f * scale && p.y > basePoint.y - 0.15f * scale;
            }).ToList();
            if (near.Count == 0) return basePoint + front * 0.012f * scale + Vector3.down * 0.06f * scale;
            float minY = near.Min(p => p.y);
            Vector3 avg = Average(near.Where(p => p.y < minY + 0.004f * scale));
            return new Vector3(avg.x, minY, avg.z);
        }

        static float CounterHeightBelow(Vector3 from, float fallback)
        {
            var hits = Physics.RaycastAll(from + Vector3.down * 0.005f, Vector3.down, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits.OrderBy(h => h.distance))
            {
                Vector3 s = hit.collider.bounds.size;
                if (s.x * s.z > 0.1f) return hit.point.y; // una superficie grande: la barra
            }
            var top = GameObject.Find("Bar/Counter/Top");
            if (top != null && top.TryGetComponent<Renderer>(out var r)) return r.bounds.max.y;
            return fallback;
        }

        /// <summary>Parte la malla de manijas en dos (por el eje horizontal más largo).</summary>
        static Mesh[] SplitMesh(Mesh src)
        {
            Bounds b = src.bounds;
            int axis = b.size.x >= b.size.z ? 0 : 2;
            float cut = b.center[axis];
            Vector3[] v = src.vertices;
            Vector3[] n = src.normals;
            Vector4[] tg = src.tangents;
            Vector2[] uv = src.uv;
            Color[] col = src.colors;

            var result = new Mesh[2];
            for (int side = 0; side < 2; side++)
            {
                var map = new Dictionary<int, int>();
                var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>();
                var nuv = new List<Vector2>(); var nc = new List<Color>();
                var subs = new List<int>[src.subMeshCount];
                for (int s = 0; s < src.subMeshCount; s++)
                {
                    subs[s] = new List<int>();
                    int[] tris = src.GetTriangles(s);
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        float c = (v[tris[i]][axis] + v[tris[i + 1]][axis] + v[tris[i + 2]][axis]) / 3f;
                        if ((c < cut ? 0 : 1) != side) continue;
                        for (int k = 0; k < 3; k++)
                        {
                            int idx = tris[i + k];
                            if (!map.TryGetValue(idx, out int ni))
                            {
                                ni = nv.Count;
                                map[idx] = ni;
                                nv.Add(v[idx]);
                                if (n.Length == v.Length) nn.Add(n[idx]);
                                if (tg.Length == v.Length) nt.Add(tg[idx]);
                                if (uv.Length == v.Length) nuv.Add(uv[idx]);
                                if (col.Length == v.Length) nc.Add(col[idx]);
                            }
                            subs[s].Add(ni);
                        }
                    }
                }
                var m = new Mesh { name = $"{src.name}_manija_{side}" };
                if (nv.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(nv);
                if (nn.Count > 0) m.SetNormals(nn);
                if (nt.Count > 0) m.SetTangents(nt);
                if (nuv.Count > 0) m.SetUVs(0, nuv);
                if (nc.Count > 0) m.SetColors(nc);
                m.subMeshCount = src.subMeshCount;
                for (int s = 0; s < src.subMeshCount; s++) m.SetTriangles(subs[s], s);
                if (nn.Count == 0) m.RecalculateNormals();
                m.RecalculateBounds();

                EnsureFolder(GenFolder);
                string path = $"{GenFolder}/{m.name}.asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(m, path);
                result[side] = m;
            }
            return result;
        }

        // =====================================================================
        // Teletransporte
        // =====================================================================

        static void BuildTeleportPoints(Transform root, Camera cam, List<Handle> handles, Vector3 front, Mats mats, StringBuilder log)
        {
            var parent = new GameObject("Puntos de teletransporte");
            Undo.RegisterCreatedObjectUndo(parent, "Armar Etapa 1");
            parent.transform.SetParent(root, false);

            Bounds floor = new Bounds(Vector3.zero, new Vector3(13f, 0f, 9f));
            var floorGo = GameObject.Find("Bar/Floor");
            if (floorGo != null) floor = WorldBounds(floorGo);
            float floorY = floor.max.y;

            Vector3 camForward = cam.transform.forward; camForward.y = 0f;
            AddPoint(parent.transform, "Entrada", new Vector3(cam.transform.position.x, floorY, cam.transform.position.z), camForward, floor, mats, log);

            if (handles.Count > 0)
            {
                Vector3 c = Average(handles.Select(h => h.spout));
                Vector3 p = c + front * 0.65f; p.y = floorY;
                AddPoint(parent.transform, "Barra", p, -front, floor, mats, log);
            }

            PointNear(parent.transform, "Pool", GameObject.Find("Bar/Pool"), 0.75f, floor, mats, log);
            var darts = GameObject.Find("Bar/Darts");
            Transform board = darts != null ? darts.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("dartboard")) : null;
            PointNear(parent.transform, "Dardos", board != null ? board.gameObject : darts, 2.37f, floor, mats, log); // distancia reglamentaria
            PointNear(parent.transform, "Arcade", GameObject.Find("Bar/Arcade"), 0.9f, floor, mats, log);
            PointNear(parent.transform, "Ventana", GameObject.Find("Bar/Windows/Window Left"), 1.1f, floor, mats, log);
        }

        static void PointNear(Transform parent, string name, GameObject target, float clearance, Bounds floor, Mats mats, StringBuilder log)
        {
            if (target == null) { log.AppendLine($"<color=orange>• No encontré el objeto para el punto '{name}'.</color>"); return; }
            Bounds b = WorldBounds(target);
            Vector3 dir = floor.center - b.center; dir.y = 0f;
            if (dir.magnitude < 0.3f) dir = Vector3.back;
            dir = Mathf.Abs(dir.x) >= Mathf.Abs(dir.z) ? new Vector3(Mathf.Sign(dir.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(dir.z));
            float extent = Mathf.Abs(Vector3.Dot(b.extents, dir));
            Vector3 p = b.center + dir * (extent + clearance);
            p.y = floor.max.y;
            AddPoint(parent, name, p, -dir, floor, mats, log);
        }

        static void AddPoint(Transform parent, string name, Vector3 position, Vector3 facing, Bounds floor, Mats mats, StringBuilder log)
        {
            const float margin = 0.45f;
            position.x = Mathf.Clamp(position.x, floor.min.x + margin, floor.max.x - margin);
            position.z = Mathf.Clamp(position.z, floor.min.z + margin, floor.max.z - margin);

            var go = new GameObject($"Punto {name}");
            Undo.RegisterCreatedObjectUndo(go, "Armar Etapa 1");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            if (facing.sqrMagnitude > 1e-6f) go.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.84f, 0.08f, 0.84f);
            box.center = new Vector3(0f, 0.04f, 0f);

            var tp = go.AddComponent<TeleportPoint>();
            tp.label = name;
            tp.labelOffset = new Vector3(0f, 0.45f, 0f);
            tp.dwellTime = 1f;
            tp.markerMaterial = mats.marker;
            tp.alignRotation = true;

            var blockers = Physics.OverlapCapsule(position + Vector3.up * 0.3f, position + Vector3.up * 1.7f, 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            string extra = blockers.Length > 0 ? $" <color=orange>(se superpone con: {string.Join(", ", blockers.Select(c => c.name).Distinct())}; movelo un poco)</color>" : "";
            log.AppendLine($"• Punto '{name}' en {Fmt(position)}{extra}.");
        }

        // =====================================================================
        // Light probes
        // =====================================================================

        /// <summary>
        /// Las luces de la escena son "Baked": lo que se mueve (manijas, jarra, cerveza) no está en el lightmap.
        /// Una grilla de light probes alrededor de la barra le lleva esa luz horneada a los objetos dinámicos.
        /// Hace falta volver a generar la iluminación una vez para que tengan datos.
        /// </summary>
        static void BuildLightProbes(Transform root, StringBuilder log)
        {
            var top = GameObject.Find("Bar/Counter/Top");
            if (top == null) { log.AppendLine("<color=orange>• No encontré Bar/Counter/Top: no agregué light probes.</color>"); return; }
            Bounds b = WorldBounds(top);
            var go = new GameObject("Light probes barra");
            Undo.RegisterCreatedObjectUndo(go, "Armar Etapa 1");
            go.transform.SetParent(root, false);
            var group = go.AddComponent<LightProbeGroup>();
            var positions = new List<Vector3>();
            float[] dx = { -1.2f, -0.6f, 0f, 0.6f, 1.2f };
            float[] heights = { 0.6f, 1.25f, 1.9f };
            for (float z = b.min.z - 0.4f; z <= b.max.z + 0.41f; z += 0.8f)
                foreach (float x in dx)
                    foreach (float y in heights)
                        positions.Add(new Vector3(b.center.x + x, y, z));
            group.probePositions = positions.ToArray();
            log.AppendLine($"• {positions.Count} light probes en la barra. <b>Volvé a generar la iluminación una vez</b> (Window → Rendering → Lighting → Generate Lighting) para que las manijas y la jarra tomen la luz del bar.");
        }

        // =====================================================================
        // Materiales
        // =====================================================================

        static Mats EnsureMaterials(StringBuilder log)
        {
            EnsureFolder(MatFolder);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var outlineShader = Shader.Find("VRBar/GazeOutline");
            var m = new Mats();

            if (outlineShader != null)
            {
                m.outline = LoadOrCreate("Contorno mirada", outlineShader);
                m.outline.SetColor("_Color", new Color(1f, 0.78f, 0.35f, 1f));
                m.outline.SetFloat("_Width", 0.004f);
            }
            else log.AppendLine("<color=orange>• No encontré el shader VRBar/GazeOutline: sin contorno de resaltado.</color>");

            m.beer = LoadOrCreate("Cerveza", lit);
            m.beer.SetColor("_BaseColor", new Color(1f, 0.74f, 0.18f, 1f));
            m.beer.SetFloat("_Smoothness", 0.85f);
            m.beer.SetFloat("_Metallic", 0f);
            SetEmission(m.beer, new Color(1f, 0.74f, 0.18f) * 0.35f);

            m.foam = LoadOrCreate("Espuma", lit);
            m.foam.SetColor("_BaseColor", new Color(0.97f, 0.94f, 0.85f, 1f));
            m.foam.SetFloat("_Smoothness", 0.25f);
            SetEmission(m.foam, new Color(0.97f, 0.94f, 0.85f) * 0.3f);

            m.glass = LoadOrCreate("Vidrio jarra", lit);
            MakeTransparent(m.glass);
            m.glass.SetColor("_BaseColor", new Color(0.85f, 0.93f, 1f, 0.22f));
            m.glass.SetFloat("_Smoothness", 0.95f);
            m.glass.SetFloat("_Cull", (float)CullMode.Off);

            m.marker = LoadOrCreate("Marcador teletransporte", unlit != null ? unlit : lit);
            m.marker.SetColor("_BaseColor", new Color(0.45f, 0.8f, 1f, 1f));

            foreach (var mat in new[] { m.outline, m.beer, m.foam, m.glass, m.marker }) if (mat != null) EditorUtility.SetDirty(mat);
            return m;
        }

        /// <summary>Un poco de emisión: las luces del bar son "Baked" y lo que se mueve solo recibe luz ambiente.</summary>
        static void SetEmission(Material mat, Color c)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", c);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_BlendModePreserveSpecular", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("ShadowCaster", false);
            mat.SetShaderPassEnabled("DepthOnly", false);
        }

        static Material LoadOrCreate(string name, Shader shader)
        {
            string path = $"{MatFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            return mat;
        }

        // =====================================================================
        // Utilidades
        // =====================================================================

        static void Cleanup(StringBuilder log)
        {
            int removed = 0;
            var old = GameObject.Find(RootName);
            if (old != null) { Undo.DestroyObjectImmediate(old); removed++; }
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t != null && t.name.StartsWith(Prefix)) { Undo.DestroyObjectImmediate(t.gameObject); removed++; }
            }
            if (removed > 0) log.AppendLine($"• Borré lo armado antes ({removed} objetos) para rearmarlo.");
        }

        static IEnumerable<GameObject> FindInstances(string assetPath)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID)
                .Select(t => t.gameObject)
                .Where(g => PrefabUtility.IsAnyPrefabInstanceRoot(g)
                            && PrefabUtility.GetPrefabAssetPathOfNearestPrefabInstanceRoot(g) == assetPath)
                .ToList();
        }

        static void ClearBatchingStatic(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;
                Undo.RecordObject(t.gameObject, "Armar Etapa 1");
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            }
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = Undo.AddComponent<T>(go);
            else Undo.RecordObject(c, "Armar Etapa 1");
            return c;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static Vector3 Average(IEnumerable<Vector3> points)
        {
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var p in points) { sum += p; n++; }
            return n > 0 ? sum / n : Vector3.zero;
        }

        static float Volume(Bounds b) => b.size.x * b.size.y * b.size.z;
        static string Fmt(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
    }
}
