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
    ///  3. Cierre     — no entra nadie más, un relámpago marca el cambio, la música baja y los últimos se van.
    ///                  Queda el habitué, que pide "una más": el participante decide si se la sirve o cierra.
    ///                  Después, la ClosingSequence: apagar música, neones, cortinas y luces, y la tarjeta final.
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
            public float arrivedAt;
        }

        [Header("Referencias")]
        public OrderBoard board;
        public PourStation beerStation;
        public ShotStation shotStation;
        public TeleportPoint barPoint;
        public LightningStorm storm;
        public ClosingSequence closing;
        public DartGame darts;
        public PoolGame pool;

        [Header("El habitué")]
        public string regularName = "Don Julio";
        public string regularDrink = "Whisky";

        [Header("Sonidos")]
        [Tooltip("Puerta de entrada: cada cliente entra con la campanita (y la puerta se abre).")]
        public BarSound entranceSound;
        [Tooltip("Música del bar (sube en la hora pico, baja en el cierre).")]
        public BarSound musicSound;
        [Tooltip("Pista sonora: tintineo de botellas (la fuente existente 'Botellas (heladera)'). En la apertura suena desde la botella pedida y después vuelve a su lugar.")]
        public BarSound hintSound;

        [Header("Pedidos")]
        public string[] openingOrders = { "Rubia", "Whisky" };
        public string[] rushOrders = { "Negra", "Vodka", "IPA", "Ron", "Gin", "Roja", "Tequila", "Rubia", "Jäger" };
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
        /// <summary>null: todavía no decidió; true: le sirvió la última; false: cerró.</summary>
        public bool? ServedRegular { get; private set; }
        public event System.Action<Act> ActChanged;

        readonly List<Order> orders = new List<Order>();
        readonly List<GazeInteractable> hinted = new List<GazeInteractable>();
        Coroutine hintLoop;
        Vector3 hintHome;
        bool hintMoved;
        int customerIndex;
        bool hintsEnabled;
        bool regularPending;

        IEnumerable<Order> Pending => orders.Where(o => !o.done);

        void Start()
        {
            customerIndex = Random.Range(0, customers.Length);
            SessionLog.Event("sesion", "escena cargada");
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

            // El habitué: "¿una más?" (una decisión con consecuencia).
            yield return Regular();

            // Cerrar el bar.
            if (closing != null)
            {
                closing.Summary = BuildSummary();
                closing.Begin();
                while (!closing.Finished) { closing.Summary = BuildSummary(); yield return null; }
            }
        }

        IEnumerator Regular()
        {
            int choice = -1;
            board?.SetTitle(regularName.ToUpperInvariant());
            board?.SetBody($"Queda uno solo en la barra.\n—¿Me servís la última, pibe?");
            board?.SetFooter("Cierre");
            board?.Swing(0.8f);
            board?.ShowChoice("Servirle", "Cerramos", c => choice = c);
            yield return new WaitUntil(() => choice >= 0);
            SessionLog.Event("habitue", choice == 0 ? "servirle" : "cerramos");

            if (choice == 0)
            {
                ServedRegular = true;
                var order = new Order { customer = regularName, drink = regularDrink, arrivedAt = Time.time };
                orders.Add(order);
                regularPending = true;
                RefreshBoard();
                yield return new WaitUntil(() => order.done);
                regularPending = false;
                board?.SetTitle(regularName.ToUpperInvariant());
                board?.SetBody("—Gracias, pibe. Hasta mañana.");
                yield return new WaitForSeconds(3f);
            }
            else
            {
                ServedRegular = false;
                board?.SetBody($"{regularName} se pone el piloto\ny sale a la lluvia.");
                yield return new WaitForSeconds(2.5f);
            }
            if (entranceSound != null) entranceSound.PlayOnce(); // se va
            yield return new WaitForSeconds(3f);
        }

        string BuildSummary()
        {
            int total = openingOrders.Length + rushOrders.Length + (ServedRegular == true ? 1 : 0);
            string s = $"Serviste {ServedOk} de {total} pedidos" + (Mistakes > 0 ? $" ({Mistakes} {(Mistakes == 1 ? "error" : "errores")})." : ", sin errores.");
            if (ServedRegular == true) s += $"\nLe serviste la última a {regularName}.";
            else if (ServedRegular == false) s += $"\n{regularName} se fue sin su última.";
            if (darts != null && darts.BestRound > 0) s += $"\nMejor ronda de dardos: {darts.BestRound}.";
            if (pool != null && pool.Shots > 0) s += $"\nPool: {pool.Pocketed} {(pool.Pocketed == 1 ? "bola embocada" : "bolas embocadas")} en {pool.Shots} {(pool.Shots == 1 ? "tiro" : "tiros")}.";
            return s + "\nLa lluvia sigue.";
        }

        IEnumerator CustomerArrives(string drink)
        {
            if (entranceSound != null) entranceSound.PlayOnce(); // la puerta se abre con su sonido (DoorSwing)
            yield return new WaitForSeconds(2.2f);                // el tiempo de llegar a la barra
            var order = new Order { customer = customers[customerIndex++ % customers.Length], drink = drink, arrivedAt = Time.time };
            orders.Add(order);
            SessionLog.Event("pedido", "llega", $"{order.customer}: {drink}");
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
            if (CurrentAct == Act.Cierre && !regularPending) return; // ya cerró la barra
            var order = Pending.FirstOrDefault(o => o.drink == drink);
            if (order != null)
            {
                order.done = true;
                ServedOk++;
                SessionLog.Event("pedido", "servido", $"{order.customer}: {drink} en {Time.time - order.arrivedAt:0.0} s");
                RefreshBoard();
                board?.ShowNote($"¡Gracias! ({order.customer})", false, 2f);
                StartCoroutine(RemoveLater(order, 2.5f));
                if (hintsEnabled) ClearHints();
            }
            else
            {
                Mistakes++;
                SessionLog.Event("pedido", "error", $"sirvió {drink}");
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
            SessionLog.Event("acto", act.ToString());
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
                    board?.SetTitle("ÚLTIMOS");
                    board?.SetBody("Ya no entra nadie.\nSe van los últimos.");
                    board?.SetFooter("Cierre");
                    break;
            }
            RefreshBoard();
            ActChanged?.Invoke(act);
        }

        void RefreshBoard()
        {
            if (board == null) return;
            if (CurrentAct == Act.Cierre && !regularPending) return; // en el cierre la pizarra la manejan el habitué y la ClosingSequence
            board.SetTitle("PEDIDOS");
            board.SetLines(orders.Select(o => (o.customer, o.drink, o.done)).ToList(), "Nadie esperando.");
            string act = CurrentAct == Act.Apertura ? "Apertura" : CurrentAct == Act.HoraPico ? "Hora pico" : "Cierre";
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
            if (!hintMoved) { hintHome = hintSound.transform.position; hintMoved = true; }
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
            if (hintMoved && hintSound != null) { hintSound.transform.position = hintHome; hintMoved = false; } // la fuente vuelve a su lugar
        }
    }
}
