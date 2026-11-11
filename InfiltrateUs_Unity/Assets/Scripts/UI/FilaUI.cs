using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum EstiloFila { Fila, Pestana, Boton }

// Una fila de lista, una pestana o un boton: todo lo que se puede seleccionar con mouse o teclado.
public class FilaUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Text texto, derecha;
    public Image fondo, marco;
    public EstiloFila estilo;
    public Action alClick, alHover;

    bool seleccionada, encima, desactivada;

    public bool Seleccionada { get { return seleccionada; } set { seleccionada = value; Refrescar(); } }
    public bool Desactivada { get { return desactivada; } set { desactivada = value; Refrescar(); } }

    public static FilaUI Crear(Transform padre, string nombre, string texto, int tam, EstiloFila estilo, bool fuenteTitulo = true)
    {
        var rt = UiKit.Nuevo(nombre, padre);
        var f = rt.gameObject.AddComponent<FilaUI>();
        f.estilo = estilo;
        f.fondo = rt.gameObject.AddComponent<Image>();
        UiKit.Warp(f.fondo);
        f.fondo.color = UiKit.Transparente;
        // dentro de la laptop el mouse lo maneja PantallaLaptop (el raycast normal no entiende la deformacion)
        bool enLaptop = padre.GetComponentInParent<PantallaLaptop>() != null;
        f.fondo.raycastTarget = !enLaptop;
        if (enLaptop) PantallaLaptop.Registrar(f);
        if (estilo != EstiloFila.Fila)
            f.marco = UiKit.Marco(rt, "Borde", UiKit.Texto, 0.6f);
        f.texto = UiKit.Txt(rt, "Texto", texto, tam, UiKit.Texto, TextAnchor.MiddleLeft, fuenteTitulo);
        UiKit.Estirar(f.texto.rectTransform, 14, 14, 0, 0);
        f.derecha = UiKit.Txt(rt, "Derecha", "", Mathf.Max(14, tam - 2), UiKit.TextoSuave, TextAnchor.MiddleRight, false);
        UiKit.Estirar(f.derecha.rectTransform, 14, 14, 0, 0);
        if (estilo == EstiloFila.Pestana || estilo == EstiloFila.Boton) f.texto.alignment = TextAnchor.MiddleCenter;
        f.Refrescar();
        return f;
    }

    public void Refrescar()
    {
        if (fondo == null) return;
        Color fondoC = UiKit.Transparente;
        Color textoC = UiKit.Texto;
        Color bordeC = UiKit.Texto;

        if (desactivada)
        {
            textoC = new Color(UiKit.TextoSuave.r, UiKit.TextoSuave.g, UiKit.TextoSuave.b, 0.45f);
            bordeC = new Color(UiKit.TextoSuave.r, UiKit.TextoSuave.g, UiKit.TextoSuave.b, 0.35f);
        }
        else if (seleccionada)
        {
            fondoC = UiKit.Seleccion;
            textoC = UiKit.Oscuro;
        }
        else if (encima)
        {
            fondoC = new Color(UiKit.Acento.r, UiKit.Acento.g, UiKit.Acento.b, 0.22f);
        }
        else if (estilo == EstiloFila.Boton)
        {
            fondoC = UiKit.Vidrio;
            bordeC = UiKit.Acento;
        }

        fondo.color = fondoC;
        texto.color = textoC;
        derecha.color = (seleccionada && !desactivada) ? new Color(0.12f, 0.2f, 0.25f, 1f) : (desactivada ? textoC : UiKit.TextoSuave);
        if (marco != null) marco.color = bordeC;
    }

    public RectTransform rectTransform { get { return (RectTransform)transform; } }
    void OnDestroy() { PantallaLaptop.Quitar(this); }

    public void PunteroEntrar() { OnPointerEnter(null); }
    public void PunteroSalir() { OnPointerExit(null); }
    public void PunteroClick() { OnPointerClick(null); }

    public void OnPointerEnter(PointerEventData e)
    {
        encima = true;
        Refrescar();
        if (!desactivada && alHover != null) alHover();
    }

    public void OnPointerExit(PointerEventData e)
    {
        encima = false;
        Refrescar();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (desactivada) return;
        if (alClick != null) alClick();
    }
}
