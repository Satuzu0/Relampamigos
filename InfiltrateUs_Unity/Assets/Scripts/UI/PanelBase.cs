using UnityEngine;

public enum PanelId { Login, Registro, Menu, Personaje, Vestuario, Tienda, Logros, Ajustes, Creditos, Salas }
public enum FondoEscena { Montana, Escritorio }

// Base de todas las ventanas. UIManager las muestra y les manda las teclas.
public abstract class PanelBase : MonoBehaviour
{
    public abstract PanelId Id { get; }
    public virtual FondoEscena Fondo { get { return FondoEscena.Escritorio; } }
    public virtual bool MostrarBarraSuperior { get { return true; } }
    // Cada atajo se escribe "TECLA|TEXTO"
    public virtual string[] Atajos { get { return new[] { "FLECHAS|NAVEGAR", "ENTER|CONFIRMAR", "ESC|ATRÁS" }; } }

    public abstract void Construir();
    public virtual void AlMostrar() { }
    public virtual void AlOcultar() { }

    public virtual void Arriba() { }
    public virtual void Abajo() { }
    public virtual void Izquierda() { }
    public virtual void Derecha() { }
    public virtual void Confirmar() { }
    public virtual void Atras() { }
    public virtual void Reset() { }
    public virtual void TabKey() { }

    protected UIManager UI { get { return UIManager.Instancia; } }
}
