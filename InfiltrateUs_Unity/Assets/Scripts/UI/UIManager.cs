using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// El director de toda la interfaz. Arma el canvas, los fondos, las barras y las ventanas,
// y le manda las teclas a la ventana que esté abierta.
[DefaultExecutionOrder(-50)]
public class UIManager : MonoBehaviour
{
    public static UIManager Instancia { get; private set; }

    public const float AnchoRef = 1920f, AltoRef = 1080f;

    // La pantalla de la laptop de fondo_escritorio es un cuadrilatero en perspectiva (pixeles de la escena 1920x1080,
    // medidos desde arriba a la izquierda). El menu se arma en un rectangulo "virtual" y se deforma para calzar aca.
    const float PantAncho = 709f, PantAlto = 470f;
    static readonly Vector2 PantSupIzq = new Vector2(70f, 241f);
    static readonly Vector2 PantSupDer = new Vector2(770f, 302f);
    static readonly Vector2 PantInfDer = new Vector2(812f, 698f);
    static readonly Vector2 PantInfIzq = new Vector2(128f, 853f);

    public List<PersonajeInfo> Personajes { get; private set; } = new List<PersonajeInfo>();
    public bool Ocupado { get; private set; }

    RectTransform escena, zonaEsc, zonaMon, contEsc, contMon, barraSup;
    Text hintsEsc, hintsMon, topUsuario, topNivel, topTokens;
    Image topXp, veloContraste, veloBrillo, monedaIcono;
    FondoUI fondo;

    readonly Dictionary<PanelId, PanelBase> paneles = new Dictionary<PanelId, PanelBase>();
    readonly Stack<PanelId> historial = new Stack<PanelId>();
    PanelBase actual;

    const float AnchoBarraXp = 150f;

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;

        ArmarCanvas();
        ArmarEscena();
        ArmarEventSystem();

        Registrar<LoginUI>("Login");
        Registrar<RegistroUI>("Registro");
        Registrar<MenuUI>("Menu");
        Registrar<PersonajeUI>("Personaje");
        Registrar<VestuarioUI>("Vestuario");
        Registrar<TiendaUI>("Tienda");
        Registrar<LogrosUI>("Logros");
        Registrar<AjustesUI>("Ajustes");
        Registrar<CreditosUI>("Creditos");
        Registrar<SalasUI>("Salas");

        AplicarAjustesVisuales();
    }

    void Start()
    {
        IrLimpio(PanelId.Login);
    }

    void OnEnable() { PlayerSession.AlCambiar += RefrescarBarraSuperior; }
    void OnDisable() { PlayerSession.AlCambiar -= RefrescarBarraSuperior; }
    void OnDestroy() { if (Instancia == this) Instancia = null; }

    // Construccion

    void ArmarCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(AnchoRef, AltoRef);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
    }

    void ArmarEscena()
    {
        // franja negra por si la ventana no es 16:9
        var negro = UiKit.Img(transform, "Negro", Color.black);
        UiKit.Estirar(negro.rectTransform);

        escena = UiKit.Nuevo("Escena", transform);
        UiKit.Ubicar(escena, UiKit.Centro, 0, 0, AnchoRef, AltoRef);
        escena.gameObject.AddComponent<RectMask2D>();

        fondo = FondoUI.Crear(escena);

        // zona montana (login y registro): todo el ancho
        zonaMon = UiKit.Nuevo("ZonaMontana", escena);
        UiKit.Estirar(zonaMon);
        contMon = UiKit.Nuevo("Contenido", zonaMon);
        UiKit.Estirar(contMon, 0, 0, 0, 70);
        hintsMon = CrearHints(zonaMon, 30, 26);

        // zona escritorio: la pantalla de la laptop
        zonaEsc = UiKit.Nuevo("PantallaLaptop", escena);
        UiKit.Ubicar(zonaEsc, UiKit.ArribaIzq, 0, 0, PantAncho, PantAlto);
        var laptop = zonaEsc.gameObject.AddComponent<PantallaLaptop>();
        laptop.Configurar(zonaEsc, PantAncho, PantAlto,
            new Vector2(PantSupIzq.x, -PantSupIzq.y), new Vector2(PantSupDer.x, -PantSupDer.y),
            new Vector2(PantInfDer.x, -PantInfDer.y), new Vector2(PantInfIzq.x, -PantInfIzq.y));

        veloContraste = UiKit.Img(zonaEsc, "VeloContraste", new Color(0, 0, 0, 0));
        UiKit.Estirar(veloContraste.rectTransform);

        barraSup = UiKit.Nuevo("BarraSuperior", zonaEsc);
        barraSup.anchorMin = new Vector2(0, 1);
        barraSup.anchorMax = new Vector2(1, 1);
        barraSup.pivot = new Vector2(0.5f, 1);
        barraSup.anchoredPosition = Vector2.zero;
        barraSup.sizeDelta = new Vector2(0, 40);
        ArmarBarraSuperior();

        contEsc = UiKit.Nuevo("Contenido", zonaEsc);
        UiKit.Estirar(contEsc, 8, 8, 44, 30);

        hintsEsc = CrearHints(zonaEsc, 20, 24);

        // el toast y el brillo van encima de todo
        Toast.Crear(escena);

        veloBrillo = UiKit.Img(transform, "VeloBrillo", new Color(0, 0, 0, 0));
        UiKit.Estirar(veloBrillo.rectTransform);
    }

    Text CrearHints(Transform padre, float alto, int tam)
    {
        var t = UiKit.Txt(padre, "Atajos", "", tam, UiKit.TextoSuave, TextAnchor.MiddleCenter, false);
        var r = t.rectTransform;
        r.anchorMin = new Vector2(0, 0);
        r.anchorMax = new Vector2(1, 0);
        r.pivot = new Vector2(0.5f, 0);
        r.anchoredPosition = new Vector2(0, 4);
        r.sizeDelta = new Vector2(-16, alto);
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    void ArmarBarraSuperior()
    {
        var fondoBarra = UiKit.Img(barraSup, "Fondo", new Color(0.02f, 0.06f, 0.09f, 0.7f));
        UiKit.Estirar(fondoBarra.rectTransform);

        topUsuario = UiKit.Txt(barraSup, "Usuario", "", 26, UiKit.Texto, TextAnchor.MiddleLeft, true);
        topUsuario.fontSize = 18;
        UiKit.Ubicar(topUsuario.rectTransform, UiKit.ArribaIzq, 12, 0, 230, 40);
        topUsuario.horizontalOverflow = HorizontalWrapMode.Overflow;

        topNivel = UiKit.Txt(barraSup, "Nivel", "", 22, UiKit.Acento, TextAnchor.MiddleLeft, false);
        UiKit.Ubicar(topNivel.rectTransform, UiKit.ArribaIzq, 250, 0, 80, 40);

        topXp = UiKit.Barra(barraSup, "BarraXp", AnchoBarraXp, 14, UiKit.Acento);
        UiKit.Ubicar(topXp.transform.parent as RectTransform, UiKit.ArribaIzq, 330, -13, AnchoBarraXp, 14);

        topTokens = UiKit.Txt(barraSup, "Tokens", "0", 26, UiKit.Oro, TextAnchor.MiddleRight, false);
        UiKit.Ubicar(topTokens.rectTransform, UiKit.ArribaDer, -48, 0, 150, 40);

        monedaIcono = UiKit.Img(barraSup, "Moneda", Color.white, UiKit.SpriteMoneda);
        UiKit.Ubicar(monedaIcono.rectTransform, UiKit.ArribaDer, -12, -6, 28, 28);
    }

    void ArmarEventSystem()
    {
        var es = FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem");
            es = go.AddComponent<EventSystem>();
        }
#if ENABLE_INPUT_SYSTEM
        // con el Input System nuevo hace falta este modulo, si no el mouse no funciona
        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            var viejo = es.GetComponent<StandaloneInputModule>();
            if (viejo != null) Destroy(viejo);
            es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
#else
        if (es.GetComponent<StandaloneInputModule>() == null) es.gameObject.AddComponent<StandaloneInputModule>();
#endif
    }

    T Registrar<T>(string nombre) where T : PanelBase
    {
        var rt = UiKit.Nuevo(nombre, escena);
        var p = rt.gameObject.AddComponent<T>();
        rt.SetParent(p.Fondo == FondoEscena.Montana ? contMon : contEsc, false);
        UiKit.Estirar(rt);
        p.Construir();
        rt.gameObject.SetActive(false);
        paneles[p.Id] = p;
        return p;
    }

    // Navegacion

    public void Ir(PanelId id)
    {
        if (actual != null) historial.Push(actual.Id);
        Mostrar(id);
    }

    public void IrLimpio(PanelId id)
    {
        historial.Clear();
        Mostrar(id);
    }

    public void Volver()
    {
        if (historial.Count > 0) { Mostrar(historial.Pop()); return; }
        Mostrar(PlayerSession.Activa ? PanelId.Menu : PanelId.Login);
    }

    void Mostrar(PanelId id)
    {
        if (actual != null)
        {
            actual.AlOcultar();
            actual.gameObject.SetActive(false);
        }
        actual = paneles[id];

        bool escritorio = actual.Fondo == FondoEscena.Escritorio;
        fondo.Mostrar(actual.Fondo);
        zonaEsc.gameObject.SetActive(escritorio);
        zonaMon.gameObject.SetActive(!escritorio);
        barraSup.gameObject.SetActive(actual.MostrarBarraSuperior);

        // la barra superior ocupa lugar solo si se ve
        UiKit.Estirar(contEsc, 8, 8, actual.MostrarBarraSuperior ? 44 : 8, 30);

        actual.gameObject.SetActive(true);
        RefrescarBarraSuperior();
        RefrescarHints();
        actual.AlMostrar();
    }

    public PanelId? PanelActual { get { return actual != null ? (PanelId?)actual.Id : null; } }

    // Personaje de fondo

    // Pone en el fondo el personaje que el jugador tiene guardado en la BD (o nada)
    public void ActualizarFondoPersonaje()
    {
        PersonajeInfo p = null;
        if (PlayerSession.Activa && PlayerSession.Perfil.PersonajeId.HasValue)
            p = Personajes.Find(x => x.Id == PlayerSession.Perfil.PersonajeId.Value);
        fondo.PonerPersonaje(p);
    }

    // Para ver un personaje en el fondo mientras se esta eligiendo
    public void VistaPreviaPersonaje(PersonajeInfo p)
    {
        fondo.PonerPersonaje(p);
    }

    public async Task CargarPersonajes()
    {
        Personajes = await PerfilRepository.ListarPersonajes();
        ActualizarFondoPersonaje();
    }

    // Tareas con la BD

    public async void Correr(Func<Task> trabajo, Action alTerminar = null)
    {
        if (Ocupado) return;
        Ocupado = true;
        RefrescarHints();
        try
        {
            await trabajo();
            if (alTerminar != null) alTerminar();
        }
        catch (ErrorJuego e) { Toast.Mostrar(e.Message, UiKit.Peligro); }
        catch (Exception e)
        {
            Debug.LogException(e);
            Toast.Mostrar(Errores.Traducir(e).Message, UiKit.Peligro);
        }
        finally
        {
            Ocupado = false;
            RefrescarHints();
        }
    }

    public async void Correr<T>(Func<Task<T>> trabajo, Action<T> alTerminar)
    {
        if (Ocupado) return;
        Ocupado = true;
        RefrescarHints();
        try
        {
            T r = await trabajo();
            if (alTerminar != null) alTerminar(r);
        }
        catch (ErrorJuego e) { Toast.Mostrar(e.Message, UiKit.Peligro); }
        catch (Exception e)
        {
            Debug.LogException(e);
            Toast.Mostrar(Errores.Traducir(e).Message, UiKit.Peligro);
        }
        finally
        {
            Ocupado = false;
            RefrescarHints();
        }
    }

    public void CerrarSesion()
    {
        Correr(async () =>
        {
            await AuthRepository.Logout();
            Personajes.Clear();
            fondo.PonerPersonaje(null);
        }, () => IrLimpio(PanelId.Login));
    }

    public void SalirDelJuego()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Barras

    void RefrescarBarraSuperior()
    {
        if (topUsuario == null) return;
        var p = PlayerSession.Perfil;
        if (p == null)
        {
            topUsuario.text = "";
            topNivel.text = "";
            topTokens.text = "0";
            UiKit.PonerBarra(topXp, 0, AnchoBarraXp);
            return;
        }
        topUsuario.text = p.Username.ToUpperInvariant();
        topNivel.text = "NIV " + p.Nivel;
        topTokens.text = PlayerSession.Tokens.ToString("N0");
        UiKit.PonerBarra(topXp, p.ProgresoNivel, AnchoBarraXp);
    }

    public void RefrescarHints()
    {
        if (actual == null) return;
        string texto;
        if (Ocupado) texto = "<color=#f8bc38>CARGANDO...</color>";
        else if (!AjustesLocales.MostrarAyudas) texto = "";
        else
        {
            var partes = new List<string>();
            foreach (var a in actual.Atajos)
            {
                var d = a.Split('|');
                partes.Add("<color=#5eedf5>[" + d[0] + "]</color> " + (d.Length > 1 ? d[1] : ""));
            }
            texto = string.Join("   ", partes.ToArray());
        }
        hintsEsc.text = texto;
        hintsMon.text = texto;
    }

    public void AplicarAjustesVisuales()
    {
        if (veloBrillo != null)
            veloBrillo.color = new Color(0, 0, 0, (1f - AjustesLocales.Brillo) * 0.75f);
        if (veloContraste != null)
            veloContraste.color = new Color(0, 0, 0, AjustesLocales.ContrasteAlto ? 0.6f : 0f);
        if (Screen.fullScreen != AjustesLocales.PantallaCompleta && Application.isPlaying)
            Screen.fullScreen = AjustesLocales.PantallaCompleta;
        RefrescarHints();
    }

    // Teclado

    void Update()
    {
        if (actual == null || Ocupado) return;

        bool escribiendo = false;
        if (EventSystem.current != null)
        {
            var sel = EventSystem.current.currentSelectedGameObject;
            if (sel != null)
            {
                var campo = sel.GetComponent<InputField>();
                escribiendo = campo != null && campo.isFocused;
            }
        }

        if (Entrada.Arriba) actual.Arriba();
        if (Entrada.Abajo) actual.Abajo();
        if (Entrada.Confirmar) actual.Confirmar();
        if (Entrada.Atras) actual.Atras();
        if (Entrada.Tab) actual.TabKey();

        // izquierda/derecha y R los usa el campo de texto para escribir, asi que ahi no los mandamos
        if (!escribiendo)
        {
            if (Entrada.Izquierda) actual.Izquierda();
            if (Entrada.Derecha) actual.Derecha();
            if (Entrada.Reset) actual.Reset();
        }
    }
}
