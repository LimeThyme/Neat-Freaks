param(
    [Parameter(Mandatory=$true)][string]$UnityEditorDirectory,
    [Parameter(Mandatory=$true)][string]$GameManagedDirectory,
    [Parameter(Mandatory=$true)][string]$HarmonyAssembly,
    [Parameter(Mandatory=$true)][string]$PaletteFile,
    [Parameter(Mandatory=$true)][string]$ResultsDirectory,
    [switch]$CompileOnly,
    [string]$ArtPackDirectory = '',
    [string]$AutoPackDirectory = ''
)
$ErrorActionPreference = 'Stop'
$kit = Split-Path $PSScriptRoot -Parent
$source = Join-Path $kit 'Unity/Assets/Untracked/ModBuilder/Neat Freaks - 0.1.6/Source'
$mono = Join-Path $UnityEditorDirectory 'Data/MonoBleedingEdge/bin/mono.exe'
$compiler = Join-Path $UnityEditorDirectory 'Data/MonoBleedingEdge/lib/mono/4.5/csc.exe'
foreach ($path in @($mono,$compiler,$HarmonyAssembly,$PaletteFile)) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing input: $path" }
}
if ([Reflection.AssemblyName]::GetAssemblyName($HarmonyAssembly).Version -lt [Version]'2.4.2.0') { throw 'Harmony 2.4.2.0 or newer required.' }
New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$results = (Resolve-Path -LiteralPath $ResultsDirectory).Path
$references = @('Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','FullSerializer - Unity.dll','UnityEngine.CoreModule.dll') | ForEach-Object {
    $path = Join-Path $GameManagedDirectory $_
    if (!(Test-Path -LiteralPath $path)) { throw "Missing reference: $path" }
    "/reference:$path"
}
$references += "/reference:$HarmonyAssembly"
$sources = @(Get-ChildItem -LiteralPath $source -Filter '*.cs' | ForEach-Object FullName)
$dll = Join-Path $results 'NeatFreaks.dll'
& $mono $compiler /nologo /target:library /warnaserror+ "/out:$dll" @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Production source compilation failed.' }
'PASS: all production source compiled against installed DFU, Unity, FullSerializer and Harmony assemblies.' | Tee-Object -FilePath (Join-Path $results 'compile.txt')
if ($CompileOnly) { return }
$exe = Join-Path $results 'Checks.exe'
& $mono $compiler /nologo /target:exe /warnaserror+ "/out:$exe" @references "/reference:$dll" (Join-Path $PSScriptRoot 'Checks.cs.txt')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
$previousMonoPath = $env:MONO_PATH
try {
    $env:MONO_PATH = "$GameManagedDirectory;$(Split-Path $HarmonyAssembly -Parent);$results"
    & $mono $exe (Join-Path $kit 'StreamingAssets/Textures/NeatFreaks') $PaletteFile (Join-Path $results 'Fixtures') (Split-Path $source -Parent) $ArtPackDirectory $AutoPackDirectory | Tee-Object -FilePath (Join-Path $results 'tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Developer tests failed.' }
} finally { $env:MONO_PATH = $previousMonoPath }
