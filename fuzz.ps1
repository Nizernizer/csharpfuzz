param (
    [Parameter(Mandatory = $true)]
    [string]$project,
    [Parameter(Mandatory = $true)]
    [string]$i,
    [string]$x = $null,
    [int]$t = 10000,
    [string]$command = "sharpfuzz"
)

Set-StrictMode -Version Latest

$outputDir = ".\FuzzTarget\published_output"
$findingsDir = "findings"

if (Test-Path $outputDir) { 
    Remove-Item -Recurse -Force $outputDir 
}

if (Test-Path $findingsDir) {
    Remove-Item -Recurse -Force $findingsDir 
}

dotnet publish $project -c release -o $outputDir

$projectName = (Get-Item $project).BaseName
$projectDll = "$projectName.dll"
$project = Join-Path $outputDir $projectDll

$exclusions = @(
    "dnlib.dll",
    "SharpFuzz.dll",
    "SharpFuzz.Common.dll",
    $projectDll
)

$fuzzingTargets = Get-ChildItem $outputDir -Filter *.dll `
| Where-Object { $_.Name -notin $exclusions } `
| Where-Object { $_.Name -notlike "System.*.dll" }

if (($fuzzingTargets | Measure-Object).Count -eq 0) {
    Write-Error "No fuzzing targets found"
    exit 1
}

foreach ($fuzzingTarget in $fuzzingTargets) {
    # --- 添加详细的调试信息 ---
    Write-Output "----------------------------------------"
    Write-Output "Processing target: $($fuzzingTarget.Name)"
    Write-Output "Full Path: $($fuzzingTarget.FullName)"
    Write-Output "Exists?: $($fuzzingTarget.Exists)"
    Write-Output "Target is Container?: $($fuzzingTarget.PSIsContainer)"
    Write-Output "Command to execute: & '$command' '$($fuzzingTarget.FullName)'" # 明确使用 FullName 并加引号
    Write-Output "----------------------------------------"
    # --- 调试信息结束 ---
    
    Write-Output "Instrumenting $($fuzzingTarget.Name)" # 原有信息行保持
    
    # --- 修改调用方式，明确使用 FullName 并确保路径被引号包围 ---
    # & $command $fuzzingTarget # 原有方式
    & $command $fuzzingTarget.FullName # 尝试使用 FullName 属性
    
    if ($LastExitCode -ne 0) {
        Write-Error "An error occurred while instrumenting $($fuzzingTarget.Name). Exit code: $LastExitCode"
        # --- 可选：添加更详细的错误信息 ---
        # Write-Error "Command was: & '$command' '$($fuzzingTarget.FullName)'"
        # --- ---
        exit 1
    }
}

$env:AFL_SKIP_BIN_CHECK = 1

if ($x) {
    afl-fuzz -i $i -o $findingsDir -t $t -m none -x $x dotnet $project
}
else {
    afl-fuzz -i $i -o $findingsDir -t $t -m none dotnet $project
}