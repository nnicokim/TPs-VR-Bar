using System.Collections;
using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Una canilla de cerveza. Va en el pivote de la manija (en su base), así al rotar se "tira" como una real.
    /// Elegirla le pide a la PourStation que sirva esta cerveza. Mientras la estación está ocupada
    /// (sirviendo o con una jarra llena esperando), la canilla no se resalta: la restricción también comunica.
    /// </summary>
    public class BeerTap : GazeInteractable
    {
        public PourStation station;

        [Header("Cerveza")]
        public string beerName = "Rubia";
        public Color beerColor = new Color(1f, 0.72f, 0.15f, 1f);

        [Header("Geometría (la completa el armado automático)")]
        [Tooltip("Lo que rota al tirar (normalmente este mismo objeto, ubicado en la base de la manija).")]
        public Transform lever;
        [Tooltip("Eje de giro, en el espacio del padre de la palanca.")]
        public Vector3 leverAxis = Vector3.right;
        [Tooltip("Ángulo al tirar (el signo define hacia dónde).")]
        public float pullAngle = 40f;
        [Tooltip("Boca de la canilla: de acá sale el chorro.")]
        public Transform spout;
        [Tooltip("Punto sobre la barra, debajo de la boca, donde se apoya la jarra.")]
        public Transform pourTarget;

        Quaternion restRotation;
        float currentAngle;

        void Awake()
        {
            if (lever == null) lever = transform;
            restRotation = lever.localRotation;
        }

        protected override bool IsAvailable() => station != null && station.CanPour;

        public override void OnSelect(GazePointer pointer)
        {
            if (station != null) station.Pour(this);
        }

        public IEnumerator Pull(float duration) => AnimateLever(pullAngle, duration, true);
        public IEnumerator Release(float duration) => AnimateLever(0f, duration, false);

        IEnumerator AnimateLever(float target, float duration, bool easeOut)
        {
            float from = currentAngle;
            yield return Ease.Run(duration, t =>
            {
                float e = easeOut ? Ease.OutCubic(t) : Ease.OutBack(t);
                SetAngle(Mathf.LerpUnclamped(from, target, e));
            });
        }

        void SetAngle(float angle)
        {
            currentAngle = angle;
            if (lever == null) return;
            Vector3 axis = leverAxis.sqrMagnitude > 0f ? leverAxis.normalized : Vector3.right;
            lever.localRotation = Quaternion.AngleAxis(angle, axis) * restRotation;
        }
    }
}
