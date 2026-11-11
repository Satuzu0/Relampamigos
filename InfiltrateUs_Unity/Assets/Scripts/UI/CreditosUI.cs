using UnityEngine;
using UnityEngine.UI;

// Edita estas lineas para poner los nombres de tu equipo XD
public class CreditosUI : PanelBase
{
    public override PanelId Id { get { return PanelId.Creditos; } }
    public override string[] Atajos { get { return new[] { "ESC|ATRÁS" }; } }

    const string Texto =
        "<color=#9fc7d4>Juego social 2D multijugador</color>\n" +
        "<color=#9fc7d4>Proyecto de Ingeniería de Sistemas · UNIFRANZ, La Paz</color>\n\n" +
        "<color=#f8bc38>DESARROLLO</color>\n" +
        "Ahias Benjamín Nogales Morales (Satuzu)\n\n" +
        "<color=#f8bc38>TECNOLOGÍA</color>\n" +
        "Unity 6 · C# · MariaDB · MySqlConnector\n\n" +
        "<color=#9fc7d4>Gracias por jugar!</color>";

    public override void Construir()
    {
        var caja = UiKit.Caja(transform, "Caja");
        UiKit.Estirar(caja);

        var t = UiKit.Txt(caja, "Titulo", "INFILTRATE US", 30, UiKit.Acento, TextAnchor.MiddleCenter, true);
        UiKit.Ubicar(t.rectTransform, UiKit.ArribaCentro, 0, -14, 660, 44);

        var c = UiKit.Txt(caja, "Texto", Texto, 24, UiKit.Texto, TextAnchor.UpperCenter, false);
        UiKit.Ubicar(c.rectTransform, UiKit.ArribaCentro, 0, -64, 660, 280);
    }

    public override void Atras() { UI.Volver(); }
    public override void Confirmar() { UI.Volver(); }
}
