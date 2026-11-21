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
    /// Menú "VR Bar → Armar Etapa 2" (temporal: se corre una vez, se guarda la escena y se borra este script).
    /// Agrega pedidos y actos, la pizarra, los tragos con botellas del estante, las puertas que se abren
    /// con su sonido y la tormenta. Se puede correr de nuevo: deshace lo anterior y lo rearma.
    /// Todo queda en un solo paso de Deshacer (Ctrl+Z).
    /// </summary>
    public static class SetupEtapa2
    {
        const string Prefix = "[Etapa2] ";
        const string RootName = "Interaccion (Etapa 2)";
        const string MatFolder = "Assets/Materials/Interaccion";

        static readonly (string brand, string drink, Color color)[] Bottles =
        {
            ("Chivas", "Whisky", new Color(0.78f, 0.45f, 0.12f)),
            ("Absolut Vodka", "Vodka", new Color(0.9f, 0.94f, 1f)),
            ("Beefeater", "Gin", new Color(0.85f, 0.95f, 0.97f)),
            ("Jagermeister", "Jäger", new Color(0.22f, 0.1f, 0.04f)),
            ("Olmeca", "Tequila", new Color(0.97f, 0.88f, 0.55f)),
            ("Havana_Club", "Ron", new Color(0.72f, 0.44f, 0.16f)),
        };

        [MenuItem("VR Bar/Armar Etapa 2 (pedidos, tragos, puertas y tormenta)")]
        public static void Build()
        {
            var log = new StringBuilder("<b>VR Bar · Etapa 2</b>\n");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Armar Etapa 2");
            int group = Undo.GetCurrentGroup();

            Cleanup(log);

            var beer = Object.FindFirstObjectByType<PourStation>();
            var barPoint = Object.FindObjectsByType<TeleportPoint>(FindObjectsSortMode.None).FirstOrDefault(p => p.label == "Barra");
            var taps = Object.FindObjectsByType<BeerTap>(FindObjectsSortMode.None);
            if (beer == null || barPoint == null || taps.Length == 0)
            {
                Debug.LogError("VR Bar: falta lo de la Etapa 1 (Estacion de cerveza, punto Barra o canillas). ¿Está abierta SampleScene?");
                return;
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Armar Etapa 2");

            // ---------- Materiales ----------
            EnsureFolder(MatFolder);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var boardMat = LoadOrCreate("Pizarra", lit);
            boardMat.SetColor("_BaseColor", new Color(0.09f, 0.13f, 0.11f));
            boardMat.SetFloat("_Smoothness", 0.08f);
            var frameMat = LoadOrCreate("Madera marco", lit);
            frameMat.SetColor("_BaseColor", new Color(0.32f, 0.2f, 0.11f));
            frameMat.SetFloat("_Smoothness", 0.3f);
            var nightMat = LoadOrCreate("Vano entrada (noche)", unlit);
            nightMat.SetColor("_BaseColor", new Color(0.02f, 0.03f, 0.05f));
            var kitchenMat = LoadOrCreate("Vano cocina (luz)", unlit);
            kitchenMat.SetColor("_BaseColor", new Color(0.32f, 0.2f, 0.09f));
            var glassMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/Vidrio jarra.mat");
            var liquidMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/Cerveza.mat");
            foreach (var m in new[] { boardMat, frameMat, nightMat, kitchenMat }) EditorUtility.SetDirty(m);

            // ---------- Sonidos existentes ----------
            var sounds = Object.FindObjectsByType<BarSound>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Func<string, BarSound> Sound = name => sounds.FirstOrDefault(x => x.gameObject.name == name);
            var entrance = Sound("Puerta entrada");
            var kitchen = Sound("Puerta cocina");
            var music = Sound("Parlante (musica del bar)");
            var abrir = Sound("Abrir botella");
            var hielo = Sound("Hielo");
            var botellas = Sound("Botellas (heladera)");
            var caja = Sound("Caja registradora");
            var rain = sounds.Where(s => s.gameObject.name.StartsWith("Lluvia")).ToArray();
            // Ahora los dispara la interacción: los clientes entran con los pedidos y el participante abre botellas y pone hielo.
            foreach (var s in new[] { entrance, abrir, hielo })
                if (s != null) { Undo.RecordObject(s, "Armar Etapa 2"); s.automatic = false; }
            log.AppendLine("• 'Puerta entrada', 'Abrir botella' y 'Hielo' ya no suenan solos: los dispara la interacción.");

            // Sin fuentes nuevas: la pista usa 'Botellas (heladera)' (vuelve a su lugar al terminar).
            var hint = botellas;
            // Trueno: solo si agregaron una grabación (nombre con "trueno" o "thunder" en Assets/Sounds).
            var thunderClips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Sounds" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => { var n = System.IO.Path.GetFileName(p).ToLowerInvariant(); return n.Contains("trueno") || n.Contains("thunder"); })
                .Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p)).Where(c => c != null).ToArray();
            BarSound thunder = null;
            var rainSource = rain.FirstOrDefault();
            if (thunderClips.Length > 0 && rainSource != null)
            {
                // Fuente del trueno junto a la ventana: copia la configuración de la lluvia (suena apagado por el vidrio).
                var go = Object.Instantiate(rainSource.gameObject, rainSource.transform.parent);
                Undo.RegisterCreatedObjectUndo(go, "Armar Etapa 2");
                go.name = $"{Prefix}Trueno (ventana)";
                go.transform.position = rainSource.transform.position;
                thunder = go.GetComponent<BarSound>();
                thunder.clips = thunderClips;
                thunder.loop = false;
                thunder.automatic = false;
                var src = go.GetComponent<AudioSource>();
                src.minDistance = 3f; src.maxDistance = 40f;
                log.AppendLine($"• Trueno: {thunderClips.Length} grabación(es) encontrada(s) ({string.Join(", ", thunderClips.Select(c => c.name))}).");
            }
            else log.AppendLine("<color=orange>• No hay grabación de trueno en Assets/Sounds (nombre con \"trueno\" o \"thunder\"): el relámpago queda mudo. Agregala y volvé a correr el menú.</color>");

            // ---------- Geometría de referencia ----------
            Vector3 front = beer.serveDirection; front.y = 0f; front = front.sqrMagnitude > 0f ? front.normalized : Vector3.right;
            Vector3 tapsCenter = Average(taps.Select(t => t.pourTarget != null ? t.pourTarget.position : t.transform.position));
            var top = GameObject.Find("Bar/Counter/Top");
            Bounds counter = top != null ? WorldBounds(top) : new Bounds(tapsCenter, new Vector3(0.7f, 0.08f, 5f));
            Vector3 along = counter.size.z >= counter.size.x ? Vector3.forward : Vector3.right; // eje largo de la barra
            float roomAhead = Vector3.Dot(counter.max - tapsCenter, along), roomBehind = Vector3.Dot(tapsCenter - counter.min, along);
            Vector3 side = roomAhead >= roomBehind ? along : -along;
            var ceiling = GameObject.Find("Bar/Ceiling");
            float ceilingY = ceiling != null ? WorldBounds(ceiling).min.y : 3.05f;

            // ---------- Pizarra ----------
            var boardGo = new GameObject("Pizarra de pedidos");
            Undo.RegisterCreatedObjectUndo(boardGo, "Armar Etapa 2");
            boardGo.transform.SetParent(root.transform, false);
            Vector3 boardPos = new Vector3(counter.center.x, 2.3f, barPoint.transform.position.z);
            Vector3 away = boardPos - barPoint.transform.position; away.y = 0f;
            boardGo.transform.SetPositionAndRotation(boardPos, Quaternion.LookRotation(away.normalized, Vector3.up));
            var board = boardGo.AddComponent<OrderBoard>();
            board.boardMaterial = boardMat;
            board.frameMaterial = frameMat;
            board.ceilingHeight = ceilingY;
            log.AppendLine($"• Pizarra colgada sobre la barra en {Fmt(boardPos)}, de frente al barman.");

            // ---------- Tragos ----------
            var shotGo = new GameObject("Estacion de tragos");
            Undo.RegisterCreatedObjectUndo(shotGo, "Armar Etapa 2");
            shotGo.transform.SetParent(root.transform, false);
            var spot = new GameObject("Apoyo vaso");
            Undo.RegisterCreatedObjectUndo(spot, "Armar Etapa 2");
            spot.transform.SetParent(shotGo.transform, false);
            spot.transform.position = tapsCenter + side * 0.6f;
            var shot = shotGo.AddComponent<ShotStation>();
            shot.glassSpot = spot.transform;
            shot.serveDirection = front;
            shot.glassMaterial = glassMat;
            shot.liquidMaterial = liquidMat;
            shot.abrirBotellaSound = abrir;
            shot.hieloSound = hielo;
            shot.botellasSound = botellas;
            shot.cajaSound = caja;
            log.AppendLine($"• Estación de tragos: el vaso se apoya en {Fmt(spot.transform.position)}.");

            var shelf = GameObject.Find("Bar/Shelf Bottles");
            foreach (var entry in Bottles)
            {
                string brand = entry.brand, drink = entry.drink;
                Color color = entry.color;
                var candidates = shelf != null
                    ? shelf.GetComponentsInChildren<Transform>(true).Where(t => t.parent == shelf.transform && t.name == brand)
                    : Enumerable.Empty<Transform>();
                var bottle = candidates.OrderBy(t => Vector3.Distance(t.position, barPoint.transform.position + Vector3.up * 1.5f)).FirstOrDefault();
                if (bottle == null) { log.AppendLine($"<color=orange>• No encontré la botella '{brand}'.</color>"); continue; }
                ClearBatchingStatic(bottle.gameObject);
                var mf = bottle.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh != null);
                if (bottle.GetComponentInChildren<Collider>(true) == null && mf != null)
                {
                    var col = Undo.AddComponent<BoxCollider>(mf.gameObject);
                    col.isTrigger = true;
                    col.center = mf.sharedMesh.bounds.center;
                    col.size = mf.sharedMesh.bounds.size * 1.1f;
                }
                // Cartel en el borde del estante, debajo de la botella, de frente al barman.
                Vector3 tagPos = bottle.position + front * 0.12f + Vector3.down * 0.015f;
                Physics.SyncTransforms();
                if (Physics.Raycast(bottle.position + Vector3.up * 0.03f, Vector3.down, out RaycastHit shelfHit, 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    Bounds sb = shelfHit.collider.bounds;
                    float edge = Vector3.Dot(sb.center, front) + Mathf.Abs(Vector3.Dot(sb.extents, front));
                    Vector3 onEdge = bottle.position + front * (edge - Vector3.Dot(bottle.position, front) + 0.004f);
                    tagPos = new Vector3(onEdge.x, sb.center.y - 0.006f, onEdge.z);
                }
                var src = Undo.AddComponent<BottleSource>(bottle.gameObject);
                src.tagPosition = tagPos;
                src.tagFacing = front;
                src.tagMaterial = boardMat;
                src.station = shot;
                src.drinkName = drink;
                src.liquidColor = color;
                src.label = $"{drink} · {brand.Replace("_", " ")}";
                src.labelOffset = front * 0.18f + Vector3.up * 0.3f; // delante del estante, hacia el barman
                src.dwellTime = 1f;
                src.outlineWidth = 0.003f;
                log.AppendLine($"• Botella '{brand}' → {drink} ({Fmt(bottle.position)}).");
            }

            // ---------- Puertas ----------
            Bounds floor = new Bounds(Vector3.zero, new Vector3(13f, 0f, 9f));
            var floorGo = GameObject.Find("Bar/Floor");
            if (floorGo != null) floor = WorldBounds(floorGo);
            SetupDoor("Door Entrance", entrance, 80f, nightMat, rain, floor, root.transform, log);
            SetupDoor("Door Kitchen", kitchen, 70f, kitchenMat, null, floor, root.transform, log);

            // ---------- Tormenta ----------
            var lightGo = new GameObject("Relampago (luz en tiempo real)");
            Undo.RegisterCreatedObjectUndo(lightGo, "Armar Etapa 2");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.15f, -0.55f, -1f).normalized, Vector3.up);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.78f, 0.85f, 1f);
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.enabled = false;
            var storm = lightGo.AddComponent<LightningStorm>();
            storm.flashLight = light;
            storm.thunder = thunder;
            log.AppendLine("• Tormenta: relámpago cada 25–55 s (luz en tiempo real, solo encendida durante el destello).");

            // ---------- Pedidos ----------
            var managerGo = new GameObject("Pedidos y actos");
            Undo.RegisterCreatedObjectUndo(managerGo, "Armar Etapa 2");
            managerGo.transform.SetParent(root.transform, false);
            var manager = managerGo.AddComponent<OrderManager>();
            manager.board = board;
            manager.beerStation = beer;
            manager.shotStation = shot;
            manager.barPoint = barPoint;
            manager.storm = storm;
            manager.entranceSound = entrance;
            manager.musicSound = music;
            manager.hintSound = hint;
            if (entrance == null || music == null) log.AppendLine("<color=orange>• No encontré 'Puerta entrada' o el parlante: revisá las referencias de 'Pedidos y actos'.</color>");

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            log.AppendLine("Listo. Guardá la escena (Ctrl+S) y dale Play. No hace falta rehornear la luz.");
            Debug.Log(log.ToString());
        }

        // =====================================================================

        static void SetupDoor(string doorName, BarSound sound, float angle, Material vanoMat, BarSound[] louder, Bounds floor, Transform root, StringBuilder log)
        {
            var doorRoot = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == doorName);
            if (doorRoot == null) { log.AppendLine($"<color=orange>• No encontré '{doorName}'.</color>"); return; }
            var door = doorRoot.Find("Door");
            var handle = doorRoot.Find("Handle");
            if (door == null) { log.AppendLine($"<color=orange>• '{doorName}' no tiene un hijo 'Door'.</color>"); return; }

            Bounds b = WorldBounds(door.gameObject);
            bool wideX = b.size.x >= b.size.z;
            Vector3 along = wideX ? Vector3.right : Vector3.forward;
            Vector3 normal = wideX ? Vector3.forward : Vector3.right;
            Vector3 toRoom = floor.center - b.center; toRoom.y = 0f;
            if (Vector3.Dot(toRoom, normal) < 0f) normal = -normal; // hacia adentro del salón
            float handleSide = handle != null ? Mathf.Sign(Vector3.Dot(handle.position - b.center, along)) : 1f;
            float halfWidth = Mathf.Abs(Vector3.Dot(b.extents, along));
            float thickness = Mathf.Abs(Vector3.Dot(b.size, normal));
            Vector3 hinge = b.center - along * handleSide * halfWidth; // bisagra del lado opuesto al picaporte

            var pivot = new GameObject($"{Prefix}Bisagra");
            Undo.RegisterCreatedObjectUndo(pivot, "Armar Etapa 2");
            pivot.transform.SetParent(doorRoot, false);
            pivot.transform.position = hinge;
            ClearBatchingStatic(door.gameObject);
            Undo.SetTransformParent(door, pivot.transform, "Armar Etapa 2");
            if (handle != null) { ClearBatchingStatic(handle.gameObject); Undo.SetTransformParent(handle, pivot.transform, "Armar Etapa 2"); }

            // Signo: que el lado libre de la puerta vaya hacia el salón.
            Vector3 arm = b.center - hinge;
            float sign = Vector3.Dot(Quaternion.AngleAxis(angle, Vector3.up) * arm - arm, normal) >= 0f ? 1f : -1f;

            var swing = pivot.AddComponent<DoorSwing>();
            swing.sound = sound;
            swing.openAngle = angle * sign;
            swing.louderWhileOpen = louder;

            // Vano oscuro detrás de la puerta: al abrirla se ve un hueco (calle de noche / cocina) y no la pared.
            var vano = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(vano, "Armar Etapa 2");
            vano.name = $"Vano {doorName}";
            Object.DestroyImmediate(vano.GetComponent<Collider>());
            vano.transform.SetParent(root, false);
            vano.transform.position = b.center - normal * (thickness * 0.5f - 0.003f);
            vano.transform.rotation = Quaternion.LookRotation(normal, Vector3.up);
            vano.transform.localScale = new Vector3(halfWidth * 2f * 0.97f, b.size.y * 0.985f, 0.004f);
            var r = vano.GetComponent<Renderer>();
            r.sharedMaterial = vanoMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            log.AppendLine($"• Puerta '{doorName}': bisagra en {Fmt(hinge)}, abre {swing.openAngle:0}° hacia el salón con '{(sound != null ? sound.gameObject.name : "—")}'.");
        }

        static void Cleanup(StringBuilder log)
        {
            int n = 0;
            // Puertas: devolver la hoja y el picaporte a su lugar antes de borrar la bisagra.
            foreach (var pivot in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == $"{Prefix}Bisagra").ToList())
            {
                foreach (var child in pivot.Cast<Transform>().ToList())
                    Undo.SetTransformParent(child, pivot.parent, "Armar Etapa 2");
                Undo.DestroyObjectImmediate(pivot.gameObject);
                n++;
            }
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t != null && t.name.StartsWith(Prefix)).ToList())
            {
                if (t == null) continue;
                Undo.DestroyObjectImmediate(t.gameObject);
                n++;
            }
            foreach (var b in Object.FindObjectsByType<BottleSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(b);
                n++;
            }
            var old = GameObject.Find(RootName);
            if (old != null) { Undo.DestroyObjectImmediate(old); n++; }
            if (n > 0) log.AppendLine($"• Deshice lo armado antes ({n} cosas) para rearmarlo.");
        }

        static void ClearBatchingStatic(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;
                Undo.RecordObject(t.gameObject, "Armar Etapa 2");
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            }
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

        static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static Vector3 Average(IEnumerable<Vector3> points)
        {
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var p in points) { sum += p; n++; }
            return n > 0 ? sum / n : Vector3.zero;
        }

        static string Fmt(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
    }
}
