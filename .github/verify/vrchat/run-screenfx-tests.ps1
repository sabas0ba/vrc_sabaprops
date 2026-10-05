param(
    [string]$Project = "$PSScriptRoot/../../../build/WorldProject",
    [string]$Unity = 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe',
    [ValidateSet('Setup', 'Tests', 'Capture', 'Package')][string]$Mode = 'Tests'
)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath $Project).Path
$resultDirectory = Join-Path $projectPath 'TestResults'
New-Item -ItemType Directory -Force $resultDirectory | Out-Null
$logPath = Join-Path $resultDirectory "screenfx-$Mode.log"
$unityArguments = @('-batchmode', '-projectPath', ('"' + $projectPath + '"'), '-logFile', ('"' + $logPath + '"'))
if ($Mode -eq 'Setup') {
    $unityArguments += @('-quit', '-executeMethod', 'SabaProps.ScreenFx.WorldTests.ScreenFxDemoCapture.Configure')
} elseif ($Mode -eq 'Capture') {
    $unityArguments += @('-quit', '-executeMethod', 'SabaProps.ScreenFx.WorldTests.ScreenFxDemoCapture.Generate')
} elseif ($Mode -eq 'Package') {
    $unityArguments += @('-quit', '-executeMethod', 'SabaProps.ScreenFx.WorldTests.ScreenFxDemoCapture.FinalizeDelivery')
} else {
    $results = Join-Path $resultDirectory 'screenfx-world.xml'
    if (Test-Path -LiteralPath $results) { Remove-Item -LiteralPath $results }
    $unityArguments += @('-runTests', '-testPlatform', 'EditMode', '-assemblyNames', 'SabaProps.ScreenFx.WorldTests', '-testResults', ('"' + $results + '"'))
}
$process = Start-Process -FilePath $Unity -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Unity failed: $($process.ExitCode). See $logPath" }
if ($Mode -eq 'Setup') {
    $settings = Join-Path $projectPath 'ProjectSettings/ProjectSettings.asset'
    if (-not (Select-String -LiteralPath $settings -Pattern '^\s*activeInputHandler: 2$')) {
        throw "Input setup was not applied. See $logPath"
    }
}
if ($Mode -eq 'Capture' -and -not (Test-Path -LiteralPath (Join-Path $resultDirectory 'screenfx-udon-demo.png'))) {
    throw "Capture was not produced. See $logPath"
}
if ($Mode -eq 'Tests') {
    [xml]$report = Get-Content -LiteralPath $results
    if ($report.'test-run'.result -ne 'Passed' -or [int]$report.'test-run'.total -eq 0) {
        throw "Tests did not pass. See $results"
    }
    $report.'test-run' | Select-Object result, total, passed, failed, skipped
}
Write-Output "Log: $logPath"
