using UnityEngine;

namespace BarInteraccion
{
    /// <summary>"Rearmar mesa": texto de tiza sobre la banda que se elige con la mirada.</summary>
    public class PoolResetButton : GazeInteractable
    {
        public PoolGame game;
        TMPro.TextMeshPro text;
        Color baseColor = new Color(0.95f, 0.93f, 0.85f);

        void Awake()
        {
            text = GetComponentInChildren<TMPro.TextMeshPro>();
            if (dwellTime <= 0f) dwellTime = 1.2f;
        }

        protected override bool IsAvailable() => game != null && game.State != PoolGame.Phase.Rolling;
        public override void OnSelect(GazePointer pointer) { if (game != null) game.ResetTable(); }

        protected override void ApplyHighlight(float amount)
        {
            if (text != null) text.color = Color.Lerp(baseColor, highlightColor, amount);
        }
    }
}
