using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Acto 3, segunda parte: cerrar el bar. La pizarra pasa a ser una lista de tareas y el participante
    /// las hace en el orden que quiera: apagar la música, apagar los neones, cerrar las cortinas y bajar las
    /// llaves del tablero de luces (junto a la puerta de la cocina). Cada llave oscurece el bar un escalón.
    /// Al terminar: un último relámpago ilumina el bar vacío y aparece la tarjeta final para volver a empezar.
    /// </summary>
    public class ClosingSequence : MonoBehaviour
    {
        public OrderBoard board;
        public LightningStorm storm;
        public ClosingTask[] tasks;

        [Header("Oscuridad por llave bajada (0 = nada, 1 = negro)")]
        public float[] darknessSteps = { 0.3f, 0.5f, 0.72f };
        public float finalDarkness = 0.82f;

        public bool Running { get; private set; }
        public bool Finished { get; private set; }

        /// <summary>Resumen de la noche (lo arma el OrderManager) para la tarjeta final.</summary>
        public string Summary { get; set; } = "";

        DarknessOverlay darkness;
        int switchesOff;

        public void Begin()
        {
            if (Running || Finished) return;
            Running = true;
            var cam = GazePointer.Instance != null ? GazePointer.Instance.gameObject : (Camera.main != null ? Camera.main.gameObject : null);
            if (cam != null)
            {
                darkness = cam.GetComponent<DarknessOverlay>();
                if (darkness == null) darkness = cam.AddComponent<DarknessOverlay>();
                darkness.storm = storm;
            }
            foreach (var t in tasks.Where(t => t != null))
            {
                t.Completed += OnTaskCompleted;
                t.Arm(true);
            }
            board?.SetTitle("CERRÁ EL BAR");
            board?.SetFooter("Cierre");
            RefreshBoard();
            board?.Swing(0.6f);
        }

        void OnTaskCompleted(ClosingTask task)
        {
            if (task is LightSwitch)
            {
                float step = darknessSteps.Length > 0 ? darknessSteps[Mathf.Min(switchesOff, darknessSteps.Length - 1)] : 0.5f;
                switchesOff++;
                if (darkness != null) darkness.FadeTo(step, 0.25f);
            }
            RefreshBoard();
            if (tasks.Where(t => t != null).All(t => t.Done)) StartCoroutine(Ending());
        }

        void RefreshBoard()
        {
            if (board == null) return;
            var groups = new List<(string, bool)>();
            foreach (var g in tasks.Where(t => t != null).GroupBy(t => t.group))
            {
                int done = g.Count(t => t.Done), total = g.Count();
                string text = total > 1 && done > 0 && done < total ? $"{g.Key} ({done}/{total})" : g.Key;
                groups.Add((text, done == total));
            }
            board.SetChecklist(groups);
        }

        IEnumerator Ending()
        {
            Running = false;
            Finished = true;
            board?.SetTitle("BUENAS NOCHES");
            board?.SetBody("Cerrado.\nSolo queda la lluvia.");
            board?.SetFooter("");
            yield return new WaitForSeconds(2.5f);
            if (darkness != null) darkness.FadeTo(finalDarkness, 3f);
            yield return new WaitForSeconds(3.5f);
            if (storm != null) storm.Flash(1.6f); // el último relámpago ilumina el bar vacío
            yield return new WaitForSeconds(4f);
            EndCard.Show(Summary);
        }
    }
}
