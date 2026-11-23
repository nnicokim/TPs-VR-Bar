using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Prender y apagar la emisión de renderers sin tocar sus materiales (MaterialPropertyBlock).
    /// Cubre los dos sombreadores del proyecto: URP Lit (_EmissionColor) y glTFast (emissiveFactor).
    /// </summary>
    public static class Emission
    {
        static readonly int UrpId = Shader.PropertyToID("_EmissionColor");
        static readonly int GltfId = Shader.PropertyToID("emissiveFactor");
        static MaterialPropertyBlock off, on;

        public static void Set(Renderer[] renderers, bool lit)
        {
            if (renderers == null) return;
            if (off == null)
            {
                off = new MaterialPropertyBlock();
                off.SetColor(UrpId, Color.black);
                off.SetColor(GltfId, Color.black);
                on = new MaterialPropertyBlock(); // vacío: vuelven los valores del material
            }
            foreach (var r in renderers)
                if (r != null) r.SetPropertyBlock(lit ? on : off);
        }
    }
}
