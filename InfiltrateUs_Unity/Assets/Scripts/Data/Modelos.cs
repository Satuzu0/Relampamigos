using System;
using System.Collections.Generic;

public class PerfilJugador
{
    public int PlayerId;
    public string Username;
    public string Nombre;
    public int Xp;
    public int Nivel;
    public int XpNivelActual;      // xp que pide el nivel actual
    public int XpSiguienteNivel;   // xp que pide el siguiente (0 si es el maximo)
    public int? PersonajeId;
    public int Partidas, Ganadas, GanadasInfiltrado, Kills, Tareas, VotosCorrectos, Racha;

    public float ProgresoNivel
    {
        get
        {
            if (XpSiguienteNivel <= XpNivelActual) return 1f;
            return Math.Max(0f, Math.Min(1f, (Xp - XpNivelActual) / (float)(XpSiguienteNivel - XpNivelActual)));
        }
    }
}

public class PersonajeInfo
{
    public int Id;
    public string Codigo, Nombre, Carrera, Descripcion, SpriteRef;
}

public class ProductoTienda
{
    public int ProductId, ItemId, CategoriaId, Precio;
    public string Codigo, Nombre, Descripcion, Rareza, CategoriaCodigo, CategoriaNombre, IconRef;
    public bool Comprado;
}

public class ItemInventario
{
    public int ItemId, CategoriaId;
    public string Codigo, Nombre, Descripcion, Rareza, CategoriaCodigo, CategoriaNombre, IconRef;
    public bool Equipado;
}

public class LogroInfo
{
    public int Id, Meta, Progreso, Recompensa;
    public string Codigo, Nombre, Descripcion, Condicion;
    public bool Desbloqueado;
}

public class ColorInfo
{
    public int Id;
    public string Codigo, Nombre, Hex;
}

public class SalaInfo
{
    public int RoomId, MapId, ModoId, Min, Max, Jugadores, Puerto;
    public string Codigo, Estado, Host, Direccion;
    public bool Privada;
}

public class MiembroSala
{
    public int PlayerId, ColorId;
    public string Username, ColorHex;
    public bool Listo, Conectado, EsHost;
}

// Error "bonito" que se puede mostrar directo al jugador.
public class ErrorJuego : Exception
{
    public ErrorJuego(string mensaje) : base(mensaje) { }
}
