# Compiles the game (normal and PRIZMA_AUTOTEST) and the editor scripts with Unity's own
# compiler arguments, without Unity running a compile. Catches errors while the Editor is
# closed or unfocused. Needs one successful Unity compile first, for Library/Bee.
#
#   powershell -File Tools/compile-check.ps1
param([string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1")

$project = Split-Path $PSScriptRoot -Parent
$out = Join-Path $env:TEMP "prizma_compile"
New-Item -ItemType Directory -Force $out | Out-Null

$dotnet = "$Unity\Editor\Data\DotNetSdk\dotnet.exe"
$csc = Get-ChildItem "$Unity\Editor\Data\DotNetSdk\sdk" -Recurse -Filter csc.dll | Where-Object { $_.FullName -match 'bincore' } | Select-Object -First 1 -ExpandProperty FullName
$dag = Get-ChildItem "$project\Library\Bee\artifacts" -Directory -Filter "*E.dag" | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName

Push-Location $project

function Strip($file) {
    Get-Content $file | Where-Object { $_ -notmatch '\.cs"?\s*$' -and $_ -notmatch '^[-/](out|refout|doc|errorlog|generatedfilesout):' }
}

$sources = Get-ChildItem "$project\Assets\Scripts" -Recurse -Filter *.cs | ForEach-Object { '"' + $_.FullName + '"' }
$failed = 0

foreach ($variant in @(@{ Name = "Assembly-CSharp"; Define = $null }, @{ Name = "Assembly-CSharp-AutoTest"; Define = "PRIZMA_AUTOTEST" })) {
    $rsp = @(Strip "$dag\Assembly-CSharp.rsp") + $sources
    if ($variant.Define) { $rsp += "-define:$($variant.Define)" }
    $rsp += "-out:`"$out\$($variant.Name).dll`""
    Set-Content "$out\$($variant.Name).rsp" $rsp -Encoding utf8
    $result = & $dotnet exec $csc /noconfig "@$out\$($variant.Name).rsp" 2>&1
    "$($variant.Name): exit $LASTEXITCODE"
    $result | Select-String -Pattern " error " | Select-Object -First 30 | ForEach-Object { $_.Line.Replace("$project\", "") }
    if ($LASTEXITCODE -ne 0) { $failed++ }
}

# The editor assembly must see the game assembly just compiled, not Unity's last one (which may
# be a reference assembly without the newest types). Whatever form Unity wrote the reference in,
# drop it and point at ours.
$ref = '-r:"' + (($out -replace '\\', '/') + '/Assembly-CSharp.dll') + '"'
$editor = @(Strip "$dag\Assembly-CSharp-Editor.rsp" | Where-Object { $_ -notmatch 'Assembly-CSharp(\.ref)?\.dll' })
$editor += $ref
$editor += Get-ChildItem "$project\Assets\Editor" -Filter *.cs | ForEach-Object { '"' + $_.FullName + '"' }
$editor += "-out:`"$out\Assembly-CSharp-Editor.dll`""
Set-Content "$out\editor.rsp" $editor -Encoding utf8
$result = & $dotnet exec $csc /noconfig "@$out\editor.rsp" 2>&1
"Assembly-CSharp-Editor: exit $LASTEXITCODE"
$result | Select-String -Pattern " error " | Select-Object -First 20 | ForEach-Object { $_.Line }
if ($LASTEXITCODE -ne 0) { $failed++ }

Pop-Location
exit $failed
