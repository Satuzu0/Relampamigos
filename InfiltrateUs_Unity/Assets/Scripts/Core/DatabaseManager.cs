using System;
using System.Threading.Tasks;
using MySqlConnector;
using UnityEngine;

// Se encarga de abrir conexiones a MariaDB. Los datos se editan en el Inspector.
public class DatabaseManager : MonoBehaviour
{
    public static DatabaseManager Instancia { get; private set; }

    [Header("Conexión (XAMPP local)")]
    public string servidor = "127.0.0.1";
    public uint puerto = 3306;
    public string baseDatos = "infiltrate_us_v1";
    public string usuario = "root";
    public string clave = "";

    [Header("Juego")]
    public int tokensDeBienvenida = 1000;

    string cadena;

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        DontDestroyOnLoad(gameObject);
        Reconstruir();
    }

    public void Reconstruir()
    {
        var b = new MySqlConnectionStringBuilder
        {
            Server = servidor,
            Port = puerto,
            Database = baseDatos,
            UserID = usuario,
            Password = clave,
            SslMode = MySqlSslMode.None,
            AllowUserVariables = true,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 15,
            CharacterSet = "utf8mb4"
        };
        cadena = b.ConnectionString;
    }

    // Abre una conexion nueva. Quien la pida la cierra con using.
    public async Task<MySqlConnection> AbrirAsync()
    {
        if (string.IsNullOrEmpty(cadena)) Reconstruir();
        var con = new MySqlConnection(cadena);
        await con.OpenAsync();
        return con;
    }

    // Atajo: abre, corre tu lógica y cierra. Si algo falla lo traduce a un mensaje en español.
    public static async Task<T> Usar<T>(Func<MySqlConnection, Task<T>> trabajo)
    {
        if (Instancia == null) throw new ErrorJuego("Falta el objeto DatabaseManager en la escena.");
        try
        {
            using (var con = await Instancia.AbrirAsync())
            {
                return await trabajo(con);
            }
        }
        catch (ErrorJuego) { throw; }
        catch (Exception ex)
        {
            throw Errores.Traducir(ex);
        }
    }

    public static async Task Usar(Func<MySqlConnection, Task> trabajo)
    {
        await Usar<bool>(async c => { await trabajo(c); return true; });
    }
}
