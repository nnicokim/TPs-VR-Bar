using System;
using System.Collections;
using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Va en el XR Origin. Mueve al participante a un TeleportPoint con un fundido corto:
    /// oscurece, mueve el origen para que la cámara quede sobre el punto (manteniendo la altura
    /// de los ojos), opcionalmente gira la vista hacia donde apunta el punto, y aclara.
    /// El punto donde uno está se oculta (no tiene sentido ofrecer ir a donde ya estás).
    /// </summary>
    public class GazeTeleporter : MonoBehaviour
    {
        public static GazeTeleporter Instance { get; private set; }

        public Transform cameraTransform;
        public ScreenFader fader;
        public float fadeOut = 0.15f;
        public float fadeIn = 0.25f;

        public bool Busy { get; private set; }
        public TeleportPoint CurrentPoint { get; private set; }

        public event Action<TeleportPoint> Teleported;

        void Awake()
        {
            Instance = this;
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (fader == null && cameraTransform != null) fader = cameraTransform.GetComponent<ScreenFader>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            // Si arrancamos parados sobre un punto, ese queda oculto.
            if (cameraTransform == null) return;
            TeleportPoint nearest = null;
            float best = 0.8f;
            foreach (var p in FindObjectsByType<TeleportPoint>(FindObjectsSortMode.None))
            {
                Vector3 d = p.transform.position - cameraTransform.position; d.y = 0f;
                if (d.magnitude < best) { best = d.magnitude; nearest = p; }
            }
            if (nearest != null) SetCurrent(nearest);
        }

        public void Teleport(TeleportPoint point)
        {
            if (Busy || point == null || cameraTransform == null) return;
            StartCoroutine(TeleportRoutine(point));
        }

        IEnumerator TeleportRoutine(TeleportPoint point)
        {
            Busy = true;
            var pointer = GazePointer.Instance;
            if (pointer != null) pointer.Locked = true;
            if (fader != null) yield return fader.FadeTo(1f, fadeOut);

            if (point.alignRotation)
            {
                float delta = Mathf.DeltaAngle(cameraTransform.eulerAngles.y, point.transform.eulerAngles.y);
                transform.RotateAround(cameraTransform.position, Vector3.up, delta);
            }
            Vector3 offset = point.transform.position - cameraTransform.position;
            offset.y = 0f; // conserva la altura de los ojos
            transform.position += offset;
            SetCurrent(point);
            SessionLog.Event("teletransporte", point.label);

            yield return null; // un frame en negro para que todo se acomode
            if (fader != null) yield return fader.FadeTo(0f, fadeIn);
            if (pointer != null) pointer.Locked = false;
            Busy = false;
            Teleported?.Invoke(point);
        }

        void SetCurrent(TeleportPoint point)
        {
            if (CurrentPoint != null) CurrentPoint.SetOccupied(false);
            CurrentPoint = point;
            if (CurrentPoint != null) CurrentPoint.SetOccupied(true);
        }
    }
}
