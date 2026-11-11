using UnityEngine;
using UnityEngine.UI;

// Mensajito flotante arriba. Toast.Mostrar("hola", UiKit.Ok);
public class Toast : MonoBehaviour
{
    static Toast instancia;
    Text texto;
    CanvasGroup grupo;
    float tiempo;

    public static void Crear(Transform padre)
    {
        var caja = UiKit.Caja(padre, "Toast", new Color(0.02f, 0.06f, 0.09f, 0.94f));
        UiKit.Ubicar(caja, UiKit.ArribaCentro, 0, -120, 1100, 70);
        var t = caja.gameObject.AddComponent<Toast>();
        t.grupo = caja.gameObject.AddComponent<CanvasGroup>();
        t.grupo.alpha = 0;
        t.grupo.blocksRaycasts = false;
        t.grupo.interactable = false;
        t.texto = UiKit.Txt(caja, "Mensaje", "", 30, UiKit.Texto, TextAnchor.MiddleCenter, false);
        UiKit.Estirar(t.texto.rectTransform, 16, 16, 4, 4);
        instancia = t;
    }

    public static void Mostrar(string mensaje, Color? color = null)
    {
        if (instancia == null) return;
        instancia.texto.text = mensaje;
        instancia.texto.color = color ?? UiKit.Texto;
        instancia.tiempo = 3.2f;
    }

    void Update()
    {
        if (tiempo > 0) tiempo -= Time.unscaledDeltaTime;
        float meta = tiempo > 0 ? 1f : 0f;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, meta, Time.unscaledDeltaTime * 5f);
    }

    void OnDestroy() { if (instancia == this) instancia = null; }
}
