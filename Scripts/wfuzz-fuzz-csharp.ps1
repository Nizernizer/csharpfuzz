# WFuzz C# Fuzzing Script (PowerShell Version)
# Usage: wfuzz-fuzz-csharp <assembly> <test_entry> [corpus_dir] [output_dir]

param(
    [Parameter(Mandatory=$true)][string]$assembly,
    [Parameter(Mandatory=$true)][string]$testEntry,
    [string]$corpusDir = "corpus",
    [string]$outputDir = "output"
)

# 颜色输出函数
function Write-Color {
    param(
        [string]$text,
        [string]$color = "White"
    )
    $colorMap = @{
        "Red" = [ConsoleColor]::Red
        "Green" = [ConsoleColor]::Green
        "Yellow" = [ConsoleColor]::Yellow
    }
    $origColor = [Console]::ForegroundColor
    [Console]::ForegroundColor = $colorMap[$color]
    Write-Host $text
    [Console]::ForegroundColor = $origColor
}

# 设置环境变量
$env:WFUZZ_RUNTIME_PATH = if ($env:WFUZZ_RUNTIME_PATH) { $env:WFUZZ_RUNTIME_PATH } else {
    Join-Path (Split-Path $MyInvocation.MyCommand.Path) "..\WFuzzRuntime\bin\Release\net9.0"
}
$env:AFL_PATH = if ($env:AFL_PATH) { $env:AFL_PATH } else { "C:\AFL" } # 修改为你的AFL安装路径
$env:DOTNET_PATH = if ($env:DOTNET_PATH) { $env:DOTNET_PATH } else { (Get-Command dotnet).Source }

# 检查依赖
if (-not (Test-Path $assembly -PathType Leaf)) {
    Write-Color "Error: Assembly not found: $assembly" "Red"
    exit 1
}

$runtimeDll = Join-Path $env:WFUZZ_RUNTIME_PATH "WFuzzRuntime.dll"
if (-not (Test-Path $runtimeDll -PathType Leaf)) {
    Write-Color "Error: WFuzzRuntime.dll not found at: $runtimeDll" "Red"
    Write-Color "Please build the project first with: dotnet build -c Release" "Yellow"
    exit 1
}

$aflFuzz = Join-Path $env:AFL_PATH "afl-fuzz.exe"
if (-not (Test-Path $aflFuzz -PathType Leaf)) {
    Write-Color "Error: afl-fuzz.exe not found at: $aflFuzz" "Red"
    Write-Color "Please install AFL++ for Windows" "Yellow"
    exit 1
}

if (-not $env:DOTNET_PATH) {
    Write-Color "Error: dotnet command not found" "Red"
    Write-Color "Please install .NET SDK" "Yellow"
    exit 1
}

# 创建必要的目录
New-Item -ItemType Directory -Force -Path $corpusDir | Out-Null
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# 如果语料库为空，创建初始种子
if (-not (Get-ChildItem $corpusDir)) {
    Write-Color "Creating initial seeds..." "Yellow"
    "A" | Out-File (Join-Path $corpusDir "seed_001")
    "42" | Out-File (Join-Path $corpusDir "seed_002")
    "test" | Out-File (Join-Path $corpusDir "seed_003")
    [byte[]](0x00,0x01,0x02,0x03) | Set-Content (Join-Path $corpusDir "seed_004") -AsByteStream
    [byte[]](0xff,0xfe,0xfd,0xfc) | Set-Content (Join-Path $corpusDir "seed_005") -AsByteStream
}

# AFL++ 环境变量
$env:AFL_SKIP_CPUFREQ = 1
$env:AFL_I_DONT_CARE_ABOUT_MISSING_CRASHES = 1
$env:AFL_CRASH_DIR = (Join-Path $outputDir "crashes")
$env:AFL_AUTORESUME = 1
$env:AFL_SKIP_BIN_CHECK = 1

# 显示配置信息
Write-Color "WFuzz C# Fuzzer Configuration:" "Green"
Write-Host "  Assembly: $assembly"
Write-Host "  Test Entry: $testEntry"
Write-Host "  Corpus Dir: $corpusDir"
Write-Host "  Output Dir: $outputDir"
Write-Host "  Runtime: $runtimeDll"
Write-Host ""

# 构建执行命令
$execCmd = "$env:DOTNET_PATH $runtimeDll --assembly $assembly --entry $testEntry"

# 启动模糊测试
Write-Color "Starting fuzzing..." "Green"
Write-Host "Command: $aflFuzz -i $corpusDir -o $outputDir -- $execCmd"
Write-Host ""

& $aflFuzz -i $corpusDir -o $outputDir -t 1000 -m none -- $execCmd