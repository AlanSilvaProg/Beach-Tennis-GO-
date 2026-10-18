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

    // Fills in whatever is missing (prefabs, scene) the first time the scripts compile.
    // Existing prefabs, materials and the scene are never overwritten, so manual tweaks are safe.
    [InitializeOnLoadMethod]
    static void AutoSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (!NeedsSetup()) return;
            Debug.Log("[BeachTennisGO] Missing prefabs or scene content, running Setup Project...");
            Run(false);
        };
    }

    static bool BallHasTrail()
    {
        return System.IO.File.Exists(PrefabDir + "/Ball.prefab")
            && System.IO.File.ReadAllText(PrefabDir + "/Ball.prefab").Contains("Assembly-CSharp::BallTrail");
    }

    // Adds the trail to the Ball prefab without touching anything else in it.
    static void EnsureBallTrail()
    {
        if (BallHasTrail()) return;
        var path = PrefabDir + "/Ball.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var mat = Mat("BallTrail", Color.white, "Sprites/Default");
        var trail = root.AddComponent<BallTrail>();
        trail.outer = AddTrailChild(root.transform, "Trail", mat, 0);
        trail.core = AddTrailChild(root.transform, "TrailCore", mat, 1);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[BeachTennisGO] Ball trail added to Ball prefab.");
    }

    static TrailRenderer AddTrailChild(Transform parent, string name, Material mat, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TrailRenderer>();
        t.sharedMaterial = mat;
        t.sortingOrder = order;
        t.textureMode = LineTextureMode.Stretch;
        return t;
    }

    static bool NeedsSetup()
    {
        if (!BallHasTrail()) return true;
        foreach (var n in new[] { "Ball", "Court", "Net", "Racket", "PlayerRacket", "AIRacket" })
            if (!System.IO.File.Exists(PrefabDir + "/" + n + ".prefab")) return true;
        return !SceneHasGameManager();
    }

    static bool SceneHasGameManager()
    {
        return System.IO.File.Exists(ScenePath) && System.IO.File.ReadAllText(ScenePath).Contains("Assembly-CSharp::GameManager");
    }

    [MenuItem("Tools/Beach Tennis GO/Setup Project (keep existing)")]
    public static void RunKeep() { Run(false); }

    [MenuItem("Tools/Beach Tennis GO/Rebuild Everything (overwrite prefabs and scene)")]
    public static void RunForce() { Run(true); }

    public static void Run(bool force)
    {
        ConfigurePlayer();
        BuildScene(force);
        EnsureBallTrail();
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Setup done: prefabs in Assets/Prefabs, scene Assets/Scene/Gameplay.unity");
    }

    static Material Mat(string name, Color color, string shader = "Unlit/Color")
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets", "Materials");
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find(shader));
            m.color = color;
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    const string PrefabDir = "Assets/Prefabs";

    static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    static GameObject Save(GameObject go, string name)
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets", "Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static GameObject BuildBallPrefab(Material ballMat, Material shadowMat)
    {
        var root = new GameObject("Ball");
        var ball = root.AddComponent<Ball>();
        var body = Primitive(PrimitiveType.Sphere, "Body", root.transform, Vector3.zero, Vector3.one * ball.radius * 2f, ballMat);
        var shadow = Primitive(PrimitiveType.Cylinder, "Shadow", root.transform, Vector3.zero, new Vector3(ball.shadowSize, 0.001f, ball.shadowSize), shadowMat);
        ball.body = body.transform;
        ball.shadow = shadow.transform;
        return Save(root, "Ball");
    }

    static GameObject BuildCourtPrefab(Material sandMat, Material areaMat, Material lineMat)
    {
        var root = new GameObject("Court");
        var court = root.AddComponent<Court>();
        Primitive(PrimitiveType.Cube, "Sand", root.transform, new Vector3(0f, -0.025f, 0f), new Vector3(60f, 0.05f, 60f), sandMat);
        court.area = Primitive(PrimitiveType.Cube, "Area", root.transform, Vector3.zero, Vector3.one, areaMat).transform;
        court.lineNear = Primitive(PrimitiveType.Cube, "LineNear", root.transform, Vector3.zero, Vector3.one, lineMat).transform;
        court.lineFar = Primitive(PrimitiveType.Cube, "LineFar", root.transform, Vector3.zero, Vector3.one, lineMat).transform;
        court.lineLeft = Primitive(PrimitiveType.Cube, "LineLeft", root.transform, Vector3.zero, Vector3.one, lineMat).transform;
        court.lineRight = Primitive(PrimitiveType.Cube, "LineRight", root.transform, Vector3.zero, Vector3.one, lineMat).transform;
        court.lineCentre = Primitive(PrimitiveType.Cube, "LineCentre", root.transform, Vector3.zero, Vector3.one, lineMat).transform;
        court.Fit();
        return Save(root, "Court");
    }

    static GameObject BuildNetPrefab()
    {
        var root = new GameObject("Net");
        var net = root.AddComponent<Net>();
        var netModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Rede/volleyball+net+3d+model.fbx");
        var band = (GameObject)PrefabUtility.InstantiatePrefab(netModel);
        band.name = "Band";
        band.transform.SetParent(root.transform, false);
        net.band = band.transform;
        net.Fit();
        return Save(root, "Net");
    }

    static GameObject BuildRacketPrefabs()
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/padel+racket+3d+model.fbx");
        var root = new GameObject("Racket");
        var racket = root.AddComponent<Racket>();
        var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        model.name = "Model";
        model.transform.SetParent(root.transform, false);
        racket.model = model.transform;
        var basePrefab = Save(root, "Racket");

        var p = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        p.name = "PlayerRacket";
        p.AddComponent<PlayerRacketController>();
        var pr = p.GetComponent<Racket>();
        pr.facingYaw = 0f;
        var playerPrefab = PrefabUtility.SaveAsPrefabAsset(p, PrefabDir + "/PlayerRacket.prefab");
        Object.DestroyImmediate(p);

        var a = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        a.name = "AIRacket";
        a.AddComponent<AiRacketController>();
        var ar = a.GetComponent<Racket>();
        ar.facingYaw = 180f;
        PrefabUtility.SaveAsPrefabAsset(a, PrefabDir + "/AIRacket.prefab");
        Object.DestroyImmediate(a);
        return basePrefab;
    }

    static T Spawn<T>(string prefabName) where T : Component
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + prefabName + ".prefab");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        return inst.GetComponent<T>();
    }

    static bool PrefabExists(string name) { return System.IO.File.Exists(PrefabDir + "/" + name + ".prefab"); }

    static void BuildScene(bool force)
    {
        var sandMat = Mat("Sand", new Color(0.95f, 0.85f, 0.60f));
        var areaMat = Mat("CourtArea", new Color(0.90f, 0.78f, 0.50f));
        var lineMat = Mat("Line", new Color(0.97f, 0.97f, 0.97f));
        var ballMat = Mat("Ball", new Color(1f, 0.95f, 0.35f));
        var shadowMat = Mat("Shadow", new Color(0.45f, 0.38f, 0.22f));

        AssetDatabase.DeleteAsset(PrefabDir + "/Table.prefab");
        AssetDatabase.DeleteAsset(MatDir + "/Table.mat");
        if (force || !PrefabExists("Ball")) BuildBallPrefab(ballMat, shadowMat);
        if (force || !PrefabExists("Court")) BuildCourtPrefab(sandMat, areaMat, lineMat);
        if (force || !PrefabExists("Net")) BuildNetPrefab();
        if (force || !PrefabExists("Racket") || !PrefabExists("PlayerRacket") || !PrefabExists("AIRacket")) BuildRacketPrefabs();
        if (!force && SceneHasGameManager()) return;

        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponent<Camera>() == null && root.GetComponent<Light>() == null) Object.DestroyImmediate(root);

        var camGo = GameObject.Find("Main Camera");
        if (camGo == null) camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        camGo.transform.position = new Vector3(0f, 6f, -22f);
        camGo.transform.LookAt(new Vector3(0f, 0.6f, 1f));
        cam.fieldOfView = 55f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.82f, 0.95f);
        cam.allowHDR = false;
        cam.allowMSAA = false;

        var court = Spawn<Court>("Court");
        var oldRig = camGo.GetComponent<CameraRig>();
        if (oldRig != null) Object.DestroyImmediate(oldRig);
        var rig = camGo.AddComponent<CameraRig>();
        rig.court = court;
        var net = Spawn<Net>("Net");
        var ball = Spawn<Ball>("Ball");
        var player = Spawn<PlayerRacketController>("PlayerRacket");
        player.cam = cam;
        var ai = Spawn<AiRacketController>("AIRacket");
        player.Racket.MoveTo(player.restPosition);
        ai.Racket.MoveTo(ai.restPosition);

        var go = new GameObject("GameManager");
        var gm = go.AddComponent<GameManager>();
        var hud = go.AddComponent<HudView>();
        gm.court = court; gm.net = net; gm.ball = ball; gm.player = player; gm.ai = ai; gm.hud = hud; gm.cameraRig = rig;

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
