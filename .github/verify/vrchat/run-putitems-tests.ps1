param([string]$Unity = '')
$ErrorActionPreference = 'Stop'
$repoPath = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$projectPath = Join-Path $repoPath 'build/PutItemsProject'
$versionFile = Join-Path $repoPath '.github/verify/CIProject/ProjectSettings/ProjectVersion.txt'
$versionMatch = Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion: (.+)$'
$version = $versionMatch.Matches[0].Groups[1].Value.Trim()
if (-not $Unity) { $Unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe" }
if (-not (Test-Path -LiteralPath $Unity)) { throw "Unity $version not found: $Unity" }
if (-not (Test-Path -LiteralPath (Join-Path $projectPath 'Packages/com.vrchat.worlds'))) {
    throw 'Run prepare_putitems.py in the pinned container first.'
}
$resultPath = Join-Path $projectPath 'results.xml'
$logPath = Join-Path $projectPath 'unity.log'
# 削除対象は上記の固定されたプロジェクト内の結果ファイルだけです。
if (Test-Path -LiteralPath $resultPath) { Remove-Item -LiteralPath $resultPath }
$arguments = @('-batchmode', '-projectPath', ('"' + $projectPath + '"'),
    '-runTests', '-testPlatform', 'EditMode', '-testFilter', 'SabaProps.PutItems.Tests',
    '-testResults', ('"' + $resultPath + '"'), '-logFile', ('"' + $logPath + '"'))
$timer = [Diagnostics.Stopwatch]::StartNew()
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
while (-not $process.WaitForExit(10000)) {
    $logBytes = 0
    if (Test-Path -LiteralPath $logPath) { $logBytes = (Get-Item -LiteralPath $logPath).Length }
    Write-Output ("Unity running: {0:N0}s, log {1} bytes" -f $timer.Elapsed.TotalSeconds, $logBytes)
}
$process.Refresh()
$unityExitCode = $process.ExitCode
if (-not (Test-Path -LiteralPath $resultPath)) { throw "No test results; see $logPath" }
[xml]$results = Get-Content -LiteralPath $resultPath -Raw
$cases = @($results.SelectNodes('//test-case'))
$failed = @($cases | Where-Object { $_.result -ne 'Passed' })
foreach ($case in $cases) { Write-Output "[$($case.result)] $($case.fullname)" }
if ($unityExitCode -ne 0 -or $cases.Count -eq 0 -or $failed.Count -gt 0) { throw "Tests: $($cases.Count), non-passed: $($failed.Count)" }
Write-Output "All $($cases.Count) tests passed. Results: $resultPath"
