using System;
using System.Collections.Generic;
using MySqlConnector;

// Convierte los errores de la BD (los SIGNAL de los procedimientos) a mensajes en espanol.
public static class Errores
{
    static readonly Dictionary<string, string> textos = new Dictionary<string, string>
    {
        { "ROOM_NOT_FOUND", "No existe una sala con ese código." },
        { "ROOM_NOT_JOINABLE", "Esa sala ya empezó o está cerrada." },
        { "ROOM_FULL", "La sala está llena." },
        { "ALREADY_IN_A_ROOM", "Ya estás dentro de una sala." },
        { "ALREADY_IN_ANOTHER_ROOM", "Ya estás en otra sala. Sal primero." },
        { "NOT_ROOM_HOST", "Solo el anfitrión puede hacer eso." },
        { "NOT_ENOUGH_PLAYERS", "Faltan jugadores para empezar." },
        { "CHARACTER_NOT_AVAILABLE", "Todos tienen que elegir un personaje antes de empezar." },
        { "MAP_NOT_AVAILABLE", "No hay mapas disponibles. Corre el SQL de semillas." },
        { "MODE_NOT_AVAILABLE", "No hay modo de juego disponible." },
        { "INVALID_ROOM_CAPACITY", "La capacidad de la sala no es válida para ese mapa." },
        { "ROOM_CODE_UNAVAILABLE", "No se pudo generar un código de sala, intenta otra vez." },
        { "INSUFFICIENT_FUNDS", "No te alcanzan los tokens." },
        { "ITEM_ALREADY_OWNED", "Ya tienes ese objeto." },
        { "ITEM_NOT_OWNED", "Ese objeto todavía no es tuyo." },
        { "PRODUCT_NOT_FOUND", "Ese producto ya no existe." },
        { "PRODUCT_NOT_AVAILABLE", "Ese producto no está disponible ahora." },
        { "PLAYER_NOT_FOUND", "No se encontró al jugador." },
        { "PLAYER_NOT_ACTIVE", "Tu cuenta no está activa." },
        { "ACCOUNT_LOCKED", "Cuenta bloqueada unos minutos por demasiados intentos." },
        { "IDEMPOTENCY_KEY_REUSED", "Esa compra ya se había procesado." },
        { "MATCH_NOT_FOUND", "No existe esa partida." },
        { "MATCH_NOT_IN_PROGRESS", "Esa partida ya terminó." },
        { "ACHIEVEMENT_NOT_FOUND", "No existe ese logro." }
    };

    public static ErrorJuego Traducir(Exception ex)
    {
        var my = ex as MySqlException;
        if (my != null)
        {
            string msg = my.Message ?? "";
            foreach (var kv in textos)
                if (msg.Contains(kv.Key)) return new ErrorJuego(kv.Value);

            if (msg.Contains("uk_player_username")) return new ErrorJuego("Ese usuario ya existe, elige otro.");
            if (msg.Contains("uk_player_email")) return new ErrorJuego("Ese correo ya está registrado.");
            if (msg.Contains("chk_player_username")) return new ErrorJuego("Usuario inválido: 3 a 30 caracteres con letras, números, _ . o -");
            if (msg.Contains("chk_player_email")) return new ErrorJuego("El correo no parece válido.");
            if (msg.Contains("uk_member_color")) return new ErrorJuego("Ese color ya lo tiene otro jugador.");
            if (msg.Contains("does not exist")) return new ErrorJuego("Falta algo en la base de datos (" + Corto(msg) + "). Importa los SQL.");
            if (msg.Contains("Access denied")) return new ErrorJuego("La BD rechazó el usuario o la clave del DatabaseManager.");
            if (msg.Contains("Unable to connect") || msg.Contains("Connect Timeout") || msg.Contains("connect"))
                return new ErrorJuego("No se pudo conectar a la base de datos. Revisa que MySQL esté encendido en XAMPP.");
            return new ErrorJuego("Error de base de datos: " + Corto(msg));
        }
        if (ex is TimeoutException) return new ErrorJuego("La base de datos tardó demasiado en responder.");
        return new ErrorJuego("Algo falló: " + Corto(ex.Message));
    }

    static string Corto(string s)
    {
        if (s == null) return "";
        s = s.Replace("\n", " ");
        return s.Length > 110 ? s.Substring(0, 110) + "..." : s;
    }
}
