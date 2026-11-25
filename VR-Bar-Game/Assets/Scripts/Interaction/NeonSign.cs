using System.Collections;
using BarAudio;
using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Cartel de neón que se apaga al mirarlo: parpadea unas veces (el zumbido tartamudea con él)
    /// y se queda apagado. El zumbido (A5) se apaga con la luz.
    /// </summary>
    public class NeonSign : ClosingTask
    {
        public Renderer[] tubes;
        public BarSound buzz;

        static readonly float[] Pattern = { 0.08f, 0.06f, 0.12f, 0.05f, 0.04f, 0.18f, 0.07f, 0.3f };

        protected override IEnumerator Perform()
        {
            bool lit = true;
            foreach (float d in Pattern)
            {
                lit = !lit;
                Emission.Set(tubes, lit);
                if (buzz != null) buzz.SetVolumeScale(lit ? 1f : 0.15f, 0f);
                yield return new WaitForSeconds(d);
            }
            Emission.Set(tubes, false);
            if (buzz != null) buzz.Silence(0.3f);
        }
    }
}
