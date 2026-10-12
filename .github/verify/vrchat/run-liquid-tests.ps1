param(
    [string]$Project = "$PSScriptRoot/../../../build/WorldProjectCI",
    [string]$Unity = 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe',
    [string]$Filter = 'SabaProps.Liquid.WorldTests',
    [switch]$StartOnly
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path "$PSScriptRoot/../../..").Path
$projectPath = (Resolve-Path -LiteralPath $Project).Path
if (-not $projectPath.StartsWith($repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The test project must be inside this repository.'
}
$running = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'"
foreach ($process in $running) {
    if ($process.CommandLine -and $process.CommandLine.Contains($projectPath)) {
        throw "The test project is already open in Unity (PID $($process.ProcessId))."
    }
}
$package = 'io.github.sabas0ba.sabaprops.liquid'
Copy-Item -Path "$repository/Packages/$package/*" -Destination "$projectPath/Packages/$package" -Recurse -Force
Copy-Item -Path "$PSScriptRoot/Tests/Liquid/*.cs" -Destination "$projectPath/Assets/WorldTests/Liquid" -Force
$results = Join-Path $projectPath 'TestResults'
New-Item -ItemType Directory -Force $results | Out-Null
$log = Join-Path $results 'liquid-contact.log'
$report = Join-Path $results 'liquid-contact.xml'
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
$arguments = @('-batchmode', '-projectPath', ('"' + $projectPath + '"'), '-logFile', ('"' + $log + '"'),
    '-runTests', '-testPlatform', 'EditMode', '-testFilter', $Filter, '-testResults', ('"' + $report + '"'))
$unityProcess = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID: $($unityProcess.Id); log: $log; report: $report"
if ($StartOnly) { return }
$unityProcess.WaitForExit()
if ($unityProcess.ExitCode -ne 0) { throw "Unity failed: $($unityProcess.ExitCode). See $log" }
[xml]$result = Get-Content -LiteralPath $report -Encoding UTF8
if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.total -eq 0) { throw "Tests failed: $report" }
$result.'test-run' | Select-Object result, total, passed, failed, skipped
