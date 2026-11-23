using System;
using System.Collections;
using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Algo que hay que hacer para cerrar el bar (apagar un neón, cerrar unas cortinas, bajar una llave de luz,
    /// apagar el parlante). Solo se puede usar cuando la secuencia de cierre lo "arma": durante la noche no
    /// aparece como utilizable. Mientras está armado y pendiente, late suavemente (attract).
    /// </summary>
    public abstract class ClosingTask : GazeInteractable
    {
        [Tooltip("Grupo de la lista de la pizarra (ej. \"Apagar los neones\").")]
        public string group = "";

        public bool Armed { get; private set; }
        public bool Done { get; private set; }
        protected bool Busy { get; set; }

        public event Action<ClosingTask> Completed;

        protected override bool IsAvailable() => Armed && !Done && !Busy;

        public void Arm(bool armed)
        {
            Armed = armed;
            Attract = armed && !Done;
        }

        public override void OnSelect(GazePointer pointer)
        {
            if (!IsAvailable()) return;
            Busy = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return Perform();
            Busy = false;
            Done = true;
            Attract = false;
            SessionLog.Event("cierre", group, name);
            Completed?.Invoke(this);
        }

        /// <summary>La animación de la tarea (apagar, cerrar…).</summary>
        protected abstract IEnumerator Perform();
    }
}
