using System.Collections.Generic;
using System.Linq;
using System.Text;
using BarAudio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BarInteraccion.EditorTools
{
    /// <summary>
    /// Menú "VR Bar → Armar Etapa 3" (temporal: se corre una vez, se guarda la escena y se borra este script).
    /// Agrega el cierre del bar (parlante, neones, cortinas, tablero de luces y tarjeta final), el pedido del
    /// habitué, los dardos y el arcade secreto. Se puede correr de nuevo: deshace lo anterior y lo rearma.
    /// </summary>
    public static class SetupEtapa3
    {
        const string Prefix = "[Etapa3] ";
        const string RootName = "Interaccion (Etapa 3)";
        const string MatFolder = "Assets/Materials/Interaccion";

        [MenuItem("VR Bar/Armar Etapa 3 (cierre, habitué, dardos y arcade)")]
        public static void Build()
        {
            var log = new StringBuilder("<b>VR Bar · Etapa 3</b>\n");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Armar Etapa 3");
            int group = Undo.GetCurrentGroup();

            Cleanup(log);

            var manager = Object.FindFirstObjectByType<OrderManager>();
            var board = Object.FindFirstObjectByType<OrderBoard>();
            var storm = Object.FindFirstObjectByType<LightningStorm>();
            if (manager == null || board == null)
            {
                Debug.LogError("VR Bar: falta lo de la Etapa 2 (Pedidos y actos, Pizarra). ¿Está abierta SampleScene?");
                return;
            }
            var sounds = Object.FindObjectsByType<BarSound>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Func<string, BarSound> Sound = name => sounds.FirstOrDefault(x => x.gameObject.name == name);
            var boardMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/Pizarra.mat");
            var woodMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/Madera marco.mat");

            Bounds floor = new Bounds(Vector3.zero, new Vector3(13f, 0f, 9f));
            var floorGo = GameObject.Find("Bar/Floor");
            if (floorGo != null) floor = WorldBounds(floorGo);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Armar Etapa 3");
            var tasks = new List<ClosingTask>();

            // ---------- Parlante ----------
            var music = Sound("Parlante (musica del bar)");
            if (music != null)
            {
                var go = new GameObject($"{Prefix}Parlante");
                Undo.RegisterCreatedObjectUndo(go, "Armar Etapa 3");
                go.transform.SetParent(root.transform, false);
                Vector3 p = music.transform.position;
                p.x = Mathf.Clamp(p.x, floor.min.x + 0.45f, floor.max.x - 0.45f);
                p.z = Mathf.Clamp(p.z, floor.min.z + 0.45f, floor.max.z - 0.45f);
                p.y = Mathf.Min(p.y, 2.7f);
                Vector3 toRoom = floor.center - p; toRoom.y = 0f;
                go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(toRoom.normalized, Vector3.up) * Quaternion.Euler(12f, 0f, 0f));
                var sp = go.AddComponent<SpeakerBox>();
                sp.music = music;
                sp.group = "Apagar la música";
                sp.label = "Apagar la música";
                sp.labelOffset = new Vector3(0f, -0.38f, 0f);
                sp.dwellTime = 1f;
                tasks.Add(sp);
                log.AppendLine($"• Parlante (caja con LED) donde sale la música: {Fmt(p)}.");
            }
            else log.AppendLine("<color=orange>• No encontré 'Parlante (musica del bar)'.</color>");

            // ---------- Neones ----------
            var neonDefs = new[] { new[] { "Assets/Models/Signs/exit_sign.glb", "Neon cartel EXIT" }, new[] { "Assets/Models/Signs/neon_palm_tree.glb", "Neon palmera" } };
            foreach (var def in neonDefs)
            {
                string path = def[0], buzzName = def[1];
                foreach (var inst in FindInstances(path))
                {
                    ClearBatchingStatic(inst);
                    AddBoundsCollider(inst, 1.15f);
                    var neon = Undo.AddComponent<NeonSign>(inst);
                    neon.tubes = inst.GetComponentsInChildren<MeshRenderer>(true);
                    neon.buzz = Sound(buzzName);
                    neon.group = "Apagar los neones";
                    neon.label = "Apagar";
                    neon.labelOffset = new Vector3(0f, -0.35f, 0f);
                    neon.dwellTime = 1f;
                    tasks.Add(neon);
                    log.AppendLine($"• Neón '{inst.name}' (zumbido: {(neon.buzz != null ? neon.buzz.gameObject.name : "—")}).");
                }
            }

            // ---------- Cortinas ----------
            foreach (var cort in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(t => t.name == "Cortinas"))
            {
                var l = cort.Find("Cortina izquierda");
                var r = cort.Find("Cortina derecha");
                if (l == null || r == null) continue;
                ClearBatchingStatic(cort.gameObject);
                // Zona para mirar: toda la ventana (cortinas + vidrio), un poco hacia el salón.
                Bounds b = WorldBounds(cort.parent != null ? cort.parent.gameObject : cort.gameObject);
                var zone = new GameObject($"{Prefix}Zona cortinas");
                Undo.RegisterCreatedObjectUndo(zone, "Armar Etapa 3");
                zone.transform.SetParent(cort, false);
                var box = zone.AddComponent<BoxCollider>();
                box.isTrigger = true;
                zone.transform.position = new Vector3(b.center.x, b.center.y, b.center.z);
                zone.transform.rotation = Quaternion.identity;
                Vector3 lossy = cort.lossyScale;
                box.size = new Vector3(b.size.x / Mathf.Max(0.001f, lossy.x), b.size.y / Mathf.Max(0.001f, lossy.y), Mathf.Max(0.1f, b.size.z) / Mathf.Max(0.001f, lossy.z));
                var cp = Undo.AddComponent<CurtainPair>(cort.gameObject);
                cp.left = l.GetComponent<Renderer>();
                cp.right = r.GetComponent<Renderer>();
                cp.group = "Cerrar las cortinas";
                cp.label = "Cerrar las cortinas";
                cp.labelOffset = new Vector3(0f, -0.9f, -0.3f);
                cp.dwellTime = 1.2f;
                cp.outlineWidth = 0.006f;
                tasks.Add(cp);
                log.AppendLine($"• Cortinas de '{(cort.parent != null ? cort.parent.name : cort.name)}'.");
            }

            // ---------- Tablero de luces (junto a la puerta de la cocina) ----------
            var kitchenDoor = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Door Kitchen");
            var cabinet = GameObject.Find("Bar/Counter/Back Cabinet");
            if (kitchenDoor != null)
            {
                Bounds db = WorldBounds(kitchenDoor.gameObject);
                bool doorAlongZ = db.size.z >= db.size.x;
                Vector3 normal = doorAlongZ ? Vector3.right : Vector3.forward;
                if (Vector3.Dot(floor.center - db.center, normal) < 0f) normal = -normal;
                Vector3 along = doorAlongZ ? Vector3.forward : Vector3.right;
                float edge = Vector3.Dot(db.max, along), other = edge + 0.6f;
                if (cabinet != null)
                {
                    Bounds cb = WorldBounds(cabinet);
                    float cabMin = Vector3.Dot(cb.min, along);
                    if (cabMin > edge + 0.2f) other = cabMin;
                }
                float mid = (edge + other) * 0.5f;
                float wall = Vector3.Dot(db.center, normal) - Mathf.Abs(Vector3.Dot(db.extents, normal));
                // La cara real de la pared: un rayo desde el salón hacia ella (el marco de la puerta sobresale).
                Vector3 probe = along * mid + normal * (Vector3.Dot(db.center, normal) + 1f) + Vector3.up * 1.35f;
                Physics.SyncTransforms();
                if (Physics.Raycast(probe, -normal, out RaycastHit wallHit, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    wall = Vector3.Dot(wallHit.point, normal);
                Vector3 pos = along * mid + normal * (wall + 0.012f);
                pos.y = 1.35f;

                var panel = new GameObject($"{Prefix}Tablero de luces");
                Undo.RegisterCreatedObjectUndo(panel, "Armar Etapa 3");
                panel.transform.SetParent(root.transform, false);
                panel.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-normal, Vector3.up));
                var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
                back.name = "Tabla";
                Object.DestroyImmediate(back.GetComponent<Collider>());
                back.transform.SetParent(panel.transform, false);
                back.transform.localPosition = new Vector3(0f, -0.02f, 0.006f);
                back.transform.localScale = new Vector3(0.4f, 0.22f, 0.01f);
                if (woodMat != null) back.GetComponent<Renderer>().sharedMaterial = woodMat;

                var zones = LampZones();
                string[] names = { "SALÓN", "JUEGOS", "BARRA" };
                for (int i = 0; i < 3; i++)
                {
                    var sw = new GameObject($"Llave {names[i]}");
                    sw.transform.SetParent(panel.transform, false);
                    sw.transform.localPosition = new Vector3((i - 1) * 0.12f, 0.01f, -0.004f);
                    var ls = sw.AddComponent<LightSwitch>();
                    ls.zoneName = names[i];
                    ls.bulbs = zones[i].bulbs.ToArray();
                    ls.lights = zones[i].lights.ToArray();
                    ls.group = "Apagar las luces (tablero junto a la cocina)";
                    ls.label = $"Apagar {names[i].ToLowerInvariant()}";
                    ls.labelOffset = new Vector3(0f, 0.16f, 0f);
                    ls.dwellTime = 1f;
                    ls.outlineWidth = 0.002f;
                    tasks.Add(ls);
                    log.AppendLine($"• Llave {names[i]}: {zones[i].bulbs.Count} bombillas, {zones[i].lights.Count} luces.");
                }
                log.AppendLine($"• Tablero de luces junto a la puerta de la cocina: {Fmt(pos)}.");
            }
            else log.AppendLine("<color=orange>• No encontré 'Door Kitchen' para ubicar el tablero de luces.</color>");

            // ---------- Secuencia de cierre ----------
            var closingGo = new GameObject("Cierre del bar");
            Undo.RegisterCreatedObjectUndo(closingGo, "Armar Etapa 3");
            closingGo.transform.SetParent(root.transform, false);
            var closing = closingGo.AddComponent<ClosingSequence>();
            closing.board = board;
            closing.storm = storm;
            closing.tasks = tasks.ToArray();
            Undo.RecordObject(manager, "Armar Etapa 3");
            manager.closing = closing;
            log.AppendLine($"• Cierre: {tasks.Count} tareas en la pizarra.");

            // ---------- Dardos ----------
            var dartboard = FindInstances("Assets/Models/Darts/dartboard_1k 1.fbx").FirstOrDefault();
            var dartsSet = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Darts Set (Generated)");
            if (dartboard != null && dartsSet != null)
            {
                ClearBatchingStatic(dartboard);
                Bounds bb = WorldBounds(dartboard);
                Vector3 n = Snap(floor.center - bb.center);
                float depth = Mathf.Abs(Vector3.Dot(bb.extents, n));
                Vector3 face = bb.center + n * depth;
                float radius = Mathf.Min(InPlane(bb.extents, n));
                AddBoundsCollider(dartboard, 1.05f);

                var order = new[] { "Dart Blue 1", "Dart Blue 2", "Dart Blue 3", "Dart Red 1", "Dart Red 2", "Dart Red 3" };
                var darts = order.Select(nm => dartsSet.Find(nm)).Where(t => t != null).ToArray();
                foreach (var d in darts) ClearBatchingStatic(d.gameObject);
                var game = Undo.AddComponent<DartGame>(dartboard);
                game.darts = darts;
                if (darts.Length > 0) { DartAxis(darts[0], out Vector3 tip, out Vector3 tail); game.tipLocal = tip; game.backLocal = tail; }
                game.boardCenter = face;
                game.boardNormal = n;
                game.scoringRadius = radius * 170f / 225.5f; // un tablero reglamentario mide 451 mm; el doble exterior, 340 mm
                game.dwellTime = 0.8f;
                game.labelOffset = Vector3.up * (radius + 0.08f);
                game.panelMaterial = boardMat;
                var hit = Sound("Dardos");
                if (hit != null) { Undo.RecordObject(hit, "Armar Etapa 3"); hit.automatic = false; }
                game.hitSound = hit;
                // Pizarrita al costado del tablero, del lado con más pared libre (hacia la pilastra).
                Vector3 lateral = Vector3.Cross(Vector3.up, n).normalized;
                var pilaster = GameObject.Find("Bar/Pilasters/Pilaster Darts");
                float sideSign = pilaster != null ? Mathf.Sign(Vector3.Dot(pilaster.transform.position - bb.center, lateral)) : -1f;
                game.scorePanelPosition = bb.center - n * depth + n * 0.012f + lateral * sideSign * (radius + 0.3f);
                manager.darts = game; // la mejor ronda aparece en la tarjeta final
                log.AppendLine($"• Dardos: {darts.Length} dardos, tablero de radio {radius:0.000} m, cara en {Fmt(face)}. 'Dardos' ya no suena solo.");
            }
            else log.AppendLine("<color=orange>• No encontré el tablero o el set de dardos.</color>");

            // ---------- Arcade ----------
            var arcade = FindInstances("Assets/Models/Arcade/Arcade.fbx").FirstOrDefault();
            var sign = GameObject.Find("Bar/Arcade/Out of Order");
            if (arcade != null)
            {
                ClearBatchingStatic(arcade);
                AddBoundsCollider(arcade, 1.02f);
                var secret = Undo.AddComponent<ArcadeSecret>(arcade);
                secret.cabinet = arcade.transform;
                secret.hum = Sound("Arcade fuera de servicio");
                secret.dwellTime = 2.5f;
                secret.label = "";
                secret.floorY = floor.max.y;
                if (sign != null)
                {
                    secret.sign = sign.transform;
                    Bounds sb = WorldBounds(sign);
                    Vector3 n = Snap(floor.center - sb.center);
                    secret.screenNormal = n;
                    secret.screenCenter = sb.center + n * Mathf.Abs(Vector3.Dot(sb.extents, n));
                }
                log.AppendLine("• Arcade secreto: 3 golpes (tocar o mirarlo 2,5 s) y revive.");
            }
            else log.AppendLine("<color=orange>• No encontré el arcade.</color>");

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            log.AppendLine("Listo. Guardá la escena (Ctrl+S) y dale Play.");
            Debug.Log(log.ToString());
        }

        // =====================================================================

        class Zone { public List<Renderer> bulbs = new List<Renderer>(); public List<Light> lights = new List<Light>(); }

        /// <summary>Lámparas del bar en tres zonas: 0 salón, 1 juegos (pool, dardos, arcade), 2 barra.</summary>
        static Zone[] LampZones()
        {
            var zones = new[] { new Zone(), new Zone(), new Zone() };
            var lightsRoot = GameObject.Find("Bar/Lights");
            if (lightsRoot == null) return zones;
            foreach (Transform lamp in lightsRoot.transform)
            {
                int z = ZoneOf(lamp);
                foreach (var r in lamp.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.sharedMaterials.Any(m => m != null && m.IsKeywordEnabled("_EMISSION"))) zones[z].bulbs.Add(r);
                zones[z].lights.AddRange(lamp.GetComponentsInChildren<Light>(true));
            }
            return zones;
        }

        static int ZoneOf(Transform lamp)
        {
            Vector3 p = lamp.position;
            if (lamp.GetComponentsInChildren<Light>(true).Any() && lamp.name.Contains("Pool")) return 1;
            if (p.x < -5f) return 2;  // detrás de la barra
            if (p.x > 5f) return 1;   // lado de los juegos (dardos, arcade)
            return 0;
        }

        /// <summary>Punta (extremo angosto) y cola del dardo en coordenadas locales de su raíz.</summary>
        static void DartAxis(Transform dart, out Vector3 tip, out Vector3 tail)
        {
            var pts = new List<Vector3>();
            foreach (var mf in dart.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices) pts.Add(dart.InverseTransformPoint(mf.transform.TransformPoint(v)));
            }
            tip = new Vector3(0f, 0f, 0.08f); tail = -tip;
            if (pts.Count < 4) return;
            Vector3 min = pts[0], max = pts[0];
            foreach (var p in pts) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            Vector3 size = max - min;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            float lo = min[axis], hi = max[axis], len = hi - lo;
            Vector3 center = (min + max) * 0.5f;
            // Radio medio de los vértices cercanos a cada extremo: el más angosto es la punta.
            var nearLo = pts.Where(p => p[axis] < lo + len * 0.1f).ToList();
            var nearHi = pts.Where(p => p[axis] > hi - len * 0.1f).ToList();
            bool tipIsLo = Spread(nearLo, center, axis) < Spread(nearHi, center, axis);
            tip = center; tip[axis] = tipIsLo ? lo : hi;
            tail = center; tail[axis] = tipIsLo ? hi : lo;
        }

        static float Spread(List<Vector3> pts, Vector3 center, int axis)
        {
            if (pts.Count == 0) return 0f;
            float acc = 0f;
            foreach (var p in pts) { var d = p - center; d[axis] = 0f; acc += d.magnitude; }
            return acc / pts.Count;
        }

        static void AddBoundsCollider(GameObject go, float inflate)
        {
            if (go.GetComponent<Collider>() != null) return;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var t = go.transform;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var r in renderers)
            {
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 l = t.InverseTransformPoint(c);
                    min = Vector3.Min(min, l); max = Vector3.Max(max, l);
                }
            }
            var box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;
            box.center = (min + max) * 0.5f;
            box.size = (max - min) * inflate;
        }

        static void Cleanup(StringBuilder log)
        {
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t != null && t.name.StartsWith(Prefix)).ToList())
            {
                if (t == null) continue;
                Undo.DestroyObjectImmediate(t.gameObject);
                n++;
            }
            foreach (var c in Object.FindObjectsByType<ClosingTask>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { Undo.DestroyObjectImmediate(c); n++; }
            foreach (var c in Object.FindObjectsByType<DartGame>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { Undo.DestroyObjectImmediate(c); n++; }
            foreach (var c in Object.FindObjectsByType<ArcadeSecret>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { Undo.DestroyObjectImmediate(c); n++; }
            var old = GameObject.Find(RootName);
            if (old != null) { Undo.DestroyObjectImmediate(old); n++; }
            if (n > 0) log.AppendLine($"• Deshice lo armado antes ({n} cosas) para rearmarlo.");
        }

        static IEnumerable<GameObject> FindInstances(string assetPath)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(t => t.gameObject)
                .Where(g => PrefabUtility.IsAnyPrefabInstanceRoot(g) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) == assetPath)
                .ToList();
        }

        static void ClearBatchingStatic(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;
                Undo.RecordObject(t.gameObject, "Armar Etapa 3");
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            }
        }

        static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Dirección horizontal alineada al eje dominante (normal de una pared).</summary>
        static Vector3 Snap(Vector3 d)
        {
            d.y = 0f;
            return Mathf.Abs(d.x) >= Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(d.z));
        }

        static float[] InPlane(Vector3 extents, Vector3 n)
        {
            if (Mathf.Abs(n.x) > 0.5f) return new[] { extents.y, extents.z };
            if (Mathf.Abs(n.z) > 0.5f) return new[] { extents.x, extents.y };
            return new[] { extents.x, extents.z };
        }

        static string Fmt(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
    }
}
