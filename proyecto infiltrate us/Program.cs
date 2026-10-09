using TestC_.Data;

string connectionString =
    "Server=localhost;Database=infiltrate_us;User ID=root;Password=;";

try
{
    var repository = new PlayerRepository(connectionString);

    var players = repository.GetAll();

    Console.WriteLine("Jugadores:");

    foreach (var player in players)
    {
        Console.WriteLine(
            $"{player.PlayerId} - {player.Username} - {player.Email} - Nivel: {player.Level}"
        );
    }
}
catch (Exception ex)
{
    Console.WriteLine("Error: " + ex.Message);
}