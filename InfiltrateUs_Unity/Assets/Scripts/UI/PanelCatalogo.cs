using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Base de vestuario y tienda: pestanas arriba, lista a la izquierda y detalle con icono a la derecha.
public abstract class PanelCatalogo : PanelBase
{
    protected ListaNavegable lista;
    protected int tab;
    protected Image icono;
    protected Text nombre, rareza, descripcion, extra, vacio;

    RectTransform raizTabs;
    readonly List<FilaUI> pestanas = new List<FilaUI>();
    int cantidadTabs;

    protected abstract void AlCambiarPestana();   // hay que rellenar la lista de nuevo
    protected abstract void AlSeleccionar(int i); // cambio la fila elegida

    protected void ConstruirBase()
    {
        raizTabs = UiKit.Nuevo("Pestanas", transform);
        UiKit.Ubicar(raizTabs, UiKit.ArribaIzq, 0, 0, 693, 34);

        lista = new ListaNavegable(transform, "Lista", 36, 22, 3);
        UiKit.Ubicar(lista.raiz, UiKit.ArribaIzq, 0, -42, 330, 314);
        lista.alCambiar = AlSeleccionar;

        var det = UiKit.Caja(transform, "Detalle");
        UiKit.Ubicar(det, UiKit.ArribaIzq, 344, -42, 349, 314);

        icono = UiKit.Img(det, "Icono", Color.white);
        icono.preserveAspect = true;
        UiKit.Ubicar(icono.rectTransform, UiKit.ArribaIzq, 16, -14, 96, 96);

        nombre = UiKit.Txt(det, "Nombre", "", 18, UiKit.Acento, TextAnchor.UpperLeft, true);
        UiKit.Ubicar(nombre.rectTransform, UiKit.ArribaIzq, 124, -14, 214, 56);

        rareza = UiKit.Txt(det, "Rareza", "", 24, UiKit.Texto, TextAnchor.MiddleLeft, false);
        UiKit.Ubicar(rareza.rectTransform, UiKit.ArribaIzq, 124, -76, 214, 30);

        descripcion = UiKit.Txt(det, "Descripcion", "", 24, UiKit.Texto, TextAnchor.UpperLeft, false);
        UiKit.Ubicar(descripcion.rectTransform, UiKit.ArribaIzq, 16, -124, 317, 120);

        extra = UiKit.Txt(det, "Extra", "", 20, UiKit.Oro, TextAnchor.MiddleLeft, true);
        UiKit.Ubicar(extra.rectTransform, UiKit.ArribaIzq, 16, -254, 317, 48);

        vacio = UiKit.Txt(transform, "Vacio", "", 26, UiKit.TextoSuave, TextAnchor.MiddleCenter, false);
        UiKit.Ubicar(vacio.rectTransform, UiKit.ArribaIzq, 0, -150, 693, 90);
        vacio.gameObject.SetActive(false);
    }

    protected void PonerPestanas(List<string> nombres, int activa)
    {
        foreach (var p in pestanas) if (p != null) Destroy(p.gameObject);
        pestanas.Clear();
        cantidadTabs = nombres.Count;
        if (cantidadTabs == 0) return;

        float ancho = Mathf.Min(150f, 693f / cantidadTabs);
        for (int i = 0; i < cantidadTabs; i++)
        {
            int idx = i;
            var f = FilaUI.Crear(raizTabs, "Tab" + i, nombres[i].ToUpperInvariant(), 16, EstiloFila.Pestana);
            UiKit.Ubicar(f.GetComponent<RectTransform>(), UiKit.ArribaIzq, i * (ancho + 4), 0, ancho, 34);
            f.alClick = () => PonerPestana(idx);
            pestanas.Add(f);
        }
        tab = Mathf.Clamp(activa, 0, cantidadTabs - 1);
        for (int i = 0; i < pestanas.Count; i++) pestanas[i].Seleccionada = (i == tab);
    }

    protected void PonerPestana(int i)
    {
        if (cantidadTabs == 0) return;
        tab = ((i % cantidadTabs) + cantidadTabs) % cantidadTabs;
        for (int k = 0; k < pestanas.Count; k++) pestanas[k].Seleccionada = (k == tab);
        AlCambiarPestana();
    }

    protected void PonerDetalle(string iconRef, string nom, string rar, Color colorRar, string desc, string textoExtra, Color colorExtra)
    {
        icono.sprite = Arte.Cargar(iconRef);
        icono.enabled = icono.sprite != null;
        nombre.text = (nom ?? "").ToUpperInvariant();
        rareza.text = rar;
        rareza.color = colorRar;
        descripcion.text = desc;
        extra.text = textoExtra;
        extra.color = colorExtra;
    }

    protected void LimpiarDetalle()
    {
        icono.enabled = false;
        nombre.text = rareza.text = descripcion.text = extra.text = "";
    }

    protected void MostrarVacio(string mensaje)
    {
        vacio.text = mensaje;
        vacio.gameObject.SetActive(!string.IsNullOrEmpty(mensaje));
    }

    public override void Arriba() { lista.Mover(-1); }
    public override void Abajo() { lista.Mover(1); }
    public override void Izquierda() { PonerPestana(tab - 1); }
    public override void Derecha() { PonerPestana(tab + 1); }
    public override void TabKey() { PonerPestana(tab + 1); }
    public override void Atras() { UI.Volver(); }
    public override void Confirmar() { lista.Confirmar(); }
}
