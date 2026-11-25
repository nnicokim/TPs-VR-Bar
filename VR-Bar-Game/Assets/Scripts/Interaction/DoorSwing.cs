using System.Collections;
using BarAudio;
using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Puerta que se abre cuando suena su sonido (la campanita de la entrada, la puerta de la cocina):
    /// lo que antes era solo "fuera de campo" ahora también se ve. Va en la bisagra (el pivote).
    /// Mientras está abierta puede subir otros sonidos (la lluvia de la calle entra por la puerta).
    /// </summary>
    public class DoorSwing : MonoBehaviour
    {
        [Tooltip("El sonido que la abre (BarSound de Audio (Bar)).")]
        public BarSound sound;
        [Tooltip("Ángulo de apertura en grados (el signo define hacia dónde).")]
        public float openAngle = 75f;
        public float openDuration = 0.55f;
        public float closeDuration = 0.9f;
        [Tooltip("Segundos que queda abierta (mín, máx).")]
        public Vector2 holdSeconds = new Vector2(1.2f, 2.2f);

        [Header("Mientras está abierta")]
        public BarSound[] louderWhileOpen;
        public float louderFactor = 1.8f;

        Quaternion closed;
        float angle;
        Coroutine routine;

        public bool IsOpen => angle > 1f;

        void Awake() => closed = transform.localRotation;

        void OnEnable() { if (sound != null) sound.Played += OnSoundPlayed; }
        void OnDisable() { if (sound != null) sound.Played -= OnSoundPlayed; }

        void OnSoundPlayed(BarSound s) => Open();

        public void Open()
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(OpenRoutine());
        }

        IEnumerator OpenRoutine()
        {
            SetLouder(true);
            float from = angle;
            yield return Ease.Run(openDuration * Mathf.Clamp01(1f - from / openAngle + 0.2f), t => SetAngle(Mathf.Lerp(from, openAngle, Ease.OutCubic(t))));
            yield return new WaitForSeconds(Random.Range(holdSeconds.x, holdSeconds.y));
            SetLouder(false);
            float start = angle;
            // Se cierra sola con un pequeño rebote al llegar al marco.
            yield return Ease.Run(closeDuration, t => SetAngle(Mathf.LerpUnclamped(start, 0f, Ease.OutBack(t))));
            SetAngle(0f);
            routine = null;
        }

        void SetAngle(float a)
        {
            angle = a;
            transform.localRotation = Quaternion.AngleAxis(a, Vector3.up) * closed;
        }

        void SetLouder(bool on)
        {
            if (louderWhileOpen == null) return;
            foreach (var s in louderWhileOpen)
                if (s != null) s.SetVolumeScale(on ? louderFactor : 1f, on ? 0.4f : 1.2f);
        }
    }
}
