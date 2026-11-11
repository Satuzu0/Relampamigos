using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;

// Perfil, saldo de tokens y personajes.
public static class PerfilRepository
{
    public static Task<PerfilJugador> CargarPerfil(int playerId)
    {
        return DatabaseManager.Usar(async con =>
        {
            var p = new PerfilJugador { PlayerId = playerId };
            using (var cmd = DbLeer.Comando(con, "SELECT * FROM v_player_public WHERE player_id = @id", "@id", playerId))
            using (var r = await cmd.ExecuteReaderAsync())
            {
                if (!await r.ReadAsync()) throw new ErrorJuego("No se encontró tu perfil.");
                p.Username = r.Txt("username");
                p.Nombre = string.IsNullOrEmpty(r.Txt("display_name")) ? p.Username : r.Txt("display_name");
                p.Xp = r.Ent("xp");
                p.Nivel = r.Ent("level_number");
                p.PersonajeId = r.EntN("selected_character_id");
                p.Partidas = r.Ent("games_played");
                p.Ganadas = r.Ent("games_won");
                p.GanadasInfiltrado = r.Ent("infiltrator_wins");
                p.Kills = r.Ent("kills");
                p.Tareas = r.Ent("tasks_completed");
                p.VotosCorrectos = r.Ent("correct_votes");
                p.Racha = r.Ent("login_streak");
            }

            // xp que pide el nivel actual y el siguiente, para la barra de progreso
            using (var cmd = DbLeer.Comando(con,
                "SELECT level_number, xp_required FROM level WHERE level_number IN (@n, @n + 1)", "@n", p.Nivel))
            using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    if (r.Ent("level_number") == p.Nivel) p.XpNivelActual = r.Ent("xp_required");
                    else p.XpSiguienteNivel = r.Ent("xp_required");
                }
            }
            return p;
        });
    }

    public static Task<long> CargarTokens(int playerId)
    {
        return DatabaseManager.Usar(async con =>
        {
            using (var cmd = DbLeer.Comando(con,
                "SELECT w.balance FROM player_wallet w JOIN currency c ON c.currency_id = w.currency_id " +
                "WHERE w.player_id = @id AND c.currency_code = 'TOKEN'", "@id", playerId))
            {
                object o = await cmd.ExecuteScalarAsync();
                return (o == null || o is System.DBNull) ? 0L : System.Convert.ToInt64(o);
            }
        });
    }

    // Recarga perfil + tokens y avisa a toda la UI
    public static async Task Refrescar()
    {
        if (!PlayerSession.Activa) return;
        var p = await CargarPerfil(PlayerSession.PlayerId);
        long t = await CargarTokens(PlayerSession.PlayerId);
        PlayerSession.ActualizarPerfil(p, t);
    }

    public static Task<List<PersonajeInfo>> ListarPersonajes()
    {
        return DatabaseManager.Usar(async con =>
        {
            var lista = new List<PersonajeInfo>();
            using (var cmd = DbLeer.Comando(con,
                "SELECT character_id, character_code, character_name, career, description, sprite_ref " +
                "FROM game_character WHERE status = 'active' ORDER BY character_id"))
            using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                    lista.Add(new PersonajeInfo
                    {
                        Id = r.Ent("character_id"),
                        Codigo = r.Txt("character_code"),
                        Nombre = r.Txt("character_name"),
                        Carrera = r.Txt("career"),
                        Descripcion = r.Txt("description"),
                        SpriteRef = r.Txt("sprite_ref")
                    });
            }
            return lista;
        });
    }

    // personajeId = 0 o null quita el personaje
    public static async Task ElegirPersonaje(int? personajeId)
    {
        int pid = PlayerSession.PlayerId;
        await DatabaseManager.Usar(con => DbLeer.Ejecutar(con, "CALL sp_select_character(@p, @c)",
            "@p", pid, "@c", personajeId.HasValue ? (object)personajeId.Value : null));
        await Refrescar();
    }
}
