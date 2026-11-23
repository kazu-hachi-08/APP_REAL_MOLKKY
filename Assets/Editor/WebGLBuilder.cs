using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// WebGL の設定とビルドを行う。
/// メニュー: Build/WebGL、またはコマンドラインから
///   Unity.exe -batchmode -quit -projectPath . -executeMethod WebGLBuilder.Build
/// </summary>
public static class WebGLBuilder
{
    public const string OutputDir = "Builds/WebGL";

    [MenuItem("Build/Apply WebGL Settings")]
    public static void ApplySettings()
    {
        PlayerSettings.productName = "RealMolkky";

        // GitHub Pages は Content-Encoding を付けられないため、
        // 圧縮ファイルをブラウザ側で展開する Decompression Fallback を使う
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = true; // 更新時にキャッシュが残らないように

        // スマホでも動くよう、例外は最小限
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;

        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
        PlayerSettings.runInBackground = true;

        AssetDatabase.SaveAssets();
        Debug.Log("[WebGLBuilder] WebGL settings applied.");
    }

    [MenuItem("Build/WebGL")]
    public static void Build()
    {
        ApplySettings();

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        }

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("Build Settings にシーンが登録されていません。");
        }

        if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, true);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });

        var summary = report.summary;
        Debug.Log($"[WebGLBuilder] Result: {summary.result}, size: {summary.totalSize / (1024f * 1024f):F1} MB, time: {summary.totalTime}");

        if (summary.result != BuildResult.Succeeded)
        {
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw new Exception("WebGL build failed.");
        }

        // GitHub Pages で Jekyll 処理を無効化（_ で始まるファイル対策）
        File.WriteAllText(Path.Combine(OutputDir, ".nojekyll"), string.Empty);
    }
}
