$ErrorActionPreference = 'Stop'
if (-not ($IsWindows -or $env:OS -eq 'Windows_NT' -or $IsLinux)) { throw 'Use Windows or Linux to build this package.' }
foreach ($tool in 'dotnet', 'macrodeck-plugin') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool is required" }
}
$repo = $PSScriptRoot
$buildRoot = [IO.Path]::GetFullPath((Join-Path (Split-Path $repo -Parent) 'MacroDeck-builds/slideshow'))
$stage = Join-Path $buildRoot 'stage'
$project = 'ImageSlideshow'
$stageProject = Join-Path $stage "src/$project"

New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stageProject -Force | Out-Null
foreach ($file in 'Directory.Build.props', 'Directory.Packages.props', 'NuGet.config') {
    Copy-Item -LiteralPath (Join-Path $repo $file) -Destination $stage
}
Get-ChildItem -LiteralPath (Join-Path $repo "src/$project") -Force |
    Where-Object { $_.Name -notin @('bin', 'obj', '.macrodeck-dev-state') } |
    Copy-Item -Destination $stageProject -Recurse -Force

& macrodeck-plugin build --source $stageProject --output (Join-Path $buildRoot 'artifacts') --force
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
