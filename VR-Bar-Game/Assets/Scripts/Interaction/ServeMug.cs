using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// El vaso (o la jarra) de una estación de bebidas. Cuando está lleno "llama" (pulso suave del vidrio) y,
    /// al elegirlo, se sirve: se desliza por la barra hacia los clientes.
    /// El resaltado no es un contorno (se vería a través del vidrio) sino un tinte cálido del vidrio.
    /// </summary>
    public class ServeMug : GazeInteractable
    {
        public IDrinkStation station;
        public Renderer[] glassRenderers;
        public Color glassColor = new Color(0.85f, 0.93f, 1f, 0.22f);
        public Color glowColor = new Color(1f, 0.8f, 0.4f, 0.55f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;

        protected override bool IsAvailable() => station != null && station.IsReadyToServe;

        public override void OnSelect(GazePointer pointer)
        {
            if (station != null) station.Serve();
        }

        protected override void ApplyHighlight(float amount)
        {
            if (glassRenderers == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            Color c = Color.Lerp(glassColor, glowColor, amount);
            foreach (var r in glassRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block);
            }
        }
    }
}
