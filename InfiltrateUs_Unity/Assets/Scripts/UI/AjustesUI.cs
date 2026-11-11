using System;
using System.Collections.Generic;
using UnityEngine;

// Ajustes con pestanas GENERAL / AUDIO / ACCESIBILIDAD. Se guardan en esta PC (PlayerPrefs).
public class AjustesUI : PanelBase
{
    public override PanelId Id { get { return PanelId.Ajustes; } }
    public override string[] Atajos { get { return new[] { "ARRIBA/ABAJO|MOVER", "IZQ/DER|CAMBIAR", "TAB|PESTAÑA", "R|RESTABLECER", "ESC|ATRÁS" }; } }

    class Opcion
    {
        public string Etiqueta;
        public Func<string> Valor;       // null si es un boton
        public Action<int> Cambiar;      // -1 o +1
        public Action Accion;            // para botones
    }

    readonly string[] nombresTabs = { "GENERAL", "AUDIO", "ACCESIBILIDAD" };
    readonly List<FilaUI> pestanas = new List<FilaUI>();
    readonly List<Opcion>[] opciones = new List<Opcion>[3];
    ListaNavegable lista;
    int tab;

    public override void Construir()
    {
        float ancho = 693f / 3f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var f = FilaUI.Crear(transform, "Tab" + i, nombresTabs[i], 18, EstiloFila.Pestana);
            UiKit.Ubicar(f.GetComponent<RectTransform>(), UiKit.ArribaIzq, i * ancho, 0, ancho - 4, 36);
            f.alClick = () => PonerTab(idx);
            pestanas.Add(f);
        }

        lista = new ListaNavegable(transform, "Lista", 44, 24, 4);
        UiKit.Ubicar(lista.raiz, UiKit.ArribaIzq, 0, -46, 693, 310);
        lista.alConfirmar = i => { var o = opciones[tab][i]; if (o.Accion != null) o.Accion(); else if (o.Cambiar != null) o.Cambiar(1); };

        opciones[0] = new List<Opcion>
        {
            Interruptor("PANTALLA COMPLETA", () => AjustesLocales.PantallaCompleta, v => AjustesLocales.PantallaCompleta = v),
            Deslizador("BRILLO", () => AjustesLocales.Brillo, v => AjustesLocales.Brillo = v, 0.2f),
            new Opcion { Etiqueta = "CERRAR SESIÓN", Accion = () => UI.CerrarSesion() },
            new Opcion { Etiqueta = "RESTABLECER AJUSTES", Accion = Restablecer }
        };
        opciones[1] = new List<Opcion>
        {
            Deslizador("VOLUMEN GENERAL", () => AjustesLocales.VolGeneral, v => AjustesLocales.VolGeneral = v, 0f),
            Deslizador("MÚSICA", () => AjustesLocales.VolMusica, v => AjustesLocales.VolMusica = v, 0f),
            Deslizador("EFECTOS", () => AjustesLocales.VolEfectos, v => AjustesLocales.VolEfectos = v, 0f)
        };
        opciones[2] = new List<Opcion>
        {
            Interruptor("CONTRASTE ALTO", () => AjustesLocales.ContrasteAlto, v => AjustesLocales.ContrasteAlto = v),
            Interruptor("MOSTRAR AYUDAS", () => AjustesLocales.MostrarAyudas, v => AjustesLocales.MostrarAyudas = v)
        };
    }

    Opcion Interruptor(string etiqueta, Func<bool> leer, Action<bool> escribir)
    {
        return new Opcion
        {
            Etiqueta = etiqueta,
            Valor = () => leer() ? "[ SI ]" : "[ NO ]",
            Cambiar = d => { escribir(!leer()); AjustesLocales.Aplicar(); }
        };
    }

    Opcion Deslizador(string etiqueta, Func<float> leer, Action<float> escribir, float minimo)
    {
        return new Opcion
        {
            Etiqueta = etiqueta,
            Valor = () =>
            {
                int n = Mathf.RoundToInt(leer() * 10f);
                return "< " + new string('#', n) + new string('-', 10 - n) + " " + Mathf.RoundToInt(leer() * 100f) + "% >";
            },
            Cambiar = d =>
            {
                float v = Mathf.Round((leer() + d * 0.1f) * 10f) / 10f;
                escribir(Mathf.Clamp(v, minimo, 1f));
                AjustesLocales.Aplicar();
            }
        };
    }

    public override void AlMostrar() { PonerTab(0); }

    void PonerTab(int i)
    {
        tab = ((i % 3) + 3) % 3;
        for (int k = 0; k < 3; k++) pestanas[k].Seleccionada = (k == tab);
        Rellenar(0);
    }

    void Rellenar(int fila)
    {
        lista.Limpiar();
        foreach (var o in opciones[tab])
        {
            var f = lista.Agregar(o.Etiqueta);
            f.derecha.fontSize = 24;
            f.derecha.color = UiKit.Acento;
        }
        RefrescarValores();
        lista.Seleccionar(Mathf.Min(fila, lista.Cantidad - 1));
    }

    void RefrescarValores()
    {
        for (int i = 0; i < lista.Cantidad; i++)
        {
            var o = opciones[tab][i];
            lista.filas[i].derecha.text = o.Valor != null ? o.Valor() : "";
        }
    }

    void Restablecer()
    {
        AjustesLocales.Restablecer();
        AjustesLocales.Aplicar();
        RefrescarValores();
        Toast.Mostrar("Ajustes restablecidos", UiKit.Ok);
    }

    void Mover(int d)
    {
        int i = lista.Indice;
        if (i < 0) return;
        var o = opciones[tab][i];
        if (o.Cambiar != null) { o.Cambiar(d); RefrescarValores(); }
        else PonerTab(tab + d);   // en los botones, izquierda/derecha cambian de pestana
    }

    public override void Arriba() { lista.Mover(-1); }
    public override void Abajo() { lista.Mover(1); }
    public override void Izquierda() { Mover(-1); }
    public override void Derecha() { Mover(1); }
    public override void TabKey() { PonerTab(tab + 1); }
    public override void Reset() { Restablecer(); }
    public override void Confirmar() { lista.Confirmar(); RefrescarValores(); }
    public override void Atras() { UI.Volver(); }
}
