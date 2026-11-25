using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BarInteraccion
{
    /// <summary>
    /// Retículo de mirada para Cardboard. Se arma solo (no necesita assets):
    ///  - Punto chico cuando no hay nada para usar.
    ///  - Anillo que aparece y crece sobre algo utilizable (ofrecimiento: "esto responde").
    ///  - Relleno radial que avanza con el dwell (feedback del tiempo que falta).
    /// Se coloca a la distancia de lo que se mira (sin conflicto de convergencia) y mantiene
    /// siempre el mismo tamaño angular. Se dibuja por encima de todo.
    /// </summary>
    public class GazeReticle : MonoBehaviour
    {
        [Tooltip("Distancia del retículo cuando el rayo no pega en nada.")]
        public float idleDistance = 3f;

        [Tooltip("Diámetro angular del anillo (grados).")]
        public float ringAngle = 2.4f;

        [Tooltip("Diámetro del punto en reposo, relativo al anillo.")]
        [Range(0.05f, 1f)] public float dotRatio = 0.22f;

        public Color idleColor = new Color(1f, 1f, 1f, 0.85f);
        public Color activeColor = new Color(1f, 0.78f, 0.35f, 1f);

        Transform canvasTransform;
        RectTransform dot, ring;
        Image dotImage, ringImage, fillImage;
        float hover, pulse, distance;

        void Awake()
        {
            Build();
            distance = idleDistance;
        }

        public void SetState(float hitDistance, bool overInteractable, float dwellProgress)
        {
            if (canvasTransform == null) return;
            distance = Mathf.Lerp(distance, hitDistance, 1f - Mathf.Exp(-Time.deltaTime * 25f));
            hover = Mathf.MoveTowards(hover, overInteractable ? 1f : 0f, Time.deltaTime * 8f);
            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 4f);

            float d = Mathf.Max(0.05f, distance * 0.97f);
            float worldSize = 2f * d * Mathf.Tan(ringAngle * 0.5f * Mathf.Deg2Rad);
            canvasTransform.localPosition = new Vector3(0f, 0f, d);
            canvasTransform.localRotation = Quaternion.identity;
            canvasTransform.localScale = Vector3.one * (worldSize / 100f);

            float dotSize = 100f * dotRatio * Mathf.Lerp(1f, 0.7f, hover) * (1f + pulse * 0.8f);
            dot.sizeDelta = new Vector2(dotSize, dotSize);
            dotImage.color = Color.Lerp(idleColor, activeColor, hover);

            float ringSize = 100f * Mathf.Lerp(0.45f, 1f, Ease.OutCubic(hover)) * (1f + pulse * 0.3f);
            ring.sizeDelta = new Vector2(ringSize, ringSize);
            Color rc = activeColor; rc.a = 0.45f * hover;
            ringImage.color = rc;
            Color fc = activeColor; fc.a = hover;
            fillImage.color = fc;
            fillImage.fillAmount = dwellProgress;
        }

        /// <summary>Pequeño "golpe" visual al elegir algo.</summary>
        public void Pulse() { pulse = 1f; }

        void Build()
        {
            var go = new GameObject("Reticulo", typeof(RectTransform));
            go.layer = gameObject.layer;
            canvasTransform = go.transform;
            canvasTransform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 500;
            ((RectTransform)canvasTransform).sizeDelta = new Vector2(100f, 100f);

            var mat = OnTopMaterial();
            Sprite ringSprite = CircleSprite(0.8f);
            ringImage = MakeImage("Anillo", canvasTransform, ringSprite, mat, out ring);
            fillImage = MakeImage("Progreso", ring, ringSprite, mat, out RectTransform fillRect);
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.sizeDelta = Vector2.zero;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = true;
            fillImage.fillAmount = 0f;
            dotImage = MakeImage("Punto", canvasTransform, CircleSprite(0f), mat, out dot);
        }

        static Image MakeImage(string name, Transform parent, Sprite sprite, Material mat, out RectTransform rt)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(100f, 100f);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.material = mat;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Material de UI que ignora la profundidad: el retículo nunca queda tapado.</summary>
        internal static Material OnTopMaterial()
        {
            var mat = new Material(Canvas.GetDefaultCanvasMaterial());
            mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            return mat;
        }

        /// <summary>Círculo (inner = 0) o anillo (inner = radio interno relativo), con borde suave.</summary>
        internal static Sprite CircleSprite(float inner)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float r = size * 0.5f;
            float edge = 1.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float outer = Mathf.Clamp01((r - 1f - d) / edge);
                    float innerA = inner > 0f ? Mathf.Clamp01((d - inner * r) / edge) : 1f;
                    byte a = (byte)(255f * outer * innerA);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
