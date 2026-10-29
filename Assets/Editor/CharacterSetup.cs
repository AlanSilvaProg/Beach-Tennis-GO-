using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools / Beach Tennis GO / Setup characters: puts a character in the scenes (Gameplay and Online) for every racket: the player, the CPU and the other
// online player. They are normal objects of the scene ("<racket name>Character"), so they can be seen and tuned in the editor. Run it again after changing
// the model: it makes the missing ones and leaves the others alone. Delete one in the scene and run this to get a fresh one.
public static class CharacterSetup
{
    static readonly string[] Scenes = { "Assets/Scene/Gameplay.unity", "Assets/Scene/Online.unity" };
    const string BlobPath = "Assets/Resources/BlobShadow.png";

    [MenuItem("Tools/Beach Tennis GO/Setup characters")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        MakeBlobSprite();
        string back = EditorSceneManager.GetActiveScene().path;
        foreach (var path in Scenes)
        {
            if (!File.Exists(path)) continue;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int made = SetupOpenScene();
            if (made > 0) EditorSceneManager.SaveOpenScenes();
            Debug.Log("[CharacterSetup] " + path + ": " + made + " character(s) made");
        }
        if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
    }

    // The characters of the open scene. Returns how many were made.
    public static int SetupOpenScene()
    {
        int made = 0;
        foreach (var c in Object.FindObjectsByType<RacketController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var human = c as PlayerRacketController;
            bool follow = human == null || human is RemoteRacketController;
            if (PlayerCharacter.For(c.Racket) != null) continue;
            var pc = PlayerCharacter.Create(c.Racket, follow, follow ? new Color(1f, 0.82f, 0.8f) : Color.white);
            if (pc == null) continue;
            EditorUtility.SetDirty(c.Racket);
            Undo.RegisterCreatedObjectUndo(pc.gameObject, "Make character");
            made++;
        }
        if (made > 0) EditorSceneManager.MarkAllScenesDirty();
        return made;
    }

    // The round soft shadow of the characters, as a picture (so the scene does not hold a texture made at run time).
    static void MakeBlobSprite()
    {
        if (File.Exists(BlobPath)) return;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        File.WriteAllBytes(BlobPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(BlobPath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(BlobPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spritePixelsPerUnit = n / 1.2f;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
    }
}
