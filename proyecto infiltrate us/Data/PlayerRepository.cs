using MySqlConnector;
using TestC_.Models;

namespace TestC_.Data;

public class PlayerRepository
{
    private readonly string _connectionString;

    public PlayerRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public List<Player> GetAll()
    {
        var players = new List<Player>();

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            SELECT player_id, username, email, password_hash,
                   tokens, level, xp, selected_character_id, is_online
            FROM player
            """;

        using var command = new MySqlCommand(sql, connection);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var player = new Player
            {
                PlayerId = reader.GetInt32("player_id"),
                Username = reader.GetString("username"),
                Email = reader.GetString("email"),
                PasswordHash = reader.GetString("password_hash"),
                Tokens = reader.GetInt32("tokens"),
                Level = reader.GetInt32("level"),
                Xp = reader.GetInt32("xp"),

                SelectedCharacterId = reader.IsDBNull(
                    reader.GetOrdinal("selected_character_id"))
                    ? null
                    : reader.GetInt32("selected_character_id"),

                IsOnline = reader.GetBoolean("is_online")
            };

            players.Add(player);
        }

        return players;
    }
}