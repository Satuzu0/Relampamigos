using UnityEngine;
using UnityEngine.UI;

// Los fondos de la escena: montana (login), escritorio (menus) y el personaje de fondo.
// El personaje NO sale hasta que el jugador elige uno.
public class FondoUI : MonoBehaviour
{
    Image montana, escritorio, personaje;
    FondoEscena actual = FondoEscena.Montana;
    bool hayPersonaje;
    float baseY = 150f;

    public const float PersonajeX = 1470f;   // centro horizontal del personaje en la escena de 1920x1080
    public const float PersonajeAlto = 700f;

    public static FondoUI Crear(RectTransform escena)
    {
        var f = escena.gameObject.AddComponent<FondoUI>();

        f.montana = UiKit.Img(escena, "FondoMontana", Color.white, Arte.Cargar("Fondos/fondo_login"));
        UiKit.Estirar(f.montana.rectTransform);

        f.escritorio = UiKit.Img(escena, "FondoEscritorio", Color.white, Arte.Cargar("Fondos/fondo_escritorio"));
        UiKit.Estirar(f.escritorio.rectTransform);

        f.personaje = UiKit.Img(escena, "PersonajeFondo", Color.white);
        f.personaje.preserveAspect = true;
        f.personaje.gameObject.SetActive(false);

        f.Mostrar(FondoEscena.Montana);
        return f;
    }

    public void Mostrar(FondoEscena que)
    {
        actual = que;
        montana.gameObject.SetActive(que == FondoEscena.Montana);
        escritorio.gameObject.SetActive(que == FondoEscena.Escritorio);
        personaje.gameObject.SetActive(que == FondoEscena.Escritorio && hayPersonaje);
    }

    // null = que no salga nadie
    public void PonerPersonaje(PersonajeInfo p)
    {
        Sprite s = p != null ? Arte.Cargar(p.SpriteRef + "/down_0") : null;
        hayPersonaje = s != null;
        if (!hayPersonaje)
        {
            personaje.gameObject.SetActive(false);
            return;
        }
        personaje.sprite = s;
        float ancho = PersonajeAlto * (s.rect.width / s.rect.height);
        UiKit.Ubicar(personaje.rectTransform, UiKit.AbajoIzq, PersonajeX - ancho / 2f, baseY, ancho, PersonajeAlto);
        personaje.gameObject.SetActive(actual == FondoEscena.Escritorio);
    }

    void Update()
    {
        // respiracion suave del personaje
        if (hayPersonaje && personaje.gameObject.activeSelf)
        {
            var p = personaje.rectTransform.anchoredPosition;
            p.y = baseY + Mathf.Sin(Time.unscaledTime * 1.8f) * 4f;
            personaje.rectTransform.anchoredPosition = p;
        }
    }
}
