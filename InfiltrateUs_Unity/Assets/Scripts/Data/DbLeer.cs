using System;
using MySqlConnector;

// Atajos para leer columnas por nombre sin repetir IsDBNull a cada rato.
public static class DbLeer
{
    public static string Txt(this MySqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i));
    }

    public static int Ent(this MySqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));
    }

    public static long Lar(this MySqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? 0L : Convert.ToInt64(r.GetValue(i));
    }

    public static int? EntN(this MySqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        if (r.IsDBNull(i)) return null;
        return Convert.ToInt32(r.GetValue(i));
    }

    public static bool Bool(this MySqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return !r.IsDBNull(i) && Convert.ToInt64(r.GetValue(i)) != 0;
    }

    public static bool EsNulo(this MySqlDataReader r, string col)
    {
        return r.IsDBNull(r.GetOrdinal(col));
    }

    // Ejecuta un CALL con parametros normales y devuelve el valor de la variable OUT.
    public static async System.Threading.Tasks.Task<long> LlamarConSalida(MySqlConnection con, string sqlCall, string variableOut, params object[] paresNombreValor)
    {
        using (var cmd = new MySqlCommand(sqlCall, con))
        {
            for (int i = 0; i + 1 < paresNombreValor.Length; i += 2)
                cmd.Parameters.AddWithValue((string)paresNombreValor[i], paresNombreValor[i + 1] ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        using (var q = new MySqlCommand("SELECT " + variableOut, con))
        {
            object o = await q.ExecuteScalarAsync();
            return (o == null || o is DBNull) ? 0L : Convert.ToInt64(o);
        }
    }

    public static async System.Threading.Tasks.Task Ejecutar(MySqlConnection con, string sql, params object[] paresNombreValor)
    {
        using (var cmd = new MySqlCommand(sql, con))
        {
            for (int i = 0; i + 1 < paresNombreValor.Length; i += 2)
                cmd.Parameters.AddWithValue((string)paresNombreValor[i], paresNombreValor[i + 1] ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public static MySqlCommand Comando(MySqlConnection con, string sql, params object[] paresNombreValor)
    {
        var cmd = new MySqlCommand(sql, con);
        for (int i = 0; i + 1 < paresNombreValor.Length; i += 2)
            cmd.Parameters.AddWithValue((string)paresNombreValor[i], paresNombreValor[i + 1] ?? DBNull.Value);
        return cmd;
    }
}
