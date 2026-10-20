using System.IO;
using UnityEditor;
using UnityEngine;

// Makes the holes of the net model see-through. The model has a single mesh and a single texture, so the holes are found in the
// texture: the net cells are a flat mid grey, the cords are darker and the tape/posts are lighter. Writes Assets/Models/Rede/NetAlpha.png
// (the original colours + that alpha), creates Assets/Materials/NetSheer.mat and assigns it to the Net prefab's renderer.
// Safe to run again (e.g. after tweaking the thresholds below).
public static class NetTransparency
{
    const string Source = "Assets/Models/Rede/volleyball+net+3d+model.fbm/volleyball+net+3d+model_basecolor.jpg";
    const string Output = "Assets/Models/Rede/NetAlpha.png";
    const string MaterialPath = "Assets/Materials/NetSheer.mat";
    const string PrefabPath = "Assets/Prefabs/Net.prefab";

    const float CellGrey = 0.47f;   // luminance of an empty cell
    const float HoleBand = 0.035f;  // fully transparent within this distance of CellGrey
    const float SoftBand = 0.08f;   // fully opaque beyond this distance (smooth in between)
    const float HoleColour = 0.2f;  // colour stored in the holes, so mip maps keep the cords dark

    [MenuItem("Tools/Beach Tennis GO/Make Net Transparent")]
    public static void Run()
    {
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(File.ReadAllBytes(Source));
        int w = src.width, h = src.height, n = w / 2;
        var px = src.GetPixels32();
        var outPx = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int k = 0; k < 4; k++)
                {
                    var c = px[(y * 2 + k / 2) * w + x * 2 + k % 2];
                    float lum = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / 255f;
                    float al = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HoleBand, SoftBand, Mathf.Abs(lum - CellGrey)));
                    float cr = c.r / 255f, cg = c.g / 255f, cb = c.b / 255f;
                    r += Mathf.Lerp(HoleColour, cr, al); g += Mathf.Lerp(HoleColour, cg, al); b += Mathf.Lerp(HoleColour, cb, al); a += al;
                }
                outPx[y * n + x] = new Color(r / 4f, g / 4f, b / 4f, a / 4f);
            }
        var dst = new Texture2D(n, n, TextureFormat.RGBA32, false);
        dst.SetPixels32(outPx);
        File.WriteAllBytes(Output, dst.EncodeToPNG());
        Object.DestroyImmediate(src); Object.DestroyImmediate(dst);
        AssetDatabase.ImportAsset(Output, ImportAssetOptions.ForceUpdate);

        var ti = (TextureImporter)AssetImporter.GetAtPath(Output);
        ti.textureType = TextureImporterType.Default;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.maxTextureSize = 1024;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "WebGL", overridden = true, maxTextureSize = 512, format = TextureImporterFormat.Automatic,
            textureCompression = TextureImporterCompression.Compressed, compressionQuality = 70
        });
        ti.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Output);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("BeachTennisGO/NetSheer"));
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.mainTexture = tex;
        EditorUtility.SetDirty(mat);

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        int changed = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            changed++;
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Net transparency applied to " + changed + " renderer(s).");
    }
}
