using System.Collections.Generic;
using UnityEngine;

// Carga sprites desde Resources. Si la imagen no esta importada como Sprite, la convierte al vuelo XD
public static class Arte
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Cargar(string ruta, float borde = 0f)
    {
        if (string.IsNullOrEmpty(ruta)) return null;
        Sprite s;
        if (cache.TryGetValue(ruta, out s) && s != null) return s;

        s = Resources.Load<Sprite>(ruta);
        if (s == null)
        {
            var tex = Resources.Load<Texture2D>(ruta);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(borde, borde, borde, borde));
            }
        }
        if (s == null) Debug.LogWarning("No encontré la imagen Resources/" + ruta);
        cache[ruta] = s;
        return s;
    }
}
