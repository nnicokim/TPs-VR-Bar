using System.Collections;
using BarAudio;
using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Tormenta afuera: cada tanto un relámpago ilumina el salón por un instante (una luz en tiempo real
    /// que solo se enciende durante el destello) y, un poco después, llega el trueno. La demora entre luz
    /// y sonido sugiere la distancia. Si el sonido del trueno no tiene clips, el relámpago es mudo.
    /// </summary>
    public class LightningStorm : MonoBehaviour
    {
        public Light flashLight;
        public BarSound thunder;
        [Tooltip("Segundos entre relámpagos (mín, máx).")]
        public Vector2 interval = new Vector2(25f, 55f);
        [Tooltip("Demora del trueno (mín, máx).")]
        public Vector2 thunderDelay = new Vector2(0.8f, 3f);
        public float intensity = 1.6f;
        public bool automatic = true;

        /// <summary>Brillo actual del destello (0 a 1). La oscuridad del cierre se abre con el relámpago.</summary>
        public float FlashLevel => flashLight != null && intensity > 0f ? Mathf.Clamp01(flashLight.intensity / intensity) : 0f;

        void Start()
        {
            if (flashLight != null) { flashLight.intensity = 0f; flashLight.enabled = false; }
            if (automatic) StartCoroutine(Loop());
        }

        IEnumerator Loop()
        {
            yield return new WaitForSeconds(Random.Range(interval.x * 0.4f, interval.x));
            while (true)
            {
                Flash();
                yield return new WaitForSeconds(Random.Range(interval.x, interval.y));
            }
        }

        /// <summary>Un relámpago ahora (también lo usan los actos como golpe dramático).</summary>
        public void Flash(float strength = 1f) => StartCoroutine(FlashRoutine(strength));

        IEnumerator FlashRoutine(float strength)
        {
            if (flashLight != null)
            {
                flashLight.enabled = true;
                int pulses = Random.Range(2, 4);
                for (int i = 0; i < pulses; i++)
                {
                    float peak = intensity * strength * Random.Range(0.6f, 1f);
                    yield return Ease.Run(0.04f, t => flashLight.intensity = peak * t);
                    yield return Ease.Run(Random.Range(0.06f, 0.16f), t => flashLight.intensity = peak * (1f - t));
                    yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));
                }
                flashLight.intensity = 0f;
                flashLight.enabled = false;
            }
            yield return new WaitForSeconds(Random.Range(thunderDelay.x, thunderDelay.y));
            if (thunder != null && thunder.clips != null && thunder.clips.Length > 0) thunder.PlayOnce();
        }
    }
}
