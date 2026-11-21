using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Una botella del estante que se puede usar. Al elegirla, la ShotStation la levanta, la lleva
    /// volando sobre el vaso, sirve y la devuelve a su lugar. Recuerda su pose original.
    /// Como las botellas están detrás del barman, el pedido obliga a darse vuelta: usa el espacio de la RV.
    /// </summary>
    public class BottleSource : GazeInteractable
    {
        public ShotStation station;

        [Header("Bebida")]
        public string drinkName = "Whisky";
        public Color liquidColor = new Color(0.75f, 0.42f, 0.12f, 1f);

        public Vector3 RestPosition { get; private set; }
        public Quaternion RestRotation { get; private set; }

        /// <summary>Punta del pico, relativa a la pose (en el espacio de rotación de la botella).</summary>
        public Vector3 TipOffset { get; private set; }

        /// <summary>Centro de la botella, relativo a la pose.</summary>
        public Vector3 CenterOffset { get; private set; }

        public float Height { get; private set; }

        void Awake()
        {
            RestPosition = transform.position;
            RestRotation = transform.rotation;
            Bounds b = WorldBounds();
            Height = b.size.y;
            Quaternion inv = Quaternion.Inverse(RestRotation);
            TipOffset = inv * (new Vector3(b.center.x, b.max.y, b.center.z) - RestPosition);
            CenterOffset = inv * (b.center - RestPosition);
        }

        protected override bool IsAvailable() => station != null && station.CanPour;

        public override void OnSelect(GazePointer pointer)
        {
            if (station != null) station.Pour(this);
        }

        /// <summary>Ubica la botella con la rotación dada de forma que la punta del pico quede en "tip".</summary>
        public void PlaceTipAt(Vector3 tip, Quaternion rotation)
        {
            transform.SetPositionAndRotation(tip - rotation * TipOffset, rotation);
        }

        public Vector3 TipPosition => transform.position + transform.rotation * TipOffset;

        Bounds WorldBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(transform.position, Vector3.one * 0.1f);
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
