using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// La tienda: se paga con TOKEN y la compra la hace la BD (sp_purchase_item).
public class TiendaUI : PanelCatalogo
{
    public override PanelId Id { get { return PanelId.Tienda; } }
    public override string[] Atajos { get { return new[] { "ARRIBA/ABAJO|MOVER", "IZQ/DER|CATEGORÍA", "ENTER|COMPRAR", "ESC|ATRÁS" }; } }

    List<ProductoTienda> productos = new List<ProductoTienda>();
    List<ProductoTienda> visibles = new List<ProductoTienda>();
    List<KeyValuePair<int, string>> categorias = new List<KeyValuePair<int, string>>();   // id 0 = TODOS
    int pendiente = -1;   // producto al que ya le dimos un ENTER

    public override void Construir()
    {
        ConstruirBase();
        lista.alConfirmar = Comprar;
    }

    public override void AlMostrar()
    {
        UI.ActualizarFondoPersonaje();
        Cargar(0, 0);
    }

    void Cargar(int tabInicial, int filaInicial)
    {
        pendiente = -1;
        UI.Correr(() => TiendaRepository.ListarProductos(), lista2 =>
        {
            productos = lista2;
            categorias.Clear();
            categorias.Add(new KeyValuePair<int, string>(0, "TODOS"));
            foreach (var g in productos.GroupBy(x => x.CategoriaId))
                categorias.Add(new KeyValuePair<int, string>(g.Key, g.First().CategoriaNombre));
            PonerPestanas(categorias.Select(c => c.Value).ToList(), tabInicial);
            Rellenar(filaInicial);
        });
    }

    protected override void AlCambiarPestana() { Rellenar(0); }

    void Rellenar(int fila)
    {
        pendiente = -1;
        lista.Limpiar();
        int catId = categorias[tab].Key;
        visibles = productos.Where(x => catId == 0 || x.CategoriaId == catId).ToList();
        if (visibles.Count == 0)
        {
            LimpiarDetalle();
            MostrarVacio("No hay productos en esta categoría.");
            return;
        }
        MostrarVacio("");
        foreach (var p in visibles)
        {
            var f = lista.Agregar(p.Nombre.ToUpperInvariant());
            f.derecha.text = p.Comprado ? "COMPRADO" : p.Precio.ToString("N0");
            f.derecha.color = p.Comprado ? UiKit.Ok : UiKit.Oro;
        }
        lista.Seleccionar(Mathf.Min(fila, visibles.Count - 1));
    }

    protected override void AlSeleccionar(int i)
    {
        pendiente = -1;
        if (i < 0 || i >= visibles.Count) return;
        var p = visibles[i];
        string ext;
        Color col;
        if (p.Comprado) { ext = "YA LO TIENES"; col = UiKit.Ok; }
        else if (PlayerSession.Tokens < p.Precio) { ext = p.Precio.ToString("N0") + " TOKENS\nTE FALTAN " + (p.Precio - PlayerSession.Tokens).ToString("N0"); col = UiKit.Peligro; }
        else { ext = p.Precio.ToString("N0") + " TOKENS"; col = UiKit.Oro; }
        PonerDetalle(p.IconRef, p.Nombre, UiKit.NombreRareza(p.Rareza), UiKit.ColorRareza(p.Rareza), p.Descripcion, ext, col);
    }

    void Comprar(int i)
    {
        if (i < 0 || i >= visibles.Count) return;
        var p = visibles[i];
        if (p.Comprado) { Toast.Mostrar("Ya tienes " + p.Nombre + ".", UiKit.TextoSuave); return; }
        if (PlayerSession.Tokens < p.Precio) { Toast.Mostrar("No te alcanzan los tokens.", UiKit.Peligro); return; }

        // primer ENTER pregunta, segundo ENTER compra
        if (pendiente != p.ProductId)
        {
            pendiente = p.ProductId;
            Toast.Mostrar("¿Comprar " + p.Nombre + " por " + p.Precio.ToString("N0") + "? Presiona ENTER otra vez", UiKit.Oro);
            return;
        }
        pendiente = -1;
        int tabAhora = tab;
        UI.Correr(() => TiendaRepository.Comprar(p), () =>
        {
            Toast.Mostrar("Compraste " + p.Nombre + "!", UiKit.Ok);
            Cargar(tabAhora, i);
        });
    }
}
