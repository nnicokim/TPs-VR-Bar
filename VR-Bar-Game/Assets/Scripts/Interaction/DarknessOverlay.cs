using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BarInteraccion
{
    /// <summary>
    /// Oscuridad del cierre. Las luces del bar están horneadas (Baked): apagar una lámpara no cambia el lightmap.
    /// Por eso, al apagar cada zona, además de apagar sus bombillas se oscurece la vista un escalón.
    /// Un velo negro semitransparente pegado a la cámara, debajo del retículo (que siempre se ve).
    /// Cuando hay un relámpago el velo se abre: el destello ilumina el bar a oscuras.
    /// </summary>
    public class DarknessOverlay : MonoBehaviour
    {
        public LightningStorm storm;
        [Range(0f, 1f)] public float flashOpens = 0.85f;

        Canvas canvas;
        Image image;
        float level;
        Coroutine fade;

        public float Level => level;

        void Awake()
        {
            var go = new GameObject("Oscuridad", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 400; // debajo del retículo (500) y del fundido (1000)
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(100f, 100f);
            var cam = GetComponent<Camera>();
            rt.localPosition = new Vector3(0f, 0f, (cam != null ? cam.nearClipPlane : 0.01f) + 0.03f);
            rt.localScale = Vector3.one * 0.02f;

            var imgGo = new GameObject("Velo", typeof(RectTransform));
            imgGo.layer = gameObject.layer;
            var irt = (RectTransform)imgGo.transform;
            irt.SetParent(rt, false);
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.sizeDelta = Vector2.zero;
            image = imgGo.AddComponent<Image>();
            image.material = GazeReticle.OnTopMaterial();
            image.raycastTarget = false;
            Apply();
        }

        public void FadeTo(float target, float seconds)
        {
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeRoutine(Mathf.Clamp01(target), seconds));
        }

        IEnumerator FadeRoutine(float target, float seconds)
        {
            float from = level;
            yield return Ease.Run(seconds, t => level = Mathf.Lerp(from, target, Ease.InOutCubic(t)));
            fade = null;
        }

        void LateUpdate() => Apply();

        void Apply()
        {
            if (image == null) return;
            float flash = storm != null ? storm.FlashLevel : 0f;
            float a = level * (1f - flashOpens * flash);
            image.color = new Color(0f, 0f, 0f, a);
            canvas.enabled = a > 0.001f;
        }
    }
}
