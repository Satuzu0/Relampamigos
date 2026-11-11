using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// La pantalla de la laptop del fondo. Todo lo que se dibuja adentro se deforma en perspectiva
// para calzar con el cuadrilatero real de la pantalla (ver WarpPantalla).
// Aca tambien se maneja el mouse, porque el raycast normal de Unity no sabe de la deformacion.
public class PantallaLaptop : MonoBehaviour
{
    public static PantallaLaptop Instancia { get; private set; }

    public RectTransform rect;
    public float Ancho, Alto;   // tamano "virtual" de la zona, sin deformar

    float a, b, c, d, e, f, g, h;
    float[] n = new float[9];

    static readonly HashSet<FilaUI> filas = new HashSet<FilaUI>();
    FilaUI encima;

    public Rect RectVirtual { get { return new Rect(0, -Alto, Ancho, Alto); } }

    public static void Registrar(FilaUI fila) { filas.Add(fila); }
    public static void Quitar(FilaUI fila) { filas.Remove(fila); }

    // Las esquinas vienen en coordenadas de la zona (x a la derecha, y negativo hacia abajo)
    public void Configurar(RectTransform zona, float ancho, float alto, Vector2 sup_izq, Vector2 sup_der, Vector2 inf_der, Vector2 inf_izq)
    {
        Instancia = this;
        rect = zona;
        Ancho = ancho;
        Alto = alto;

        float x0 = sup_izq.x, y0 = sup_izq.y, x1 = sup_der.x, y1 = sup_der.y;
        float x2 = inf_der.x, y2 = inf_der.y, x3 = inf_izq.x, y3 = inf_izq.y;

        float dx1 = x1 - x2, dx2 = x3 - x2, dx3 = x0 - x1 + x2 - x3;
        float dy1 = y1 - y2, dy2 = y3 - y2, dy3 = y0 - y1 + y2 - y3;
        float den = dx1 * dy2 - dx2 * dy1;
        g = (dx3 * dy2 - dx2 * dy3) / den;
        h = (dx1 * dy3 - dx3 * dy1) / den;
        a = x1 - x0 + g * x1;
        b = x3 - x0 + h * x3;
        c = x0;
        d = y1 - y0 + g * y1;
        e = y3 - y0 + h * y3;
        f = y0;

        // inversa (solo cofactores, la escala no importa en coordenadas homogeneas)
        n[0] = e - f * h;  n[1] = c * h - b;  n[2] = b * f - c * e;
        n[3] = f * g - d;  n[4] = a - c * g;  n[5] = c * d - a * f;
        n[6] = d * h - e * g; n[7] = b * g - a * h; n[8] = a * e - b * d;
    }

    void OnDestroy() { if (Instancia == this) Instancia = null; }

    // De la zona sin deformar a donde se ve realmente en la escena
    public Vector2 Adelante(Vector2 p)
    {
        float u = p.x / Ancho;
        float v = -p.y / Alto;
        float w = g * u + h * v + 1f;
        return new Vector2((a * u + b * v + c) / w, (d * u + e * v + f) / w);
    }

    // Al reves: de un punto de la escena a la zona sin deformar
    public Vector2 Atras(Vector2 q)
    {
        float u = n[0] * q.x + n[1] * q.y + n[2];
        float v = n[3] * q.x + n[4] * q.y + n[5];
        float w = n[6] * q.x + n[7] * q.y + n[8];
        if (Mathf.Abs(w) < 1e-6f) w = 1e-6f;
        return new Vector2(u / w * Ancho, -(v / w) * Alto);
    }

    // Mouse

    void Update()
    {
        Vector2 pos;
        bool click;
        if (!LeerMouse(out pos, out click)) return;

        FilaUI nueva = null;
        Vector2 local;
        if (rect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, pos, null, out local))
        {
            Vector2 v = Atras(local);
            foreach (var fila in filas)
            {
                if (fila == null || !fila.gameObject.activeInHierarchy) continue;
                if (Contiene(fila, v)) { nueva = fila; break; }
            }
        }

        if (nueva != encima)
        {
            if (encima != null) encima.PunteroSalir();
            encima = nueva;
            if (encima != null) encima.PunteroEntrar();
        }
        if (click && encima != null) encima.PunteroClick();
    }

    readonly Vector3[] esq = new Vector3[4];

    bool Contiene(FilaUI fila, Vector2 v)
    {
        fila.rectTransform.GetWorldCorners(esq);
        Vector2 min = rect.InverseTransformPoint(esq[0]);
        Vector2 max = rect.InverseTransformPoint(esq[2]);
        if (v.x < min.x || v.x > max.x || v.y < min.y || v.y > max.y) return false;
        var clip = fila.GetComponentInParent<ClipLocal>();
        if (clip != null && !clip.EnZona(rect).Contains(v)) return false;
        return true;
    }

    static bool LeerMouse(out Vector2 pos, out bool click)
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m == null) { pos = Vector2.zero; click = false; return false; }
        pos = m.position.ReadValue();
        click = m.leftButton.wasPressedThisFrame;
        return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
        pos = Input.mousePosition;
        click = Input.GetMouseButtonDown(0);
        return true;
#else
        pos = Vector2.zero; click = false; return false;
#endif
    }
}
