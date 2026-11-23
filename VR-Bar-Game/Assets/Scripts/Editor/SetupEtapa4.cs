using System.Collections.Generic;
using System.Linq;
using System.Text;
using BarAudio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BarInteraccion.EditorTools
{
    /// <summary>
    /// Menú "VR Bar → Armar Etapa 4 (pool)" (temporal: se corre una vez, se guarda la escena y se borra).
    /// El modelo de la mesa está estirado en altura (escala 1,3 en Y), así que las bolas y el taco originales
    /// quedan ovalados y no sirven para física: se ocultan y se crean bolas esféricas (misma malla y material)
    /// y un taco nuevo. Paño, bandas, troneras y bordes se calculan a partir del modelo.
    /// </summary>
    public static class SetupEtapa4
    {
        const string Prefix = "[Etapa4] ";
        const string TablePath = "Assets/Models/Pool/source/pool_table_scene.fbx";
        const string MatFolder = "Assets/Materials/Interaccion";

        // Medidas del modelo (cm): mitad del campo hasta la nariz de las bandas / mitad de la mesa.
        const float LongRatio = 117f / 124.6f, ShortRatio = 56f / 70.3f;

        [MenuItem("VR Bar/Armar Etapa 4 (pool)")]
        public static void Build()
        {
            var log = new StringBuilder("<b>VR Bar · Etapa 4 (pool)</b>\n");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Armar Etapa 4");
            int group = Undo.GetCurrentGroup();

            var table = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(tr => tr.gameObject)
                .FirstOrDefault(g => PrefabUtility.IsAnyPrefabInstanceRoot(g) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) == TablePath);
            if (table == null) { Debug.LogError("VR Bar: no encontré pool_table_scene en la escena."); return; }

            Cleanup(table, log);

            var renderers = table.GetComponentsInChildren<MeshRenderer>(true);
            var tableR = renderers.FirstOrDefault(x => MeshName(x).Contains("pooltable"));
            var ballRs = renderers.Where(x => MeshName(x).StartsWith("billiard_ball")).ToList();
            var cueRs = renderers.Where(x => MeshName(x).StartsWith("pool_cue")).ToList();
            if (tableR == null || ballRs.Count == 0) { Debug.LogError("VR Bar: el modelo del pool no tiene la mesa o las bolas esperadas."); return; }

            Bounds tb = tableR.bounds;
            bool longX = tb.size.x >= tb.size.z;
            float halfL = (longX ? tb.extents.x : tb.extents.z) * LongRatio;
            float halfS = (longX ? tb.extents.z : tb.extents.x) * ShortRatio;
            Vector3 L = longX ? Vector3.right : Vector3.forward, S = longX ? Vector3.forward : Vector3.right;
            float r = ballRs.Average(b => Mathf.Min(b.bounds.extents.x, b.bounds.extents.z));
            float feltY = ballRs.Min(b => b.bounds.min.y);
            Vector3 center = new Vector3(tb.center.x, 0f, tb.center.z);
            log.AppendLine($"• Mesa: campo {halfL * 2:0.00} × {halfS * 2:0.00} m, paño a {feltY:0.000} m, bolas de {r * 2 * 100:0.0} cm.");

            // Los colliders propios del modelo (si los hubiera) taparían el paño para la mirada y chocarían con las bolas.
            int off = 0;
            foreach (var col in table.GetComponentsInChildren<Collider>(true))
                if (col.enabled) { Undo.RecordObject(col, "Armar Etapa 4"); col.enabled = false; off++; }
            if (off > 0) log.AppendLine($"• Desactivé {off} collider(s) del modelo de la mesa.");

            // Materiales de física.
            var felt = PhysMat("Pool paño", 0.2f, 0.2f, 0f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Minimum);
            var rail = PhysMat("Pool banda", 0.1f, 0.1f, 0.78f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Average);
            var ballMat = PhysMat("Pool bola", 0.05f, 0.05f, 0.93f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Average);

            var root = new GameObject($"{Prefix}Pool (juego)");
            Undo.RegisterCreatedObjectUndo(root, "Armar Etapa 4");
            root.transform.position = center;
            var game = root.AddComponent<PoolGame>();
            game.feltY = feltY;
            game.halfField = longX ? new Vector2(halfL, halfS) : new Vector2(halfS, halfL);

            // Paño, bandas (con huecos en las troneras), bordes de seguridad y troneras.
            Box(root.transform, "Paño", center + Vector3.up * (feltY - 0.025f), (L * (halfL + 0.14f) + S * (halfS + 0.14f)) * 2f + Vector3.up * 0.05f, felt);
            float corner = 0.09f, middle = 0.065f, th = 0.06f, h = 0.06f, y = feltY + h * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                // Lados largos: dos tramos, cortados por la tronera del medio.
                float a0 = -halfL + corner, a1 = -middle, b0 = middle, b1 = halfL - corner;
                Vector3 sOff = S * side * (halfS + th * 0.5f);
                Box(root.transform, "Banda larga", center + L * ((a0 + a1) * 0.5f) + sOff + Vector3.up * y, L * (a1 - a0) + S * th + Vector3.up * h, rail);
                Box(root.transform, "Banda larga", center + L * ((b0 + b1) * 0.5f) + sOff + Vector3.up * y, L * (b1 - b0) + S * th + Vector3.up * h, rail);
                // Cabeceras.
                Vector3 lOff = L * side * (halfL + th * 0.5f);
                Box(root.transform, "Banda corta", center + lOff + Vector3.up * y, S * (2f * (halfS - corner)) + L * th + Vector3.up * h, rail);
                // Bordes de seguridad (detrás de las troneras): nada se escapa de la mesa.
                Box(root.transform, "Borde", center + S * side * (halfS + 0.14f) + Vector3.up * (feltY + 0.05f), L * (2f * halfL + 0.3f) + S * 0.02f + Vector3.up * 0.1f, rail);
                Box(root.transform, "Borde", center + L * side * (halfL + 0.14f) + Vector3.up * (feltY + 0.05f), S * (2f * halfS + 0.3f) + L * 0.02f + Vector3.up * 0.1f, rail);
            }
            int pockets = 0;
            foreach (float sl in new[] { -1f, 1f })
                foreach (float ss in new[] { -1f, 1f })
                    Pocket(root.transform, game, center + L * sl * (halfL + 0.025f) + S * ss * (halfS + 0.025f) + Vector3.up * (feltY + r), 0.065f, ref pockets);
            foreach (float ss in new[] { -1f, 1f })
                Pocket(root.transform, game, center + S * ss * (halfS + 0.045f) + Vector3.up * (feltY + r), 0.055f, ref pockets);
            log.AppendLine($"• Paño, 6 bandas, bordes y {pockets} troneras.");

            // Bolas: la blanca es la que está lejos del triángulo.
            Vector3 rackCenter = Average(ballRs.Select(b => b.bounds.center));
            var cueBallR = ballRs.OrderByDescending(b => (b.bounds.center - rackCenter).sqrMagnitude).First();
            var balls = new List<PoolBall>();
            int n = 0;
            foreach (var br in ballRs.OrderBy(b => b == cueBallR ? 0 : 1))
            {
                var mf = br.GetComponent<MeshFilter>();
                var body = new GameObject(br == cueBallR ? "Bola blanca" : $"Bola {++n}");
                Undo.RegisterCreatedObjectUndo(body, "Armar Etapa 4");
                body.transform.SetParent(root.transform, false);
                Vector3 c = br.bounds.center; c.y = feltY + r + 0.0005f;
                body.transform.position = c;
                var rb = body.AddComponent<Rigidbody>();
                rb.mass = 0.17f;
                rb.linearDamping = 0.35f;
                rb.angularDamping = 0.6f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var sc = body.AddComponent<SphereCollider>();
                sc.radius = r;
                sc.contactOffset = 0.002f;
                sc.sharedMaterial = ballMat;
                var pb = body.AddComponent<PoolBall>();
                pb.radius = r;
                pb.isCue = br == cueBallR;
                pb.game = game;
                Visual(body.transform, mf.sharedMesh, br.sharedMaterials, br.transform.rotation, 2f * r / MaxSize(mf.sharedMesh.bounds));
                balls.Add(pb);
                Hide(br.gameObject);
            }
            game.balls = balls.ToArray();
            game.cueBall = balls[0];
            log.AppendLine($"• {balls.Count} bolas físicas (la blanca: '{cueBallR.name}'). Las originales quedaron ocultas.");

            // Taco: el que está acostado sobre la mesa.
            var cueR = cueRs.OrderBy(c => c.bounds.size.y).FirstOrDefault();
            if (cueR != null)
            {
                var mf = cueR.GetComponent<MeshFilter>();
                var cueGo = new GameObject($"{Prefix}Taco");
                Undo.RegisterCreatedObjectUndo(cueGo, "Armar Etapa 4");
                cueGo.transform.SetParent(root.transform, true);
                cueGo.transform.position = cueR.bounds.center;
                Axis(mf.sharedMesh, out Vector3 tipM, out Vector3 backM);
                float worldLen = Vector3.Distance(cueR.transform.TransformPoint(tipM), cueR.transform.TransformPoint(backM));
                float k = worldLen / Mathf.Max(0.001f, Vector3.Distance(tipM, backM));
                Quaternion rot = cueR.transform.rotation;
                Visual(cueGo.transform, mf.sharedMesh, cueR.sharedMaterials, rot, k);
                Vector3 cm = mf.sharedMesh.bounds.center;
                game.cue = cueGo.transform;
                game.cueTipLocal = rot * ((tipM - cm) * k);
                game.cueBackLocal = rot * ((backM - cm) * k);
                Hide(cueR.gameObject);
                log.AppendLine($"• Taco nuevo ({worldLen:0.00} m) en lugar de '{cueR.name}'.");
            }

            // Sonidos: la fuente "Pool" toca el clip que corresponde (ya no suena sola).
            var sound = Object.FindObjectsByType<BarSound>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(s => s.gameObject.name == "Pool");
            if (sound != null) { Undo.RecordObject(sound, "Armar Etapa 4"); sound.automatic = false; }
            game.sound = sound;
            game.breakClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Foley/F1_pool_apertura.wav");
            game.hitClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Foley/F2_pool_choque_bolas.wav");
            game.pocketClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Foley/F3_pool_tiro_y_tronera.wav");
            game.lineMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/Marcador teletransporte.mat");
            log.AppendLine($"• Sonido: '{(sound != null ? sound.gameObject.name : "—")}' (F1 apertura, F2 choque, F3 tronera); ya no suena solo.");

            // "Rearmar mesa", sobre la banda del lado del punto de teletransporte "Pool".
            var point = Object.FindObjectsByType<TeleportPoint>(FindObjectsSortMode.None).FirstOrDefault(p => p.label == "Pool");
            Vector3 toPlayer = point != null ? point.transform.position - center : -S;
            float sideSign = Mathf.Sign(Vector3.Dot(toPlayer, S));
            if (sideSign == 0f) sideSign = -1f;
            var btn = new GameObject($"{Prefix}Rearmar mesa");
            Undo.RegisterCreatedObjectUndo(btn, "Armar Etapa 4");
            btn.transform.SetParent(root.transform, false);
            btn.transform.SetPositionAndRotation(center + S * sideSign * (halfS + 0.12f) - L * halfL * 0.85f + Vector3.up * (feltY + 0.2f),
                Quaternion.LookRotation(-S * sideSign, Vector3.up));
            var text = btn.AddComponent<TextMeshPro>();
            text.text = "[ Rearmar mesa ]";
            text.fontSize = 0.5f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.95f, 0.93f, 0.85f);
            text.rectTransform.sizeDelta = new Vector2(0.5f, 0.08f);
            var bc = btn.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(0.42f, 0.09f, 0.03f);
            var reset = btn.AddComponent<PoolResetButton>();
            reset.game = game;
            reset.dwellTime = 1.2f;

            var manager = Object.FindFirstObjectByType<OrderManager>();
            if (manager != null) { Undo.RecordObject(manager, "Armar Etapa 4"); manager.pool = game; }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            log.AppendLine("Listo. Guardá la escena (Ctrl+S) y dale Play.");
            Debug.Log(log.ToString());
        }

        // =====================================================================

        static void Visual(Transform body, Mesh mesh, Material[] mats, Quaternion worldRot, float k)
        {
            var v = new GameObject("Malla");
            Undo.RegisterCreatedObjectUndo(v, "Armar Etapa 4");
            v.transform.SetParent(body, false);
            v.transform.rotation = worldRot;
            v.transform.localScale = Vector3.one * k;
            v.transform.localPosition = -(v.transform.localRotation * (mesh.bounds.center * k)); // malla centrada en el cuerpo
            v.AddComponent<MeshFilter>().sharedMesh = mesh;
            v.AddComponent<MeshRenderer>().sharedMaterials = mats;
        }

        static void Box(Transform parent, string name, Vector3 worldCenter, Vector3 size, PhysicsMaterial mat)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Armar Etapa 4");
            go.transform.SetParent(parent, false);
            go.transform.position = worldCenter;
            var b = go.AddComponent<BoxCollider>();
            b.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            b.sharedMaterial = mat;
            b.contactOffset = 0.002f;
        }

        static void Pocket(Transform parent, PoolGame game, Vector3 pos, float radius, ref int count)
        {
            var go = new GameObject($"Tronera {++count}");
            Undo.RegisterCreatedObjectUndo(go, "Armar Etapa 4");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = radius;
            go.AddComponent<PoolPocket>().game = game;
        }

        static PhysicsMaterial PhysMat(string name, float dyn, float stat, float bounce, PhysicsMaterialCombine friction, PhysicsMaterialCombine bounceCombine)
        {
            string path = $"{MatFolder}/{name}.physicMaterial";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (m == null) { m = new PhysicsMaterial(name); AssetDatabase.CreateAsset(m, path); }
            m.dynamicFriction = dyn; m.staticFriction = stat; m.bounciness = bounce;
            m.frictionCombine = friction; m.bounceCombine = bounceCombine;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Punta (extremo angosto) y cola de una malla alargada, en coordenadas de la malla.
        /// Promedia los vértices de cada punta, así funciona aunque la malla esté en diagonal.</summary>
        static void Axis(Mesh mesh, out Vector3 tip, out Vector3 back)
        {
            var pts = mesh.vertices;
            Bounds b = mesh.bounds;
            Vector3 size = b.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            float lo = b.min[axis], hi = b.max[axis], len = hi - lo;
            Vector3 sumLo = Vector3.zero, sumHi = Vector3.zero; int nLo = 0, nHi = 0;
            foreach (var p in pts)
            {
                if (p[axis] < lo + len * 0.03f) { sumLo += p; nLo++; }
                else if (p[axis] > hi - len * 0.03f) { sumHi += p; nHi++; }
            }
            Vector3 cLo = nLo > 0 ? sumLo / nLo : b.center, cHi = nHi > 0 ? sumHi / nHi : b.center;
            float spreadLo = 0f, spreadHi = 0f;
            foreach (var p in pts)
            {
                if (p[axis] < lo + len * 0.03f) spreadLo += Flat(p - cLo, axis);
                else if (p[axis] > hi - len * 0.03f) spreadHi += Flat(p - cHi, axis);
            }
            bool tipIsLo = nLo > 0 && nHi > 0 && spreadLo / nLo < spreadHi / nHi;
            tip = tipIsLo ? cLo : cHi;
            back = tipIsLo ? cHi : cLo;
        }

        static float Flat(Vector3 v, int axis) { v[axis] = 0f; return v.magnitude; }

        static void Hide(GameObject go)
        {
            Undo.RecordObject(go, "Armar Etapa 4");
            go.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        }

        static void Cleanup(GameObject table, StringBuilder log)
        {
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(x => x != null && x.name.StartsWith(Prefix)).ToList())
            {
                if (t == null) continue;
                Undo.DestroyObjectImmediate(t.gameObject);
                n++;
            }
            // Volver a mostrar las bolas y el taco originales (para medirlos de nuevo).
            foreach (var r in table.GetComponentsInChildren<MeshRenderer>(true))
            {
                string m = MeshName(r);
                if ((m.StartsWith("billiard_ball") || m.StartsWith("pool_cue")) && !r.gameObject.activeSelf)
                {
                    Undo.RecordObject(r.gameObject, "Armar Etapa 4");
                    r.gameObject.SetActive(true);
                    n++;
                }
            }
            if (n > 0) log.AppendLine($"• Deshice lo armado antes ({n} cosas) para rearmarlo.");
        }

        static string MeshName(Renderer r)
        {
            var mf = r.GetComponent<MeshFilter>();
            string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "";
            return (mesh + " " + r.gameObject.name).ToLowerInvariant().Trim();
        }

        static float MaxSize(Bounds b) => Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));

        static Vector3 Average(IEnumerable<Vector3> pts)
        {
            Vector3 s = Vector3.zero; int n = 0;
            foreach (var p in pts) { s += p; n++; }
            return n > 0 ? s / n : Vector3.zero;
        }
    }
}
