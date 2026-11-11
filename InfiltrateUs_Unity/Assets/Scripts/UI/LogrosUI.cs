using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Logros: se leen de la BD y los que ya cumpliste se desbloquean solos.
public class LogrosUI : PanelBase
{
    public override PanelId Id { get { return PanelId.Logros; } }
    public override string[] Atajos { get { return new[] { "ARRIBA/ABAJO|MOVER", "ESC|ATRÁS" }; } }

    ListaNavegable lista;
    Text cabecera, nombre, descripcion, progresoTxt, premio;
    Image barra;
    List<LogroInfo> logros = new List<LogroInfo>();
    const float AnchoBarra = 317f;

    public override void Construir()
    {
        cabecera = UiKit.Txt(transform, "Cabecera", "", 20, UiKit.Acento, TextAnchor.MiddleLeft, true);
        UiKit.Ubicar(cabecera.rectTransform, UiKit.ArribaIzq, 4, 0, 400, 30);

        lista = new ListaNavegable(transform, "Lista", 36, 22, 3);
        UiKit.Ubicar(lista.raiz, UiKit.ArribaIzq, 0, -38, 330, 318);
        lista.alCambiar = Detalle;

        var det = UiKit.Caja(transform, "Detalle");
        UiKit.Ubicar(det, UiKit.ArribaIzq, 344, -38, 349, 318);

        nombre = UiKit.Txt(det, "Nombre", "", 20, UiKit.Acento, TextAnchor.UpperLeft, true);
        UiKit.Ubicar(nombre.rectTransform, UiKit.ArribaIzq, 16, -14, 317, 56);

        descripcion = UiKit.Txt(det, "Descripcion", "", 24, UiKit.Texto, TextAnchor.UpperLeft, false);
        UiKit.Ubicar(descripcion.rectTransform, UiKit.ArribaIzq, 16, -76, 317, 100);

        barra = UiKit.Barra(det, "Progreso", AnchoBarra, 20, UiKit.Acento);
        UiKit.Ubicar(barra.transform.parent as RectTransform, UiKit.ArribaIzq, 16, -190, AnchoBarra, 20);

        progresoTxt = UiKit.Txt(det, "ProgresoTxt", "", 24, UiKit.Texto, TextAnchor.MiddleLeft, false);
        UiKit.Ubicar(progresoTxt.rectTransform, UiKit.ArribaIzq, 16, -214, 317, 30);

        premio = UiKit.Txt(det, "Premio", "", 20, UiKit.Oro, TextAnchor.MiddleLeft, true);
        UiKit.Ubicar(premio.rectTransform, UiKit.ArribaIzq, 16, -256, 317, 40);
    }

    public override void AlMostrar()
    {
        UI.ActualizarFondoPersonaje();
        long antes = PlayerSession.Tokens;
        UI.Correr(() => LogrosRepository.CargarYSincronizar(), l =>
        {
            logros = l;
            if (PlayerSession.Tokens > antes)
                Toast.Mostrar("Logro desbloqueado! +" + (PlayerSession.Tokens - antes).ToString("N0") + " tokens", UiKit.Ok);
            Rellenar();
        });
    }

    void Rellenar()
    {
        lista.Limpiar();
        int hechos = 0;
        foreach (var l in logros)
        {
            var f = lista.Agregar(l.Nombre.ToUpperInvariant());
            if (l.Desbloqueado) { hechos++; f.derecha.text = "LISTO"; f.derecha.color = UiKit.Ok; }
            else f.derecha.text = Mathf.Min(l.Progreso, l.Meta) + "/" + l.Meta;
        }
        cabecera.text = "LOGROS " + hechos + "/" + logros.Count;
        if (logros.Count > 0) lista.Seleccionar(0);
    }

    void Detalle(int i)
    {
        if (i < 0 || i >= logros.Count) return;
        var l = logros[i];
        nombre.text = l.Nombre.ToUpperInvariant();
        descripcion.text = l.Descripcion;
        int prog = Mathf.Min(l.Progreso, l.Meta);
        UiKit.PonerBarra(barra, l.Meta > 0 ? prog / (float)l.Meta : 1f, AnchoBarra);
        progresoTxt.text = l.Desbloqueado ? "DESBLOQUEADO" : prog + " de " + l.Meta;
        progresoTxt.color = l.Desbloqueado ? UiKit.Ok : UiKit.Texto;
        premio.text = l.Recompensa > 0 ? "PREMIO: " + l.Recompensa.ToString("N0") + " TOKENS" : "";
    }

    public override void Arriba() { lista.Mover(-1); }
    public override void Abajo() { lista.Mover(1); }
    public override void Atras() { UI.Volver(); }
}
