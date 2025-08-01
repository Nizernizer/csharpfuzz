# 测试覆盖率收集功能的脚本

param(
    [string]$Configuration = "Release"
)

Write-Host "=== WFuzz Coverage Test Script ===" -ForegroundColor Green
Write-Host ""

# 构建项目
Write-Host "1. Building projects with coverage support..." -ForegroundColor Yellow
dotnet build -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# 创建测试输入
Write-Host ""
Write-Host "2. Creating test inputs..." -ForegroundColor Yellow
$TestInputDir = ".\TestInputs"
New-Item -ItemType Directory -Force -Path $TestInputDir | Out-Null

# 创建不同的测试输入以触发不同的代码路径
# 输入1：正常计算
[byte[]]$normalInput = @(
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x24, 0x40,  # 10.0
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x40   # 2.0
)
[System.IO.File]::WriteAllBytes("$TestInputDir\normal.bin", $normalInput)

# 输入2：除零异常
[byte[]]$divByZeroInput = @(
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x24, 0x40,  # 10.0
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00   # 0.0
)
[System.IO.File]::WriteAllBytes("$TestInputDir\divbyzero.bin", $divByZeroInput)

# 输入3：特殊值
[byte[]]$specialInput = @(
    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xEF, 0x7F,  # NaN
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F   # 1.0
)
[System.IO.File]::WriteAllBytes("$TestInputDir\special.bin", $specialInput)

# 运行覆盖率测试
Write-Host ""
Write-Host "3. Running coverage tests..." -ForegroundColor Yellow

$RuntimePath = ".\WFuzzRuntime\bin\$Configuration\net9.0\WFuzzRuntime.dll"
$GeneratedDll = ".\TestOutput\bin\$Configuration\net9.0\WFuzzGen.Generated.dll"
$TestEntry = "TestLibrary_Calculator_Divide_double_double"

# 创建覆盖率报告目录
$CoverageDir = ".\CoverageReports"
New-Item -ItemType Directory -Force -Path $CoverageDir | Out-Null

# 测试1：正常输入（带覆盖率）
Write-Host ""
Write-Host "Test 1: Normal input with coverage" -ForegroundColor Cyan
$env:__AFL_SHM_ID = "12345"  # 模拟 AFL 共享内存 ID
dotnet $RuntimePath `
    --assembly $GeneratedDll `
    --entry $TestEntry `
    --input "$TestInputDir\normal.bin" `
    --coverage `
    --debug > "$CoverageDir\coverage_normal.bin" 2>&1

$exitCode1 = $LASTEXITCODE
Write-Host "Exit code: $exitCode1" -ForegroundColor $(if ($exitCode1 -eq 0) { "Green" } else { "Red" })

# 测试2：异常输入（带覆盖率）
Write-Host ""
Write-Host "Test 2: Exception input with coverage" -ForegroundColor Cyan
dotnet $RuntimePath `
    --assembly $GeneratedDll `
    --entry $TestEntry `
    --input "$TestInputDir\divbyzero.bin" `
    --coverage `
    --debug > "$CoverageDir\coverage_exception.bin" 2>&1

$exitCode2 = $LASTEXITCODE
Write-Host "Exit code: $exitCode2" -ForegroundColor $(if ($exitCode2 -eq 1) { "Green" } else { "Red" })

# 测试3：特殊值输入（带覆盖率）
Write-Host ""
Write-Host "Test 3: Special value input with coverage" -ForegroundColor Cyan
dotnet $RuntimePath `
    --assembly $GeneratedDll `
    --entry $TestEntry `
    --input "$TestInputDir\special.bin" `
    --coverage `
    --debug > "$CoverageDir\coverage_special.bin" 2>&1

$exitCode3 = $LASTEXITCODE
Write-Host "Exit code: $exitCode3" -ForegroundColor $(if ($exitCode3 -eq 0) { "Green" } else { "Red" })

# 分析覆盖率数据
Write-Host ""
Write-Host "4. Analyzing coverage data..." -ForegroundColor Yellow

# 简单的覆盖率分析
function Analyze-Coverage {
    param([string]$CoverageFile)
    
    if (Test-Path $CoverageFile) {
        $bytes = [System.IO.File]::ReadAllBytes($CoverageFile)
        $nonZero = ($bytes | Where-Object { $_ -ne 0 }).Count
        $percentage = [math]::Round(($nonZero / $bytes.Length) * 100, 2)
        
        Write-Host "  File: $(Split-Path $CoverageFile -Leaf)"
        Write-Host "  Non-zero entries: $nonZero / $($bytes.Length)"
        Write-Host "  Coverage: $percentage%"
        
        # 显示前几个非零条目
        $firstNonZero = @()
        for ($i = 0; $i -lt [Math]::Min($bytes.Length, 1000); $i++) {
            if ($bytes[$i] -ne 0) {
                $firstNonZero += "[$i]=$($bytes[$i])"
                if ($firstNonZero.Count -ge 5) { break }
            }
        }
        if ($firstNonZero.Count -gt 0) {
            Write-Host "  First non-zero entries: $($firstNonZero -join ', ')"
        }
    } else {
        Write-Host "  Coverage file not found: $CoverageFile" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Coverage Analysis:" -ForegroundColor Cyan
Analyze-Coverage "$CoverageDir\coverage_normal.bin"
Write-Host ""
Analyze-Coverage "$CoverageDir\coverage_exception.bin"
Write-Host ""
Analyze-Coverage "$CoverageDir\coverage_special.bin"

# 测试共享内存模式
Write-Host ""
Write-Host "5. Testing shared memory mode..." -ForegroundColor Yellow

# Windows 不完全支持 POSIX 共享内存，但我们可以测试内存映射文件
$SharedMemTest = {
    $RuntimePath = $args[0]
    $GeneratedDll = $args[1]
    $TestEntry = $args[2]
    $InputFile = $args[3]
    
    # 创建共享内存
    $mmf = [System.IO.MemoryMappedFiles.MemoryMappedFile]::CreateOrOpen("wfuzz_coverage", 65536)
    
    try {
        # 运行测试
        & dotnet $RuntimePath `
            --assembly $GeneratedDll `
            --entry $TestEntry `
            --input $InputFile `
            --coverage
        
        # 读取覆盖率数据
        $accessor = $mmf.CreateViewAccessor(0, 65536)
        $buffer = New-Object byte[] 256
        $accessor.ReadArray(0, $buffer, 0, 256)
        
        $nonZero = ($buffer | Where-Object { $_ -ne 0 }).Count
        Write-Host "  Shared memory non-zero entries: $nonZero"
        
        $accessor.Dispose()
    }
    finally {
        $mmf.Dispose()
    }
}

Write-Host ""
Write-Host "Running shared memory test..." -ForegroundColor Cyan
& $SharedMemTest $RuntimePath $GeneratedDll $TestEntry "$TestInputDir\normal.bin"

# 测试不同的测试入口
Write-Host ""
Write-Host "6. Testing different entry points..." -ForegroundColor Yellow

$TestEntries = @(
    "TestLibrary_Calculator_Add_int_int",
    "TestLibrary_StringProcessor_Process_string",
    "TestLibrary_Calculator_Multiply_int_int"
)

foreach ($entry in $TestEntries) {
    Write-Host ""
    Write-Host "Testing: $entry" -ForegroundColor Cyan
    
    # 为不同的入口创建合适的输入
    $inputData = switch -Wildcard ($entry) {
        "*_int_int" {
            # 两个整数
            @(0x0A, 0x00, 0x00, 0x00, 0x14, 0x00, 0x00, 0x00)  # 10, 20
        }
        "*_string" {
            # 字符串
            [System.Text.Encoding]::UTF8.GetBytes("Test String")
        }
        default {
            # 默认输入
            @(0x01, 0x02, 0x03, 0x04)
        }
    }
    
    $inputFile = "$TestInputDir\input_$($entry).bin"
    [System.IO.File]::WriteAllBytes($inputFile, $inputData)
    
    dotnet $RuntimePath `
        --assembly $GeneratedDll `
        --entry $entry `
        --input $inputFile `
        --coverage `
        --timeout 500 2>&1 | Out-Null
    
    Write-Host "  Exit code: $LASTEXITCODE"
}

# 汇总报告
Write-Host ""
Write-Host "7. Coverage Summary Report" -ForegroundColor Yellow
Write-Host ""
Write-Host "=== Test Results Summary ===" -ForegroundColor Green
Write-Host "Normal calculation test: $(if ($exitCode1 -eq 0) { 'PASSED' } else { 'FAILED' })"
Write-Host "Exception handling test: $(if ($exitCode2 -eq 1) { 'PASSED' } else { 'FAILED' })"
Write-Host "Special value test: $(if ($exitCode3 -eq 0) { 'PASSED' } else { 'FAILED' })"
Write-Host ""
Write-Host "Coverage collection: ENABLED"
Write-Host "Coverage reports saved to: $CoverageDir"
Write-Host ""
Write-Host "=== Test completed ===" -ForegroundColor Green