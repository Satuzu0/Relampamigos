using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Lee el teclado sin importar si el proyecto usa el Input System nuevo o el viejo XD
public static class Entrada
{
    public static bool Arriba { get { return Tecla(KeyCode.UpArrow); } }
    public static bool Abajo { get { return Tecla(KeyCode.DownArrow); } }
    public static bool Izquierda { get { return Tecla(KeyCode.LeftArrow); } }
    public static bool Derecha { get { return Tecla(KeyCode.RightArrow); } }
    public static bool Confirmar { get { return Tecla(KeyCode.Return) || Tecla(KeyCode.KeypadEnter); } }
    public static bool Atras { get { return Tecla(KeyCode.Escape); } }
    public static bool Reset { get { return Tecla(KeyCode.R); } }
    public static bool Tab { get { return Tecla(KeyCode.Tab); } }

    static bool Tecla(KeyCode k)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (k)
        {
            case KeyCode.UpArrow: return kb.upArrowKey.wasPressedThisFrame;
            case KeyCode.DownArrow: return kb.downArrowKey.wasPressedThisFrame;
            case KeyCode.LeftArrow: return kb.leftArrowKey.wasPressedThisFrame;
            case KeyCode.RightArrow: return kb.rightArrowKey.wasPressedThisFrame;
            case KeyCode.Return: return kb.enterKey.wasPressedThisFrame;
            case KeyCode.KeypadEnter: return kb.numpadEnterKey.wasPressedThisFrame;
            case KeyCode.Escape: return kb.escapeKey.wasPressedThisFrame;
            case KeyCode.R: return kb.rKey.wasPressedThisFrame;
            case KeyCode.Tab: return kb.tabKey.wasPressedThisFrame;
        }
        return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(k);
#else
        return false;
#endif
    }
}
