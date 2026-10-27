using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace BarAudio.EditorTools
{
    /// <summary>
    /// Menú "Bar Audio": arma toda la sonorización de la escena en un objeto "Audio (Bar)".
    /// Se puede volver a ejecutar: borra el "Audio (Bar)" anterior y lo reconstruye.
    /// Los sonidos se buscan por código en Assets/Sounds (ej. "F8_" = todos los vasos).
    /// </summary>
    public static class BarAudioSceneBuilder
    {
        const string SoundsRoot = "Assets/Sounds";
        const string MixerPath = "Assets/Sounds/BarMixer.mixer";
        const string RootName = "Audio (Bar)";

        static AudioMixer mixer;
        static Dictionary<string, AudioMixerGroup> groups;
        static bool useSpatializer;

        [MenuItem("Bar Audio/1. Reimportar sonidos (ajustes optimizados)")]
        public static void ReimportSounds()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SoundsRoot }))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
                n++;
            }
            Debug.Log("[Bar Audio] Reimportados " + n + " sonidos con los ajustes de BarAudioImporter.");
        }

        [MenuItem("Bar Audio/2. Construir audio en la escena")]
        public static void Build()
        {
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) { Debug.LogError("[Bar Audio] No encontré el mixer en " + MixerPath); return; }
            groups = new Dictionary<string, AudioMixerGroup>();
            foreach (var g in mixer.FindMatchingGroups("Master")) groups[g.name] = g;
            useSpatializer = !string.IsNullOrEmpty(AudioSettings.GetSpatializerPluginName());

            Scene scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName) Undo.DestroyObjectImmediate(go);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Construir audio del bar");
            Transform R = root.transform;

            // ---------------- Referencias de la escena ----------------
            Transform cam = Find("Main Camera");
            Transform camOffset = Find("Camera Offset");
            Transform doorEntrance = Find("Door", "Door Entrance");
            Transform doorKitchen = Find("Door", "Door Kitchen");
            Transform pool = Find("pool_table_scene");
            Transform dartboard = Find("dartboard_1k");
            Transform arcade = Find("Arcade", "Bar");
            Transform neon = Find("Neon_Palm_Tree");
            Transform exitSign = Find("Exit_Sign");
            Transform tap = Find("beer_tap (1)");
            Transform windowL = Find("Window", "Window Left");
            Transform windowR = Find("Window", "Window Right");

            Bounds poolB = BoundsOf(pool, new Vector3(0f, 0.4f, 0f), new Vector3(2.4f, 0.8f, 1.3f));
            Vector3 poolTop = new Vector3(poolB.center.x, 0.85f, poolB.center.z);
            Vector3 dartsC = BoundsOf(dartboard, new Vector3(6.8f, 1.73f, 3.4f), Vector3.one * 0.45f).center;
            Vector3 arcadeC = BoundsOf(arcade, new Vector3(6.3f, 1f, 0f), new Vector3(0.8f, 1.8f, 0.8f)).center;
            Vector3 tapC = BoundsOf(tap, new Vector3(-5.3f, 1.2f, -2.2f), Vector3.one * 0.3f).center;
            Debug.Log($"[Bar Audio] Pool bounds {poolB.center} size {poolB.size} | Dardos {dartsC} | Arcade {arcadeC} | Canilla {tapC}");

            // Mesas (donde suenan murmullo, risas y vasos)
            Vector3[] tables =
            {
                new Vector3(2.5f, 1.25f, 2.5f), new Vector3(-2.5f, 1.25f, 2.5f), new Vector3(0f, 1.25f, 3f),
                new Vector3(2.5f, 1.25f, -2.5f), new Vector3(-2.5f, 1.25f, -2.5f)
            };

            // ---------------- MÚSICA (diegética, parlantes) ----------------
            Transform musica = Node("Musica (diegetica)", R, Vector3.zero).transform;
            var spk1 = Node("Parlante esquina delantera izq.", musica, new Vector3(-6.5f, 2.75f, -4.5f));
            var spk2 = Node("Parlante esquina trasera der.", musica, new Vector3(6.5f, 2.75f, 4.5f));
            var s1 = Src(spk1, "Musica", null, 0.75f, 3f, 25f, 90f, 20);
            var s2 = Src(spk2, "Musica", null, 0.75f, 3f, 25f, 90f, 20);
            foreach (var spk in new[] { spk1, spk2 })
            {   // "color" de parlante de bar: sin graves profundos ni agudos extremos
                spk.AddComponent<AudioHighPassFilter>().cutoffFrequency = 90f;
                spk.AddComponent<AudioLowPassFilter>().cutoffFrequency = 12000f;
            }
            var playlist = musica.gameObject.AddComponent<MusicPlaylist>();
            playlist.speakers = new[] { s1, s2 };
            playlist.songs = new[] { Clip("M1"), Clip("M2"), Clip("M3") };

            // ---------------- AMBIENTE ----------------
            Transform amb = Node("Ambiente", R, Vector3.zero).transform;
            Loop(Node("Murmullo fin de noche (cama 2D)", amb, new Vector3(0f, 1.6f, 0f)), "Voces", Clip("A1"), 0.3f, 0f, 0f, 0f, 10, false);
            for (int i = 0; i < 3; i++)
                Loop(Node("Murmullo mesa " + (i + 1), amb, tables[i]), "Voces", Clip("A2"), 0.4f, 1.2f, 15f, 120f, 40, true, 0.04f);
            var rainL = Loop(Node("Lluvia ventana izq.", amb, Pos(windowL, new Vector3(-3.1f, 1.95f, 4.9f)) + new Vector3(0f, 0f, 0.3f)), "Ambiente", Clip("A3"), 0.5f, 1.5f, 18f, 140f, 30);
            var rainR = Loop(Node("Lluvia ventana der.", amb, Pos(windowR, new Vector3(3.1f, 1.95f, 4.9f)) + new Vector3(0f, 0f, 0.3f)), "Ambiente", Clip("A3"), 0.5f, 1.5f, 18f, 140f, 30);
            rainL.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 2500f; // a través del vidrio
            rainR.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 2500f;
            Loop(Node("Heladera de cervezas", amb, new Vector3(-6.5f, 0.5f, 0.4f)), "Ambiente", Clip("A6"), 0.35f, 0.6f, 7f, 0f, 60);
            Loop(Node("Neon palmera", amb, BoundsOf(neon, new Vector3(4.4f, 1.55f, -4.95f), Vector3.one).center), "Ambiente", Clip("A5"), 0.22f, 0.4f, 5f, 0f, 80, true, 0.03f);
            Loop(Node("Neon cartel EXIT", amb, BoundsOf(exitSign, new Vector3(0f, 2.35f, -4.78f), Vector3.one * 0.4f).center), "Ambiente", Clip("A5"), 0.15f, 0.4f, 4f, 0f, 80, true, 0.06f);

            // ---------------- PUERTAS ----------------
            Transform puertas = Node("Puertas", R, Vector3.zero).transform;
            {
                Bounds b = BoundsOf(doorEntrance, new Vector3(0f, 1.05f, -4.78f), new Vector3(1f, 2.1f, 0.06f));
                var go = Node("Puerta entrada", puertas, b.center);
                var src = Src(go, "Foley", null, 0.9f, 1.2f, 20f, 0f, 50);
                var calle = Node("Calle (lluvia del otro lado)", go.transform, b.center + new Vector3(0f, 0f, -0.8f));
                var calleSrc = Src(calle, "Ambiente", Clip("A3"), 0.1f, 1f, 15f, 90f, 40);
                calle.AddComponent<AudioLowPassFilter>().cutoffFrequency = 600f;
                var door = go.AddComponent<DoorAudio>();
                door.doorSource = src;
                door.doorClips = new[] { Clip("F4") };
                door.openAt = 0.4f; door.closeAt = 3.75f; door.swingSeconds = 0.6f;
                door.beyondSource = calleSrc; door.closedVolume = 0.08f; door.openVolume = 0.9f;
                door.closedCutoff = 600f; door.openCutoff = 9000f;
                door.interval = new Vector2(45f, 110f);
                // Las puertas son Static (luz horneada + batching) y la manija es otro objeto: por eso no se anima.
                // Para animarla: sacarle "Static", agrupar puerta+manija y asignar doorLeaf / hingeWorldPosition.
                door.hingeWorldPosition = new Vector3(b.min.x, b.center.y, b.center.z);
                door.openAngle = -75f;
            }
            {
                Bounds b = BoundsOf(doorKitchen, new Vector3(-6.78f, 1.05f, -3.3f), new Vector3(0.06f, 2.1f, 1f));
                var go = Node("Puerta cocina", puertas, b.center);
                var src = Src(go, "Foley", null, 0.8f, 1.2f, 18f, 0f, 50);
                var cocina = Node("Cocina (del otro lado)", go.transform, b.center + new Vector3(-0.9f, 0.15f, 0f));
                var cocinaSrc = Src(cocina, "Ambiente", Clip("A4"), 0.3f, 1f, 12f, 90f, 40);
                cocina.AddComponent<AudioLowPassFilter>().cutoffFrequency = 500f;
                var door = go.AddComponent<DoorAudio>();
                door.doorSource = src;
                door.doorClips = new[] { Clip("F5") };
                door.openAt = 0.6f; door.closeAt = 2.6f; door.swingSeconds = 0.5f;
                door.beyondSource = cocinaSrc; door.closedVolume = 0.3f; door.openVolume = 0.9f;
                door.closedCutoff = 500f; door.openCutoff = 8000f;
                door.interval = new Vector2(30f, 80f);
                door.hingeWorldPosition = new Vector3(b.center.x, b.center.y, b.max.z);
                door.openAngle = -75f;
            }

            // ---------------- BARRA (foley) ----------------
            Transform barra = Node("Barra", R, Vector3.zero).transform;
            Shots(Node("Canilla de cerveza", barra, tapC), "Foley", Clips("F6"), 0.8f, new Vector2(25f, 60f), 1f, 15f, 1, null, 0f, new Vector2(0.6f, 0.85f), new Vector2(0.97f, 1.03f));
            Shots(Node("Abrir botella", barra, new Vector3(-6.2f, 1.0f, 1.2f)), "Foley", Clips("F7"), 0.8f, new Vector2(40f, 90f), 1f, 15f, 1, null, 0f, new Vector2(0.5f, 0.8f), new Vector2(0.95f, 1.05f));
            var vasos = Shots(Node("Vasos", barra, new Vector3(-5f, 1.1f, 0f)), "Foley", Clips("F8"), 1f, new Vector2(4f, 12f), 0.8f, 15f, 2,
                new[] { new Vector3(-5f, 1.1f, -1.5f), new Vector3(-5f, 1.1f, 0.2f), new Vector3(-5f, 1.1f, 1.8f), new Vector3(-6.3f, 1.0f, 0.4f),
                        new Vector3(2.5f, 1.05f, 2.5f), new Vector3(-2.5f, 1.05f, 2.5f), new Vector3(0f, 1.05f, 3f), new Vector3(2.5f, 1.05f, -2.5f) },
                0.3f, new Vector2(0.35f, 0.8f), new Vector2(0.94f, 1.06f));
            vasos.burst = new Vector2Int(1, 2); vasos.burstGap = new Vector2(0.2f, 0.6f);
            Shots(Node("Hielo", barra, new Vector3(-6.2f, 1.0f, -0.5f)), "Foley", Clips("F9"), 0.7f, new Vector2(30f, 70f), 0.8f, 12f, 1, null, 0.2f, new Vector2(0.5f, 0.8f), new Vector2(0.94f, 1.06f));
            Shots(Node("Caja registradora", barra, new Vector3(-6.4f, 1.0f, 2.5f)), "Foley", Clips("F10"), 0.7f, new Vector2(50f, 120f), 1f, 15f, 1, null, 0f, new Vector2(0.5f, 0.75f), new Vector2(0.98f, 1.02f));
            var botellas = Shots(Node("Botellas (reponer heladera)", barra, new Vector3(-6.5f, 0.6f, 0.4f)), "Foley", Clips("F11"), 0.7f, new Vector2(35f, 90f), 0.8f, 12f, 2, null, 0.3f, new Vector2(0.4f, 0.8f), new Vector2(0.95f, 1.05f));
            botellas.burst = new Vector2Int(2, 4); botellas.burstGap = new Vector2(0.15f, 0.5f);
            Shots(Node("Banquetas", barra, new Vector3(-4.35f, 0.3f, 0f)), "Foley", Clips("F13"), 0.6f, new Vector2(25f, 70f), 1f, 14f, 1,
                new[] { new Vector3(-4.35f, 0.3f, -1.17f), new Vector3(-4.35f, 0.3f, 0.72f), new Vector3(-4.35f, 0.3f, 1.98f),
                        new Vector3(2.1f, 0.3f, 2.05f), new Vector3(-2f, 0.3f, 2f), new Vector3(2.9f, 0.3f, -2.9f) },
                0f, new Vector2(0.4f, 0.8f), new Vector2(0.9f, 1.1f));

            // ---------------- VOCES ----------------
            Transform voces = Node("Voces", R, Vector3.zero).transform;
            Shots(Node("Risas en las mesas", voces, tables[0]), "Voces", Concat(Clips("V1"), Clips("V2")), 0.6f, new Vector2(30f, 75f), 1.5f, 18f, 1,
                new[] { tables[0], tables[1], tables[2] }, 0.3f, new Vector2(0.4f, 0.7f), new Vector2(0.95f, 1.05f));
            var reacciones = Shots(Node("Reacciones jugadores de pool", voces, poolTop + new Vector3(0f, 0.75f, 0f)), "Voces", Concat(Clips("V1"), Clips("V2"), Clips("V3")), 0.7f, new Vector2(999f, 999f), 1.5f, 18f, 1,
                new[] { poolTop + new Vector3(1.6f, 0.75f, 0f), poolTop + new Vector3(-1.6f, 0.75f, 0f), poolTop + new Vector3(0f, 0.75f, 1.2f), poolTop + new Vector3(0f, 0.75f, -1.2f) },
                0.2f, new Vector2(0.5f, 0.8f), new Vector2(0.96f, 1.04f));
            reacciones.playAutomatically = false;

            // ---------------- JUEGOS (grupo que NO se aleja en el recuerdo) ----------------
            Transform juegos = Node("Juegos", R, Vector3.zero).transform;
            var poolGo = Node("Pool", juegos, poolTop);
            Src(poolGo, "Juegos", null, 0.9f, 1.2f, 20f, 40f, 45);
            Src(poolGo, "Juegos", null, 0.9f, 1.2f, 20f, 40f, 45);
            var sim = poolGo.AddComponent<PoolGameSimulator>();
            sim.breakClip = Clip("F1");
            sim.hitClips = Clips("F2");
            sim.pocketClips = Clips("F3");
            sim.reactions = reacciones;
            sim.tableHalfSize = new Vector2(Mathf.Min(poolB.extents.x * 0.75f, 1.2f), Mathf.Min(poolB.extents.z * 0.75f, 1.2f));

            var poolEco = Shots(Node("Pool (eco del recuerdo)", juegos, poolTop), "Juegos", Concat(Clips("F2"), Clips("F3")), 0.8f, new Vector2(999f, 999f), 1.2f, 20f, 2, null, 0.6f, new Vector2(0.5f, 0.9f), new Vector2(0.95f, 1.05f));
            poolEco.playAutomatically = false; poolEco.burst = new Vector2Int(1, 3); poolEco.burstGap = new Vector2(0.3f, 1f);

            var dardos = Shots(Node("Dardos", juegos, dartsC), "Juegos", Clips("F12"), 0.8f, new Vector2(60f, 150f), 1f, 16f, 2, null, 0.08f, new Vector2(0.6f, 0.95f), new Vector2(0.95f, 1.05f));
            dardos.burst = new Vector2Int(3, 3); dardos.burstGap = new Vector2(1.5f, 3f);

            Loop(Node("Arcade fuera de servicio (zumbido)", juegos, arcadeC), "Juegos", Clip("A7"), 0.3f, 0.6f, 8f, 0f, 70);

            // ---------------- RECUERDO (extradiegético) ----------------
            var recuerdoGo = Node("Recuerdo (extradiegetico)", R, Vector3.zero);
            var memSrc = Src(recuerdoGo, "Recuerdo", Clip("M4"), 0f, 1f, 1f, 0f, 0, false);
            var memory = recuerdoGo.AddComponent<MemoryLayer>();
            memory.head = cam;
            memory.mixer = mixer;
            memory.memoryMusic = memSrc;
            memory.memoryMusicVolume = 0.55f;
            // La mesa de pool trae las lámparas colgantes en el mismo modelo (llega hasta el techo):
            // por eso el blanco de la mirada es un punto sobre el paño y no el modelo entero.
            var poolGaze = Node("Blanco de mirada - pano del pool", recuerdoGo.transform, poolTop).transform;
            memory.targets = new[]
            {
                new MemoryTarget { label = "Pool", target = poolGaze, gazeSize = 1.2f, proximity = 1.0f, echo = poolEco },
                new MemoryTarget { label = "Dardos", target = dartboard, gazeSize = 2.5f, proximity = 1.2f, echo = dardos },
                new MemoryTarget { label = "Arcade", target = arcade, gazeSize = 0.6f, proximity = 1.2f }
            };

            // ---------------- REVERB DEL SALÓN ----------------
            var rev = Node("Reverb del salon", R, new Vector3(0f, 1.5f, 0f)).AddComponent<AudioReverbZone>();
            rev.minDistance = 6f;
            rev.maxDistance = 10f;
            rev.reverbPreset = AudioReverbPreset.User;
            rev.room = -1000; rev.roomHF = -600; rev.roomLF = 0;
            rev.decayTime = 1.1f; rev.decayHFRatio = 0.6f;
            rev.reflections = -900; rev.reflectionsDelay = 0.012f;
            rev.reverb = -700; rev.reverbDelay = 0.02f;
            rev.HFReference = 5000f; rev.LFReference = 250f;
            rev.diffusion = 100f; rev.density = 100f;

            // ---------------- Prueba en el Editor sin celular ----------------
            if (camOffset != null && camOffset.GetComponent<EditorMouseLook>() == null)
                Undo.AddComponent<EditorMouseLook>(camOffset.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log("[Bar Audio] Listo: " + root.GetComponentsInChildren<AudioSource>(true).Length + " fuentes de audio creadas. Guardá la escena (Ctrl+S).");
        }

        // ======================= helpers =======================

        static GameObject Node(string name, Transform parent, Vector3 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            return go;
        }

        static AudioSource Src(GameObject go, string group, AudioClip clip, float volume, float minDist, float maxDist, float spread, int priority, bool is3D = true)
        {
            var s = go.AddComponent<AudioSource>();
            if (groups.TryGetValue(group, out var g)) s.outputAudioMixerGroup = g;
            else Debug.LogWarning("[Bar Audio] Falta el grupo " + group + " en el mixer");
            s.clip = clip;
            s.volume = volume;
            s.playOnAwake = false;
            s.priority = priority;
            s.dopplerLevel = 0f; // nada se mueve rápido: el Doppler solo agregaría desafinación
            if (is3D)
            {
                s.spatialBlend = 1f;
                s.spatialize = useSpatializer; // HRTF si hay un plugin configurado en Project Settings > Audio
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = minDist;
                s.maxDistance = maxDist;
                s.spread = spread;
                s.reverbZoneMix = 1f;
            }
            else
            {
                s.spatialBlend = 0f;
                s.spatialize = false;
                s.bypassReverbZones = true;
                s.reverbZoneMix = 0f;
            }
            return s;
        }

        static AudioSource Loop(GameObject go, string group, AudioClip clip, float volume, float minDist, float maxDist, float spread, int priority, bool is3D = true, float pitchVar = 0.02f)
        {
            var s = Src(go, group, clip, volume, minDist, maxDist, spread, priority, is3D);
            s.loop = true;
            var l = go.AddComponent<LoopEmitter>();
            l.pitchVariation = pitchVar;
            return s;
        }

        static RandomOneShots Shots(GameObject go, string group, AudioClip[] clips, float volume, Vector2 interval, float minDist, float maxDist,
            int voices, Vector3[] points, float jitter, Vector2 volRange, Vector2 pitchRange)
        {
            for (int i = 0; i < Mathf.Max(1, voices); i++) Src(go, group, null, volume, minDist, maxDist, 0f, 90);
            var r = go.AddComponent<RandomOneShots>();
            r.clips = clips;
            r.interval = interval;
            r.worldPoints = points ?? new Vector3[0];
            r.positionJitter = jitter;
            r.volume = volRange;
            r.pitch = pitchRange;
            return r;
        }

        static AudioClip[] Clips(string code)
        {
            var list = new List<AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SoundsRoot }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(p).StartsWith(code + "_"))
                    list.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(p));
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (list.Count == 0) Debug.LogWarning("[Bar Audio] No encontré sonidos " + code + "_* en " + SoundsRoot);
            return list.ToArray();
        }

        static AudioClip Clip(string code)
        {
            var c = Clips(code);
            return c.Length > 0 ? c[0] : null;
        }

        static AudioClip[] Concat(params AudioClip[][] arrays)
        {
            var list = new List<AudioClip>();
            foreach (var a in arrays) list.AddRange(a);
            return list.ToArray();
        }

        static Transform Find(string name, string parentName = null)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name && (parentName == null || (t.parent != null && t.parent.name == parentName)))
                        return t;
            Debug.LogWarning("[Bar Audio] No encontré '" + name + "'" + (parentName != null ? " dentro de '" + parentName + "'" : "") + "; uso una posición aproximada.");
            return null;
        }

        static Vector3 Pos(Transform t, Vector3 fallback)
        {
            return t != null ? BoundsOf(t, fallback, Vector3.one).center : fallback;
        }

        static Bounds BoundsOf(Transform t, Vector3 fallbackCenter, Vector3 fallbackSize)
        {
            if (t != null)
            {
                var rs = t.GetComponentsInChildren<Renderer>(true);
                if (rs.Length > 0)
                {
                    Bounds b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    return b;
                }
                return new Bounds(t.position, fallbackSize);
            }
            return new Bounds(fallbackCenter, fallbackSize);
        }
    }
}
