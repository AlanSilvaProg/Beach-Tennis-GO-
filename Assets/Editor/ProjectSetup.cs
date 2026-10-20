using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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
        EditorApplication.delayCall += TryAutoSetup;
        // If the scripts reloaded while in Play mode, try again once back in Edit mode.
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += TryAutoSetup;
        };
    }

    static void TryAutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!NeedsSetup()) return;
        Debug.Log("[BeachTennisGO] Missing prefabs or scene content, running Setup Project...");
        Run(false);
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

    static bool BallHasLandingMarker()
    {
        return System.IO.File.Exists(PrefabDir + "/Ball.prefab")
            && System.IO.File.ReadAllText(PrefabDir + "/Ball.prefab").Contains("Assembly-CSharp::LandingMarker");
    }

    // Adds the landing pulse to the Ball prefab without touching anything else in it.
    static void EnsureLandingMarker()
    {
        if (BallHasLandingMarker()) return;
        var shader = Shader.Find("BeachTennisGO/LandingPulse");
        if (shader == null) { Debug.LogWarning("[BeachTennisGO] LandingPulse shader not found yet."); return; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/LandingPulse.mat");
        if (mat == null)
        {
            mat = new Material(shader);
            mat.SetColor("_CoreColor", new Color(1f, 1f, 0.85f, 1f));
            AssetDatabase.CreateAsset(mat, MatDir + "/LandingPulse.mat");
        }
        var path = PrefabDir + "/Ball.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var lm = root.AddComponent<LandingMarker>();
        var quad = Primitive(PrimitiveType.Quad, "LandingMarker", root.transform, Vector3.zero, Vector3.one, mat);
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        lm.marker = quad.GetComponent<Renderer>();
        lm.marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lm.marker.receiveShadows = false;
        lm.marker.enabled = false;
        lm.marker.transform.localScale = new Vector3(lm.diameter, lm.diameter, 1f);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[BeachTennisGO] Landing marker added to Ball prefab.");
    }

    const string AdSpritePath = "Assets/Sprites/publi.jpeg";
    const string AdPanelPrefab = PrefabDir + "/AdPanel.prefab";

    static bool SceneHasAdPanels()
    {
        return System.IO.File.Exists(ScenePath) && System.IO.File.ReadAllText(ScenePath).Contains(AssetDatabase.AssetPathToGUID("Assets/Scripts/AdPanelRing.cs"));
    }

    // Small WebGL-friendly import: sprite, crunched DXT1, mipmaps for the oblique view, no alpha.
    static void ConfigureAdSprite()
    {
        var imp = AssetImporter.GetAtPath(AdSpritePath) as TextureImporter;
        if (imp == null || imp.userData == "btgo-ad-v1") return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        var ts = new TextureImporterSettings();
        imp.ReadTextureSettings(ts);
        ts.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(ts);
        imp.mipmapEnabled = true;
        imp.filterMode = FilterMode.Bilinear;
        imp.anisoLevel = 4;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.alphaSource = TextureImporterAlphaSource.None;
        imp.alphaIsTransparency = false;
        imp.textureCompression = TextureImporterCompression.Compressed;
        imp.crunchedCompression = true;
        imp.compressionQuality = 75;
        imp.maxTextureSize = 1024;
        var web = imp.GetPlatformTextureSettings("WebGL");
        web.overridden = true;
        web.maxTextureSize = 1024;
        web.format = TextureImporterFormat.DXT1Crunched;
        web.compressionQuality = 75;
        imp.SetPlatformTextureSettings(web);
        imp.userData = "btgo-ad-v1";
        imp.SaveAndReimport();
    }

    static void EnsureAdPrefab()
    {
        if (System.IO.File.Exists(AdPanelPrefab)) return;
        var shader = Shader.Find("BeachTennisGO/AdPanel");
        if (shader == null) { Debug.LogWarning("[BeachTennisGO] AdPanel shader not found yet."); return; }
        var screenMat = new Material(shader);
        AssetDatabase.CreateAsset(screenMat, MatDir + "/AdPanel.mat");
        var frameMat = Mat("AdFrame", new Color(0.08f, 0.08f, 0.1f));

        var root = new GameObject("AdPanel");
        var panel = root.AddComponent<AdPanel>();
        panel.frame = Primitive(PrimitiveType.Cube, "Frame", root.transform, Vector3.zero, Vector3.one, frameMat).transform;
        var front = Primitive(PrimitiveType.Quad, "ScreenFront", root.transform, new Vector3(0f, 0f, -0.06f), Vector3.one, screenMat);
        var back = Primitive(PrimitiveType.Quad, "ScreenBack", root.transform, new Vector3(0f, 0f, 0.06f), Vector3.one, screenMat);
        back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        panel.screens = new[] { front.GetComponent<Renderer>(), back.GetComponent<Renderer>() };
        panel.sprites = new[] { AssetDatabase.LoadAssetAtPath<Sprite>(AdSpritePath) };
        panel.Fit();
        Save(root, "AdPanel");
    }

    // Stadium-style perimeter: a closed rectangle of boards around the court, facing inwards, with no gaps
    // at the corners. Every other board loops (staggered); the rest stay idle.
    static void EnsureAdPanelsInScene(bool rebuild = false)
    {
        if (!System.IO.File.Exists(AdPanelPrefab)) return;
        if (!rebuild && SceneHasAdPanels()) return;
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogWarning("[BeachTennisGO] Save the open scene, then run Tools > Beach Tennis GO > Add Ad Panels.");
                return;
            }
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var court = Object.FindFirstObjectByType<Court>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AdPanelPrefab);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AdSpritePath);
        if (court == null || prefab == null) return;

        var old = GameObject.Find("AdPanels");
        if (old != null) Object.DestroyImmediate(old);

        var proto = prefab.GetComponent<AdPanel>();
        var root = new GameObject("AdPanels");
        var ring = root.AddComponent<AdPanelRing>();
        ring.court = court;
        ring.panelPrefab = proto;
        ring.width = proto.size.x;
        ring.height = proto.size.y;
        ring.SyncPanels();
        int count = 0;
        foreach (var run in ring.runs) count += run.panels.Length;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] Ad panels added: " + count);
    }

    const string BallTexturePath = "Assets/Sprites/BallTexture.jpeg";

    // Small WebGL-friendly import for the ball texture (it covers a few pixels on screen).
    static void ConfigureBallTexture()
    {
        var imp = AssetImporter.GetAtPath(BallTexturePath) as TextureImporter;
        if (imp == null || imp.userData == "btgo-ball-v1") return;
        imp.textureType = TextureImporterType.Default;
        imp.mipmapEnabled = true;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.textureCompression = TextureImporterCompression.Compressed;
        imp.crunchedCompression = true;
        imp.compressionQuality = 75;
        imp.maxTextureSize = 512;
        var web = imp.GetPlatformTextureSettings("WebGL");
        web.overridden = true;
        web.maxTextureSize = 512;
        web.format = TextureImporterFormat.DXT1Crunched;
        web.compressionQuality = 75;
        imp.SetPlatformTextureSettings(web);
        imp.userData = "btgo-ball-v1";
        imp.SaveAndReimport();
    }

    // Ball material: texture with the grey panel recoloured by the tint (see BallTint.shader).
    static void EnsureBallMaterial()
    {
        var shader = Shader.Find("BeachTennisGO/BallTint");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BallTexturePath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Ball.mat");
        if (shader == null || tex == null || mat == null || mat.shader == shader) return;
        ConfigureBallTexture();
        mat.shader = shader;
        mat.SetTexture("_MainTex", tex);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Ball material now uses BallTint.");
    }

    const string HudFontPath = "Assets/Fonts/LilitaOne-Regular.ttf";

    // Creates the HUD canvas in the scene (once) and wires it to HudView. After that it is plain scene content
    // to edit by hand; it is only rebuilt when the "HUD" object or the references are missing.
    static void EnsureHud()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogWarning("[BeachTennisGO] Save the open scene, then run Tools > Beach Tennis GO > Add HUD Canvas.");
                return;
            }
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var view = Object.FindFirstObjectByType<HudView>();
        if (view == null) return;
        if (view.scoreText != null && view.infoText != null && view.bannerText != null) return;

        var old = GameObject.Find("HUD");
        if (old != null) Object.DestroyImmediate(old);

        var font = AssetDatabase.LoadAssetAtPath<Font>(HudFontPath);
        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f; // scale with the height

        // Full-width strips; `top` is the distance from the top edge and `anchorY` the anchor height (0..1).
        Text MakeText(string name, int size, float anchorY, float top, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, anchorY);
            rt.anchorMax = new Vector2(1f, anchorY);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.sizeDelta = new Vector2(0f, height);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.7f);
            o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        view.scoreText = MakeText("Score", 56, 1f, 10f, 70f);
        view.infoText = MakeText("Info", 24, 1f, 78f, 30f);
        view.bannerText = MakeText("Banner", 24, 0.62f, 0f, 140f);
        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] HUD canvas added to the scene.");
    }

    // Creates the fake shadows in the scene (once): a "Shadows" object with the sun settings, one shadow per racket and one for the net.
    static void EnsureShadows()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (Object.FindFirstObjectByType<ShadowSun>() != null) return;
        var net = Object.FindFirstObjectByType<Net>();
        var player = Object.FindFirstObjectByType<PlayerRacketController>();
        var ai = Object.FindFirstObjectByType<AiRacketController>();
        if (net == null || player == null || ai == null) return;

        Material ShadowMat(string name, float shape)
        {
            string path = "Assets/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("BeachTennisGO/FakeShadow")); AssetDatabase.CreateAsset(m, path); }
            m.SetFloat("_Shape", shape);
            EditorUtility.SetDirty(m);
            return m;
        }
        var racketMat = ShadowMat("RacketShadow", 0f);
        var netMat = ShadowMat("NetShadow", 1f);

        var root = new GameObject("Shadows");
        var sun = root.AddComponent<ShadowSun>();
        var court = Object.FindFirstObjectByType<Court>();

        T Make<T>(string name, Material mat) where T : MonoBehaviour
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root.transform, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.AddComponent<T>();
        }
        var ps = Make<RacketShadow>("PlayerRacketShadow", racketMat); ps.racket = player.Racket; ps.sun = sun; ps.court = court;
        var cs = Make<RacketShadow>("AIRacketShadow", racketMat); cs.racket = ai.Racket; cs.sun = sun; cs.court = court;
        var ns = Make<NetShadow>("NetShadow", netMat); ns.net = net; ns.sun = sun; ns.court = court;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Fake shadows added to the scene.");
    }

    [MenuItem("Tools/Beach Tennis GO/Add Fake Shadows")]
    public static void AddFakeShadows() { EnsureShadows(); }

    const string AudioFolder = "Assets/Audio";

    // Wires the racket hit and sand landing sounds into the scene's GameManager (an AudioSource on the same object); idempotent.
    static void EnsureAudio()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null) return;

        AudioClip[] Load(string prefix)
        {
            var list = new System.Collections.Generic.List<AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets(prefix + " t:AudioClip", new[] { AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!System.IO.Path.GetFileName(path).StartsWith(prefix)) continue;
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer != null)
                {
                    importer.forceToMono = true;
                    importer.loadInBackground = false;
                    var s = importer.defaultSampleSettings;
                    s.loadType = AudioClipLoadType.DecompressOnLoad; // tiny clips: no latency on the first play
                    s.compressionFormat = AudioCompressionFormat.Vorbis;
                    s.quality = 0.6f;
                    importer.defaultSampleSettings = s;
                    importer.SaveAndReimport();
                }
                list.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }

        var src = gm.GetComponent<AudioSource>();
        if (src == null) src = gm.gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        gm.sfx = src;
        gm.racketHits = Load("RacketHit");
        gm.sandLandings = Load("SandLand");

        // Announcer: its own AudioSource (so a call can be cut off without touching the sound effects) and the voice lines.
        var announcer = Object.FindFirstObjectByType<ShotAnnouncer>();
        if (announcer == null)
        {
            var go = new GameObject("Announcer", typeof(AudioSource), typeof(ShotAnnouncer));
            announcer = go.GetComponent<ShotAnnouncer>();
            announcer.voice = go.GetComponent<AudioSource>();
            announcer.voice.playOnAwake = false;
            announcer.voice.spatialBlend = 0f;
        }
        var calls = (ShotAnnouncer.Call[])System.Enum.GetValues(typeof(ShotAnnouncer.Call));
        announcer.lines = new ShotAnnouncer.Line[calls.Length];
        for (int i = 0; i < calls.Length; i++)
            announcer.lines[i] = new ShotAnnouncer.Line { call = calls[i], clips = Load("Voice_" + calls[i] + "_") };
        EditorUtility.SetDirty(announcer);
        gm.announcer = announcer;
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] Racket hit + sand landing sounds wired to the GameManager (" + gm.racketHits.Length + " / " + gm.sandLandings.Length + " clips).");
    }

    [MenuItem("Tools/Beach Tennis GO/Add Game Sounds")]
    public static void AddGameSounds() { EnsureAudio(); }

    const string BallIconPath = "Assets/Sprites/ball2D.png";

    // Creates the "LandingAlert" object under the HUD canvas (once): blinking ball icon + red arrow shown when the landing marker is off screen.
    static void EnsureLandingAlert()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var hud = Object.FindFirstObjectByType<HudView>();
        if (hud == null || hud.scoreText == null) return;
        if (Object.FindFirstObjectByType<LandingAlert>() != null) return;

        var importer = AssetImporter.GetAtPath(BallIconPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BallIconPath);

        var canvasTf = hud.scoreText.canvas.transform;
        var root = new GameObject("LandingAlert", typeof(RectTransform), typeof(CanvasGroup), typeof(LandingAlert));
        root.transform.SetParent(canvasTf, false);
        var rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
        var group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;

        RectTransform MakeImage(string name, Sprite sp, Color color, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero; // positioned in screen space from the bottom-left corner
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = sp; img.color = color; img.raycastTarget = false; img.preserveAspect = true;
            return rt;
        }

        var alert = root.GetComponent<LandingAlert>();
        alert.group = group;
        alert.icon = MakeImage("Ball", sprite, Color.white, 72f);
        alert.arrow = MakeImage("Arrow", null, new Color(0.95f, 0.1f, 0.1f, 1f), 40f).GetComponent<Image>();
        alert.marker = Object.FindFirstObjectByType<LandingMarker>();
        alert.cam = Camera.main;
        EditorUtility.SetDirty(alert);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] LandingAlert added to the HUD.");
    }

    [MenuItem("Tools/Beach Tennis GO/Add Landing Alert")]
    public static void AddLandingAlert() { EnsureLandingAlert(); }

    [MenuItem("Tools/Beach Tennis GO/Add HUD Canvas")]
    public static void AddHudCanvas() { EnsureHud(); EnsureLandingAlert(); }

    [MenuItem("Tools/Beach Tennis GO/Rebuild Ad Panels (resets per-panel settings)")]
    public static void RebuildAdPanels()
    {
        ConfigureAdSprite();
        EnsureAdPrefab();
        EnsureAdPanelsInScene(true);
    }

    [MenuItem("Tools/Beach Tennis GO/Add Ad Panels")]
    public static void AddAdPanels()
    {
        ConfigureAdSprite();
        EnsureAdPrefab();
        EnsureAdPanelsInScene();
    }

    static bool NeedsSetup()
    {
        if (!BallHasTrail() || !BallHasLandingMarker()) return true;
        if (!System.IO.File.Exists(AdPanelPrefab) || !SceneHasAdPanels()) return true;
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
        EnsureLandingMarker();
        EnsureBallMaterial();
        AddAdPanels();
        EnsureHud();
        EnsureLandingAlert();
        EnsureAudio();
        EnsureShadows();
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
        rig.playerRacket = player.Racket;
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
