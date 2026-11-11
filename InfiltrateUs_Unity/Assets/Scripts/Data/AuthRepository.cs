using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MySqlConnector;

// Registro, login y logout contra los procedimientos sp_register_player, sp_get_login_data,
// sp_login_succeeded, sp_login_failed y sp_logout.
public static class AuthRepository
{
    static readonly Regex reUsuario = new Regex("^[A-Za-z0-9_.-]{3,30}$");

    public static async Task<int> Registrar(string usuario, string correo, string clave)
    {
        usuario = (usuario ?? "").Trim();
        correo = (correo ?? "").Trim();
        if (!reUsuario.IsMatch(usuario)) throw new ErrorJuego("Usuario inválido: 3 a 30 caracteres con letras, números, _ . o -");
        if (correo.Length > 0 && !Regex.IsMatch(correo, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) throw new ErrorJuego("El correo no parece válido.");
        if (string.IsNullOrEmpty(clave) || clave.Length < 6) throw new ErrorJuego("La contraseña necesita al menos 6 caracteres.");

        // El hash tarda un poquito, mejor fuera del hilo principal
        string hash = await Task.Run(() => PasswordHasher.Hashear(clave));
        int bono = DatabaseManager.Instancia != null ? DatabaseManager.Instancia.tokensDeBienvenida : 0;

        return await DatabaseManager.Usar(async con =>
        {
            long id = await DbLeer.LlamarConSalida(con,
                "CALL sp_register_player(@p_user, @p_mail, @p_hash, @out_id)", "@out_id",
                "@p_user", usuario, "@p_mail", correo, "@p_hash", hash);

            if (bono > 0)
            {
                long tokenId = 0;
                using (var cmd = new MySqlCommand("SELECT currency_id FROM currency WHERE currency_code = 'TOKEN'", con))
                {
                    object o = await cmd.ExecuteScalarAsync();
                    if (o != null && !(o is DBNull)) tokenId = Convert.ToInt64(o);
                }
                if (tokenId > 0)
                {
                    await DbLeer.LlamarConSalida(con,
                        "CALL sp_wallet_apply(@p_id, @p_cur, @p_amt, 'welcome_bonus', NULL, NULL, @p_key, @out_tx, @out_rep)", "@out_tx",
                        "@p_id", id, "@p_cur", tokenId, "@p_amt", (long)bono, "@p_key", "welcome:" + id);
                }
            }
            return (int)id;
        });
    }

    public static async Task Login(string usuario, string clave)
    {
        usuario = (usuario ?? "").Trim();
        if (usuario.Length == 0 || string.IsNullOrEmpty(clave)) throw new ErrorJuego("Escribe tu usuario y tu contraseña.");

        string tokenHash = null;
        int playerId = 0;

        await DatabaseManager.Usar(async con =>
        {
            string hashGuardado = null;
            string estado = null;
            using (var cmd = DbLeer.Comando(con, "CALL sp_get_login_data(@p_user)", "@p_user", usuario))
            using (var r = await cmd.ExecuteReaderAsync())
            {
                if (await r.ReadAsync())
                {
                    playerId = r.Ent("player_id");
                    hashGuardado = r.Txt("password_hash");
                    estado = r.Txt("account_status");
                }
            }

            if (hashGuardado == null) throw new ErrorJuego("Usuario o contraseña incorrectos.");
            if (estado != "active") throw new ErrorJuego("Tu cuenta no está activa.");

            bool ok = await Task.Run(() => PasswordHasher.Verificar(clave, hashGuardado));
            if (!ok)
            {
                await DbLeer.Ejecutar(con, "CALL sp_login_failed(@p_id, 5, 300)", "@p_id", playerId);
                throw new ErrorJuego("Usuario o contraseña incorrectos.");
            }

            string token;
            PasswordHasher.NuevoToken(out token, out tokenHash);
            await DbLeer.LlamarConSalida(con,
                "CALL sp_login_succeeded(@p_id, @p_tok, 86400, NULL, @p_ver, @out_sid)", "@out_sid",
                "@p_id", playerId, "@p_tok", tokenHash, "@p_ver", UnityEngine.Application.version);
        });

        var perfil = await PerfilRepository.CargarPerfil(playerId);
        long tokens = await PerfilRepository.CargarTokens(playerId);
        PlayerSession.Iniciar(perfil, tokenHash, tokens);
    }

    public static async Task Logout()
    {
        string th = PlayerSession.TokenHash;
        PlayerSession.Cerrar();
        if (string.IsNullOrEmpty(th)) return;
        try { await DatabaseManager.Usar(con => DbLeer.Ejecutar(con, "CALL sp_logout(@p_tok)", "@p_tok", th)); }
        catch { /* si falla no pasa nada, la sesion expira sola XD */ }
    }
}
