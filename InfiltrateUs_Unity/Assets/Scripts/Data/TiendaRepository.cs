using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;

// Tienda, inventario y vestuario: sp_purchase_item, sp_equip_item, sp_unequip_category.
public static class TiendaRepository
{
    public static Task<List<ProductoTienda>> ListarProductos()
    {
        int pid = PlayerSession.PlayerId;
        return DatabaseManager.Usar(async con =>
        {
            var lista = new List<ProductoTienda>();
            string sql =
                "SELECT p.product_id, p.item_id, i.item_code, i.item_name, i.description, i.rarity, i.icon_ref, " +
                "       i.category_id, c.category_code, c.category_name, p.price, " +
                "       EXISTS(SELECT 1 FROM player_inventory inv WHERE inv.player_id = @pid AND inv.item_id = p.item_id) AS owned " +
                "FROM shop_product p " +
                "JOIN cosmetic_item i ON i.item_id = p.item_id " +
                "JOIN cosmetic_category c ON c.category_id = i.category_id " +
                "WHERE p.is_active = 1 AND i.is_active = 1 " +
                "  AND (p.available_from IS NULL OR p.available_from <= NOW()) " +
                "  AND (p.available_until IS NULL OR p.available_until > NOW()) " +
                "ORDER BY c.sort_order, p.price, i.item_name";
            using (var cmd = DbLeer.Comando(con, sql, "@pid", pid))
            using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                    lista.Add(new ProductoTienda
                    {
                        ProductId = r.Ent("product_id"),
                        ItemId = r.Ent("item_id"),
                        Codigo = r.Txt("item_code"),
                        Nombre = r.Txt("item_name"),
                        Descripcion = r.Txt("description"),
                        Rareza = r.Txt("rarity"),
                        IconRef = r.Txt("icon_ref"),
                        CategoriaId = r.Ent("category_id"),
                        CategoriaCodigo = r.Txt("category_code"),
                        CategoriaNombre = r.Txt("category_name"),
                        Precio = r.Ent("price"),
                        Comprado = r.Bool("owned")
                    });
            }
            return lista;
        });
    }

    public static async Task Comprar(ProductoTienda prod)
    {
        int pid = PlayerSession.PlayerId;
        // La llave evita cobrar dos veces si se repite el click
        string llave = "buy:" + pid + ":" + prod.ProductId + ":" + Guid.NewGuid().ToString("N").Substring(0, 12);
        await DatabaseManager.Usar(con => DbLeer.LlamarConSalida(con,
            "CALL sp_purchase_item(@p_id, @p_prod, @p_key, @out_pur)", "@out_pur",
            "@p_id", pid, "@p_prod", prod.ProductId, "@p_key", llave));
        await PerfilRepository.Refrescar();
    }

    public static Task<List<ItemInventario>> ListarInventario()
    {
        int pid = PlayerSession.PlayerId;
        return DatabaseManager.Usar(async con =>
        {
            var lista = new List<ItemInventario>();
            string sql =
                "SELECT i.item_id, i.item_code, i.item_name, i.description, i.rarity, i.icon_ref, " +
                "       i.category_id, c.category_code, c.category_name, (pe.item_id IS NOT NULL) AS equipped " +
                "FROM player_inventory inv " +
                "JOIN cosmetic_item i ON i.item_id = inv.item_id " +
                "JOIN cosmetic_category c ON c.category_id = i.category_id " +
                "LEFT JOIN player_equipment pe ON pe.player_id = inv.player_id AND pe.item_id = inv.item_id " +
                "WHERE inv.player_id = @pid " +
                "ORDER BY c.sort_order, i.item_name";
            using (var cmd = DbLeer.Comando(con, sql, "@pid", pid))
            using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                    lista.Add(new ItemInventario
                    {
                        ItemId = r.Ent("item_id"),
                        Codigo = r.Txt("item_code"),
                        Nombre = r.Txt("item_name"),
                        Descripcion = r.Txt("description"),
                        Rareza = r.Txt("rarity"),
                        IconRef = r.Txt("icon_ref"),
                        CategoriaId = r.Ent("category_id"),
                        CategoriaCodigo = r.Txt("category_code"),
                        CategoriaNombre = r.Txt("category_name"),
                        Equipado = r.Bool("equipped")
                    });
            }
            return lista;
        });
    }

    public static Task Equipar(int itemId)
    {
        int pid = PlayerSession.PlayerId;
        return DatabaseManager.Usar(con => DbLeer.Ejecutar(con, "CALL sp_equip_item(@p, @i)", "@p", pid, "@i", itemId));
    }

    public static Task Desequipar(int categoriaId)
    {
        int pid = PlayerSession.PlayerId;
        return DatabaseManager.Usar(con => DbLeer.Ejecutar(con, "CALL sp_unequip_category(@p, @c)", "@p", pid, "@c", categoriaId));
    }
}
