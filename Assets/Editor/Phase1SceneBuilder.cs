using RealMolkky.DebugTools;
using RealMolkky.Molkky;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// フェーズ1の物理サンドボックスのシーンを生成する。
/// メニュー: Build/Create Phase1 Sandbox Scene
/// コマンドライン: -executeMethod Phase1SceneBuilder.CreateAndBuild
/// </summary>
public static class Phase1SceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Phase1Sandbox.unity";
    const string MeshDir = "Assets/Meshes";
    const string MatDir = "Assets/Materials/Phase1";

    /// <summary>シーンに置いた物理まわりの参照</summary>
    public struct World
    {
        public GameObject root;
        public MolkkyRig rig;
        public ThrowSimulator simulator;
        public ThrowPlayer player;
        public Camera camera;
    }

    [MenuItem("Build/Create Phase1 Sandbox Scene")]
    public static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var world = CreateWorld("Phase1Sandbox");
        var ui = world.root.AddComponent<Phase1SandboxUI>();
        ui.Bind(world.rig, world.simulator, world.player, world.camera);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Phase2SceneBuilder.ApplyBuildScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Phase1SceneBuilder] Created {ScenePath}");
    }

    /// <summary>光・地面・投げるライン・ピン・棒・カメラと、物理の部品を今のシーンに作る（フェーズ2でも使う）</summary>
    public static World CreateWorld(string rootName)
    {

        // 光（フェーズ0と同じ仮のもの）
        var lightGo = new GameObject("Sun");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.96f, 0.9f);
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.6f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.3f, 0.2f);

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var grassMat = CreateMat(lit, "Grass", new Color(0.32f, 0.52f, 0.22f), 0.15f);
        var pinMat = CreateMat(lit, "Pin", new Color(0.86f, 0.74f, 0.55f), 0.35f);
        var stickMat = CreateMat(lit, "Stick", new Color(0.62f, 0.42f, 0.24f), 0.35f);
        var lineMat = CreateMat(lit, "Line", Color.white, 0.1f);

        var pinMesh = SaveMesh(MolkkyMeshFactory.CreatePin(), "MolkkyPin");
        var stickMesh = SaveMesh(MolkkyMeshFactory.CreateStick(), "MolkkyStick");

        // 地面: 見た目は Plane、当たり判定は厚みのある箱（すり抜け対策）
        var ground = new GameObject("Ground");
        var groundVisual = GameObject.CreatePrimitive(PrimitiveType.Plane);
        groundVisual.name = "Visual";
        Object.DestroyImmediate(groundVisual.GetComponent<Collider>());
        groundVisual.transform.SetParent(ground.transform);
        groundVisual.transform.localPosition = new Vector3(0f, 0f, MolkkySpec.ThrowDistance * 0.5f);
        groundVisual.transform.localScale = new Vector3(2f, 1f, 2f); // 20m x 20m
        groundVisual.GetComponent<Renderer>().sharedMaterial = grassMat;
        var groundBox = ground.AddComponent<BoxCollider>();
        groundBox.center = new Vector3(0f, -0.5f, MolkkySpec.ThrowDistance * 0.5f);
        groundBox.size = new Vector3(20f, 1f, 20f);

        // 投げるライン
        var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "ThrowLine";
        Object.DestroyImmediate(line.GetComponent<Collider>());
        line.transform.position = new Vector3(0f, 0.002f, 0f);
        line.transform.localScale = new Vector3(1.0f, 0.004f, 0.03f);
        line.GetComponent<Renderer>().sharedMaterial = lineMat;

        // ピン
        var pinsRoot = new GameObject("Pins").transform;
        var pinBodies = new Rigidbody[MolkkySpec.PinCount];
        for (int n = 1; n <= MolkkySpec.PinCount; n++)
        {
            var pin = CreateBody($"Pin_{n:00}", pinMesh, pinMat);
            pin.transform.SetParent(pinsRoot);
            pin.transform.position = MolkkySpec.InitialPinPosition(n);
            pinBodies[n - 1] = pin.GetComponent<Rigidbody>();
        }

        // 棒
        var stick = CreateBody("Stick", stickMesh, stickMat);
        stick.transform.position = new Vector3(0f, 0.6f, 0.2f);

        // カメラ（投げる人の背後）
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.05f;
        cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0f, 1.5f, -1.2f);
        camGo.transform.LookAt(new Vector3(0f, 0.05f, MolkkySpec.ThrowDistance + 0.08f));

        // 物理と記録
        var root = new GameObject(rootName);
        var rig = root.AddComponent<MolkkyRig>();
        rig.Bind(stick.GetComponent<Rigidbody>(), pinBodies, groundBox);
        var sim = root.AddComponent<ThrowSimulator>();
        sim.Bind(rig);
        var player = root.AddComponent<ThrowPlayer>();
        player.Bind(rig);

        return new World { root = root, rig = rig, simulator = sim, player = player, camera = cam };
    }

    static GameObject CreateBody(string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        var col = go.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
        col.convex = true;
        var rb = go.AddComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        return go;
    }

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets", "Meshes");
        string path = $"{MeshDir}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Material CreateMat(Shader shader, string name, Color color, float smoothness)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Materials", "Phase1");

        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>コマンドライン用: シーン生成→WebGLビルド</summary>
    public static void CreateAndBuild()
    {
        Create();
        WebGLBuilder.Build();
    }
}
