using RealMolkky.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// フェーズ0の動作確認シーンを生成する。
/// 棒とピンは仮の円柱（本物の寸法）で、見た目はフェーズ4で作り込む。
/// </summary>
public static class Phase0SceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Phase0Check.unity";

    const float PinDiameter = 0.055f;
    const float PinHeight = 0.15f;
    const float StickDiameter = 0.055f;
    const float StickLength = 0.225f;
    const float ThrowDistance = 3.5f;

    // 手前の列から（仕様書の並び）
    static readonly int[][] PinRows =
    {
        new[] { 1, 2 },
        new[] { 3, 10, 4 },
        new[] { 5, 11, 12, 6 },
        new[] { 7, 9, 8 },
    };

    [MenuItem("Build/Create Phase0 Check Scene")]
    public static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 光
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

        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        var grassMat = CreateMat(litShader, "Phase0_Grass", new Color(0.32f, 0.52f, 0.22f), 0.15f);
        var woodMat = CreateMat(litShader, "Phase0_Wood", new Color(0.82f, 0.68f, 0.48f), 0.35f);
        var stickMat = CreateMat(litShader, "Phase0_Stick", new Color(0.7f, 0.5f, 0.3f), 0.35f);

        // 地面（10m x 10m）
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, 0f, ThrowDistance * 0.5f);
        ground.GetComponent<Renderer>().sharedMaterial = grassMat;

        // 投げるライン
        var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "ThrowLine";
        line.transform.position = new Vector3(0f, 0.002f, 0f);
        line.transform.localScale = new Vector3(1.0f, 0.004f, 0.03f);
        Object.DestroyImmediate(line.GetComponent<Collider>());
        line.GetComponent<Renderer>().sharedMaterial = CreateMat(litShader, "Phase0_Line", Color.white, 0.1f);

        // ピン（円柱プリミティブは 高さ2 / 直径1）
        var pinsRoot = new GameObject("Pins").transform;
        float rowStep = PinDiameter * 0.866f; // 隣り合うピンが接する三角形の並び
        for (int r = 0; r < PinRows.Length; r++)
        {
            var row = PinRows[r];
            float z = ThrowDistance + r * rowStep;
            float startX = -(row.Length - 1) * PinDiameter * 0.5f;
            for (int i = 0; i < row.Length; i++)
            {
                var pin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pin.name = $"Pin_{row[i]:00}";
                pin.transform.SetParent(pinsRoot);
                pin.transform.position = new Vector3(startX + i * PinDiameter, PinHeight * 0.5f, z);
                pin.transform.localScale = new Vector3(PinDiameter, PinHeight * 0.5f, PinDiameter);
                pin.GetComponent<Renderer>().sharedMaterial = woodMat;
            }
        }

        // 棒（投げるライン上に横向きに置く）
        var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stick.name = "Stick";
        stick.transform.position = new Vector3(0.25f, StickDiameter * 0.5f, 0.15f);
        stick.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        stick.transform.localScale = new Vector3(StickDiameter, StickLength * 0.5f, StickDiameter);
        stick.GetComponent<Renderer>().sharedMaterial = stickMat;

        // カメラ（投げる人の背後）
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.05f;
        cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0f, 1.5f, -1.2f);
        camGo.transform.LookAt(new Vector3(0f, 0.05f, ThrowDistance + 0.08f));

        // 端末情報
        new GameObject("DeviceInfoOverlay").AddComponent<DeviceInfoOverlay>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log($"[Phase0SceneBuilder] Created {ScenePath}");
    }

    static Material CreateMat(Shader shader, string name, Color color, float smoothness)
    {
        const string dir = "Assets/Materials/Phase0";
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Materials", "Phase0");

        string path = $"{dir}/{name}.mat";
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
