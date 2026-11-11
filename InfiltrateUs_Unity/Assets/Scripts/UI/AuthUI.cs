using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Base de login y registro: el logo arriba y una cajita con campos y botones.
public abstract class FormMontana : PanelBase
{
    public override FondoEscena Fondo { get { return FondoEscena.Montana; } }
    public override bool MostrarBarraSuperior { get { return false; } }
    public override string[] Atajos { get { return new[] { "ARRIBA/ABAJO|MOVER", "ENTER|CONFIRMAR" }; } }

    protected RectTransform caja;
    protected readonly List<InputField> campos = new List<InputField>();
    protected readonly List<FilaUI> botones = new List<FilaUI>();
    protected int foco;
    float cursorY;

    protected abstract string Titulo { get; }
    protected abstract void Enviar();

    int Total { get { return campos.Count + botones.Count; } }

    // Hay que llamarlo primero en Construir
    protected void CrearBase()
    {
        var logo = UiKit.Img(transform, "Logo", Color.white, Arte.Cargar("Fondos/logo_infiltrate_us"));
        logo.preserveAspect = true;
        UiKit.Ubicar(logo.rectTransform, UiKit.ArribaCentro, 0, -30, 760, 356);

        caja = UiKit.Caja(transform, "Caja", new Color(0.02f, 0.06f, 0.09f, 0.82f));
        UiKit.Ubicar(caja, UiKit.ArribaCentro, 0, -410, 640, 300);

        var t = UiKit.Txt(caja, "Titulo", Titulo, 28, UiKit.Acento, TextAnchor.MiddleCenter, true);
        UiKit.Ubicar(t.rectTransform, UiKit.ArribaCentro, 0, -14, 600, 44);
        cursorY = 70;
    }

    protected InputField AgregarCampo(string pista, bool clave)
    {
        var c = UiKit.Campo(caja, "Campo" + campos.Count, pista, clave, 560, 56, 30);
        UiKit.Ubicar(c.GetComponent<RectTransform>(), UiKit.ArribaCentro, 0, -cursorY, 560, 56);
        cursorY += 68;
        campos.Add(c);
        return c;
    }

    protected FilaUI AgregarBoton(string texto, Action accion)
    {
        int idx = campos.Count + botones.Count;
        var b = FilaUI.Crear(caja, "Boton" + botones.Count, texto, 24, EstiloFila.Boton);
        UiKit.Ubicar(b.GetComponent<RectTransform>(), UiKit.ArribaCentro, 0, -cursorY, 560, 52);
        cursorY += 62;
        b.alClick = () => { if (!UI.Ocupado) { PonerFoco(idx); accion(); } };
        b.alHover = () => PonerFoco(idx, false);
        botones.Add(b);
        return b;
    }

    protected void TerminarCaja()
    {
        UiKit.Ubicar(caja, UiKit.ArribaCentro, 0, -410, 640, cursorY + 20);
    }

    protected void PonerFoco(int i, bool enfocarCampo = true)
    {
        if (Total == 0) return;
        foco = ((i % Total) + Total) % Total;
        for (int b = 0; b < botones.Count; b++)
            botones[b].Seleccionada = (campos.Count + b == foco);

        if (foco < campos.Count)
        {
            if (enfocarCampo)
            {
                campos[foco].Select();
                campos[foco].ActivateInputField();
            }
        }
        else if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public override void AlMostrar()
    {
        foreach (var c in campos) if (c.contentType == InputField.ContentType.Password) c.text = "";
        StartCoroutine(EnfocarDespues());
    }

    IEnumerator EnfocarDespues()
    {
        yield return null;
        PonerFoco(0);
    }

    void Update()
    {
        // si el jugador hizo click en un campo, nos ponemos de acuerdo con el
        if (EventSystem.current == null) return;
        var sel = EventSystem.current.currentSelectedGameObject;
        if (sel == null) return;
        int i = campos.FindIndex(c => c.gameObject == sel);
        if (i >= 0 && i != foco) PonerFoco(i, false);
    }

    public override void Arriba() { PonerFoco(foco - 1); }
    public override void Abajo() { PonerFoco(foco + 1); }
    public override void TabKey() { PonerFoco(foco + 1); }

    public override void Confirmar()
    {
        if (foco < campos.Count)
        {
            if (foco == campos.Count - 1) Enviar();
            else PonerFoco(foco + 1);
        }
        else
        {
            var b = botones[foco - campos.Count];
            if (b.alClick != null) b.alClick();
        }
    }
}

public class LoginUI : FormMontana
{
    public override PanelId Id { get { return PanelId.Login; } }
    protected override string Titulo { get { return "INICIAR SESIÓN"; } }

    public override void Construir()
    {
        CrearBase();
        AgregarCampo("Usuario", false);
        AgregarCampo("Contraseña", true);
        AgregarBoton("INGRESAR", Enviar);
        AgregarBoton("CREAR CUENTA", () => UI.Ir(PanelId.Registro));
        AgregarBoton("SALIR", () => UI.SalirDelJuego());
        TerminarCaja();
    }

    protected override void Enviar()
    {
        string u = campos[0].text, c = campos[1].text;
        UI.Correr(async () =>
        {
            await AuthRepository.Login(u, c);
            await UI.CargarPersonajes();
        }, () =>
        {
            Toast.Mostrar("Bienvenido, " + PlayerSession.Perfil.Username + "", UiKit.Ok);
            UI.IrLimpio(PanelId.Menu);
        });
    }
}

public class RegistroUI : FormMontana
{
    public override PanelId Id { get { return PanelId.Registro; } }
    protected override string Titulo { get { return "CREAR CUENTA"; } }

    public override void Construir()
    {
        CrearBase();
        AgregarCampo("Usuario (3 a 30, sin espacios)", false);
        AgregarCampo("Correo (opcional)", false);
        AgregarCampo("Contraseña (mínimo 6)", true);
        AgregarCampo("Repite la contraseña", true);
        AgregarBoton("REGISTRARME", Enviar);
        AgregarBoton("VOLVER", () => UI.Volver());
        TerminarCaja();
        UiKit.Ubicar(caja, UiKit.ArribaCentro, 0, -380, 640, caja.sizeDelta.y);
    }

    public override void Atras() { UI.Volver(); }

    protected override void Enviar()
    {
        string u = campos[0].text, m = campos[1].text, c = campos[2].text, c2 = campos[3].text;
        if (c != c2) { Toast.Mostrar("Las contraseñas no coinciden.", UiKit.Peligro); return; }
        UI.Correr(async () =>
        {
            await AuthRepository.Registrar(u, m, c);
            await AuthRepository.Login(u, c);
            await UI.CargarPersonajes();
        }, () =>
        {
            Toast.Mostrar("¡Cuenta creada! Ya tienes tus tokens de bienvenida.", UiKit.Ok);
            UI.IrLimpio(PanelId.Menu);
        });
    }
}
