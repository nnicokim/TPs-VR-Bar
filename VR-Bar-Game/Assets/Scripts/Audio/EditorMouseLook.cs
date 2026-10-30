using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BarAudio
{
    /// <summary>
    /// Solo para probar en el Editor sin celular: mantener clic derecho y mover el mouse para girar la cabeza.
    /// En el celular (Cardboard) no hace nada.
    /// </summary>
    public class EditorMouseLook : MonoBehaviour
    {
        public float sensitivity = 0.12f;
        float yaw;
        float pitch;

#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
        void Update()
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed) return;
            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * sensitivity;
            pitch = Mathf.Clamp(pitch - d.y * sensitivity, -85f, 85f);
            transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }
#endif
    }
}
