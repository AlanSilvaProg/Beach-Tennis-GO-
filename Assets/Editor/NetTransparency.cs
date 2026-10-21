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
    const string ModelPath = "Assets/Models/Rede/volleyball+net+3d+model.fbx";
    const float EndX = 0.395f;      // model-space |x| beyond which the mesh is the posts / brackets / side hardware: always opaque

    // Texels used by the posts and their brackets (the triangles at the two ends of the model), which must never become see-through
    // even though their texture is grey like the net cells. Rasterises those triangles in UV space, slightly grown for mip maps.
    static bool[] SolidMask(int w, int h)
    {
        var mask = new bool[w * h];
        var mesh = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath).GetComponentInChildren<MeshFilter>().sharedMesh;
        Vector3[] v = mesh.vertices;
        Vector2[] uv = mesh.uv;
        int[] t = mesh.triangles;
        int endTris = 0;
        for (int i = 0; i < t.Length; i += 3)
        {
            if (Mathf.Abs((v[t[i]].x + v[t[i + 1]].x + v[t[i + 2]].x) / 3f) < EndX) continue;
            endTris++;
            Vector2 a = Vector2.Scale(uv[t[i]], new Vector2(w, h)), b = Vector2.Scale(uv[t[i + 1]], new Vector2(w, h)), c = Vector2.Scale(uv[t[i + 2]], new Vector2(w, h));
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x)) - 1), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x)) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y)) - 1), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y)) + 1);
            float den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (Mathf.Abs(den) < 1e-6f) { mask[y * w + x] = true; continue; } // degenerate sliver: mark its pixels
                    float l1 = ((b.y - c.y) * (x + 0.5f - c.x) + (c.x - b.x) * (y + 0.5f - c.y)) / den;
                    float l2 = ((c.y - a.y) * (x + 0.5f - c.x) + (a.x - c.x) * (y + 0.5f - c.y)) / den;
                    float l3 = 1f - l1 - l2;
                    if (l1 >= -0.05f && l2 >= -0.05f && l3 >= -0.05f) mask[y * w + x] = true;
                }
        }
        for (int pass = 0; pass < 3; pass++) // grow by 3 texels
        {
            var grown = (bool[])mask.Clone();
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                    if (mask[y * w + x]) { grown[y * w + x - 1] = grown[y * w + x + 1] = grown[(y - 1) * w + x] = grown[(y + 1) * w + x] = true; }
            mask = grown;
        }
        // Sanity check: the net cells must not overlap the solid area in the texture.
        int overlap = 0, sheet = 0;
        for (int i = 0; i < t.Length; i += 3)
        {
            if (Mathf.Abs((v[t[i]].x + v[t[i + 1]].x + v[t[i + 2]].x) / 3f) >= EndX) continue;
            Vector2 u = (uv[t[i]] + uv[t[i + 1]] + uv[t[i + 2]]) / 3f;
            sheet++;
            if (mask[Mathf.Clamp((int)(u.y * h), 0, h - 1) * w + Mathf.Clamp((int)(u.x * w), 0, w - 1)]) overlap++;
        }
        Debug.Log("[BeachTennisGO] Net: " + endTris + " post/bracket triangles kept solid; " + overlap + " of " + sheet + " net triangles share texels with them.");
        return mask;
    }

    [MenuItem("Tools/Beach Tennis GO/Make Net Transparent")]
    public static void Run()
    {
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(File.ReadAllBytes(Source));
        int w = src.width, h = src.height, n = w / 2;
        var px = src.GetPixels32();
        var outPx = new Color32[n * n];
        bool[] solid = SolidMask(w, h);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int k = 0; k < 4; k++)
                {
                    int idx = (y * 2 + k / 2) * w + x * 2 + k % 2;
                    var c = px[idx];
                    float lum = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / 255f;
                    float al = solid[idx] ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HoleBand, SoftBand, Mathf.Abs(lum - CellGrey)));
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
