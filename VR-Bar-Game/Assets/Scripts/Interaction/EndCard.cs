using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BarInteraccion
{
    /// <summary>
    /// Tarjeta final: aparece flotando delante del participante (a la altura de los ojos) con el resumen de la noche
    /// y una opción para volver a empezar. Se dibuja por encima de la oscuridad del cierre.
    /// </summary>
    public class EndCard : GazeInteractable
    {
        TextMeshProUGUI restart;
        bool restarting;

        public static EndCard Show(string summary)
        {
            var cam = GazePointer.Instance != null ? GazePointer.Instance.transform : Camera.main.transform;
            Vector3 fwd = cam.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;

            // A 2 m, o más cerca si hay una pared delante (si no, quedaría escondida detrás).
            float dist = 2f;
            if (Physics.Raycast(cam.position, fwd, out RaycastHit wall, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                dist = Mathf.Clamp(wall.distance - 0.2f, 0.7f, 2f);
            var go = new GameObject("Tarjeta final", typeof(RectTransform));
            go.transform.SetPositionAndRotation(cam.position + fwd * dist, Quaternion.LookRotation(fwd, Vector3.up));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 450; // encima de la oscuridad (400), debajo del retículo (500)
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(1000f, 700f);
            rt.localScale = Vector3.one * 0.001f * (dist / 2f); // 1 m x 0,7 m a 2 m (mismo tamaño aparente si está más cerca)

            var mat = GazeReticle.OnTopMaterial();
            var bg = Child<Image>(rt, "Fondo", Vector2.zero, new Vector2(1000f, 700f));
            bg.color = new Color(0.05f, 0.07f, 0.06f, 0.92f);
            bg.material = mat;

            var title = Child<TextMeshProUGUI>(rt, "Titulo", new Vector2(0f, 240f), new Vector2(900f, 120f));
            title.text = "LA ÚLTIMA RONDA";
            title.fontSize = 84; title.fontStyle = FontStyles.Bold; title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(0.95f, 0.93f, 0.85f);

            var body = Child<TextMeshProUGUI>(rt, "Resumen", new Vector2(0f, 10f), new Vector2(860f, 320f));
            body.text = summary;
            body.fontSize = 40; body.alignment = TextAlignmentOptions.Center;
            body.color = new Color(0.85f, 0.85f, 0.8f);

            var card = go.AddComponent<EndCard>();
            card.restart = Child<TextMeshProUGUI>(rt, "Reiniciar", new Vector2(0f, -250f), new Vector2(700f, 90f));
            card.restart.text = "[ Mirá acá para volver a empezar ]";
            card.restart.fontSize = 44; card.restart.alignment = TextAlignmentOptions.Center;
            card.restart.color = new Color(0.95f, 0.93f, 0.85f);
            foreach (var g in go.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            // Encima de todo: el fondo usa el material de UI sin profundidad; los textos, su propio material con ZTest Always.
            foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>())
                t.fontMaterial.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);

            // Solo la línea de "volver a empezar" se puede mirar.
            var hit = new GameObject("Zona reinicio");
            hit.transform.SetParent(go.transform, false);
            hit.transform.localPosition = new Vector3(0f, -250f, 0f);
            var col = hit.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(700f, 110f, 20f);
            card.dwellTime = 1.5f;

            card.StartCoroutine(card.Appear(rt));
            SessionLog.Event("fin", "tarjeta final", summary);
            SessionLog.Event("fin", "selecciones", $"mirada {SessionLog.DwellSelections}, toque {SessionLog.TapSelections}");
            SessionLog.Flush();
            return card;
        }

        IEnumerator Appear(RectTransform rt)
        {
            Vector3 s = rt.localScale;
            yield return Ease.Run(0.6f, t => rt.localScale = s * Mathf.Lerp(0.85f, 1f, Ease.OutBack(t)));
        }

        protected override bool IsAvailable() => !restarting;

        protected override void ApplyHighlight(float amount)
        {
            if (restart != null) restart.color = Color.Lerp(new Color(0.95f, 0.93f, 0.85f), highlightColor, amount);
        }

        public override void OnSelect(GazePointer pointer)
        {
            if (restarting) return;
            restarting = true;
            SessionLog.Event("sesion", "reinicio");
            SessionLog.Flush();
            StartCoroutine(Restart());
        }

        IEnumerator Restart()
        {
            var fader = GazePointer.Instance != null ? GazePointer.Instance.GetComponent<ScreenFader>() : null;
            if (fader != null) yield return fader.FadeTo(1f, 1.2f);
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }

        static T Child<T>(RectTransform parent, string name, Vector2 pos, Vector2 size) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go.AddComponent<T>();
        }
    }
}
