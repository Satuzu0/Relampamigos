namespace TestC_.Models;

public class Player
{
    public int PlayerId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public int Tokens { get; set; }

    public int Level { get; set; }

    public int Xp { get; set; }

    public int? SelectedCharacterId { get; set; }

    public bool IsOnline { get; set; }
}