# Visual check without a person at the keyboard. Copies the project to a temp folder (the open
# Editor keeps its own copy locked), builds a Windows player with PRIZMA_AUTOTEST using a
# batchmode Unity, runs it portrait-sized on fresh save data, and leaves one screenshot per
# screen in %TEMP%\prizma_autotest\shots.
#
#   powershell -File Tools/autotest.ps1            # sync + build + run
#   powershell -File Tools/autotest.ps1 -SkipBuild # run the last build again
#   powershell -File Tools/autotest.ps1 -SkipBuild -Width 540 -Height 960 -Tag 16x9
#
# -TopInset simulates a phone's camera cutout, in canvas units: the Windows player has no safe
# area of its own, so without it every shot shows more room at the top than a phone has.
param(
    [switch]$SkipBuild,
    [int]$Width = 432,
    [int]$Height = 936,
    [int]$TopInset = 90,
    [string]$Tag = "",
    # Which monitor the player window opens on (1-based; 0 leaves it to Windows). A second screen
    # keeps the test out of the way of whoever is working on the first.
    [int]$Monitor = 0,
    # "adventure" runs only the adventure's shots; "play" sets up a save and leaves the game open.
    [string]$Only = "",
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1"
)

$project = Split-Path $PSScriptRoot -Parent
$work = Join-Path $env:TEMP "prizma_autotest"
$copy = "$work\project"
$player = "$work\player\PRIZMA.exe"
$shots = if ($Tag) { "$work\shots_$Tag" } else { "$work\shots" }
New-Item -ItemType Directory -Force $work | Out-Null

if (-not $SkipBuild) {
    # Library comes along the first time so the copy does not reimport from scratch.
    $dirs = if (Test-Path "$copy\Library") { "Assets", "Packages", "ProjectSettings" } else { "Assets", "Packages", "ProjectSettings", "Library" }
    foreach ($d in $dirs) {
        robocopy "$project\$d" "$copy\$d" /MIR /MT:16 /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
    }

    $p = Start-Process -FilePath "$Unity\Editor\Unity.exe" -ArgumentList @(
        "-batchmode", "-quit", "-projectPath", "`"$copy`"", "-buildTarget", "Win64",
        "-executeMethod", "BlockPuzzle.EditorTools.AutoTestBuild.BuildWindows",
        "-autotestBuildPath", "`"$player`"", "-logFile", "`"$work\build.log`"") -Wait -PassThru
    "build exit: $($p.ExitCode)"
    Select-String -Path "$work\build.log" -Pattern "error CS|\[AutoTestBuild\]" | Select-Object -Last 10 | ForEach-Object { $_.Line }
    if ($p.ExitCode -ne 0) { exit 1 }
}

# Fresh save data every run, so the first-run tutorial and empty tables are what get captured.
$company = (Select-String -Path "$project\ProjectSettings\ProjectSettings.asset" -Pattern '^\s*companyName: (.+)$').Matches[0].Groups[1].Value
$product = (Select-String -Path "$project\ProjectSettings\ProjectSettings.asset" -Pattern '^\s*productName: (.+)$').Matches[0].Groups[1].Value
Remove-Item "HKCU:\Software\$company\$product" -Recurse -ErrorAction SilentlyContinue

if (Test-Path $shots) { Remove-Item $shots -Recurse -Force }
New-Item -ItemType Directory $shots | Out-Null

$runArgs = @(
    "-screen-fullscreen", "0", "-screen-width", "$Width", "-screen-height", "$Height",
    "-autotestTopInset", "$TopInset",
    "-autotestOut", "`"$shots`"", "-logFile", "`"$work\player.log`"")
if ($Monitor -gt 0) { $runArgs += @("-monitor", "$Monitor") }
if ($Only) { $runArgs += @("-autotestOnly", $Only) }
$run = Start-Process -FilePath $player -ArgumentList $runArgs -PassThru
if ($Only -eq "play") { "player left running (pid $($run.Id))"; exit 0 }
if (-not $run.WaitForExit(300000)) { $run.Kill(); "player timed out" }
"player exit: $($run.ExitCode)"
Select-String -Path "$work\player.log" -Pattern "Exception|\[AutoTest\] done" | Select-Object -First 15 | ForEach-Object { $_.Line }
Get-ChildItem $shots -Filter *.png | Select-Object Name, Length
