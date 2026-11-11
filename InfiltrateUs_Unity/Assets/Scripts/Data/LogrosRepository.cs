using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;

// Logros: el avance sale de las estadisticas del perfil y se sincroniza con sp_update_achievement_progress.
public static class LogrosRepository
{
    public static int ValorActual(PerfilJugador p, string condicion)
    {
        switch (condicion)
        {
            case "games_played": return p.Partidas;
            case "games_won": return p.Ganadas;
            case "tasks_completed": return p.Tareas;
            case "infiltrator_wins": return p.GanadasInfiltrado;
            case "correct_votes": return p.VotosCorrectos;
            case "kills": return p.Kills;
            case "login_streak": return p.Racha;
            case "level_reached": return p.Nivel;
        }
        return 0;
    }

    // Devuelve los logros y, de paso, desbloquea los que ya se cumplieron.
    public static async Task<List<LogroInfo>> CargarYSincronizar()
    {
        await PerfilRepository.Refrescar();
        var perfil = PlayerSession.Perfil;
        int pid = PlayerSession.PlayerId;
        bool huboRecompensa = false;

        var lista = await DatabaseManager.Usar(async con =>
        {
            var res = new List<LogroInfo>();
            string sql =
                "SELECT a.achievement_id, a.achievement_code, a.achievement_name, a.description, a.condition_type, " +
                "       a.target_value, a.reward_amount, pa.progress, pa.unlocked_at " +
                "FROM achievement a " +
                "LEFT JOIN player_achievement pa ON pa.achievement_id = a.achievement_id AND pa.player_id = @pid " +
                "WHERE a.is_active = 1 ORDER BY a.achievement_id";
            using (var cmd = DbLeer.Comando(con, sql, "@pid", pid))
            using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    var l = new LogroInfo
                    {
                        Id = r.Ent("achievement_id"),
                        Codigo = r.Txt("achievement_code"),
                        Nombre = r.Txt("achievement_name"),
                        Descripcion = r.Txt("description"),
                        Condicion = r.Txt("condition_type"),
                        Meta = r.Ent("target_value"),
                        Recompensa = r.Ent("reward_amount"),
                        Progreso = r.Ent("progress"),
                        Desbloqueado = !r.EsNulo("unlocked_at")
                    };
                    res.Add(l);
                }
            }

            foreach (var l in res)
            {
                if (l.Desbloqueado) { l.Progreso = l.Meta; continue; }
                int valor = ValorActual(perfil, l.Condicion);
                if (valor > l.Progreso)
                {
                    long desbloqueado = await DbLeer.LlamarConSalida(con,
                        "CALL sp_update_achievement_progress(@p, @a, @v, @out_u)", "@out_u",
                        "@p", pid, "@a", l.Id, "@v", valor);
                    l.Progreso = valor > l.Meta ? l.Meta : valor;
                    if (desbloqueado == 1) { l.Desbloqueado = true; if (l.Recompensa > 0) huboRecompensa = true; }
                }
            }
            return res;
        });

        if (huboRecompensa) await PerfilRepository.Refrescar();
        return lista;
    }
}
