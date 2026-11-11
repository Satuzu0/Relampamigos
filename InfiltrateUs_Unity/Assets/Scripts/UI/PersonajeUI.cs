using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Aqui se elige el personaje. Mientras recorres la lista, el personaje aparece en el fondo.
public class PersonajeUI : PanelBase
{
    public override PanelId Id { get { return PanelId.Personaje; } }
    public override string[] Atajos { get { return new[] { "ARRIBA/ABAJO|VER", "ENTER|ELEGIR", "ESC|ATRÁS" }; } }

    ListaNavegable lista;
    Text titulo, carrera, descripcion, estado;
    readonly List<PersonajeInfo> items = new List<PersonajeInfo>();   // la posicion 0 de la lista es NINGUNO

    public override void Construir()
    {
        lista = new ListaNavegable(transform, "Lista", 44, 24, 4);
        UiKit.Ubicar(lista.raiz, UiKit.ArribaIzq, 0, 0, 270, 356);
        lista.alCambiar = Previsualizar;
        lista.alConfirmar = ElegirEn;

        var tarjeta = UiKit.Caja(transform, "Tarjeta");
        UiKit.Ubicar(tarjeta, UiKit.ArribaIzq, 284, 0, 401, 200);

        titulo = UiKit.Txt(tarjeta, "Nombre", "", 26, UiKit.Acento, TextAnchor.MiddleLeft, true);
        UiKit.Ubicar(titulo.rectTransform, UiKit.ArribaIzq, 16, -12, 370, 40);
        titulo.horizontalOverflow = HorizontalWrapMode.Overflow;

        carrera = UiKit.Txt(tarjeta, "Carrera", "", 24, UiKit.Oro, TextAnchor.MiddleLeft, false);
        UiKit.Ubicar(carrera.rectTransform, UiKit.ArribaIzq, 16, -54, 370, 30);

        descripcion = UiKit.Txt(tarjeta, "Descripcion", "", 24, UiKit.Texto, TextAnchor.UpperLeft, false);
        UiKit.Ubicar(descripcion.rectTransform, UiKit.ArribaIzq, 16, -88, 370, 104);

        estado = UiKit.Txt(transform, "Estado", "", 26, UiKit.Ok, TextAnchor.MiddleLeft, true);
        UiKit.Ubicar(estado.rectTransform, UiKit.ArribaIzq, 284, -214, 401, 40);
        estado.fontSize = 18;
    }

    public override void AlMostrar()
    {
        lista.Limpiar();
        items.Clear();
        lista.Agregar("NINGUNO");
        foreach (var p in UI.Personajes)
        {
            items.Add(p);
            lista.Agregar(p.Nombre.ToUpperInvariant());
        }

        // arrancamos parados en el personaje que ya tiene elegido
        int inicio = 0;
        if (PlayerSession.Perfil.PersonajeId.HasValue)
        {
            int k = items.FindIndex(x => x.Id == PlayerSession.Perfil.PersonajeId.Value);
            if (k >= 0) inicio = k + 1;
        }
        lista.Seleccionar(inicio);
        MarcarElegido();
    }

    public override void AlOcultar()
    {
        // si salimos sin confirmar, el fondo vuelve a lo que esta guardado
        UI.ActualizarFondoPersonaje();
    }

    PersonajeInfo En(int indiceLista) { return indiceLista <= 0 ? null : items[indiceLista - 1]; }

    void Previsualizar(int i)
    {
        var p = En(i);
        UI.VistaPreviaPersonaje(p);
        if (p == null)
        {
            titulo.text = "NINGUNO";
            carrera.text = "";
            descripcion.text = "Sin personaje de fondo. Elige uno cuando quieras.";
        }
        else
        {
            titulo.text = p.Nombre.ToUpperInvariant();
            carrera.text = p.Carrera;
            descripcion.text = p.Descripcion;
        }
        MarcarElegido();
    }

    void MarcarElegido()
    {
        int? elegido = PlayerSession.Perfil != null ? PlayerSession.Perfil.PersonajeId : null;
        var p = En(lista.Indice);
        bool esElegido = (p == null && !elegido.HasValue) || (p != null && elegido.HasValue && p.Id == elegido.Value);
        estado.text = esElegido ? "ES TU PERSONAJE ACTUAL" : "ENTER PARA ELEGIRLO";
        estado.color = esElegido ? UiKit.Ok : UiKit.TextoSuave;

        for (int k = 0; k < lista.filas.Count; k++)
        {
            var pk = En(k);
            bool marcado = (pk == null && !elegido.HasValue) || (pk != null && elegido.HasValue && pk.Id == elegido.Value);
            lista.filas[k].derecha.text = marcado ? "[X]" : "";
        }
    }

    void ElegirEn(int i)
    {
        var p = En(i);
        int? id = p != null ? (int?)p.Id : null;
        UI.Correr(() => PerfilRepository.ElegirPersonaje(id), () =>
        {
            UI.ActualizarFondoPersonaje();
            MarcarElegido();
            Toast.Mostrar(p != null ? p.Nombre + " es tu personaje ahora" : "Personaje quitado", UiKit.Ok);
        });
    }

    public override void Arriba() { lista.Mover(-1); }
    public override void Abajo() { lista.Mover(1); }
    public override void Confirmar() { lista.Confirmar(); }
    public override void Atras() { UI.Volver(); }
}
