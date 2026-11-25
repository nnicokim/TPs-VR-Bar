using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Material para lo que se crea por código. GameObject.CreatePrimitive en el Editor usa el material por defecto de
    /// URP, pero en una build (por ejemplo iPhone) usa el "Default-Material" del render integrado, que URP no sabe
    /// dibujar: sale violeta. Este material (URP Lit, blanco) está en Resources, así que siempre entra en la build;
    /// cada script le pone su color con un MaterialPropertyBlock o le asigna su propio material.
    /// </summary>
    public static class RuntimeMaterials
    {
        static Material lit;

        public static Material Lit
        {
            get
            {
                if (lit == null) lit = Resources.Load<Material>("Interaccion/Primitivas");
                if (lit == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader != null) lit = new Material(shader) { name = "Primitivas (código)" };
                }
                return lit;
            }
        }

        /// <summary>Lo mismo que GameObject.CreatePrimitive, pero con un material que URP dibuja en cualquier build.</summary>
        public static GameObject CreatePrimitive(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var r = go.GetComponent<Renderer>();
            if (r != null && Lit != null) r.sharedMaterial = Lit;
            return go;
        }

        /// <summary>Copia del material base (para los que necesitan uno propio con otro color).</summary>
        public static Material NewLit(Color color)
        {
            if (Lit == null) return null;
            var m = new Material(Lit);
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            return m;
        }
    }
}
