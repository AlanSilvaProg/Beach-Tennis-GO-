using System.IO;
using UnityEditor;
using UnityEngine;

// Builds a seamless normal map from the sand texture (its brightness is used as a height map, so grains and ripples get a soft relief).
// Wrapping is used at the edges, so the result tiles as well as the original. Writes Assets/Sprites/sand-normal.png and imports it as a
// normal map. Run again after changing Strength / Blur.
public static class SandNormalMap
{
    const string Source = "Assets/Sprites/sand-texture.jpeg";
    public const string Output = "Assets/Sprites/sand-normal.png";
    const float Strength = 2.2f;   // slope per unit of brightness difference
    const int BlurPasses = 1;      // softens single-pixel noise so the relief is smooth

    [MenuItem("Tools/Beach Tennis GO/Build Sand Normal Map")]
    public static void Generate()
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(Source));
        int w = tex.width, h = tex.height;
        var px = tex.GetPixels32();
        var height = new float[w * h];
        for (int i = 0; i < px.Length; i++) height[i] = (px[i].r * 0.299f + px[i].g * 0.587f + px[i].b * 0.114f) / 255f;

        for (int pass = 0; pass < BlurPasses; pass++)
        {
            var blurred = new float[height.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            sum += height[((y + dy + h) % h) * w + (x + dx + w) % w];
                    blurred[y * w + x] = sum / 9f;
                }
            height = blurred;
        }

        var normals = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float L(int xx, int yy) => height[((yy + h) % h) * w + (xx + w) % w];
                // Sobel gradient (wrapped)
                float gx = (L(x + 1, y - 1) + 2f * L(x + 1, y) + L(x + 1, y + 1)) - (L(x - 1, y - 1) + 2f * L(x - 1, y) + L(x - 1, y + 1));
                float gy = (L(x - 1, y + 1) + 2f * L(x, y + 1) + L(x + 1, y + 1)) - (L(x - 1, y - 1) + 2f * L(x, y - 1) + L(x + 1, y - 1));
                var n = new Vector3(-gx * Strength * 0.25f, -gy * Strength * 0.25f, 1f).normalized;
                normals[y * w + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
            }
        var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        outTex.SetPixels32(normals);
        File.WriteAllBytes(Output, outTex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(outTex);
        AssetDatabase.ImportAsset(Output, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(Output);
        importer.textureType = TextureImporterType.NormalMap;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.npotScale = TextureImporterNPOTScale.ToNearest;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
        Debug.Log("[BeachTennisGO] Sand normal map written to " + Output);
    }
}
