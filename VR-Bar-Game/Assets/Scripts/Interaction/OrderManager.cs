using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BarAudio;
using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// "La última ronda": los pedidos y los actos de la noche.
    ///
    ///  0. Esperando  — el aro "Barra" late: el lugar del participante está detrás de la barra.
    ///  1. Apertura   — al llegar a la barra entra el primer cliente (campanita + puerta) y pide una cerveza;
    ///                  después otro pide un trago. Con pistas: lo pedido late y, si está detrás, suena.
    ///  2. Hora pico  — sube la música, los clientes entran más seguido y hay hasta dos pedidos a la vez,
    ///                  sin pistas: el participante ya sabe hacerlo (el desafío crece con la habilidad).
    ///  3. Cierre     — no entra nadie más, un relámpago marca el cambio, la música baja, los últimos se van
    ///                  y la pizarra muestra cómo fue la noche. (La secuencia de cierre es de la etapa 3.)
    ///
    /// No hay tiempo límite ni castigo: un error se anota en la pizarra y el pedido sigue esperando.
    /// </summary>
    public class OrderManager : MonoBehaviour
    {
        public enum Act { Esperando, Apertura, HoraPico, Cierre }

        public class Order
        {
            public string customer;
            public string drink;
            public bool done;
        }

        [Header("Referencias")]
        public OrderBoard board;
        public PourStation beerStation;
        public ShotStation shotStation;
        public TeleportPoint barPoint;
        public LightningStorm storm;

        [Header("Sonidos")]
        [Tooltip("Puerta de entrada: cada cliente entra con la campanita (y la puerta se abre).")]
        public BarSound entranceSound;
        [Tooltip("Música del bar (sube en la hora pico, baja en el cierre).")]
        public BarSound musicSound;
        [Tooltip("Pista sonora: tintineo que sale de la botella pedida en la apertura.")]
        public BarSound hintSound;

        [Header("Pedidos")]
        public string[] openingOrders = { "Rubia", "Whisky" };
        public string[] rushOrders = { "Negra", "Vodka", "IPA", "Gin", "Roja", "Tequila", "Rubia", "Jäger" };
        public int rushMaxPending = 2;
        [Tooltip("Segundos entre clientes en la hora pico (mín, máx).")]
        public Vector2 rushInterval = new Vector2(6f, 12f);
        public string[] customers = { "Marta", "El Tano", "Ceci", "Rulo", "Flor", "Don Julio", "Nacho", "Sofi", "Lucho", "Pili" };

        [Header("Música por acto (multiplicador de volumen)")]
        public float musicOpening = 0.85f;
        public float musicRush = 1.25f;
        public float musicClosing = 0.4f;

        public Act CurrentAct { get; private set; } = Act.Esperando;
        public int ServedOk { get; private set; }
        public int Mistakes { get; private set; }
        public event System.Action<Act> ActChanged;

        readonly List<Order> orders = new List<Order>();
        readonly List<GazeInteractable> hinted = new List<GazeInteractable>();
        Coroutine hintLoop;
        int customerIndex;
        bool hintsEnabled;

        IEnumerable<Order> Pending => orders.Where(o => !o.done);

        void Start()
        {
            customerIndex = Random.Range(0, customers.Length);
            if (beerStation != null) { beerStation.DrinkServed += OnDrinkServed; beerStation.DrinkStarted += OnDrinkStarted; }
            if (shotStation != null) { shotStation.DrinkServed += OnDrinkServed; shotStation.DrinkStarted += OnDrinkStarted; }
            if (GazeTeleporter.Instance != null) GazeTeleporter.Instance.Teleported += OnTeleported;
            if (musicSound != null) musicSound.SetVolumeScale(musicOpening, 0f);

            if (barPoint != null) barPoint.Attract = true;
            board?.SetTitle("ESTA NOCHE");
            board?.SetLines(new List<(string, string, bool)>(), "Abrimos en un rato.\nTu lugar: detrás de la barra.");
            board?.SetFooter("Llueve. Última noche del mes.");
        }

        void OnDestroy()
        {
            if (beerStation != null) { beerStation.DrinkServed -= OnDrinkServed; beerStation.DrinkStarted -= OnDrinkStarted; }
            if (shotStation != null) { shotStation.DrinkServed -= OnDrinkServed; shotStation.DrinkStarted -= OnDrinkStarted; }
            if (GazeTeleporter.Instance != null) GazeTeleporter.Instance.Teleported -= OnTeleported;
        }

        void OnTeleported(TeleportPoint point)
        {
            if (CurrentAct != Act.Esperando || point != barPoint) return;
            if (barPoint != null) barPoint.Attract = false;
            StartCoroutine(Night());
        }

        // ---------- La noche ----------

        IEnumerator Night()
        {
            // Acto 1: Apertura (con pistas, de a un pedido).
            SetAct(Act.Apertura);
            hintsEnabled = true;
            yield return new WaitForSeconds(1.5f);
            foreach (var drink in openingOrders)
            {
                yield return CustomerArrives(drink);
                yield return new WaitUntil(() => !Pending.Any());
                yield return new WaitForSeconds(1.5f);
            }
            hintsEnabled = false;
            ClearHints();

            // Acto 2: Hora pico (sin pistas, hasta dos a la vez).
            SetAct(Act.HoraPico);
            yield return new WaitForSeconds(2f);
            int next = 0;
            while (next < rushOrders.Length)
            {
                if (Pending.Count() < rushMaxPending)
                {
                    yield return CustomerArrives(rushOrders[next++]);
                    yield return new WaitForSeconds(Random.Range(rushInterval.x, rushInterval.y));
                }
                else yield return null;
            }
            yield return new WaitUntil(() => !Pending.Any());
            yield return new WaitForSeconds(2f);

            // Acto 3: Cierre.
            SetAct(Act.Cierre);
            if (storm != null) storm.Flash(1.3f);
            yield return new WaitForSeconds(2.5f);
            for (int i = 0; i < 2; i++) // los últimos se van
            {
                if (entranceSound != null) entranceSound.PlayOnce();
                yield return new WaitForSeconds(Random.Range(3f, 5f));
            }
        }

        IEnumerator CustomerArrives(string drink)
        {
            if (entranceSound != null) entranceSound.PlayOnce(); // la puerta se abre con su sonido (DoorSwing)
            yield return new WaitForSeconds(2.2f);                // el tiempo de llegar a la barra
            var order = new Order { customer = customers[customerIndex++ % customers.Length], drink = drink };
            orders.Add(order);
            RefreshBoard();
            board?.Swing();
            if (hintsEnabled) ShowHint(drink);
        }

        void OnDrinkStarted(string drink)
        {
            // Si empezó a preparar lo pedido, la pista ya cumplió.
            if (hintsEnabled && Pending.Any(o => o.drink == drink)) ClearHints();
        }

        void OnDrinkServed(string drink)
        {
            if (CurrentAct == Act.Esperando) return;
            var order = Pending.FirstOrDefault(o => o.drink == drink);
            if (order != null)
            {
                order.done = true;
                ServedOk++;
                RefreshBoard();
                board?.ShowNote($"¡Gracias! ({order.customer})", false, 2f);
                StartCoroutine(RemoveLater(order, 2.5f));
                if (hintsEnabled) ClearHints();
            }
            else
            {
                Mistakes++;
                RefreshBoard();
                string pending = string.Join(", ", Pending.Select(o => o.drink));
                board?.ShowNote(pending.Length > 0 ? $"Nadie pidió {drink}. Falta: {pending}" : $"Nadie pidió {drink}", true, 3.5f);
                if (hintsEnabled && Pending.Any()) ShowHint(Pending.First().drink);
            }
        }

        IEnumerator RemoveLater(Order order, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            orders.Remove(order);
            RefreshBoard();
        }

        void SetAct(Act act)
        {
            CurrentAct = act;
            switch (act)
            {
                case Act.Apertura:
                    if (musicSound != null) musicSound.SetVolumeScale(musicOpening, 2f);
                    break;
                case Act.HoraPico:
                    if (musicSound != null) musicSound.SetVolumeScale(musicRush, 4f);
                    board?.ShowNote("¡Se llena el bar!", false, 2.5f);
                    break;
                case Act.Cierre:
                    if (musicSound != null) musicSound.SetVolumeScale(musicClosing, 6f);
                    break;
            }
            RefreshBoard();
            ActChanged?.Invoke(act);
        }

        void RefreshBoard()
        {
            if (board == null) return;
            if (CurrentAct == Act.Cierre)
            {
                board.SetTitle("CERRAMOS");
                int total = openingOrders.Length + rushOrders.Length;
                board.SetLines(new List<(string, string, bool)>(),
                    $"Serviste {ServedOk} de {total} pedidos." + (Mistakes > 0 ? $"\nTe equivocaste {Mistakes} {(Mistakes == 1 ? "vez" : "veces")}." : "\nSin errores.") + "\nLa lluvia sigue.");
                board.SetFooter("Última ronda");
                return;
            }
            board.SetTitle("PEDIDOS");
            board.SetLines(orders.Select(o => (o.customer, o.drink, o.done)).ToList(), "Nadie esperando.");
            string act = CurrentAct == Act.Apertura ? "Apertura" : CurrentAct == Act.HoraPico ? "Hora pico" : "";
            board.SetFooter($"{act} · Servidos: {ServedOk}" + (Mistakes > 0 ? $" · Errores: {Mistakes}" : ""));
        }

        // ---------- Pistas (solo en la apertura) ----------

        void ShowHint(string drink)
        {
            ClearHints();
            foreach (var tap in FindObjectsByType<BeerTap>(FindObjectsSortMode.None))
                if (tap.beerName == drink) hinted.Add(tap);
            foreach (var bottle in FindObjectsByType<BottleSource>(FindObjectsSortMode.None))
                if (bottle.drinkName == drink) hinted.Add(bottle);
            foreach (var h in hinted) h.Attract = true;

            // Si lo pedido es una botella (queda detrás del barman), suena desde ahí: el sonido espacial guía la mirada.
            var bottleHint = hinted.OfType<BottleSource>().FirstOrDefault();
            if (bottleHint != null && hintSound != null) hintLoop = StartCoroutine(HintSound(bottleHint));
        }

        IEnumerator HintSound(BottleSource bottle)
        {
            yield return new WaitForSeconds(1.5f);
            while (true)
            {
                hintSound.PlayOnceAt(bottle.RestPosition);
                yield return new WaitForSeconds(4f);
            }
        }

        void ClearHints()
        {
            foreach (var h in hinted) if (h != null) h.Attract = false;
            hinted.Clear();
            if (hintLoop != null) { StopCoroutine(hintLoop); hintLoop = null; }
        }
    }
}
