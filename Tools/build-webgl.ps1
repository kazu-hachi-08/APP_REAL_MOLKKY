# WebGL をバッチモードでビルドする
#   ./Tools/build-webgl.ps1            -> Builds/WebGL に出力
param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe",
    [string]$Method = "WebGLBuilder.Build"
)

$ErrorActionPreference = "Stop"
$projectPath = Split-Path -Parent $PSScriptRoot
$logPath = Join-Path $projectPath "Logs\build-webgl.log"

Write-Host "Building WebGL... (log: $logPath)"
$p = Start-Process -FilePath $UnityPath -ArgumentList @(
    "-batchmode", "-quit",
    "-projectPath", "`"$projectPath`"",
    "-buildTarget", "WebGL",
    "-executeMethod", $Method,
    "-logFile", "`"$logPath`""
) -Wait -PassThru

if ($p.ExitCode -ne 0) {
    Get-Content $logPath -Tail 40
    throw "Unity build failed (exit $($p.ExitCode))"
}
Select-String -Path $logPath -Pattern "\[WebGLBuilder\]" | ForEach-Object { $_.Line }
