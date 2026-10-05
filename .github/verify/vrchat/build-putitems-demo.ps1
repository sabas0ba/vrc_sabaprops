param([string]$Unity = '')
$ErrorActionPreference = 'Stop'
$repoPath = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$projectPath = Join-Path $repoPath 'build/PutItemsProject'
$versionMatch = Select-String -LiteralPath (Join-Path $repoPath '.github/verify/CIProject/ProjectSettings/ProjectVersion.txt') -Pattern '^m_EditorVersion: (.+)$'
$version = $versionMatch.Matches[0].Groups[1].Value.Trim()
if (-not $Unity) { $Unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe" }
if (-not (Test-Path -LiteralPath $Unity)) { throw "Unity $version not found" }
$logPath = Join-Path $projectPath 'kitchen-demo.log'
$arguments = @('-batchmode', '-quit', '-projectPath', ('"' + $projectPath + '"'),
    '-executeMethod', 'SabaProps.PutItems.Editors.KitchenDemoSample.GenerateAndExport',
    '-logFile', ('"' + $logPath + '"'))
$timer = [Diagnostics.Stopwatch]::StartNew()
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
while (-not $process.WaitForExit(10000)) {
    $bytes = if (Test-Path -LiteralPath $logPath) { (Get-Item -LiteralPath $logPath).Length } else { 0 }
    Write-Output ("Building kitchen demo: {0:N0}s, log {1} bytes" -f $timer.Elapsed.TotalSeconds, $bytes)
}
$process.Refresh()
if ($process.ExitCode -ne 0) { throw "Unity exited with $($process.ExitCode); see $logPath" }
$scene = Join-Path $projectPath 'Packages/io.github.sabas0ba.sabaprops.putitems/Samples~/KitchenDemo/PutItemsKitchen.unity'
if (-not (Test-Path -LiteralPath $scene)) { throw "Bundled scene was not exported; see $logPath" }
Write-Output "Exported scene: $scene"
