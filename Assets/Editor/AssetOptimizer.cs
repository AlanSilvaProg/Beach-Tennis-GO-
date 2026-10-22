using UnityEditor;
using UnityEngine;

// Import settings that keep the WebGL download and memory small without changing how the game looks. Safe to run again.
//  - publi.jpeg (the boards' ad): at 1024 x 280 (not a power of two, with mip maps) it was stored as raw RGB (1.1 MB). The file itself is now 1024 x 256
//    (the UVs cover the whole image, so the boards look the same), which lets it be a crunched DXT1 with mip maps of ~60 KB.
//  - result-board / sand-normal / NetAlpha / ball2D: block compressed with crunch (ball2D is 128 x 128: 125 px could not be compressed).
//    Note: the "non power of 2" importer option is ignored for Sprite textures, so such images have to be resized in the file.
//  - 3D models: no tangents (nothing here uses a normal map on them), compressed vertices, no cameras / lights / animation imported.
public static class AssetOptimizer
{
    [MenuItem("Tools/Beach Tennis GO/Optimize Assets")]
    public static void Run()
    {
        int changed = 0;
        changed += Texture("Assets/Sprites/publi.jpeg", 1024, 75, TextureImporterNPOTScale.None);
        changed += Texture("Assets/Sprites/result-board.png", 1024, 85, TextureImporterNPOTScale.None);
        changed += Texture("Assets/Sprites/sand-normal.png", 512, 90, TextureImporterNPOTScale.ToNearest);
        changed += Texture("Assets/Models/Rede/NetAlpha.png", 512, 85, TextureImporterNPOTScale.ToNearest);
        changed += Texture("Assets/Sprites/ball2D.png", 128, 90, TextureImporterNPOTScale.None);
        changed += Model("Assets/Models/padel+racket+3d+model.fbx");
        changed += Model("Assets/Models/Rede/volleyball+net+3d+model.fbx");
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Assets optimized (" + changed + " importer(s) updated).");
    }

    static int Texture(string path, int maxSize, int quality, TextureImporterNPOTScale npot)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return 0;
        bool dirty = imp.crunchedCompression != true || imp.compressionQuality != quality || imp.textureCompression != TextureImporterCompression.Compressed
                     || imp.npotScale != npot || imp.maxTextureSize != maxSize;
        var web = imp.GetPlatformTextureSettings("WebGL");
        if (web.overridden) { web.overridden = false; imp.SetPlatformTextureSettings(web); dirty = true; } // the default settings above apply to WebGL too
        if (!dirty) return 0;
        imp.textureCompression = TextureImporterCompression.Compressed;
        imp.crunchedCompression = true;
        imp.compressionQuality = quality;
        imp.npotScale = npot;
        imp.maxTextureSize = maxSize;
        imp.SaveAndReimport();
        return 1;
    }

    static int Model(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return 0;
        bool dirty = imp.meshCompression != ModelImporterMeshCompression.Medium || imp.importTangents != ModelImporterTangents.None
                     || imp.importCameras || imp.importLights || imp.importAnimation;
        if (!dirty) return 0;
        imp.meshCompression = ModelImporterMeshCompression.Medium;
        imp.importTangents = ModelImporterTangents.None;
        imp.importCameras = false;
        imp.importLights = false;
        imp.importAnimation = false;
        imp.SaveAndReimport();
        return 1;
    }
}
