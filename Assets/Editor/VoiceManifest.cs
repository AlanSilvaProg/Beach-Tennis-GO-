using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Writes Assets/StreamingAssets/Voice/<lang>/index.txt (the list of voice files of that language) so the game knows what to download.
// Runs by itself when voice files change and before every build; the menu item is there in case you want to force it.
public class VoiceManifest : AssetPostprocessor
{
    const string Root = "Assets/StreamingAssets/Voice";

    [MenuItem("Tools/Beach Tennis GO/Update Voice Manifests")]
    public static void Generate()
    {
        if (!Directory.Exists(Root)) return;
        foreach (string dir in Directory.GetDirectories(Root))
        {
            var files = Directory.GetFiles(dir)
                .Select(Path.GetFileName)
                .Where(n => n.StartsWith("Voice_") && (n.EndsWith(".wav") || n.EndsWith(".mp3") || n.EndsWith(".ogg")))
                .OrderBy(n => n, System.StringComparer.Ordinal)
                .ToArray();
            string path = Path.Combine(dir, "index.txt");
            string text = string.Join("\n", files) + "\n";
            if (File.Exists(path) && File.ReadAllText(path) == text) continue;
            File.WriteAllText(path, text);
            Debug.Log("[BeachTennisGO] " + path + ": " + files.Length + " voice file(s).");
        }
        AssetDatabase.Refresh();
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        bool changed = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
            .Any(p => p.StartsWith(Root) && !p.EndsWith("index.txt") && !p.EndsWith(".meta"));
        if (changed) EditorApplication.delayCall += Generate;
    }
}
