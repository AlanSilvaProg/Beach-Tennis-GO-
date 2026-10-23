using System.Linq;
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
        var scene = OpenGameplayScene();
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
        var scene = OpenGameplayScene();
        var view = Object.FindFirstObjectByType<HudView>();
        if (view == null) return;
        if (view.infoText != null && view.bannerText != null) return;

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
        TMPro.TMP_Text MakeText(string name, int size, float anchorY, float top, float height)
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
            return TmpConvert.Convert(t);
        }

        view.infoText = MakeText("Info", 24, 1f, 140f, 30f); // below the result board
        view.bannerText = MakeText("Banner", 24, 0.62f, 0f, 140f);
        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] HUD canvas added to the scene.");
    }

    // Creates the fake shadows in the scene (once): a "Shadows" object with the sun settings, one shadow per racket and one for the net.
    static void EnsureShadows()
    {
        var scene = OpenGameplayScene();
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
        var scene = OpenGameplayScene();
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
        gm.playerHit = Load("player-hit").FirstOrDefault();
        gm.enemyHit = Load("enemy-hit").FirstOrDefault();
        gm.crowdEnd = Load("crowd-end-game").FirstOrDefault();
        gm.sandLanding = Load("SandLand").FirstOrDefault();

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
        // Voice clips are not assigned here: they are loaded from StreamingAssets/Voice/<language> at runtime (see ShotAnnouncer).
        VoiceManifest.Generate();
        EditorUtility.SetDirty(announcer);
        gm.announcer = announcer;
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] Player/enemy hit + sand landing sounds wired to the GameManager.");
    }

    [MenuItem("Tools/Beach Tennis GO/Add Game Sounds")]
    public static void AddGameSounds() { EnsureAudio(); }

    // The Gameplay scene, without reloading it when it is already open: reopening it from disk would throw away whatever has been
    // adjusted by hand in the editor and not saved yet.
    static UnityEngine.SceneManagement.Scene OpenGameplayScene()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.path == ScenePath) return active;
        return EditorSceneManager.OpenScene(ScenePath);
    }

    // Creates the sea behind the far end of the court in the scene (once): a big flat plane with the procedural Sea shader, lying just above
    // the sand. The shoreline is at the material's _ShoreZ (world z); the plane starts a little in front of it for the wet sand and the foam.
    // Move or resize the "Sea" object and change the material by hand afterwards. The camera background should keep the horizon colour.
    static void EnsureSea()
    {
        var scene = OpenGameplayScene();
        if (GameObject.Find("Sea") != null) return;
        var shader = Shader.Find("BeachTennisGO/Sea");
        if (shader == null) { Debug.LogWarning("[BeachTennisGO] Sea shader not found."); return; }

        const string matPath = "Assets/Materials/Sea.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
        var cam = Camera.main;
        if (cam != null) mat.SetColor("_HorizonColor", cam.backgroundColor);
        mat.SetFloat("_ShoreZ", 17f);
        EditorUtility.SetDirty(mat);

        var go = GameObject.CreatePrimitive(PrimitiveType.Plane); // 10 x 10 m at scale 1
        go.name = "Sea";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.position = new Vector3(0f, 0.004f, 14.5f + 200f); // starts at z 14.5 (before the shoreline), runs 400 m out
        go.transform.localScale = new Vector3(60f, 1f, 40f);           // 600 m wide
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Sea added to the scene.");
    }

    [MenuItem("Tools/Beach Tennis GO/Add Sea")]
    public static void AddSea() { EnsureSea(); }

    // Adds the Poki SDK service to the scene (once) and links it to the GameManager. The SDK script and the page-side bridge are in the
    // WebGL template. Poki events: gameplayStart on the player's first input of a match, gameplayStop when it ends, commercialBreak before the next one.
    static void EnsurePoki()
    {
        var scene = OpenGameplayScene();
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null) return;
        var service = Object.FindFirstObjectByType<PokiService>();
        if (service == null)
        {
            var go = new GameObject("PokiService", typeof(PokiService)); // the page calls back by this exact name
            service = go.GetComponent<PokiService>();
        }
        if (gm.poki == service) return;
        gm.poki = service;
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] Poki service added to the scene.");
    }

    [MenuItem("Tools/Beach Tennis GO/Add Poki SDK")]
    public static void AddPoki() { EnsurePoki(); }

    // Imports a UI sprite for the web: no mip maps, block compressed with crunch (small download), full 500 px resolution kept (no downscale).
    // The sizes were cropped to multiples of 4, so the GPU can compress them without resizing. `border` (left, bottom, right, top) = nine-slice.
    static Sprite ConfigureUiSprite(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 512;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.crunchedCompression = true;
        importer.compressionQuality = 85;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Creates the main menu in the HUD (once): the "Game mode" button (bottom left), the current mode label (top) and the selection window,
    // which slides in from the left with the three modes. Only Training does something so far. Everything is scene content to adjust by hand.
    static void EnsureGameModeMenu()
    {
        var scene = OpenGameplayScene();
        var view = Object.FindFirstObjectByType<HudView>();
        if (view == null || view.infoText == null) return;
        if (Object.FindFirstObjectByType<GameModeMenu>() != null) return;
        var canvas = view.infoText.canvas;

        var windowSprite = ConfigureUiSprite("Assets/Sprites/background-game-mode.png", new Vector4(104f, 104f, 104f, 104f));
        // One picture per language, from sheets sliced in the importer (<prefix>-en / -pt / -es).
        Sprite[] LangSprites(string sheet, string prefix)
        {
            var found = new Sprite[3];
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(sheet))
                if (o is Sprite sp) { int i = System.Array.IndexOf(new[] { prefix + "-en", prefix + "-pt", prefix + "-es" }, sp.name); if (i >= 0) found[i] = sp; }
            return found;
        }
        var gameModeSprites = LangSprites("Assets/Sprites/game-mode-lang-buttons.png", "game-mode");
        var trainingSprites = LangSprites("Assets/Sprites/training-mode-lang-button.png", "training");
        var tournamentSprites = LangSprites("Assets/Sprites/tournament-lang-button.png", "tournament");
        var gameModeSprite = gameModeSprites[0];
        var trainingSprite = trainingSprites[0];
        var tournamentSprite = tournamentSprites[0];
        var onlineSprite = ConfigureUiSprite("Assets/Sprites/online-mode-button.png", Vector4.zero);
        var font = AssetDatabase.LoadAssetAtPath<Font>(HudFontPath);

        var holder = new GameObject("GameModeMenu", typeof(RectTransform), typeof(GameModeMenu));
        holder.transform.SetParent(canvas.transform, false);
        Stretch((RectTransform)holder.transform);
        var menu = holder.GetComponent<GameModeMenu>();

        var root = new GameObject("Menu", typeof(RectTransform));
        root.transform.SetParent(holder.transform, false);
        Stretch((RectTransform)root.transform);
        menu.root = root;

        // Current mode, top centre.
        var labelGo = new GameObject("ModeLabel", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        labelGo.transform.SetParent(root.transform, false);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f);
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.anchoredPosition = new Vector2(0f, -85f);
        lrt.sizeDelta = new Vector2(880f, 120f);
        var label = labelGo.GetComponent<TMPro.TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/LilitaOne-Regular SDF.asset");
        label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 60; label.enableWordWrapping = false;
        label.color = Color.white; label.alignment = TMPro.TextAlignmentOptions.Center;
        label.raycastTarget = false; label.richText = false; label.text = "TRAINING MODE";
        label.outlineColor = new Color(0f, 0.1f, 0.25f, 0.85f); label.outlineWidth = 0.2f;
        menu.modeLabel = label;

        Button MakeButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white; colors.highlightedColor = Color.white; colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f); colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            btn.colors = colors;
            return btn;
        }

        // Round "Game mode" button, bottom left.
        menu.openButton = MakeButton("GameModeButton", root.transform, new Vector2(0f, 0f), new Vector2(85f, 85f), new Vector2(90f, 90f), gameModeSprite);
        menu.openButtonSprites = gameModeSprites; menu.trainingSprites = trainingSprites; menu.tournamentSprites = tournamentSprites;

        // Selection window: nine-slice background; parked off screen to the left (the script slides it in).
        var winGo = new GameObject("ModeWindow", typeof(RectTransform), typeof(Image));
        winGo.transform.SetParent(root.transform, false);
        var wrt = (RectTransform)winGo.transform;
        wrt.anchorMin = wrt.anchorMax = new Vector2(0f, 0.5f);
        wrt.pivot = new Vector2(0f, 0.5f);
        wrt.sizeDelta = new Vector2(320f, 500f); // opens half way: only the right half is on screen, the buttons sit in it
        wrt.anchoredPosition = new Vector2(-380f, 0f);
        var wimg = winGo.GetComponent<Image>();
        wimg.sprite = windowSprite; wimg.type = Image.Type.Sliced; wimg.pixelsPerUnitMultiplier = 2f; wimg.raycastTarget = true;
        menu.window = wrt;
        menu.trainingButton = MakeButton("TrainingButton", winGo.transform, new Vector2(0f, 0.5f), new Vector2(225f, 115f), new Vector2(96f, 96f), trainingSprite);
        menu.tournamentButton = MakeButton("TournamentButton", winGo.transform, new Vector2(0f, 0.5f), new Vector2(225f, 0f), new Vector2(96f, 96f), tournamentSprite);
        menu.onlineButton = MakeButton("OnlineButton", winGo.transform, new Vector2(0f, 0.5f), new Vector2(225f, -115f), new Vector2(96f, 96f), onlineSprite);

        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null) { gm.menu = menu; EditorUtility.SetDirty(gm); }
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] Game mode menu added to the HUD.");
    }

    static void Stretch(RectTransform rt) { Stretch(rt, 0f); }

    static void Stretch(RectTransform rt, float margin)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin); rt.offsetMax = new Vector2(-margin, -margin);
    }

    [MenuItem("Tools/Beach Tennis GO/Add Game Mode Menu")]
    public static void AddGameModeMenu() { EnsureGameModeMenu(); }

    // Win celebration (once): the big "YOU / WON" title in the HUD (white over yellow, gradient + thick dark outline like the game mode button
    // lettering) and a confetti ParticleSystem in front of the camera, which draws behind the HUD. Both are scene content to adjust by hand.
    static void EnsureWinCelebration()
    {
        var scene = OpenGameplayScene();
        var view = Object.FindFirstObjectByType<HudView>();
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (view == null || view.infoText == null || gm == null) return;
        bool changed = false;

        if (view.winTitle == null)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(HudFontPath);
            var root = new GameObject("WinTitle", typeof(RectTransform));
            root.transform.SetParent(view.infoText.canvas.transform, false);
            var rrt = (RectTransform)root.transform;
            rrt.anchorMin = new Vector2(0f, 0.5f); rrt.anchorMax = new Vector2(1f, 0.5f); // centred on the screen: clear of the result board at the top
            rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = Vector2.zero;
            rrt.sizeDelta = new Vector2(-40f, 300f);

            // Text of the group: yMin..yMax = share of the group's height; `lettering` = gradient + thick outline (like the game mode buttons).
            TMPro.TMP_Text Line(string name, float yMin, float yMax, int maxSize, Color top, Color bottom, bool lettering)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(UiTextGradient), typeof(Outline), typeof(Outline), typeof(Shadow));
                go.transform.SetParent(root.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0f, yMin); rt.anchorMax = new Vector2(1f, yMax);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var t = go.GetComponent<Text>();
                t.font = font; t.fontSize = maxSize; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 20; t.resizeTextMaxSize = maxSize; // shrinks to fit narrow (portrait) screens
                t.raycastTarget = false; t.supportRichText = false;
                t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
                t.text = name == "Line1" ? "YOU" : name == "Line2" ? "WON" : "Click / tap for next level";
                var g = go.GetComponent<UiTextGradient>(); g.top = top; g.bottom = bottom;
                var outlines = go.GetComponents<Outline>();
                var navy = new Color(0.03f, 0.12f, 0.38f, 1f);
                outlines[0].effectColor = navy; outlines[0].effectDistance = lettering ? new Vector2(6f, -6f) : new Vector2(3f, -3f);
                outlines[1].effectColor = navy; outlines[1].effectDistance = lettering ? new Vector2(3f, -3f) : new Vector2(1.5f, -1.5f);
                var sh = go.GetComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.4f); sh.effectDistance = new Vector2(0f, lettering ? -10f : -5f);
                return TmpConvert.Convert(t);
            }
            view.winLine1 = Line("Line1", 0.64f, 1f, 105, Color.white, new Color(0.72f, 0.88f, 1f), true);                    // white (to light blue)
            view.winLine2 = Line("Line2", 0.24f, 0.64f, 135, new Color(1f, 0.93f, 0.35f), new Color(1f, 0.55f, 0.05f), true); // yellow (to orange)
            view.winTitle = root;
            root.SetActive(false);
            EditorUtility.SetDirty(view);
            changed = true;
        }

        if (gm.confetti == null)
        {
            var shader = Shader.Find("BeachTennisGO/Confetti");
            const string matPath = "Assets/Materials/Confetti.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null && shader != null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            var cam = Camera.main;
            if (cam != null && mat != null)
            {
                var go = new GameObject("Confetti", typeof(ParticleSystem));
                go.transform.SetParent(cam.transform, false);
                go.transform.localPosition = new Vector3(0f, 0.4f, 8f); // in front of the camera, a bit above the centre of the screen
                go.transform.localRotation = Quaternion.identity;
                var ps = go.GetComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = ps.main;
                main.duration = 1f; main.loop = false; main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = 0.35f;
                main.maxParticles = 400;
                var colors = new Gradient();
                colors.SetKeys(new[]
                {
                    new GradientColorKey(new Color(1f, 0.25f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0.17f),
                    new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0.33f), new GradientColorKey(new Color(0.2f, 0.85f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.3f, 0.5f, 1f), 0.66f), new GradientColorKey(new Color(1f, 0.4f, 0.8f), 0.83f),
                    new GradientColorKey(Color.white, 1f)
                }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                main.startColor = new ParticleSystem.MinMaxGradient(colors) { mode = ParticleSystemGradientMode.RandomColor };

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 130), new ParticleSystem.Burst(0.2f, 70) }); // the pop, then a smaller second one

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle; // flat in the screen plane, so the pieces spread across the screen
                shape.radius = 0.35f; shape.radiusThickness = 1f; shape.arc = 360f;

                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true; limit.dampen = 0f; limit.drag = 2.2f; // slows the burst quickly and keeps the fall slow

                var noise = ps.noise;
                noise.enabled = true; noise.strength = 0.5f; noise.frequency = 0.6f; noise.scrollSpeed = 0.4f; noise.quality = ParticleSystemNoiseQuality.Low;

                var rot = ps.rotationOverLifetime;
                rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-5f, 5f);

                var fade = new Gradient();
                fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
                var col = ps.colorOverLifetime;
                col.enabled = true; col.color = new ParticleSystem.MinMaxGradient(fade);

                var rend = go.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = mat;
                rend.renderMode = ParticleSystemRenderMode.Billboard;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                gm.confetti = ps;
                EditorUtility.SetDirty(gm);
                changed = true;
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[BeachTennisGO] Win title and confetti added.");
        }
    }

    [MenuItem("Tools/Beach Tennis GO/Add Win Celebration")]
    public static void AddWinCelebration() { EnsureWinCelebration(); }

    const string ChainSpritePath = "Assets/Sprites/chain-link.png";

    // A tileable chain (one ring seen from the front, one seen edge-on), drawn here so no extra art is needed.
    static Sprite EnsureChainSprite()
    {
        if (!System.IO.File.Exists(ChainSpritePath))
        {
            const int w = 24, h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = 0f, shade = 0f;
                    // ring seen from the front, upper half of the tile
                    float rx = (x + 0.5f - 12f) / 9f, ry = (y + 0.5f - 36f) / 11.5f, rr = Mathf.Sqrt(rx * rx + ry * ry);
                    float ring = Mathf.Clamp01((1f - rr) * 8f) * Mathf.Clamp01((rr - 0.55f) * 8f);
                    if (ring > a) { a = ring; shade = 0.55f + 0.45f * Mathf.Clamp01(1f - (x + 0.5f) / w * 1.3f); }
                    // ring seen edge-on, lower half
                    float bx = Mathf.Abs(x + 0.5f - 12f), by = Mathf.Abs(y + 0.5f - 12f);
                    float bar = Mathf.Clamp01((3.6f - bx) * 2f) * Mathf.Clamp01((10.5f - by) * 2f);
                    if (bar > a) { a = bar; shade = 0.62f; }
                    byte v = (byte)(Mathf.Lerp(60f, 205f, shade));
                    px[y * w + x] = new Color32(v, (byte)(v + 4), (byte)(v + 12), (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            System.IO.File.WriteAllBytes(ChainSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ChainSpritePath, ImportAssetOptions.ForceUpdate);
        }
        var importer = AssetImporter.GetAtPath(ChainSpritePath) as TextureImporter;
        if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed; // 24 x 48: tiny, and block compression would smear the thin links
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // required to tile
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(ChainSpritePath);
    }

    // Hangs the result board from two chains (once): a "ScoreboardHanger" object at the top centre of the HUD holds the sign (the existing
    // ResultBoard, moved under it) and two tiled chain images, and animates it in / out through the top of the screen (HangingSign).
    static void EnsureScoreboardSign()
    {
        var scene = OpenGameplayScene();
        var view = Object.FindFirstObjectByType<HudView>();
        if (view == null || view.board == null || view.scoreboardSign != null) return;
        var board = (RectTransform)view.board.transform;
        var canvasTf = board.parent;
        var chainSprite = EnsureChainSprite();

        var hanger = new GameObject("ScoreboardHanger", typeof(RectTransform), typeof(HangingSign));
        hanger.transform.SetParent(canvasTf, false);
        hanger.transform.SetSiblingIndex(board.GetSiblingIndex());
        var hrt = (RectTransform)hanger.transform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = Vector2.zero;
        hrt.anchoredPosition = Vector2.zero;
        var sign = hanger.GetComponent<HangingSign>();

        RectTransform MakeChain(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(hanger.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(12f, 60f);
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = chainSprite; img.type = UnityEngine.UI.Image.Type.Tiled; img.pixelsPerUnitMultiplier = 2f; img.raycastTarget = false;
            return rt;
        }
        sign.chainLeft = MakeChain("ChainLeft");
        sign.chainRight = MakeChain("ChainRight");

        // The board keeps its place, size and scale; it now hangs a chain's length lower, under the hanger.
        board.SetParent(hanger.transform, false);
        board.anchoredPosition = new Vector2(board.anchoredPosition.x, board.anchoredPosition.y - sign.chainLength);
        sign.board = board;
        view.scoreboardSign = sign;
        EditorUtility.SetDirty(view);
        EditorUtility.SetDirty(sign);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] Scoreboard hung from two chains.");
    }

    [MenuItem("Tools/Beach Tennis GO/Add Hanging Scoreboard")]
    public static void AddHangingScoreboard() { EnsureScoreboardSign(); }

    const string RewardIconPath = "Assets/Sprites/reward-icon.png";

    // Small "video" icon (a clapperboard with a play triangle) for the rewarded button, drawn here so no extra art is needed.
    static Sprite EnsureRewardIcon()
    {
        if (!System.IO.File.Exists(RewardIconPath))
        {
            const int n = 96;
            var px = new Color32[n * n];
            Color32 clear = new Color32(0, 0, 0, 0), white = new Color32(255, 255, 255, 255), dark = new Color32(30, 30, 40, 255);
            bool InRound(float x, float y, float x0, float y0, float x1, float y1, float r)
            {
                float cx = Mathf.Clamp(x, x0 + r, x1 - r), cy = Mathf.Clamp(y, y0 + r, y1 - r);
                return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r && x >= x0 && x <= x1 && y >= y0 && y <= y1;
            }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Color32 c = clear;
                    if (InRound(x, y, 6, 6, 90, 56, 8)) c = white;                                   // body
                    if (InRound(x, y, 6, 62, 90, 86, 6)) c = ((x + y / 2) / 12) % 2 == 0 ? white : dark; // clapper with diagonal stripes
                    // play triangle in the body: (36,16) (36,48) (66,32)
                    float tx = x - 36f, ty = y - 32f;
                    if (tx >= 0 && tx <= 30 && Mathf.Abs(ty) <= 16f * (1f - tx / 30f) && y < 58) c = dark;
                    px[y * n + x] = c;
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            System.IO.File.WriteAllBytes(RewardIconPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(RewardIconPath, ImportAssetOptions.ForceUpdate);
        }
        var importer = AssetImporter.GetAtPath(RewardIconPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(RewardIconPath);
    }

    // Creates the lose screen buttons in the HUD (once): RESTART (standard, bigger, blue) above RETRY (rewarded ad: smaller, red, with a video
    // icon; never green). Poki rules: the standard option is always there, shown together with the rewarded one, at least as big and above it.
    // Also makes sure the canvas can receive clicks (GraphicRaycaster) and the scene has an EventSystem.
    static void EnsureLoseButtons()
    {
        var scene = OpenGameplayScene();
        var view = Object.FindFirstObjectByType<HudView>();
        if (view == null || view.infoText == null || view.loseButtons != null) return;
        var canvas = view.infoText.canvas;
        if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

        var font = AssetDatabase.LoadAssetAtPath<Font>(HudFontPath);
        var box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); // rounded, sliced
        var icon = EnsureRewardIcon();

        var root = new GameObject("LoseButtons", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        var rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.33f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(260f, 150f);
        rootRt.anchoredPosition = Vector2.zero;

        Button MakeButton(string name, Vector2 pos, Vector2 size, Color color, int fontSize, out TMPro.TMP_Text label, Sprite iconSprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = box; img.type = Image.Type.Sliced; img.color = Color.white;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
            colors.selectedColor = color;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.45f);
            btn.colors = colors;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            textGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(iconSprite != null ? 50f : 8f, 0f); trt.offsetMax = new Vector2(-8f, 0f);
            var lt = textGo.GetComponent<Text>();
            lt.font = font; lt.fontSize = fontSize; lt.color = Color.white; lt.alignment = TextAnchor.MiddleCenter;
            lt.raycastTarget = false; lt.supportRichText = false;
            lt.horizontalOverflow = HorizontalWrapMode.Overflow; lt.verticalOverflow = VerticalWrapMode.Overflow;
            lt.text = name.ToUpperInvariant();
            var o = textGo.GetComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.55f); o.effectDistance = new Vector2(2f, -2f);
            label = TmpConvert.Convert(lt);

            if (iconSprite != null)
            {
                var iconGo = new GameObject("VideoIcon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var irt = (RectTransform)iconGo.transform;
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = new Vector2(30f, 0f);
                irt.sizeDelta = new Vector2(38f, 38f);
                var iimg = iconGo.GetComponent<Image>();
                iimg.sprite = iconSprite; iimg.raycastTarget = false; iimg.preserveAspect = true;
            }
            return btn;
        }

        view.restartButton = MakeButton("Restart", new Vector2(0f, 40f), new Vector2(240f, 68f), new Color(0.18f, 0.50f, 0.87f), 32, out view.restartLabel, null);
        view.retryButton = MakeButton("Retry", new Vector2(0f, -34f), new Vector2(210f, 56f), new Color(0.84f, 0.27f, 0.27f), 26, out view.retryLabel, icon);
        view.loseButtons = root;
        root.SetActive(false);
        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] Lose screen buttons added to the HUD.");
    }

    [MenuItem("Tools/Beach Tennis GO/Add Lose Buttons")]
    public static void AddLoseButtons() { EnsureLoseButtons(); }

    const string SandTexturePath = "Assets/Sprites/sand-texture.jpeg";

    // Puts the seamless sand texture on the ground: the big "Sand" slab and the slightly darker court area share one world-space
    // mapping (shader BeachTennisGO/GroundSand), so the grain lines up between them. Safe to run again.
    static void EnsureSandTexture()
    {
        var importer = AssetImporter.GetAtPath(SandTexturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.npotScale = TextureImporterNPOTScale.ToNearest; // 447 px -> 512 px, so it can be block compressed
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = 60;
            importer.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SandTexturePath);
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(SandNormalMap.Output) == null) SandNormalMap.Generate();
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(SandNormalMap.Output);
        var shader = Shader.Find("BeachTennisGO/GroundSand");
        if (tex == null || shader == null) { Debug.LogWarning("[BeachTennisGO] Sand texture or GroundSand shader not found."); return; }

        void Apply(string name, Color tint)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + name + ".mat");
            if (m == null) return;
            m.shader = shader;
            m.SetTexture("_MainTex", tex);
            m.SetTexture("_BumpMap", normal);
            m.SetColor("_Color", tint);
            m.SetFloat("_TileSize", 5f);
            EditorUtility.SetDirty(m);
        }
        Apply("Sand", Color.white);
        Apply("CourtArea", new Color(0.95f, 0.92f, 0.84f)); // the court is a touch darker than the sand around it
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Sand texture and normal map applied to the Sand and CourtArea materials.");
    }

    [MenuItem("Tools/Beach Tennis GO/Apply Sand Texture")]
    public static void ApplySandTexture() { EnsureSandTexture(); }

    const string BoardSpritePath = "Assets/Sprites/result-board.png";
    const string DigitalFontPath = "Assets/Fonts/digital-7 (mono).ttf";

    // Creates the result board in the HUD (once): the board image as the background and one Text per digit placed over its windows
    // (player points, CPU points, hours / minutes / seconds). Positions are in pixels of the 612 x 408 image, measured from its top-left corner,
    // and the whole board is scaled from the "ResultBoard" object, so it can be moved and resized by hand afterwards.
    // A board that still has the older two-digit texts is converted: each is replaced by two single-digit texts that keep its font, size,
    // colour and centre, so adjustments made by hand are not lost.
    static void EnsureResultBoard()
    {
        var scene = OpenGameplayScene();
        var view = Object.FindFirstObjectByType<HudView>();
        if (view == null || view.infoText == null) return;
        if (view.board != null && view.playerDigits != null && view.playerDigits.Length == 2 && view.playerDigits[0] != null) return;

        var font = AssetDatabase.LoadAssetAtPath<Font>(DigitalFontPath);
        var led = new Color(1f, 0.28f, 0.12f);   // red-orange segments
        var amber = new Color(1f, 0.62f, 0.12f);
        Transform root;
        bool fresh = view.board == null;
        if (fresh)
        {
            var importer = AssetImporter.GetAtPath(BoardSpritePath) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            var canvasTf = view.infoText.canvas.transform;
            var oldScore = canvasTf.Find("Score"); // the plain text score this board replaces
            if (oldScore != null) Object.DestroyImmediate(oldScore.gameObject);

            var go = new GameObject("ResultBoard", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvasTf, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(612f, 408f);
            rt.localScale = Vector3.one * 0.45f;
            rt.anchoredPosition = new Vector2(0f, 19f); // hides the empty margin above the board in the image
            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BoardSpritePath); img.raycastTarget = false; img.preserveAspect = true;
            view.board = go;
            var info = (RectTransform)view.infoText.transform;
            info.anchoredPosition = new Vector2(info.anchoredPosition.x, -140f); // below the board
        }
        root = view.board.transform;

        // Two single-digit texts around a pair centre (cx, cy), in image pixels; `old` (a two-digit text) is replaced if it exists.
        TMPro.TMP_Text[] Pair(string name, float cx, float cy, float dx, float w, float h, int size, Color color)
        {
            var old = root.Find(name);
            if (old != null)
            {
                var ot = old.GetComponent<Text>();
                var ort = (RectTransform)old;
                cx = ort.anchoredPosition.x; cy = -ort.anchoredPosition.y; h = ort.sizeDelta.y;
                if (ot != null) { size = ot.fontSize; color = ot.color; if (ot.font != null) font = ot.font; }
                Object.DestroyImmediate(old.gameObject);
            }
            var pair = new TMPro.TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(name + (i == 0 ? "_Tens" : "_Units"), typeof(RectTransform), typeof(Text));
                go.transform.SetParent(root, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(cx + (i == 0 ? -dx : dx), -cy);
                r.sizeDelta = new Vector2(w, h);
                var t = go.GetComponent<Text>();
                t.font = font; t.fontSize = size; t.color = color; t.text = "0";
                t.alignment = TextAnchor.MiddleCenter;
                t.raycastTarget = false; t.supportRichText = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                pair[i] = TmpConvert.Convert(t);
            }
            return pair;
        }
        view.playerDigits = Pair("PlayerScore", 207f, 153f, 30f, 60f, 90f, 120, led);
        view.npcDigits = Pair("NpcScore", 405f, 153f, 30f, 60f, 90f, 120, led);
        var hours = Pair("Hours", 208f, 267f, 21f, 42f, 66f, 86, amber);
        var minutes = Pair("Minutes", 308f, 267f, 21f, 42f, 66f, 86, amber);
        var seconds = Pair("Seconds", 406f, 267f, 21f, 42f, 66f, 86, amber);
        view.timeDigits = new[] { hours[0], hours[1], minutes[0], minutes[1], seconds[0], seconds[1] };

        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] Result board digits ready (" + (fresh ? "created" : "converted to one Text per digit") + ").");
    }

    // Win screen: CONTINUE under the title (next level). Replaces the old "tap to continue" hint.
    static void EnsureWinNavigation()
    {
        var scene = OpenGameplayScene();
        var view = Object.FindFirstObjectByType<HudView>();
        var menu = Object.FindFirstObjectByType<GameModeMenu>();
        if (view == null || view.winTitle == null || menu == null || menu.window == null) return;
        if (view.winContinueButton != null) return;

        var font = AssetDatabase.LoadAssetAtPath<Font>(HudFontPath);
        var box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); // rounded, sliced

        Button MakeButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, int fontSize, out TMPro.TMP_Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = box; img.type = Image.Type.Sliced; img.color = Color.white;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
            colors.selectedColor = color;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.45f);
            btn.colors = colors;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            textGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 0f); trt.offsetMax = new Vector2(-8f, 0f);
            var lt = textGo.GetComponent<Text>();
            lt.font = font; lt.fontSize = fontSize; lt.color = Color.white; lt.alignment = TextAnchor.MiddleCenter;
            lt.raycastTarget = false; lt.supportRichText = false;
            lt.horizontalOverflow = HorizontalWrapMode.Overflow; lt.verticalOverflow = VerticalWrapMode.Overflow;
            lt.text = name.ToUpperInvariant();
            var o = textGo.GetComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.55f); o.effectDistance = new Vector2(2f, -2f);
            label = TmpConvert.Convert(lt);
            label.enableAutoSizing = true; label.fontSizeMin = 8; label.fontSizeMax = fontSize;
            return btn;
        }

        if (view.winContinueButton == null)
        {
            var hint = view.winTitle.transform.Find("Hint");
            if (hint != null) Object.DestroyImmediate(hint.gameObject);
            view.winContinueButton = MakeButton("Continue", view.winTitle.transform, new Vector2(0.5f, 0.1f), Vector2.zero, new Vector2(300f, 76f), new Color(0.2f, 0.66f, 0.3f), 34, out view.winContinueLabel);
            EditorUtility.SetDirty(view);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] Win screen CONTINUE button added.");
    }

    [MenuItem("Tools/Beach Tennis GO/Add Win Screen Buttons")]
    public static void AddWinNavigation() { EnsureWinNavigation(); }

    // BACK (to the main menu) on the win and lose screens, and the black veil behind the game mode window.
    static void EnsureBackButtonsAndFade()
    {
        var scene = OpenGameplayScene();
        var view = Object.FindFirstObjectByType<HudView>();
        var menu = Object.FindFirstObjectByType<GameModeMenu>();
        if (view == null || view.winTitle == null || view.loseButtons == null || menu == null || menu.window == null) return;

        var font = AssetDatabase.LoadAssetAtPath<Font>(HudFontPath);
        var box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); // rounded, sliced

        Button MakeButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, int fontSize, out TMPro.TMP_Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = box; img.type = Image.Type.Sliced; img.color = Color.white;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
            colors.selectedColor = color;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.45f);
            btn.colors = colors;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            textGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 0f); trt.offsetMax = new Vector2(-8f, 0f);
            var lt = textGo.GetComponent<Text>();
            lt.font = font; lt.fontSize = fontSize; lt.color = Color.white; lt.alignment = TextAnchor.MiddleCenter;
            lt.raycastTarget = false; lt.supportRichText = false;
            lt.horizontalOverflow = HorizontalWrapMode.Overflow; lt.verticalOverflow = VerticalWrapMode.Overflow;
            lt.text = "BACK";
            var o = textGo.GetComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.55f); o.effectDistance = new Vector2(2f, -2f);
            label = TmpConvert.Convert(lt);
            label.enableAutoSizing = true; label.fontSizeMin = 8; label.fontSizeMax = fontSize;
            return btn;
        }

        var blue = new Color(0.18f, 0.5f, 0.87f);
        if (view.winBackButton == null)
        {
            view.winBackButton = MakeButton("Back", view.winTitle.transform, new Vector2(0.5f, 0.1f), new Vector2(0f, -84f), new Vector2(200f, 58f), blue, 28, out view.winBackLabel);
            EditorUtility.SetDirty(view);
        }
        if (view.loseBackButton == null)
        {
            view.loseBackButton = MakeButton("Back", view.loseButtons.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -104f), new Vector2(190f, 52f), blue, 26, out view.loseBackLabel);
            EditorUtility.SetDirty(view);
        }
        if (menu.fade == null)
        {
            var go = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(menu.root.transform, false);
            Stretch((RectTransform)go.transform);
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); img.raycastTarget = true; // black veil: blocks every click behind the window
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img; btn.transition = Selectable.Transition.None;
            go.transform.SetAsFirstSibling();
            go.SetActive(false);
            menu.fade = img;
            EditorUtility.SetDirty(menu);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] Back buttons (win / lose screens) and the game mode fade added.");
    }

    [MenuItem("Tools/Beach Tennis GO/Add Back Buttons And Fade")]
    public static void AddBackButtonsAndFade() { EnsureBackButtonsAndFade(); }

    // Settings: the gear button (top right) and a full-screen window on a calm sand -> sea gradient with the match rules and the sound switches.
    static void EnsureSettingsWindow()
    {
        var scene = OpenGameplayScene();
        var view = Object.FindFirstObjectByType<HudView>();
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (view == null || view.infoText == null || gm == null) return;
        if (Object.FindFirstObjectByType<SettingsWindow>() != null) return;

        var canvas = view.infoText.canvas.transform;
        var font = AssetDatabase.LoadAssetAtPath<Font>(HudFontPath);
        var box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); // rounded, sliced
        var navy = new Color(0.03f, 0.12f, 0.38f, 1f);

        // Calm background: the sunburst shader without rays, dashes or spin, in the colours of the beach (sand, sea, deep sea).
        const string matPath = "Assets/Materials/SettingsBackground.mat";
        var bgMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (bgMat == null)
        {
            bgMat = new Material(Shader.Find("BeachTennisGO/RaysBackground"));
            bgMat.SetColor("_CenterColor", new Color(0.97f, 0.87f, 0.62f));
            bgMat.SetColor("_MidColor", new Color(0.22f, 0.70f, 0.86f));
            bgMat.SetColor("_EdgeColor", new Color(0.06f, 0.25f, 0.58f));
            bgMat.SetFloat("_MidRadius", 0.45f);
            bgMat.SetFloat("_RayStrength", 0f);
            bgMat.SetFloat("_Spin", 0f);
            bgMat.SetColor("_DashColor", new Color(1f, 1f, 1f, 0f));
            AssetDatabase.CreateAsset(bgMat, matPath);
        }

        TMPro.TMP_Text MakeLabel(string name, Transform parent, int size, TMPro.TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false; t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.text = name;
            var o = go.GetComponent<Outline>(); o.effectColor = navy; o.effectDistance = new Vector2(3f, -3f);
            var tmp = TmpConvert.Convert(t);
            tmp.alignment = align; tmp.enableAutoSizing = true; tmp.fontSizeMin = 8; tmp.fontSizeMax = size; tmp.enableWordWrapping = false;
            return tmp;
        }

        Button MakeButton(string name, Transform parent, Color color, string label, int size, out TMPro.TMP_Text text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = box; img.type = Image.Type.Sliced; img.color = Color.white;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
            colors.selectedColor = color;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.4f);
            btn.colors = colors;
            text = MakeLabel("Label", go.transform, size, TMPro.TextAlignmentOptions.Center);
            text.text = label;
            Stretch((RectTransform)text.transform, 6f);
            return btn;
        }

        // --- window ---
        var root = new GameObject("SettingsWindow", typeof(RectTransform));
        root.transform.SetParent(canvas, false);
        Stretch((RectTransform)root.transform, 0f);
        // The component sits on an object that is always active: on the (hidden) window it would never get its Awake, and no button would be wired.
        var holder = new GameObject("SettingsController", typeof(RectTransform));
        holder.transform.SetParent(canvas, false);
        var sw = holder.AddComponent<SettingsWindow>();
        sw.window = root;

        var bg = new GameObject("Background", typeof(RectTransform), typeof(RawImage), typeof(RaysBackground));
        bg.transform.SetParent(root.transform, false);
        Stretch((RectTransform)bg.transform, 0f);
        var bgImg = bg.GetComponent<RawImage>();
        bgImg.material = bgMat; bgImg.raycastTarget = true; // blocks every click behind the window

        sw.title = MakeLabel("Title", root.transform, 64, TMPro.TextAlignmentOptions.Center);
        var trt = (RectTransform)sw.title.transform;
        trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -28f); trt.sizeDelta = new Vector2(-40f, 90f);

        // rows: label on the left, control on the right
        const float rowH = 64f, gap = 14f;
        var rows = new GameObject("Rows", typeof(RectTransform));
        rows.transform.SetParent(root.transform, false);
        var rowsRt = (RectTransform)rows.transform;
        rowsRt.anchorMin = new Vector2(0f, 0.5f); rowsRt.anchorMax = new Vector2(1f, 0.5f); rowsRt.pivot = new Vector2(0.5f, 0.5f);
        rowsRt.anchoredPosition = new Vector2(0f, -5f); rowsRt.sizeDelta = new Vector2(760f, 5 * rowH + 4 * gap);
        sw.content = rowsRt; // SettingsWindow keeps it as wide as the screen allows, up to 760

        RectTransform MakeRow(int index, string name, out TMPro.TMP_Text label)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(rows.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -index * (rowH + gap)); rt.sizeDelta = new Vector2(0f, rowH);
            label = MakeLabel("Label", go.transform, 34, TMPro.TextAlignmentOptions.Left);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0.5f, 1f); lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var control = new GameObject("Control", typeof(RectTransform));
            control.transform.SetParent(go.transform, false);
            var crt = (RectTransform)control.transform;
            crt.anchorMin = new Vector2(0.52f, 0f); crt.anchorMax = new Vector2(1f, 1f); crt.offsetMin = crt.offsetMax = Vector2.zero;
            return crt;
        }

        void Stepper(RectTransform control, out Button less, out Button more, out TMPro.TMP_Text value)
        {
            TMPro.TMP_Text dummy;
            less = MakeButton("Less", control, new Color(0.18f, 0.5f, 0.87f), "-", 40, out dummy);
            more = MakeButton("More", control, new Color(0.18f, 0.5f, 0.87f), "+", 40, out dummy);
            var lr = (RectTransform)less.transform; lr.anchorMin = lr.anchorMax = new Vector2(0f, 0.5f); lr.pivot = new Vector2(0f, 0.5f); lr.anchoredPosition = Vector2.zero; lr.sizeDelta = new Vector2(rowH, rowH);
            var mr = (RectTransform)more.transform; mr.anchorMin = mr.anchorMax = new Vector2(1f, 0.5f); mr.pivot = new Vector2(1f, 0.5f); mr.anchoredPosition = Vector2.zero; mr.sizeDelta = new Vector2(rowH, rowH);
            value = MakeLabel("Value", control, 44, TMPro.TextAlignmentOptions.Center);
            var vr = (RectTransform)value.transform; vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = new Vector2(rowH + 6f, 0f); vr.offsetMax = new Vector2(-rowH - 6f, 0f);
        }

        void Switch(RectTransform control, out Button button, out TMPro.TMP_Text state)
        {
            button = MakeButton("Switch", control, new Color(0.2f, 0.66f, 0.3f), "ON", 32, out state);
            var br = (RectTransform)button.transform; br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;
        }

        Stepper(MakeRow(0, "PointsRow", out sw.pointsLabel), out sw.pointsLess, out sw.pointsMore, out sw.pointsValue);
        Stepper(MakeRow(1, "SetsRow", out sw.setsLabel), out sw.setsLess, out sw.setsMore, out sw.setsValue);
        Switch(MakeRow(2, "VoiceRow", out sw.voiceLabel), out sw.voiceButton, out sw.voiceState);
        Switch(MakeRow(3, "MusicRow", out sw.musicLabel), out sw.musicButton, out sw.musicState);
        Switch(MakeRow(4, "SfxRow", out sw.sfxLabel), out sw.sfxButton, out sw.sfxState);

        sw.closeButton = MakeButton("Back", root.transform, new Color(0.18f, 0.5f, 0.87f), "BACK", 34, out sw.closeLabel);
        var cr = (RectTransform)sw.closeButton.transform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f); cr.pivot = new Vector2(0.5f, 0f); cr.anchoredPosition = new Vector2(0f, 36f); cr.sizeDelta = new Vector2(240f, 66f);

        // --- gear button, top right ---
        var gearSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/settings-button.png");
        var gear = new GameObject("SettingsButton", typeof(RectTransform), typeof(Image), typeof(Button));
        gear.transform.SetParent(canvas, false);
        var grt = (RectTransform)gear.transform;
        grt.anchorMin = grt.anchorMax = new Vector2(1f, 1f); grt.pivot = new Vector2(0.5f, 0.5f);
        grt.anchoredPosition = new Vector2(-55f, -55f); grt.sizeDelta = new Vector2(60f, 60f);
        var gimg = gear.GetComponent<Image>();
        gimg.sprite = gearSprite; gimg.preserveAspect = true;
        sw.openButton = gear.GetComponent<Button>();
        root.transform.SetAsLastSibling();

        gm.settings = sw;
        root.SetActive(false);
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] Settings button and window added.");
    }

    [MenuItem("Tools/Beach Tennis GO/Add Settings Window")]
    public static void AddSettingsWindow() { EnsureSettingsWindow(); }

    [MenuItem("Tools/Beach Tennis GO/Add Result Board")]
    public static void AddResultBoard() { EnsureResultBoard(); }

    const string BallIconPath = "Assets/Sprites/ball2D.png";

    // Creates the "LandingAlert" object under the HUD canvas (once): blinking ball icon + red arrow shown when the landing marker is off screen.
    static void EnsureLandingAlert()
    {
        var scene = OpenGameplayScene();
        var hud = Object.FindFirstObjectByType<HudView>();
        if (hud == null || hud.infoText == null) return;
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

        var canvasTf = hud.infoText.canvas.transform;
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
    public static void AddHudCanvas() { EnsureHud(); EnsureResultBoard(); EnsureScoreboardSign(); EnsureLandingAlert(); EnsureLoseButtons(); EnsureGameModeMenu(); EnsureWinCelebration(); EnsureWinNavigation(); EnsureBackButtonsAndFade(); EnsureSettingsWindow(); }

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
        EnsureResultBoard();
        EnsureScoreboardSign();
        EnsureLandingAlert();
        EnsureLoseButtons();
        EnsureGameModeMenu();
        EnsureWinCelebration();
        EnsureSandTexture();
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

        var scene = OpenGameplayScene();
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
