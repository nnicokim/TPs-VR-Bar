using System;
using System.Collections;
using UnityEngine;

namespace BarInteraccion
{
    /// <summary>Curvas de animación y un "tween" mínimo con corrutinas (sin paquetes externos).</summary>
    public static class Ease
    {
        public static float OutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
        public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float InOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }
        public static float OutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
        public static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Llama a step(t) con t de 0 a 1 durante "duration" segundos (siempre termina en 1).</summary>
        public static IEnumerator Run(float duration, Action<float> step)
        {
            if (duration <= 0f) { step(1f); yield break; }
            float t = 0f;
            while (t < duration)
            {
                step(t / duration);
                yield return null;
                t += Time.deltaTime;
            }
            step(1f);
        }
    }
}
