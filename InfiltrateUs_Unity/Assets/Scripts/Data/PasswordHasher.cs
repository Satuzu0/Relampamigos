using System;
using System.Security.Cryptography;
using System.Text;

// Hash de claves con PBKDF2-SHA256. Formato: $pbkdf2-sha256$iteraciones$salt$hash
// (empieza con $ y pasa los 40 caracteres, asi cumple el check de la tabla player)
public static class PasswordHasher
{
    const int Iteraciones = 100000;

    public static string Hashear(string clave)
    {
        var salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
        byte[] hash;
        using (var kdf = new Rfc2898DeriveBytes(clave, salt, Iteraciones, HashAlgorithmName.SHA256))
            hash = kdf.GetBytes(32);
        return "$pbkdf2-sha256$" + Iteraciones + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
    }

    public static bool Verificar(string clave, string guardado)
    {
        try
        {
            var partes = guardado.Split('$');
            // ["", "pbkdf2-sha256", iter, salt, hash]
            if (partes.Length != 5 || partes[1] != "pbkdf2-sha256") return false;
            int iter = int.Parse(partes[2]);
            byte[] salt = Convert.FromBase64String(partes[3]);
            byte[] esperado = Convert.FromBase64String(partes[4]);
            byte[] calculado;
            using (var kdf = new Rfc2898DeriveBytes(clave, salt, iter, HashAlgorithmName.SHA256))
                calculado = kdf.GetBytes(esperado.Length);
            int diff = 0;
            for (int i = 0; i < esperado.Length; i++) diff |= esperado[i] ^ calculado[i];
            return diff == 0;
        }
        catch { return false; }
    }

    // Token de sesion: 32 bytes al azar. Al servidor solo le llega el SHA256 en hex (64 chars).
    public static void NuevoToken(out string token, out string tokenHash)
    {
        var raw = new byte[32];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(raw);
        token = Convert.ToBase64String(raw);
        using (var sha = SHA256.Create())
        {
            var h = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
            var sb = new StringBuilder();
            foreach (var b in h) sb.Append(b.ToString("x2"));
            tokenHash = sb.ToString();
        }
    }
}
