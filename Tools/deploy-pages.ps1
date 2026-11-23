# Builds/WebGL の中身を gh-pages ブランチに公開する（GitHub Pages）
#   ./Tools/deploy-pages.ps1
$ErrorActionPreference = "Stop"
$projectPath = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $projectPath "Builds\WebGL"
if (-not (Test-Path (Join-Path $buildDir "index.html"))) { throw "Builds/WebGL がありません。先に build-webgl.ps1 を実行してください。" }

$remote = git -C $projectPath remote get-url origin
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("molkky-pages-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory $work | Out-Null
try {
    Copy-Item -Recurse -Force (Join-Path $buildDir "*") $work
    Copy-Item -Force (Join-Path $buildDir ".nojekyll") $work -ErrorAction SilentlyContinue
    git -C $work init -q -b gh-pages
    git -C $work add -A
    git -C $work commit -q -m "Deploy WebGL build $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
    git -C $work push -f $remote gh-pages
    Write-Host "Deployed to gh-pages."
}
finally {
    Remove-Item -Recurse -Force $work -Confirm:$false
}
