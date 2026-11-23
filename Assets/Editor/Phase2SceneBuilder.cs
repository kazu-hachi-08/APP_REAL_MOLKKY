using System.IO;
using System.Linq;
using RealMolkky.DebugTools;
using RealMolkky.Play;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// フェーズ2の投げる操作のシーンを生成する。物理まわりはフェーズ1と同じものを使う。
/// メニュー: Build/Create Phase2 Throw Scene
/// コマンドライン: -executeMethod Phase2SceneBuilder.CreateAndBuild
/// </summary>
public static class Phase2SceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Phase2Throw.unity";

    [MenuItem("Build/Create Phase2 Throw Scene")]
    public static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var world = Phase1SceneBuilder.CreateWorld("Phase2Throw");

        var throwCam = world.camera.gameObject.AddComponent<ThrowCamera>();
        throwCam.Bind(world.camera);

        var controller = world.root.AddComponent<ThrowController>();
        controller.Bind(world.rig, world.simulator, throwCam);

        var ui = world.root.AddComponent<Phase2ThrowUI>();
        ui.Bind(world.rig, world.simulator, controller, world.camera);

        EditorSceneManager.SaveScene(scene, ScenePath);
        ApplyBuildScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Phase2SceneBuilder] Created {ScenePath}");
    }

    /// <summary>ビルドに入れるシーン。最初のシーンが起動時に開く</summary>
    public static void ApplyBuildScenes()
    {
        EditorBuildSettings.scenes = new[] { ScenePath, Phase1SceneBuilder.ScenePath }
            .Where(File.Exists)
            .Select(p => new EditorBuildSettingsScene(p, true))
            .ToArray();
    }

    /// <summary>コマンドライン用: 両方のシーンを作り直して WebGL ビルド</summary>
    public static void CreateAndBuild()
    {
        Phase1SceneBuilder.Create();
        Create();
        WebGLBuilder.Build();
    }
}
