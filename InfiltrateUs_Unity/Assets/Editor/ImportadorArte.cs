using UnityEditor;
using UnityEngine;

// Deja las imagenes de Resources listas para pixel art: Sprite, sin filtro borroso y sin compresion.
public class ImportadorArte : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string ruta = assetPath.Replace('\\', '/');
        if (!ruta.StartsWith("Assets/Resources/")) return;
        if (!(ruta.Contains("/Personajes/") || ruta.Contains("/UI/") || ruta.Contains("/Icons/") || ruta.Contains("/Fondos/"))) return;

        var imp = (TextureImporter)assetImporter;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 100;
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.maxTextureSize = 4096;

        // el marco se estira por las esquinas (9 cortes)
        if (ruta.EndsWith("/ui_marco.png"))
            imp.spriteBorder = new Vector4(4, 4, 4, 4);
    }
}
