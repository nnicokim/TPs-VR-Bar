using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BarInteraccion
{
    /// <summary>
    /// El único botón de Cardboard: tocar la pantalla (o el gatillo del visor, que toca la pantalla).
    /// En el Editor también vale el clic izquierdo o la barra espaciadora.
    /// </summary>
    public static class GazeInput
    {
        public static bool SelectPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0)) return true;
#endif
            return false;
        }

        public static bool Held()
        {
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) return true;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.isPressed) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(0)) return true;
#endif
            return false;
        }
    }
}
