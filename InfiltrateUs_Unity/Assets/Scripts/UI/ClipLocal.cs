using UnityEngine;

// Marca un rectangulo donde se recorta el contenido (como una mascara) dentro de la pantalla deformada.
// Se usa en vez de RectMask2D, que no entiende la deformacion.
public class ClipLocal : MonoBehaviour
{
    readonly Vector3[] esq = new Vector3[4];

    public Rect EnZona(RectTransform zona)
    {
        ((RectTransform)transform).GetWorldCorners(esq);
        Vector2 min = zona.InverseTransformPoint(esq[0]);
        Vector2 max = zona.InverseTransformPoint(esq[2]);
        return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }
}
