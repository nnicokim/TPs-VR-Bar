using System.Collections;
using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Par de cortinas de una ventana. Abiertas están recogidas a los costados; al cerrarlas cada una se
    /// desliza y se extiende hasta el centro (la tela "se despliega"). Va en el objeto "Cortinas" de la ventana.
    /// </summary>
    public class CurtainPair : ClosingTask
    {
        public Renderer left;
        public Renderer right;
        public float duration = 2.4f;
        [Tooltip("Cuánto se superponen en el centro (m).")]
        public float overlap = 0.04f;

        protected override IEnumerator Perform()
        {
            if (left == null || right == null) yield break;
            Bounds lb = left.bounds, rb = right.bounds;
            bool alongX = (lb.center - rb.center).x * (lb.center - rb.center).x >= (lb.center - rb.center).z * (lb.center - rb.center).z;

            // La cortina "izquierda" es la que está del lado menor del eje.
            if ((alongX ? lb.center.x : lb.center.z) > (alongX ? rb.center.x : rb.center.z)) { var t = left; left = right; right = t; var tb = lb; lb = rb; rb = tb; }
            float center = (Hi(lb, alongX) + Lo(rb, alongX)) * 0.5f;

            var a = new CurtainState(left.transform, Lo(lb, alongX), Hi(lb, alongX), Lo(lb, alongX), center + overlap, alongX);
            var b2 = new CurtainState(right.transform, Lo(rb, alongX), Hi(rb, alongX), center - overlap, Hi(rb, alongX), alongX);
            yield return Ease.Run(duration, t =>
            {
                float e = Ease.InOutCubic(t);
                a.Apply(left, e);
                b2.Apply(right, e);
            });
        }

        static float Lo(Bounds b, bool alongX) => alongX ? b.min.x : b.min.z;
        static float Hi(Bounds b, bool alongX) => alongX ? b.max.x : b.max.z;

        class CurtainState
        {
            readonly Vector3 scale0;
            readonly float lo0, hi0, lo1, hi1;
            readonly bool alongX;

            public CurtainState(Transform t, float lo0, float hi0, float lo1, float hi1, bool alongX)
            {
                scale0 = t.localScale; this.lo0 = lo0; this.hi0 = hi0; this.lo1 = lo1; this.hi1 = hi1; this.alongX = alongX;
            }

            public void Apply(Renderer r, float e)
            {
                float lo = Mathf.Lerp(lo0, lo1, e), hi = Mathf.Lerp(hi0, hi1, e);
                float k = (hi - lo) / Mathf.Max(0.001f, hi0 - lo0);
                Transform t = r.transform;
                // Estira en el eje horizontal local que coincide con el eje de la ventana.
                Vector3 axisLocal = t.InverseTransformDirection(alongX ? Vector3.right : Vector3.forward);
                Vector3 s = scale0;
                if (Mathf.Abs(axisLocal.x) >= Mathf.Abs(axisLocal.y) && Mathf.Abs(axisLocal.x) >= Mathf.Abs(axisLocal.z)) s.x *= k;
                else if (Mathf.Abs(axisLocal.z) >= Mathf.Abs(axisLocal.y)) s.z *= k;
                else s.y *= k;
                t.localScale = s;
                Bounds now = r.bounds;
                float c = alongX ? now.center.x : now.center.z;
                float want = (lo + hi) * 0.5f;
                t.position += (alongX ? Vector3.right : Vector3.forward) * (want - c);
            }
        }
    }
}
