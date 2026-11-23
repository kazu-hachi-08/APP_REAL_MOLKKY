using RealMolkky.DebugTools;
using RealMolkky.Play;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// フェーズ3の1台で対戦するシーンを生成する。物理と投げる操作はフェーズ2と同じものを使う。
/// メニュー: Build/Create Phase3 Match Scene
/// コマンドライン: -executeMethod Phase3SceneBuilder.CreateAndBuild
/// </summary>
public static class Phase3SceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Phase3Match.unity";

    [MenuItem("Build/Create Phase3 Match Scene")]
    public static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var world = Phase1SceneBuilder.CreateWorld("Phase3Match");

        var throwCam = world.camera.gameObject.AddComponent<ThrowCamera>();
        throwCam.Bind(world.camera);

        var controller = world.root.AddComponent<ThrowController>();
        controller.Bind(world.rig, world.simulator, throwCam);

        var match = world.root.AddComponent<MatchController>();
        match.Bind(world.rig, world.simulator, controller);

        var ui = world.root.AddComponent<Phase3MatchUI>();
        ui.Bind(match, controller);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Phase2SceneBuilder.ApplyBuildScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Phase3SceneBuilder] Created {ScenePath}");
    }

    /// <summary>コマンドライン用: すべてのシーンを作り直して WebGL ビルド</summary>
    public static void CreateAndBuild()
    {
        Phase1SceneBuilder.Create();
        Phase2SceneBuilder.Create();
        Create();
        WebGLBuilder.Build();
    }
}
