using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using MySqlConnector;

// Salas LAN: sp_create_room, sp_join_room, sp_leave_room, sp_start_match y la vista v_room_lobby.
public static class SalaRepository
{
    public const int PuertoJuego = 7777;

    // IP de la PC en la red local, es la que se guarda para que los demas se conecten luego con Netcode
    public static string IpLocal()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork) return ua.Address.ToString();
            }
        }
        catch { }
        return "127.0.0.1";
    }

    static SalaInfo LeerSala(MySqlDataReader r)
    {
        return new SalaInfo
        {
            RoomId = r.Ent("room_id"),
            Codigo = r.Txt("room_code"),
            MapId = r.Ent("map_id"),
            ModoId = r.Ent("mode_id"),
            Estado = r.Txt("room_state"),
            Privada = r.Bool("is_private"),
            Min = r.Ent("min_players"),
            Max = r.Ent("max_players"),
            Direccion = r.Txt("host_address"),
            Puerto = r.Ent("host_port"),
            Host = r.Txt("host_username"),
            Jugadores = r.Ent("current_players")
        };
    }

    public static Task<List<ColorInfo>> ListarColores()
    {
        return DatabaseManager.Usar(async con =>
        {
            var l = new List<ColorInfo>();
            using (var cmd = DbLeer.Comando(con, "SELECT color_id, color_code, display_name, hex_code FROM color_palette WHERE is_active = 1 ORDER BY color_id"))
            using (var r = await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    l.Add(new ColorInfo { Id = r.Ent("color_id"), Codigo = r.Txt("color_code"), Nombre = r.Txt("display_name"), Hex = r.Txt("hex_code") });
            return l;
        });
    }

    public static Task<SalaInfo> MiSala()
    {
        int pid = PlayerSession.PlayerId;
        return DatabaseManager.Usar(async con =>
        {
            using (var cmd = DbLeer.Comando(con,
                "SELECT v.* FROM v_room_lobby v JOIN room_member m ON m.room_id = v.room_id WHERE m.player_id = @p", "@p", pid))
            using (var r = await cmd.ExecuteReaderAsync())
                return await r.ReadAsync() ? LeerSala(r) : null;
        });
    }

    public static Task<List<SalaInfo>> ListarPublicas()
    {
        return DatabaseManager.Usar(async con =>
        {
            var l = new List<SalaInfo>();
            using (var cmd = DbLeer.Comando(con,
                "SELECT * FROM v_room_lobby WHERE is_private = 0 AND room_state = 'waiting' ORDER BY created_at DESC LIMIT 30"))
            using (var r = await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync()) l.Add(LeerSala(r));
            return l;
        });
    }

    public static Task<List<MiembroSala>> Miembros(int roomId)
    {
        return DatabaseManager.Usar(async con =>
        {
            var l = new List<MiembroSala>();
            string sql =
                "SELECT m.player_id, p.username, m.color_id, c.hex_code, m.is_ready, m.connection_status, " +
                "       (r.host_player_id = m.player_id) AS es_host " +
                "FROM room_member m " +
                "JOIN game_room r ON r.room_id = m.room_id " +
                "JOIN player p ON p.player_id = m.player_id " +
                "LEFT JOIN color_palette c ON c.color_id = m.color_id " +
                "WHERE m.room_id = @r ORDER BY m.joined_at, m.player_id";
            using (var cmd = DbLeer.Comando(con, sql, "@r", roomId))
            using (var rd = await cmd.ExecuteReaderAsync())
                while (await rd.ReadAsync())
                    l.Add(new MiembroSala
                    {
                        PlayerId = rd.Ent("player_id"),
                        Username = rd.Txt("username"),
                        ColorId = rd.Ent("color_id"),
                        ColorHex = rd.Txt("hex_code"),
                        Listo = rd.Bool("is_ready"),
                        Conectado = rd.Txt("connection_status") == "connected",
                        EsHost = rd.Bool("es_host")
                    });
            return l;
        });
    }

    public static Task<SalaInfo> Crear(bool privada)
    {
        int pid = PlayerSession.PlayerId;
        string ip = IpLocal();
        return DatabaseManager.Usar(async con =>
        {
            int mapa = 0, minJ = 1, maxJ = 10, modo = 0;
            using (var cmd = DbLeer.Comando(con, "SELECT map_id, min_players, max_players FROM map WHERE is_active = 1 ORDER BY map_id LIMIT 1"))
            using (var r = await cmd.ExecuteReaderAsync())
                if (await r.ReadAsync()) { mapa = r.Ent("map_id"); minJ = r.Ent("min_players"); maxJ = r.Ent("max_players"); }
            using (var cmd = DbLeer.Comando(con, "SELECT mode_id FROM game_mode WHERE is_active = 1 ORDER BY mode_id LIMIT 1"))
            {
                object o = await cmd.ExecuteScalarAsync();
                if (o != null && !(o is DBNull)) modo = Convert.ToInt32(o);
            }
            if (mapa == 0) throw new ErrorJuego("No hay mapas disponibles. Corre el SQL de semillas.");
            if (modo == 0) throw new ErrorJuego("No hay modo de juego disponible.");

            await DbLeer.Ejecutar(con,
                "CALL sp_create_room(@p_host, @p_map, @p_mode, @p_priv, @p_min, @p_max, @p_ip, @p_port, @out_room, @out_code)",
                "@p_host", pid, "@p_map", mapa, "@p_mode", modo, "@p_priv", privada ? 1 : 0,
                "@p_min", minJ, "@p_max", maxJ, "@p_ip", ip, "@p_port", PuertoJuego);

            int roomId; string codigo;
            using (var cmd = new MySqlCommand("SELECT @out_room, @out_code", con))
            using (var r = await cmd.ExecuteReaderAsync())
            {
                await r.ReadAsync();
                roomId = Convert.ToInt32(r.GetValue(0));
                codigo = Convert.ToString(r.GetValue(1));
            }

            // El anfitrion arranca con el primer color libre
            await DbLeer.Ejecutar(con,
                "UPDATE room_member SET color_id = (SELECT MIN(color_id) FROM color_palette WHERE is_active = 1) WHERE room_id = @r AND player_id = @p",
                "@r", roomId, "@p", pid);

            return new SalaInfo { RoomId = roomId, Codigo = codigo, Max = maxJ, Min = minJ, Jugadores = 1 };
        });
    }

    public static Task Unirse(string codigo)
    {
        codigo = (codigo ?? "").Trim().ToUpperInvariant();
        if (codigo.Length != 6) throw new ErrorJuego("El código de sala tiene 6 caracteres.");
        int pid = PlayerSession.PlayerId;
        return DatabaseManager.Usar(async con =>
        {
            long colorLibre = 0;
            using (var cmd = DbLeer.Comando(con,
                "SELECT color_id FROM color_palette WHERE is_active = 1 AND color_id NOT IN " +
                "(SELECT m.color_id FROM room_member m JOIN game_room r ON r.room_id = m.room_id WHERE r.room_code = @c AND m.color_id IS NOT NULL) " +
                "ORDER BY color_id LIMIT 1", "@c", codigo))
            {
                object o = await cmd.ExecuteScalarAsync();
                if (o != null && !(o is DBNull)) colorLibre = Convert.ToInt64(o);
            }
            await DbLeer.LlamarConSalida(con, "CALL sp_join_room(@p_code, @p_id, @p_color, @out_room)", "@out_room",
                "@p_code", codigo, "@p_id", pid, "@p_color", colorLibre > 0 ? (object)colorLibre : null);
        });
    }

    public static Task Salir()
    {
        int pid = PlayerSession.PlayerId;
        return DatabaseManager.Usar(con => DbLeer.Ejecutar(con, "CALL sp_leave_room(@p)", "@p", pid));
    }

    public static Task SetListo(int roomId, bool listo)
    {
        int pid = PlayerSession.PlayerId;
        return DatabaseManager.Usar(con => DbLeer.Ejecutar(con,
            "UPDATE room_member SET is_ready = @l WHERE room_id = @r AND player_id = @p", "@l", listo ? 1 : 0, "@r", roomId, "@p", pid));
    }

    public static Task CambiarColor(int roomId, int colorId)
    {
        int pid = PlayerSession.PlayerId;
        return DatabaseManager.Usar(con => DbLeer.Ejecutar(con,
            "UPDATE room_member SET color_id = @c WHERE room_id = @r AND player_id = @p", "@c", colorId, "@r", roomId, "@p", pid));
    }

    public static Task<long> Iniciar(int roomId)
    {
        int pid = PlayerSession.PlayerId;
        return DatabaseManager.Usar(con => DbLeer.LlamarConSalida(con,
            "CALL sp_start_match(@p_room, @p_host, @out_match)", "@out_match", "@p_room", roomId, "@p_host", pid));
    }
}
