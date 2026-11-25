namespace BarInteraccion
{
    /// <summary>
    /// Algo que se puede usar con la mirada (Cardboard: la cabeza apunta, el toque o el "dwell" confirman).
    /// El GazePointer de la cámara busca este componente en el objeto que mira (o en sus padres).
    /// </summary>
    public interface IGazeable
    {
        /// <summary>Si es falso, el puntero lo ignora: no se resalta ni se puede elegir (restricción visible).</summary>
        bool CanInteract { get; }

        /// <summary>Segundos de mirada fija para elegirlo. 0 o menos: usa el valor del GazePointer.</summary>
        float DwellTime { get; }

        void OnGazeEnter(GazePointer pointer);
        void OnGazeExit(GazePointer pointer);
        void OnSelect(GazePointer pointer);
    }
}
