using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BarInteraccion
{
    /// <summary>
    /// Fundido a negro pegado a la cámara. Se usa en el teletransporte: cortar a negro un instante
    /// evita el mareo que produce ver el mundo deslizarse (vección) y deja claro el salto.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public Color color = Color.black;

        Canvas canvas;
        Image image;
        float alpha;

        public float Alpha => alpha;

        void Awake()
        {
            var go = new GameObject("Fundido", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 1000;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(100f, 100f);
            var cam = GetComponent<Camera>();
            float near = cam != null ? cam.nearClipPlane : 0.01f;
            rt.localPosition = new Vector3(0f, 0f, near + 0.02f);
            rt.localScale = Vector3.one * 0.02f; // 2 m de lado a pocos cm: tapa todo el campo visual

            var imgGo = new GameObject("Negro", typeof(RectTransform));
            imgGo.layer = gameObject.layer;
            var irt = (RectTransform)imgGo.transform;
            irt.SetParent(rt, false);
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.sizeDelta = Vector2.zero;
            image = imgGo.AddComponent<Image>();
            image.material = GazeReticle.OnTopMaterial();
            image.raycastTarget = false;
            SetAlpha(0f);
        }

        public void SetAlpha(float a)
        {
            alpha = Mathf.Clamp01(a);
            if (image == null) return;
            Color c = color; c.a = alpha;
            image.color = c;
            canvas.enabled = alpha > 0.001f;
        }

        public IEnumerator FadeTo(float target, float duration)
        {
            float from = alpha;
            yield return Ease.Run(duration, t => SetAlpha(Mathf.Lerp(from, target, t)));
        }
    }
}
