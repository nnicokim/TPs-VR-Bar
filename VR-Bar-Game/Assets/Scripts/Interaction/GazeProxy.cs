using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Reenvía la mirada a otro objeto: mirar esto es como mirar "target".
    /// Lo usan los cartelitos del estante: mirar el cartel "GIN" resalta y elige la botella de gin.
    /// </summary>
    public class GazeProxy : MonoBehaviour, IGazeable
    {
        public GazeInteractable target;

        public bool CanInteract => target != null && target.CanInteract;
        public float DwellTime => target != null ? ((IGazeable)target).DwellTime : 0f;

        public void OnGazeEnter(GazePointer pointer) { if (target != null) target.OnGazeEnter(pointer); }
        public void OnGazeExit(GazePointer pointer) { if (target != null) target.OnGazeExit(pointer); }
        public void OnSelect(GazePointer pointer) { if (target != null) target.OnSelect(pointer); }
    }
}
