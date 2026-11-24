using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BarInteraccion
{
    internal sealed class PoolScoreboard
    {
        static readonly Color Background = new Color(0.055f, 0.045f, 0.037f);
        static readonly Color Ink = new Color(1f, 0.95f, 0.85f);
        static readonly Color Gold = new Color(1f, 0.76f, 0.38f);

        readonly RectTransform root;
        readonly TextMeshProUGUI solids, stripes, eight, powerLabel;
        readonly RectTransform powerFill;
        readonly RectTransform powerRow;
        int previousPower = -1;

        public PoolScoreboard(Transform parent, Vector3 position)
        {
            root = Rect("Marcador de pool", parent, Vector2.zero, new Vector2(640f, 174f));
            root.position = position;
            root.localScale = Vector3.one * 0.001f;
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var scoreRow = Panel("Embocadas", root, new Vector2(0f, 27f), new Vector2(640f, 120f), Background).rectTransform;
            solids = Text("Lisas", scoreRow, new Vector2(-206f, 0f), new Vector2(198f, 112f), 27f);
            stripes = Text("Rayadas", scoreRow, Vector2.zero, new Vector2(198f, 112f), 27f);
            eight = Text("Bola 8", scoreRow, new Vector2(206f, 0f), new Vector2(198f, 112f), 27f);
            Panel("Separador", scoreRow, new Vector2(-104f, 0f), new Vector2(2f, 80f), Gold * 0.55f);
            Panel("Separador", scoreRow, new Vector2(104f, 0f), new Vector2(2f, 80f), Gold * 0.55f);

            powerRow = Panel("Fuerza", root, new Vector2(0f, -54f), new Vector2(640f, 42f), Background).rectTransform;
            powerLabel = Text("Porcentaje", powerRow, new Vector2(-210f, 0f), new Vector2(200f, 40f), 25f);
            var track = Panel("Pista", powerRow, new Vector2(102f, 0f), new Vector2(390f, 12f), new Color(0.29f, 0.25f, 0.20f));
            powerFill = Panel("Progreso", track.rectTransform, Vector2.zero, Vector2.zero, Gold).rectTransform;
            powerFill.anchorMin = Vector2.zero;
            powerRow.gameObject.SetActive(false);
        }

        public void SetScore(int solidCount, int stripedCount, int eightCount)
        {
            solids.text = $"LISAS\n<size=145%><b>{solidCount}/7</b></size>";
            stripes.text = $"RAYADAS\n<size=145%><b>{stripedCount}/7</b></size>";
            eight.text = $"BOLA 8\n<size=145%><b>{eightCount}/1</b></size>";
        }

        public void SetPower(int percent)
        {
            powerRow.gameObject.SetActive(percent >= 0);
            if (percent < 0 || percent == previousPower) return;
            previousPower = percent;
            powerLabel.SetText("FUERZA {0:0}%", percent);
            powerFill.anchorMax = new Vector2(percent * 0.01f, 1f);
        }

        public void Face(Vector3 viewer, bool visible)
        {
            root.gameObject.SetActive(visible);
            Vector3 direction = root.position - viewer;
            direction.y = 0f;
            if (visible && direction.sqrMagnitude > 0.0001f)
                root.rotation = Quaternion.LookRotation(direction);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TextMeshProUGUI Text(string name, Transform parent, Vector2 position, Vector2 size, float fontSize)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Ink;
            text.raycastTarget = false;
            return text;
        }
    }
}
