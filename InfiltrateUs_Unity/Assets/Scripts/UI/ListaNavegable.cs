using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Lista vertical con scroll que se maneja con flechas, Enter y mouse.
public class ListaNavegable
{
    public RectTransform raiz;
    public readonly List<FilaUI> filas = new List<FilaUI>();
    public Action<int> alCambiar;     // cambia la seleccion (hover o flechas)
    public Action<int> alConfirmar;   // Enter o click

    ScrollRect scroll;
    RectTransform contenido, viewport;
    float altoFila, espacio;
    int tamFuente;
    int indice = -1;

    public int Indice { get { return indice; } }
    public int Cantidad { get { return filas.Count; } }
    public FilaUI Actual { get { return (indice >= 0 && indice < filas.Count) ? filas[indice] : null; } }

    public ListaNavegable(Transform padre, string nombre, float altoFila, int tamFuente, float espacio = 4f)
    {
        this.altoFila = altoFila;
        this.tamFuente = tamFuente;
        this.espacio = espacio;

        raiz = UiKit.Nuevo(nombre, padre);
        scroll = raiz.gameObject.AddComponent<ScrollRect>();

        bool enLaptop = padre.GetComponentInParent<PantallaLaptop>() != null;
        var vp = UiKit.Img(raiz, "Viewport", new Color(0, 0, 0, 0), null, !enLaptop);
        viewport = vp.rectTransform;
        UiKit.Estirar(viewport);
        // en la laptop la mascara normal no entiende la deformacion, usamos nuestro recorte
        if (enLaptop) viewport.gameObject.AddComponent<ClipLocal>();
        else viewport.gameObject.AddComponent<RectMask2D>();

        contenido = UiKit.Nuevo("Contenido", viewport);
        contenido.anchorMin = new Vector2(0, 1);
        contenido.anchorMax = new Vector2(1, 1);
        contenido.pivot = new Vector2(0.5f, 1);
        contenido.anchoredPosition = Vector2.zero;
        contenido.sizeDelta = Vector2.zero;
        var lay = contenido.gameObject.AddComponent<VerticalLayoutGroup>();
        lay.spacing = espacio;
        lay.childControlWidth = true;
        lay.childControlHeight = true;
        lay.childForceExpandWidth = true;
        lay.childForceExpandHeight = false;
        var fit = contenido.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = contenido;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        scroll.inertia = false;
    }

    public void Limpiar()
    {
        foreach (var f in filas) if (f != null) UnityEngine.Object.Destroy(f.gameObject);
        filas.Clear();
        indice = -1;
        contenido.anchoredPosition = Vector2.zero;
    }

    public FilaUI Agregar(string texto, string derecha = null, bool fuenteTitulo = true)
    {
        int i = filas.Count;
        var f = FilaUI.Crear(contenido, "Fila" + i, texto, tamFuente, EstiloFila.Fila, fuenteTitulo);
        var le = f.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = altoFila;
        le.minHeight = altoFila;
        if (derecha != null) f.derecha.text = derecha;
        f.alHover = () => Seleccionar(i);
        f.alClick = () => { Seleccionar(i); if (alConfirmar != null) alConfirmar(i); };
        filas.Add(f);
        return f;
    }

    public void Seleccionar(int i)
    {
        if (filas.Count == 0) { indice = -1; return; }
        i = Mathf.Clamp(i, 0, filas.Count - 1);
        bool cambio = i != indice;
        if (indice >= 0 && indice < filas.Count) filas[indice].Seleccionada = false;
        indice = i;
        filas[indice].Seleccionada = true;
        AsegurarVisible(indice);
        if (cambio && alCambiar != null) alCambiar(indice);
    }

    public void Mover(int d)
    {
        if (filas.Count == 0) return;
        int n = indice < 0 ? 0 : indice + d;
        // da la vuelta en los extremos
        if (n < 0) n = filas.Count - 1;
        if (n >= filas.Count) n = 0;
        Seleccionar(n);
    }

    public void Confirmar()
    {
        if (indice >= 0 && indice < filas.Count && !filas[indice].Desactivada && alConfirmar != null) alConfirmar(indice);
    }

    void AsegurarVisible(int i)
    {
        float vh = viewport.rect.height;
        if (vh <= 0) return;
        float arriba = i * (altoFila + espacio);
        float abajo = arriba + altoFila;
        float y = contenido.anchoredPosition.y;
        if (arriba < y) y = arriba;
        else if (abajo > y + vh) y = abajo - vh;
        contenido.anchoredPosition = new Vector2(0, Mathf.Max(0, y));
    }
}
