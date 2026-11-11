using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Tu inventario: aqui te pones o te quitas lo que compraste. Todo sale de la BD.
public class VestuarioUI : PanelCatalogo
{
    public override PanelId Id { get { return PanelId.Vestuario; } }
    public override string[] Atajos { get { return new[] { "ARRIBA/ABAJO|MOVER", "IZQ/DER|CATEGORÍA", "ENTER|PONER/QUITAR", "ESC|ATRÁS" }; } }

    List<ItemInventario> inventario = new List<ItemInventario>();
    List<ItemInventario> visibles = new List<ItemInventario>();
    List<KeyValuePair<int, string>> categorias = new List<KeyValuePair<int, string>>();

    public override void Construir()
    {
        ConstruirBase();
        lista.alConfirmar = Alternar;
    }

    public override void AlMostrar()
    {
        UI.ActualizarFondoPersonaje();
        Cargar(0, 0);
    }

    void Cargar(int tabInicial, int filaInicial)
    {
        UI.Correr(() => TiendaRepository.ListarInventario(), inv =>
        {
            inventario = inv;
            categorias = inv.GroupBy(x => x.CategoriaId)
                .Select(g => new KeyValuePair<int, string>(g.Key, g.First().CategoriaNombre)).ToList();
            PonerPestanas(categorias.Select(c => c.Value).ToList(), tabInicial);
            Rellenar(filaInicial);
        });
    }

    protected override void AlCambiarPestana() { Rellenar(0); }

    void Rellenar(int fila)
    {
        lista.Limpiar();
        visibles.Clear();
        if (categorias.Count == 0)
        {
            LimpiarDetalle();
            MostrarVacio("Todavía no tienes objetos.\nPasa por la TIENDA para comprar algo.");
            return;
        }
        MostrarVacio("");
        int catId = categorias[tab].Key;
        visibles = inventario.Where(x => x.CategoriaId == catId).ToList();
        foreach (var it in visibles)
        {
            var f = lista.Agregar(it.Nombre.ToUpperInvariant());
            f.derecha.text = it.Equipado ? "EQUIPADO" : "";
            f.derecha.color = UiKit.Ok;
        }
        lista.Seleccionar(Mathf.Min(fila, visibles.Count - 1));
    }

    protected override void AlSeleccionar(int i)
    {
        if (i < 0 || i >= visibles.Count) return;
        var it = visibles[i];
        PonerDetalle(it.IconRef, it.Nombre, UiKit.NombreRareza(it.Rareza), UiKit.ColorRareza(it.Rareza), it.Descripcion,
            it.Equipado ? "EQUIPADO" : "ENTER PARA PONERTELO", it.Equipado ? UiKit.Ok : UiKit.TextoSuave);
    }

    void Alternar(int i)
    {
        if (i < 0 || i >= visibles.Count) return;
        var it = visibles[i];
        int tabAhora = tab;
        UI.Correr(async () =>
        {
            if (it.Equipado) await TiendaRepository.Desequipar(it.CategoriaId);
            else await TiendaRepository.Equipar(it.ItemId);
        }, () =>
        {
            Toast.Mostrar(it.Equipado ? it.Nombre + " guardado" : it.Nombre + " puesto", UiKit.Ok);
            Cargar(tabAhora, i);
        });
    }
}
