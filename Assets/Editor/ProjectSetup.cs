using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the Gameplay scene, materials and lightweight WebGL settings.
// Run from the menu: Tools > Beach Tennis GO > Setup Project
public static class ProjectSetup
{
    const string ScenePath = "Assets/Scene/Gameplay.unity";
    const string MatDir = "Assets/Materials";

    [MenuItem("Tools/Beach Tennis GO/Setup Project")]
    public static void Run()
    {
        ConfigurePlayer();
        BuildScene();
        AssetDatabase.SaveAssets();
    }

    static Material Mat(string name, Color color, string shader = "Unlit/Color")
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets", "Materials");
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void BuildScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) Object.DestroyImmediate(l.gameObject);
        foreach (var g in Object.FindObjectsByType<Game>(FindObjectsSortMode.None)) Object.DestroyImmediate(g.gameObject);

        var go = new GameObject("Game");
        var game = go.AddComponent<Game>();
        game.racketPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/padel+racket+3d+model.fbx");
        game.tableMat = Mat("Table", new Color(0.10f, 0.30f, 0.62f));
        game.sandMat = Mat("Sand", new Color(0.95f, 0.85f, 0.60f));
        game.ballMat = Mat("Ball", new Color(1f, 0.95f, 0.35f));
        game.netMat = Mat("Net", new Color(0.95f, 0.95f, 0.95f));
        game.shadowMat = Mat("Shadow", new Color(0.05f, 0.05f, 0.05f));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static void ConfigurePlayer()
    {
        PlayerSettings.companyName = "ExGames";
        PlayerSettings.productName = "Beach Tennis GO!";
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "APPLICATION:Minimal";
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.colorSpace = ColorSpace.Gamma;
    }
}
