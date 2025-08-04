# WFuzz Test Runner Script
# For Phase 1 End-to-End Testing

param(
    [Parameter(Mandatory=$false)]
    [string]$TargetAssembly = "TestLibrary",
    
    [Parameter(Mandatory=$false)]
    [string]$TestMethod = "TestLibrary_Calculator_Divide_double_double",
    
    [Parameter(Mandatory=$false)]
    [string]$Engine = "SharpFuzz",
    
    [Parameter(Mandatory=$false)]
    [int]$Duration = 60,
    
    [Parameter(Mandatory=$false)]
    [switch]$UseAFL = $false
)

# ========== 添加编码设置 ==========
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
# ================================

Write-Host "=== WFuzz Phase 1 Test Runner ===" -ForegroundColor Cyan
Write-Host ""

# Set directories
$projectRoot = $PSScriptRoot
$outputDir = Join-Path $projectRoot "Output"
$generatedDir = Join-Path $outputDir "Generated"
$seedsDir = Join-Path $outputDir "seeds"
$findingsDir = Join-Path $outputDir "findings"

# Clean old output
if (Test-Path $outputDir) {
    Write-Host "Cleaning old output directory..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $outputDir
}

# Create directory structure
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
New-Item -ItemType Directory -Path $generatedDir -Force | Out-Null
New-Item -ItemType Directory -Path $seedsDir -Force | Out-Null
New-Item -ItemType Directory -Path $findingsDir -Force | Out-Null

# Step 1: Build all projects
Write-Host "Step 1: Building projects" -ForegroundColor Green
Write-Host "Building WFuzz core library..."
dotnet build "$projectRoot\WFuzz\WFuzz.csproj" -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "WFuzz build failed"
    exit 1
}

Write-Host "Building WFuzzAgent..."
dotnet build "$projectRoot\WFuzzAgent\WFuzzAgent.csproj" -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "WFuzzAgent build failed"
    exit 1
}

Write-Host "Building WFuzzEngine..."
dotnet build "$projectRoot\WFuzzEngine\WFuzzEngine.csproj" -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "WFuzzEngine build failed"
    exit 1
}

Write-Host "Building test target library..."
dotnet build "$projectRoot\TestLibrary\TestLibrary.csproj" -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "TestLibrary build failed"
    exit 1
}

# Step 2: Run code generator
Write-Host ""
Write-Host "Step 2: Generating test code" -ForegroundColor Green
$testLibraryDll = "$projectRoot\TestLibrary\bin\Release\net9.0\TestLibrary.dll"
dotnet run --project "$projectRoot\WFuzzGen\WFuzzGen.csproj" -- `
    $testLibraryDll `
    $generatedDir `
    --namespace TestLibrary `
    --parallel true

if ($LASTEXITCODE -ne 0) {
    Write-Error "Code generation failed"
    exit 1
}

# Step 3: Build generated code
Write-Host ""
Write-Host "Step 3: Building generated test code" -ForegroundColor Green
dotnet build "$generatedDir\WFuzzGen.Generated.csproj" -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Generated code build failed"
    exit 1
}

# Step 4: Create seed inputs
Write-Host ""
Write-Host "Step 4: Creating seed inputs" -ForegroundColor Green

# Create basic seeds
$seeds = @(
    @(),                              # Empty input
    @(0),                            # Single byte
    @(255),                          # Max byte value
    @(0, 0, 0, 0),                   # Zero integer
    @(255, 255, 255, 255),           # -1 integer
    @(1, 2, 3, 4, 5, 6, 7, 8),      # Basic sequence
    [Text.Encoding]::UTF8.GetBytes("test"),
    [Text.Encoding]::UTF8.GetBytes("0"),
    [Text.Encoding]::UTF8.GetBytes("3.14159"),
    [Text.Encoding]::UTF8.GetBytes("hello world"),
    [Text.Encoding]::UTF8.GetBytes("A" * 100)
)

$seedIndex = 0
foreach ($seed in $seeds) {
    $seedFile = Join-Path $seedsDir "seed_$seedIndex"
    [System.IO.File]::WriteAllBytes($seedFile, $seed)
    $seedIndex++
}

Write-Host "Created $($seeds.Count) seed files"

# Step 5: Run fuzzing
Write-Host ""
Write-Host "Step 5: Running fuzzing" -ForegroundColor Green
Write-Host "Target: $TestMethod"
Write-Host "Engine: $Engine"
Write-Host "Duration: $Duration seconds"
Write-Host ""

if ($UseAFL -and $Engine -eq "AFLSharp") {
    # AFL mode
    Write-Host "Running with AFL mode..." -ForegroundColor Cyan
    
    $aflPath = Get-Command afl-fuzz -ErrorAction SilentlyContinue
    if (-not $aflPath) {
        Write-Warning "AFL not installed or not in PATH. Switching to standalone mode."
        $UseAFL = $false
    }
    else {
        Write-Host "Found AFL: $($aflPath.Path)" -ForegroundColor Green
        
        $env:AFL_SKIP_BIN_CHECK = 1
        $env:AFL_NO_FORKSRV = 1
        $env:AFL_DUMB_FORKSRV = 1
        
        $aflCmd = "afl-fuzz -i ""$seedsDir"" -o ""$findingsDir"" -t 1000 -m none -- dotnet ""$generatedDir\WFuzzGen.Generated.dll"" $TestMethod"
        Write-Host "Executing: $aflCmd" -ForegroundColor Yellow
        
        $aflProcess = Start-Process -FilePath "afl-fuzz" `
            -ArgumentList "-i", $seedsDir, "-o", $findingsDir, "-t", "1000", "-m", "none", "--", `
                         "dotnet", "$generatedDir\WFuzzGen.Generated.dll", $TestMethod `
            -NoNewWindow -PassThru
        
        Write-Host "Fuzzing running... (press Ctrl+C to stop)" -ForegroundColor Green
        Start-Sleep -Seconds $Duration
        
        if (-not $aflProcess.HasExited) {
            Write-Host "Stopping AFL..." -ForegroundColor Yellow
            $aflProcess.Kill()
            Start-Sleep -Seconds 2
        }
    }
}

if (-not $UseAFL) {
    # Standalone mode
    Write-Host "Running in standalone mode..." -ForegroundColor Cyan
    
    $cliProject = "$projectRoot\WFuzzCLI\WFuzzCLI.csproj"
    $generatedDll = "$generatedDir\bin\Release\net9.0\WFuzzGen.Generated.dll"    
    $runArgs = @(
        "run",
        "--assembly", $generatedDll,
        "--caller", $TestMethod,
        "--engine", $Engine,
        "--input-dir", $seedsDir,
        "--output-dir", $findingsDir,
        "--timeout", "1000",
        "--iterations", "-1"
    )
    
    Write-Host "Executing: dotnet run --project $cliProject -- $($runArgs -join ' ')" -ForegroundColor Yellow
    
    # Create temp files for output
    $tempOutput = Join-Path $env:TEMP "wfuzz_output_$(Get-Random).txt"
    $tempError = Join-Path $env:TEMP "wfuzz_error_$(Get-Random).txt"
    
    # Start the process
    $process = Start-Process -FilePath "dotnet" `
        -ArgumentList (@("run", "--project", $cliProject, "--") + $runArgs) `
        -NoNewWindow -PassThru `
        -RedirectStandardOutput $tempOutput `
        -RedirectStandardError $tempError
    
    # Monitor progress
    $startTime = Get-Date
    $endTime = $startTime.AddSeconds($Duration)
    
    while ((Get-Date) -lt $endTime -and !$process.HasExited) {
        $elapsed = (Get-Date) - $startTime
        $remaining = $endTime - (Get-Date)
        $progress = ($elapsed.TotalSeconds / $Duration) * 100
        
        Write-Progress -Activity "Fuzzing in progress" `
            -Status "Elapsed: $($elapsed.ToString('mm\:ss')) / Remaining: $($remaining.ToString('mm\:ss'))" `
            -PercentComplete $progress
        
        # Check for output
        if (Test-Path $tempOutput) {
            $newContent = Get-Content $tempOutput -Tail 10 -ErrorAction SilentlyContinue
            if ($newContent) {
                $newContent | ForEach-Object { Write-Host $_ }
            }
        }
        
        Start-Sleep -Seconds 1
    }
    
    Write-Progress -Activity "Fuzzing in progress" -Completed
    
    # Stop the process if still running
    if (!$process.HasExited) {
        Write-Host "`nStopping fuzzing..." -ForegroundColor Yellow
        $process.Kill()
        Start-Sleep -Seconds 2
    }
    
    # Display final output
    if (Test-Path $tempOutput) {
        $finalOutput = Get-Content $tempOutput -ErrorAction SilentlyContinue
        if ($finalOutput) {
            Write-Host "`nFinal output:" -ForegroundColor Yellow
            $finalOutput | ForEach-Object { Write-Host $_ }
        }
        Remove-Item $tempOutput -Force -ErrorAction SilentlyContinue
    }
    
    if (Test-Path $tempError) {
        $errorOutput = Get-Content $tempError -ErrorAction SilentlyContinue
        if ($errorOutput) {
            Write-Host "`nErrors:" -ForegroundColor Red
            $errorOutput | ForEach-Object { Write-Host $_ }
        }
        Remove-Item $tempError -Force -ErrorAction SilentlyContinue
    }
}

# Step 6: Analyze results
Write-Host ""
Write-Host "Step 6: Analyzing results" -ForegroundColor Green

# Check crashes
$crashesDir = Join-Path $findingsDir "crashes"
$crashes = $null
if (Test-Path $crashesDir) {
    $crashes = Get-ChildItem -Path $crashesDir -Filter "*.input" -ErrorAction SilentlyContinue
    if ($crashes) {
        Write-Host "Found $($crashes.Count) crashes!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Crash list:" -ForegroundColor Yellow
        foreach ($crash in $crashes | Select-Object -First 10) {
            $crashInfoFile = $crash.FullName -replace '\.input$', '.txt'
            Write-Host "  - $($crash.Name)" -ForegroundColor White
            if (Test-Path $crashInfoFile) {
                $crashInfo = Get-Content -Path $crashInfoFile -ErrorAction SilentlyContinue | Select-Object -First 3
                if ($crashInfo) {
                    $crashInfo | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
                }
            }
        }
        if ($crashes.Count -gt 10) {
            Write-Host "  ... and $($crashes.Count - 10) more crashes" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "No crashes found" -ForegroundColor Green
    }
}
else {
    Write-Host "Crashes directory not found" -ForegroundColor Yellow
}

# Show coverage info if available
$statsFile = Join-Path $findingsDir "fuzzer_stats"
if (Test-Path $statsFile) {
    Write-Host ""
    Write-Host "Fuzzer statistics:" -ForegroundColor Yellow
    Get-Content $statsFile | Select-Object -Last 20 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
}

# Step 7: Generate report
Write-Host ""
Write-Host "Step 7: Generating test report" -ForegroundColor Green

$crashCount = 0
if ($crashes) {
    $crashCount = $crashes.Count
}

$report = @"
# WFuzz Phase 1 Test Report

Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Test Configuration
- Target Assembly: $TargetAssembly
- Test Method: $TestMethod
- Fuzzing Engine: $Engine
- Run Mode: $(if ($UseAFL) { "AFL Integration" } else { "Standalone Mode" })
- Duration: $Duration seconds

## Test Results
- Seed Count: $($seeds.Count)
- Crashes Found: $crashCount
- Output Directory: $outputDir

## Directory Structure
- Generated Code: $generatedDir
- Seed Inputs: $seedsDir
- Test Findings: $findingsDir

## Next Steps
1. Check $crashesDir directory for crash details
2. Use WFuzzGen to analyze other methods
3. Adjust seed inputs to improve coverage
4. Increase run time to find more issues

"@

$reportPath = Join-Path $outputDir "test_report.md"
$report | Out-File -FilePath $reportPath -Encoding UTF8

Write-Host "Test report saved to: $reportPath" -ForegroundColor Green
Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan

# Open output directory
if ($IsWindows) {
    explorer $outputDir
}
elseif ($IsMacOS) {
    open $outputDir
}
else {
    Write-Host "Output directory: $outputDir" -ForegroundColor Yellow
}