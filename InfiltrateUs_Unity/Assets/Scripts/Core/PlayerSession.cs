using System;

// Datos de quien inicio sesion. Es estatico para que lo lea cualquier pantalla.
public static class PlayerSession
{
    public static bool Activa { get { return Perfil != null; } }
    public static PerfilJugador Perfil { get; private set; }
    public static string TokenHash { get; private set; }
    public static long Tokens { get; private set; }

    public static event Action AlCambiar;

    public static int PlayerId { get { return Perfil != null ? Perfil.PlayerId : 0; } }

    public static void Iniciar(PerfilJugador perfil, string tokenHash, long tokens)
    {
        Perfil = perfil;
        TokenHash = tokenHash;
        Tokens = tokens;
        if (AlCambiar != null) AlCambiar();
    }

    public static void ActualizarPerfil(PerfilJugador perfil, long tokens)
    {
        Perfil = perfil;
        Tokens = tokens;
        if (AlCambiar != null) AlCambiar();
    }

    public static void SetTokens(long tokens)
    {
        Tokens = tokens;
        if (AlCambiar != null) AlCambiar();
    }

    public static void Cerrar()
    {
        Perfil = null;
        TokenHash = null;
        Tokens = 0;
        if (AlCambiar != null) AlCambiar();
    }
}
