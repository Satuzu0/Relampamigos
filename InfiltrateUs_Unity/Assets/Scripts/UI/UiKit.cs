using System;
using UnityEngine;
using UnityEngine.UI;

// Cajita de herramientas para armar la interfaz por codigo con el estilo de las imagenes de referencia.
public static class UiKit
{
    // Colores del estilo (pantalla de laptop con vidrio cian)
    public static readonly Color Texto = new Color(0.90f, 0.97f, 0.98f, 1f);
    public static readonly Color TextoSuave = new Color(0.62f, 0.78f, 0.83f, 1f);
    public static readonly Color Oscuro = new Color(0.05f, 0.09f, 0.12f, 1f);
    public static readonly Color Acento = new Color(0.36f, 0.93f, 0.96f, 1f);
    public static readonly Color Seleccion = new Color(0.82f, 0.93f, 0.95f, 1f);
    public static readonly Color Vidrio = new Color(0.03f, 0.09f, 0.12f, 0.62f);
    public static readonly Color Peligro = new Color(1f, 0.42f, 0.42f, 1f);
    public static readonly Color Ok = new Color(0.50f, 0.96f, 0.66f, 1f);
    public static readonly Color Oro = new Color(0.97f, 0.74f, 0.22f, 1f);
    public static readonly Color Transparente = new Color(0, 0, 0, 0);

    // Anclas rapidas (el pivote se pone igual que el ancla)
    public static readonly Vector2 ArribaIzq = new Vector2(0f, 1f);
    public static readonly Vector2 ArribaCentro = new Vector2(0.5f, 1f);
    public static readonly Vector2 ArribaDer = new Vector2(1f, 1f);
    public static readonly Vector2 Centro = new Vector2(0.5f, 0.5f);
    public static readonly Vector2 AbajoIzq = new Vector2(0f, 0f);
    public static readonly Vector2 AbajoCentro = new Vector2(0.5f, 0f);
    public static readonly Vector2 AbajoDer = new Vector2(1f, 0f);

    static Font fTitulo, fCuerpo;
    static Sprite sMarco, sMoneda;

    public static Font Titulo
    {
        get
        {
            if (fTitulo == null) fTitulo = Resources.Load<Font>("Fuentes/Silkscreen-Regular");
            if (fTitulo == null) fTitulo = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return fTitulo;
        }
    }

    public static Font Cuerpo
    {
        get
        {
            if (fCuerpo == null) fCuerpo = Resources.Load<Font>("Fuentes/VT323-Regular");
            if (fCuerpo == null) fCuerpo = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return fCuerpo;
        }
    }

    public static Sprite SpriteMarco { get { if (sMarco == null) sMarco = Arte.Cargar("UI/ui_marco", 4f); return sMarco; } }
    public static Sprite SpriteMoneda { get { if (sMoneda == null) sMoneda = Arte.Cargar("UI/ui_moneda"); return sMoneda; } }

    // Si el elemento esta dentro de la pantalla de la laptop, se deforma en perspectiva
    public static void Warp(Graphic g)
    {
        if (g.GetComponentInParent<PantallaLaptop>() != null && g.GetComponent<WarpPantalla>() == null)
            g.gameObject.AddComponent<WarpPantalla>();
    }

    public static RectTransform Nuevo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.localScale = Vector3.one;
        return rt;
    }

    public static void Estirar(RectTransform r, float izq = 0, float der = 0, float arriba = 0, float abajo = 0)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(izq, abajo);
        r.offsetMax = new Vector2(-der, -arriba);
    }

    // x e y se miden desde el ancla (en las anclas de arriba, y negativo baja)
    public static void Ubicar(RectTransform r, Vector2 ancla, float x, float y, float ancho, float alto)
    {
        r.anchorMin = ancla;
        r.anchorMax = ancla;
        r.pivot = ancla;
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(ancho, alto);
    }

    public static Image Img(Transform padre, string nombre, Color color, Sprite sprite = null, bool raycast = false)
    {
        var rt = Nuevo(nombre, padre);
        var im = rt.gameObject.AddComponent<Image>();
        Warp(im);
        im.sprite = sprite;
        im.color = color;
        im.raycastTarget = raycast;
        return im;
    }

    // Borde pixelado de 9 cortes que se estira a todo el padre
    public static Image Marco(Transform padre, string nombre, Color color, float grosor = 0.6f)
    {
        var im = Img(padre, nombre, color, SpriteMarco);
        im.type = Image.Type.Sliced;
        im.pixelsPerUnitMultiplier = grosor;
        Estirar(im.rectTransform);
        return im;
    }

    public static Text Txt(Transform padre, string nombre, string texto, int tam, Color color,
        TextAnchor alinear = TextAnchor.MiddleLeft, bool fuenteTitulo = false)
    {
        var rt = Nuevo(nombre, padre);
        var t = rt.gameObject.AddComponent<Text>();
        Warp(t);
        t.font = fuenteTitulo ? Titulo : Cuerpo;
        t.fontSize = tam;
        t.color = color;
        t.alignment = alinear;
        t.text = texto;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        return t;
    }

    // Caja de vidrio oscuro con borde, la base de casi todas las ventanas
    public static RectTransform Caja(Transform padre, string nombre, Color? relleno = null, Color? borde = null)
    {
        var rt = Nuevo(nombre, padre);
        var fondo = rt.gameObject.AddComponent<Image>();
        Warp(fondo);
        fondo.color = relleno ?? Vidrio;
        fondo.raycastTarget = false;
        Marco(rt, "Borde", borde ?? new Color(Acento.r, Acento.g, Acento.b, 0.55f));
        return rt;
    }

    // Campo de texto (InputField clasico)
    public static InputField Campo(Transform padre, string nombre, string pista, bool clave, float ancho, float alto, int tam = 30)
    {
        var rt = Nuevo(nombre, padre);
        var fondo = rt.gameObject.AddComponent<Image>();
        Warp(fondo);
        fondo.color = new Color(0.02f, 0.06f, 0.08f, 0.85f);
        var campo = rt.gameObject.AddComponent<InputField>();
        Marco(rt, "Borde", new Color(Acento.r, Acento.g, Acento.b, 0.7f));

        var texto = Txt(rt, "Texto", "", tam, Texto, TextAnchor.MiddleLeft, false);
        Estirar(texto.rectTransform, 14, 14, 4, 4);
        texto.horizontalOverflow = HorizontalWrapMode.Overflow;
        texto.supportRichText = false;
        var ph = Txt(rt, "Pista", pista, tam, new Color(TextoSuave.r, TextoSuave.g, TextoSuave.b, 0.65f), TextAnchor.MiddleLeft, false);
        Estirar(ph.rectTransform, 14, 14, 4, 4);
        ph.fontStyle = FontStyle.Italic;

        campo.textComponent = texto;
        campo.placeholder = ph;
        campo.targetGraphic = fondo;
        campo.lineType = InputField.LineType.SingleLine;
        campo.contentType = clave ? InputField.ContentType.Password : InputField.ContentType.Standard;
        campo.customCaretColor = true;
        campo.caretColor = Acento;
        campo.selectionColor = new Color(Acento.r, Acento.g, Acento.b, 0.35f);
        campo.caretWidth = 3;
        campo.transition = Selectable.Transition.None;
        if (ancho > 0) rt.sizeDelta = new Vector2(ancho, alto);
        else rt.sizeDelta = new Vector2(0, alto);
        return campo;
    }

    // Barra de progreso simple. Devuelve la imagen de relleno; se mueve con PonerBarra.
    public static Image Barra(Transform padre, string nombre, float ancho, float alto, Color relleno)
    {
        var rt = Nuevo(nombre, padre);
        rt.sizeDelta = new Vector2(ancho, alto);
        var fondo = rt.gameObject.AddComponent<Image>();
        Warp(fondo);
        fondo.color = new Color(0.02f, 0.06f, 0.08f, 0.85f);
        fondo.raycastTarget = false;
        var fill = Img(rt, "Relleno", relleno);
        var f = fill.rectTransform;
        f.anchorMin = new Vector2(0, 0);
        f.anchorMax = new Vector2(0, 1);
        f.pivot = new Vector2(0, 0.5f);
        f.anchoredPosition = new Vector2(2, 0);
        f.sizeDelta = new Vector2(0, -4);
        Marco(rt, "Borde", new Color(Acento.r, Acento.g, Acento.b, 0.6f));
        return fill;
    }

    // t va de 0 a 1. El ancho util se calcula con el ancho fijo de la barra.
    public static void PonerBarra(Image relleno, float t, float anchoBarra)
    {
        if (relleno == null) return;
        t = Mathf.Clamp01(t);
        relleno.rectTransform.sizeDelta = new Vector2(Mathf.Max(0, anchoBarra - 4) * t, -4);
    }

    public static string NombreRareza(string r)
    {
        switch (r)
        {
            case "common": return "COMÚN";
            case "rare": return "RARO";
            case "epic": return "ÉPICO";
            case "legendary": return "LEGENDARIO";
        }
        return r.ToUpperInvariant();
    }

    public static Color ColorRareza(string r)
    {
        switch (r)
        {
            case "rare": return new Color(0.45f, 0.75f, 1f);
            case "epic": return new Color(0.80f, 0.55f, 1f);
            case "legendary": return new Color(1f, 0.78f, 0.25f);
        }
        return TextoSuave;
    }

    public static Color HexAColor(string hex, Color porDefecto)
    {
        Color c;
        if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c)) return c;
        return porDefecto;
    }
}
