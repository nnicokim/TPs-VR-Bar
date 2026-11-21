using System;

namespace BarInteraccion
{
    /// <summary>
    /// Una estación donde se prepara una bebida y se entrega (canillas de cerveza, botellas del estante).
    /// El sistema de pedidos escucha DrinkServed y no necesita saber cómo se preparó.
    /// </summary>
    public interface IDrinkStation
    {
        /// <summary>Hay una bebida lista esperando ser entregada.</summary>
        bool IsReadyToServe { get; }

        /// <summary>Se está preparando algo (no se puede empezar otra).</summary>
        bool IsBusy { get; }

        /// <summary>Nombre de la bebida que está en el vaso (o se está preparando).</summary>
        string CurrentDrink { get; }

        void Serve();

        /// <summary>Empezó a prepararse una bebida (nombre).</summary>
        event Action<string> DrinkStarted;

        /// <summary>Se entregó una bebida (nombre).</summary>
        event Action<string> DrinkServed;
    }
}
