using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// JUGAR: crear sala, unirse con codigo o entrar a una sala publica. Dentro de la sala (lobby)
// se ve a los jugadores, el color, quien esta listo y el anfitrion puede iniciar.
// Todo pasa por la BD: el lobby se actualiza solo cada 2 segundos.
public class SalasUI : PanelBase
{
    public override PanelId Id { get { return PanelId.Salas; } }

    enum Modo { Inicio, Sala }
    Modo modo = Modo.Inicio;

    public override string[] Atajos
    {
        get
        {
            if (modo == Modo.Sala)
            {
                var yoHost = miembros.Any(m => m.PlayerId == PlayerSession.PlayerId && m.EsHost);
                return yoHost
                    ? new[] { "ENTER|LISTO", "IZQ/DER|COLOR", "TAB|INICIAR", "ESC|SALIR" }
                    : new[] { "ENTER|LISTO", "IZQ/DER|COLOR", "ESC|SALIR" };
            }
            if (pidiendoCodigo) return new[] { "ENTER|UNIRSE", "ESC|CANCELAR" };
            return new[] { "ARRIBA/ABAJO|MOVER", "ENTER|ELEGIR", "R|ACTUALIZAR", "ESC|ATRÁS" };
        }
    }

    // Vista de inicio
    RectTransform vistaInicio, vistaSala;
    ListaNavegable listaInicio;
    InputField campoCodigo;
    bool pidiendoCodigo;
    List<SalaInfo> publicas = new List<SalaInfo>();
    const int FilasFijas = 3;

    // Vista de sala
    SalaInfo sala;
    List<MiembroSala> miembros = new List<MiembroSala>();
    List<ColorInfo> colores;
    Image[] muestras;
    Text[] nombres, estados;
    Text infoSala;
    float proximaConsulta;
    bool consultando;

    public override void Construir()
    {
        // inicio
        vistaInicio = UiKit.Nuevo("Inicio", transform);
        UiKit.Estirar(vistaInicio);

        listaInicio = new ListaNavegable(vistaInicio, "Lista", 36, 22, 3);
        UiKit.Ubicar(listaInicio.raiz, UiKit.ArribaIzq, 0, 0, 693, 300);
        listaInicio.alConfirmar = ElegirInicio;

        campoCodigo = UiKit.Campo(vistaInicio, "Codigo", "Código de sala (6 letras)", false, 693, 44, 26);
        UiKit.Ubicar(campoCodigo.GetComponent<RectTransform>(), UiKit.ArribaIzq, 0, -310, 693, 44);
        campoCodigo.characterLimit = 6;
        campoCodigo.onValidateInput = (texto, pos, c) => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '\0';
        campoCodigo.gameObject.SetActive(false);

        // sala
        vistaSala = UiKit.Nuevo("Sala", transform);
        UiKit.Estirar(vistaSala);

        const int max = 10;
        muestras = new Image[max];
        nombres = new Text[max];
        estados = new Text[max];
        for (int i = 0; i < max; i++)
        {
            float y = -i * 34f;
            muestras[i] = UiKit.Img(vistaSala, "Color" + i, Color.white);
            UiKit.Ubicar(muestras[i].rectTransform, UiKit.ArribaIzq, 4, y - 5, 24, 24);
            nombres[i] = UiKit.Txt(vistaSala, "Nombre" + i, "", 24, UiKit.Texto, TextAnchor.MiddleLeft, false);
            UiKit.Ubicar(nombres[i].rectTransform, UiKit.ArribaIzq, 38, y, 220, 34);
            nombres[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            estados[i] = UiKit.Txt(vistaSala, "Estado" + i, "", 22, UiKit.TextoSuave, TextAnchor.MiddleRight, false);
            UiKit.Ubicar(estados[i].rectTransform, UiKit.ArribaIzq, 260, y, 130, 34);
        }

        var tarjeta = UiKit.Caja(vistaSala, "Tarjeta");
        UiKit.Ubicar(tarjeta, UiKit.ArribaIzq, 404, 0, 289, 356);
        infoSala = UiKit.Txt(tarjeta, "Info", "", 24, UiKit.Texto, TextAnchor.UpperLeft, false);
        UiKit.Ubicar(infoSala.rectTransform, UiKit.ArribaIzq, 14, -12, 262, 330);
    }

    // Entrar y salir de la pantalla

    public override void AlMostrar()
    {
        UI.ActualizarFondoPersonaje();
        modo = Modo.Inicio;
        pidiendoCodigo = false;
        campoCodigo.gameObject.SetActive(false);
        vistaInicio.gameObject.SetActive(true);
        vistaSala.gameObject.SetActive(false);
        sala = null;
        miembros.Clear();

        UI.Correr(async () =>
        {
            if (colores == null) colores = await SalaRepository.ListarColores();
            sala = await SalaRepository.MiSala();
            publicas = await SalaRepository.ListarPublicas();
        }, () =>
        {
            if (sala != null) EntrarASala();
            else LlenarInicio();
        });
    }

    public override void AlOcultar()
    {
        pidiendoCodigo = false;
    }

    void LlenarInicio()
    {
        modo = Modo.Inicio;
        vistaInicio.gameObject.SetActive(true);
        vistaSala.gameObject.SetActive(false);
        listaInicio.Limpiar();
        listaInicio.Agregar("CREAR SALA PÚBLICA");
        listaInicio.Agregar("CREAR SALA PRIVADA");
        listaInicio.Agregar("UNIRSE CON CÓDIGO");
        foreach (var s in publicas)
        {
            var f = listaInicio.Agregar("SALA " + s.Codigo + "  de " + s.Host, null, false);
            f.derecha.text = s.Jugadores + "/" + s.Max;
            f.derecha.color = s.Jugadores >= s.Max ? UiKit.Peligro : UiKit.Ok;
        }
        listaInicio.Seleccionar(0);
        UI.RefrescarHints();
    }

    void EntrarASala()
    {
        modo = Modo.Sala;
        pidiendoCodigo = false;
        campoCodigo.gameObject.SetActive(false);
        vistaInicio.gameObject.SetActive(false);
        vistaSala.gameObject.SetActive(true);
        if (miembros == null) miembros = new List<MiembroSala>();
        PintarSala();
        proximaConsulta = 0;
        UI.RefrescarHints();
    }

    // Acciones del inicio

    void ElegirInicio(int i)
    {
        if (i == 0) CrearSala(false);
        else if (i == 1) CrearSala(true);
        else if (i == 2) PedirCodigo();
        else
        {
            var s = publicas[i - FilasFijas];
            Unirse(s.Codigo);
        }
    }

    void CrearSala(bool privada)
    {
        UI.Correr(() => SalaRepository.Crear(privada), s =>
        {
            sala = s;
            miembros = new List<MiembroSala>();
            Toast.Mostrar("Sala creada: " + s.Codigo, UiKit.Ok);
            EntrarASala();
        });
    }

    void Unirse(string codigo)
    {
        UI.Correr(async () =>
        {
            await SalaRepository.Unirse(codigo);
            sala = await SalaRepository.MiSala();
        }, () =>
        {
            miembros = new List<MiembroSala>();
            EntrarASala();
        });
    }

    void PedirCodigo()
    {
        pidiendoCodigo = true;
        campoCodigo.text = "";
        campoCodigo.gameObject.SetActive(true);
        campoCodigo.Select();
        campoCodigo.ActivateInputField();
        UI.RefrescarHints();
    }

    void CancelarCodigo()
    {
        pidiendoCodigo = false;
        campoCodigo.gameObject.SetActive(false);
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        UI.RefrescarHints();
    }

    // Lobby

    void Update()
    {
        if (modo != Modo.Sala || consultando || UI.Ocupado) return;
        if (Time.unscaledTime < proximaConsulta) return;
        Consultar();
    }

    async void Consultar()
    {
        consultando = true;
        try
        {
            var s = await SalaRepository.MiSala();
            if (s == null)
            {
                if (modo == Modo.Sala)
                {
                    Toast.Mostrar("Ya no estás en esa sala.", UiKit.TextoSuave);
                    sala = null;
                    publicas = await SalaRepository.ListarPublicas();
                    LlenarInicio();
                }
                return;
            }
            var m = await SalaRepository.Miembros(s.RoomId);
            if (modo != Modo.Sala) return;
            sala = s;
            miembros = m;
            PintarSala();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Lobby: " + e.Message);
        }
        finally
        {
            consultando = false;
            proximaConsulta = Time.unscaledTime + 2f;
        }
    }

    void Refrescar() { proximaConsulta = 0; }

    MiembroSala Yo { get { return miembros.Find(m => m.PlayerId == PlayerSession.PlayerId); } }

    void PintarSala()
    {
        for (int i = 0; i < nombres.Length; i++)
        {
            bool hay = i < miembros.Count;
            muestras[i].gameObject.SetActive(hay);
            nombres[i].gameObject.SetActive(hay);
            estados[i].gameObject.SetActive(hay);
            if (!hay) continue;

            var m = miembros[i];
            muestras[i].color = UiKit.HexAColor(m.ColorHex, Color.white);
            bool soyYo = m.PlayerId == PlayerSession.PlayerId;
            nombres[i].text = m.Username.ToUpperInvariant() + (m.EsHost ? " (HOST)" : "");
            nombres[i].color = soyYo ? UiKit.Acento : UiKit.Texto;
            if (!m.Conectado) { estados[i].text = "SIN CONEXIÓN"; estados[i].color = UiKit.Peligro; }
            else if (m.Listo) { estados[i].text = "LISTO"; estados[i].color = UiKit.Ok; }
            else { estados[i].text = "ESPERANDO"; estados[i].color = UiKit.TextoSuave; }
        }

        if (sala == null) return;
        string dir = sala.Direccion;
        if (string.IsNullOrEmpty(dir)) dir = SalaRepository.IpLocal() + ":" + SalaRepository.PuertoJuego;
        else if (!dir.Contains(":") && sala.Puerto > 0) dir += ":" + sala.Puerto;

        var yo = Yo;
        string colorNombre = "";
        if (yo != null && colores != null)
        {
            var c = colores.Find(x => x.Id == yo.ColorId);
            if (c != null) colorNombre = c.Nombre;
        }

        infoSala.text =
            "<color=#f8bc38>SALA " + sala.Codigo + "</color>\n" +
            "<color=#9fc7d4>Dirección</color>\n" + dir + "\n" +
            "<color=#9fc7d4>Jugadores</color>  " + miembros.Count + "/" + Mathf.Max(sala.Max, miembros.Count) + "\n" +
            "<color=#9fc7d4>Tu color</color>  " + colorNombre + "\n" +
            "<color=#9fc7d4>Estado</color>  " + (sala.Estado ?? "").ToUpperInvariant();
        UI.RefrescarHints();
    }

    void AlternarListo()
    {
        var yo = Yo;
        if (yo == null || sala == null) return;
        bool nuevo = !yo.Listo;
        int id = sala.RoomId;
        UI.Correr(() => SalaRepository.SetListo(id, nuevo), Refrescar);
    }

    void CambiarColor(int d)
    {
        var yo = Yo;
        if (yo == null || colores == null || colores.Count == 0 || sala == null) return;
        var usados = miembros.Where(m => m.PlayerId != yo.PlayerId).Select(m => m.ColorId).ToList();
        int n = colores.Count;
        int idx = colores.FindIndex(c => c.Id == yo.ColorId);
        if (idx < 0) idx = 0;
        for (int k = 1; k <= n; k++)
        {
            int j = (((idx + d * k) % n) + n) % n;
            if (usados.Contains(colores[j].Id)) continue;
            int roomId = sala.RoomId, colorId = colores[j].Id;
            UI.Correr(() => SalaRepository.CambiarColor(roomId, colorId), Refrescar);
            return;
        }
    }

    void IniciarPartida()
    {
        var yo = Yo;
        if (yo == null || !yo.EsHost || sala == null) return;
        int id = sala.RoomId;
        UI.Correr(() => SalaRepository.Iniciar(id), partida =>
        {
            Toast.Mostrar("Partida creada! (la escena de juego se arma después)", UiKit.Ok);
            Refrescar();
        });
    }

    void SalirDeSala()
    {
        UI.Correr(async () =>
        {
            await SalaRepository.Salir();
            publicas = await SalaRepository.ListarPublicas();
        }, () =>
        {
            sala = null;
            miembros.Clear();
            LlenarInicio();
        });
    }

    // Teclas

    public override void Arriba() { if (modo == Modo.Inicio && !pidiendoCodigo) listaInicio.Mover(-1); }
    public override void Abajo() { if (modo == Modo.Inicio && !pidiendoCodigo) listaInicio.Mover(1); }
    public override void Izquierda() { if (modo == Modo.Sala) CambiarColor(-1); }
    public override void Derecha() { if (modo == Modo.Sala) CambiarColor(1); }
    public override void TabKey() { if (modo == Modo.Sala) IniciarPartida(); }

    public override void Reset()
    {
        if (modo != Modo.Inicio || pidiendoCodigo) return;
        UI.Correr(async () => publicas = await SalaRepository.ListarPublicas(), LlenarInicio);
    }

    public override void Confirmar()
    {
        if (modo == Modo.Sala) { AlternarListo(); return; }
        if (pidiendoCodigo) { Unirse(campoCodigo.text); return; }
        listaInicio.Confirmar();
    }

    public override void Atras()
    {
        if (modo == Modo.Sala) { SalirDeSala(); return; }
        if (pidiendoCodigo) { CancelarCodigo(); return; }
        UI.Volver();
    }
}
