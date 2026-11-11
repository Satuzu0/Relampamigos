using UnityEngine;
using UnityEngine.UI;

// Menu principal: la lista de la izquierda y la tarjeta del jugador a la derecha.
public class MenuUI : PanelBase
{
    public override PanelId Id { get { return PanelId.Menu; } }
    public override string[] Atajos { get { return new[] { "ARRIBA/ABAJO|MOVER", "ENTER|ELEGIR" }; } }

    ListaNavegable lista;
    Text nombre, nivel, xpTexto, stats;
    Image barra;
    const float AnchoBarra = 340f;

    static readonly string[] opciones = { "JUGAR", "PERSONAJE", "VESTUARIO", "TIENDA", "LOGROS", "AJUSTES", "CRÉDITOS", "SALIR" };

    public override void Construir()
    {
        lista = new ListaNavegable(transform, "Lista", 38, 24, 3);
        UiKit.Ubicar(lista.raiz, UiKit.ArribaIzq, 0, 0, 290, 356);
        foreach (var o in opciones) lista.Agregar(o);
        lista.alConfirmar = Elegir;

        var tarjeta = UiKit.Caja(transform, "Tarjeta");
        UiKit.Ubicar(tarjeta, UiKit.ArribaIzq, 304, 0, 381, 356);

        nombre = UiKit.Txt(tarjeta, "Nombre", "", 24, UiKit.Acento, TextAnchor.MiddleLeft, true);
        UiKit.Ubicar(nombre.rectTransform, UiKit.ArribaIzq, 16, -12, 350, 40);
        nombre.horizontalOverflow = HorizontalWrapMode.Overflow;

        nivel = UiKit.Txt(tarjeta, "Nivel", "", 26, UiKit.Texto, TextAnchor.MiddleLeft, false);
        UiKit.Ubicar(nivel.rectTransform, UiKit.ArribaIzq, 16, -54, 350, 30);

        barra = UiKit.Barra(tarjeta, "Xp", AnchoBarra, 18, UiKit.Acento);
        UiKit.Ubicar(barra.transform.parent as RectTransform, UiKit.ArribaIzq, 16, -88, AnchoBarra, 18);

        xpTexto = UiKit.Txt(tarjeta, "XpTexto", "", 20, UiKit.TextoSuave, TextAnchor.MiddleLeft, false);
        UiKit.Ubicar(xpTexto.rectTransform, UiKit.ArribaIzq, 16, -110, 350, 24);

        stats = UiKit.Txt(tarjeta, "Stats", "", 24, UiKit.Texto, TextAnchor.UpperLeft, false);
        UiKit.Ubicar(stats.rectTransform, UiKit.ArribaIzq, 16, -146, 350, 200);
    }

    void OnEnable() { PlayerSession.AlCambiar += Refrescar; }
    void OnDisable() { PlayerSession.AlCambiar -= Refrescar; }

    public override void AlMostrar()
    {
        if (lista.Indice < 0) lista.Seleccionar(0);
        UI.ActualizarFondoPersonaje();
        Refrescar();
    }

    void Refrescar()
    {
        var p = PlayerSession.Perfil;
        if (p == null || nombre == null) return;

        string personaje = "sin elegir";
        if (p.PersonajeId.HasValue)
        {
            var info = UI.Personajes.Find(x => x.Id == p.PersonajeId.Value);
            if (info != null) personaje = info.Nombre;
        }

        nombre.text = p.Username.ToUpperInvariant();
        nivel.text = "NIVEL " + p.Nivel;
        UiKit.PonerBarra(barra, p.ProgresoNivel, AnchoBarra);
        xpTexto.text = p.XpSiguienteNivel > p.XpNivelActual ? p.Xp + " / " + p.XpSiguienteNivel + " XP" : p.Xp + " XP (nivel máximo)";
        stats.text =
            "<color=#9fc7d4>Personaje</color>  " + personaje + "\n" +
            "<color=#9fc7d4>Partidas</color>  " + p.Partidas + "   <color=#9fc7d4>Ganadas</color>  " + p.Ganadas + "\n" +
            "<color=#9fc7d4>Como infiltrado</color>  " + p.GanadasInfiltrado + "\n" +
            "<color=#9fc7d4>Tareas</color>  " + p.Tareas + "   <color=#9fc7d4>Racha</color>  " + p.Racha;
    }

    void Elegir(int i)
    {
        switch (opciones[i])
        {
            case "JUGAR": UI.Ir(PanelId.Salas); break;
            case "PERSONAJE": UI.Ir(PanelId.Personaje); break;
            case "VESTUARIO": UI.Ir(PanelId.Vestuario); break;
            case "TIENDA": UI.Ir(PanelId.Tienda); break;
            case "LOGROS": UI.Ir(PanelId.Logros); break;
            case "AJUSTES": UI.Ir(PanelId.Ajustes); break;
            case "CRÉDITOS": UI.Ir(PanelId.Creditos); break;
            case "SALIR": UI.SalirDelJuego(); break;
        }
    }

    public override void Arriba() { lista.Mover(-1); }
    public override void Abajo() { lista.Mover(1); }
    public override void Confirmar() { lista.Confirmar(); }
}
