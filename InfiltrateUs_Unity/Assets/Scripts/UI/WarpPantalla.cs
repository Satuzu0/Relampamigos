using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Deforma el dibujo de un Image o Text para que se vea en perspectiva dentro de la pantalla de la laptop.
// Parte los triangulos grandes en pedacitos para que la deformacion no salga chueca.
public class WarpPantalla : BaseMeshEffect
{
    const float MaxLado = 36f;

    Matrix4x4 ultima;
    bool tiene;

    void LateUpdate()
    {
        var zona = PantallaLaptop.Instancia;
        if (zona == null || zona.rect == null) return;
        // si el elemento se movio (scroll, animacion) hay que regenerar la malla
        Matrix4x4 m = zona.rect.worldToLocalMatrix * transform.localToWorldMatrix;
        if (!tiene || m != ultima)
        {
            ultima = m;
            tiene = true;
            if (graphic != null) graphic.SetVerticesDirty();
        }
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        var zona = PantallaLaptop.Instancia;
        if (!IsActive() || zona == null || zona.rect == null || vh.currentVertCount == 0) return;

        var tris = new List<UIVertex>();
        vh.GetUIVertexStream(tris);

        RectTransform t = graphic.rectTransform;
        Matrix4x4 aZona = zona.rect.worldToLocalMatrix * t.localToWorldMatrix;
        Matrix4x4 deZona = t.worldToLocalMatrix * zona.rect.localToWorldMatrix;

        Rect clip = zona.RectVirtual;
        var cl = GetComponentInParent<ClipLocal>();
        if (cl != null)
        {
            Rect r = cl.EnZona(zona.rect);
            clip = Rect.MinMaxRect(Mathf.Max(clip.xMin, r.xMin), Mathf.Max(clip.yMin, r.yMin), Mathf.Min(clip.xMax, r.xMax), Mathf.Min(clip.yMax, r.yMax));
        }

        var salida = new List<UIVertex>(tris.Count * 2);
        var poli = new List<UIVertex>(8);
        var aux = new List<UIVertex>(8);

        for (int i = 0; i + 2 < tris.Count; i += 3)
        {
            UIVertex v0 = tris[i], v1 = tris[i + 1], v2 = tris[i + 2];
            v0.position = aZona.MultiplyPoint3x4(v0.position);
            v1.position = aZona.MultiplyPoint3x4(v1.position);
            v2.position = aZona.MultiplyPoint3x4(v2.position);

            poli.Clear();
            poli.Add(v0); poli.Add(v1); poli.Add(v2);
            Recortar(poli, aux, 0, clip.xMin, true);
            Recortar(poli, aux, 0, clip.xMax, false);
            Recortar(poli, aux, 1, clip.yMin, true);
            Recortar(poli, aux, 1, clip.yMax, false);
            if (poli.Count < 3) continue;

            for (int k = 1; k + 1 < poli.Count; k++)
                Partir(poli[0], poli[k], poli[k + 1], 0, salida, zona, deZona);
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(salida);
    }

    // Recorta un poligono contra una recta (Sutherland-Hodgman)
    static void Recortar(List<UIVertex> poli, List<UIVertex> aux, int eje, float valor, bool mantenerMayor)
    {
        if (poli.Count == 0) return;
        aux.Clear();
        for (int i = 0; i < poli.Count; i++)
        {
            UIVertex a = poli[i];
            UIVertex b = poli[(i + 1) % poli.Count];
            float da = Coord(a, eje) - valor;
            float db = Coord(b, eje) - valor;
            bool dentroA = mantenerMayor ? da >= 0 : da <= 0;
            bool dentroB = mantenerMayor ? db >= 0 : db <= 0;
            if (dentroA) aux.Add(a);
            if (dentroA != dentroB)
            {
                float t = da / (da - db);
                aux.Add(Mezclar(a, b, t));
            }
        }
        poli.Clear();
        poli.AddRange(aux);
    }

    static float Coord(UIVertex v, int eje) { return eje == 0 ? v.position.x : v.position.y; }

    static UIVertex Mezclar(UIVertex a, UIVertex b, float t)
    {
        UIVertex r = a;
        r.position = Vector3.Lerp(a.position, b.position, t);
        r.color = Color32.Lerp(a.color, b.color, t);
        r.uv0 = Vector4.Lerp(a.uv0, b.uv0, t);
        r.uv1 = Vector4.Lerp(a.uv1, b.uv1, t);
        return r;
    }

    static float Dist(UIVertex a, UIVertex b)
    {
        float dx = a.position.x - b.position.x, dy = a.position.y - b.position.y;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    static void Partir(UIVertex a, UIVertex b, UIVertex c, int prof, List<UIVertex> salida, PantallaLaptop zona, Matrix4x4 deZona)
    {
        float ab = Dist(a, b), bc = Dist(b, c), ca = Dist(c, a);
        float mayor = Mathf.Max(ab, Mathf.Max(bc, ca));
        if (mayor <= MaxLado || prof >= 7)
        {
            salida.Add(Llevar(a, zona, deZona));
            salida.Add(Llevar(b, zona, deZona));
            salida.Add(Llevar(c, zona, deZona));
            return;
        }
        if (mayor == ab)
        {
            var m = Mezclar(a, b, 0.5f);
            Partir(a, m, c, prof + 1, salida, zona, deZona);
            Partir(m, b, c, prof + 1, salida, zona, deZona);
        }
        else if (mayor == bc)
        {
            var m = Mezclar(b, c, 0.5f);
            Partir(a, b, m, prof + 1, salida, zona, deZona);
            Partir(a, m, c, prof + 1, salida, zona, deZona);
        }
        else
        {
            var m = Mezclar(c, a, 0.5f);
            Partir(a, b, m, prof + 1, salida, zona, deZona);
            Partir(m, b, c, prof + 1, salida, zona, deZona);
        }
    }

    static UIVertex Llevar(UIVertex v, PantallaLaptop zona, Matrix4x4 deZona)
    {
        Vector2 q = zona.Adelante(new Vector2(v.position.x, v.position.y));
        v.position = deZona.MultiplyPoint3x4(new Vector3(q.x, q.y, 0f));
        return v;
    }
}
