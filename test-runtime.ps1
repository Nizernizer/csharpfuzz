# 测试 WFuzzRuntime 基本功能的脚本

param(
    [string]$Configuration = "Release"
)

Write-Host "=== WFuzz Runtime Test Script ===" -ForegroundColor Green
Write-Host ""

# 构建项目
Write-Host "1. Building projects..." -ForegroundColor Yellow
dotnet build -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# 生成测试驱动
Write-Host ""
Write-Host "2. Generating test drivers..." -ForegroundColor Yellow
$GeneratorPath = ".\WFuzzGen\bin\$Configuration\net9.0\WFuzzGen.exe"
$TestLibraryPath = ".\TestLibrary\bin\$Configuration\net9.0\TestLibrary.dll"
$OutputDir = ".\TestOutput"

if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}

& $GeneratorPath $TestLibraryPath $OutputDir --namespace TestLibrary
if ($LASTEXITCODE -ne 0) {
    Write-Host "Generation failed!" -ForegroundColor Red
    exit 1
}

# 编译生成的代码
Write-Host ""
Write-Host "3. Building generated code..." -ForegroundColor Yellow
Push-Location $OutputDir
dotnet build -c $Configuration
Pop-Location
if ($LASTEXITCODE -ne 0) {
    Write-Host "Generated code build failed!" -ForegroundColor Red
    exit 1
}

# 准备测试输入
Write-Host ""
Write-Host "4. Preparing test inputs..." -ForegroundColor Yellow
$TestInputDir = ".\TestInputs"
New-Item -ItemType Directory -Force -Path $TestInputDir | Out-Null

# 创建会导致除零异常的输入
[byte[]]$divideByZeroInput = @(
    0x00, 0x00, 0x00, 0x00,  # 第一个 double = 0
    0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00,  # 第二个 double = 0
    0x00, 0x00, 0x00, 0x00
)
[System.IO.File]::WriteAllBytes("$TestInputDir\divide_by_zero.bin", $divideByZeroInput)

# 创建正常输入
[byte[]]$normalInput = @(
    0x00, 0x00, 0x00, 0x00,  # 第一个 double
    0x00, 0x00, 0xF0, 0x3F,  # = 1.0
    0x00, 0x00, 0x00, 0x00,  # 第二个 double
    0x00, 0x00, 0x00, 0x40   # = 2.0
)
[System.IO.File]::WriteAllBytes("$TestInputDir\normal.bin", $normalInput)

# 测试运行时
Write-Host ""
Write-Host "5. Testing WFuzzRuntime..." -ForegroundColor Yellow

$RuntimePath = ".\WFuzzRuntime\bin\$Configuration\net9.0\WFuzzRuntime.dll"
$GeneratedDll = "$OutputDir\bin\$Configuration\net9.0\WFuzzGen.Generated.dll"

# 测试正常输入
Write-Host ""
Write-Host "Test 1: Normal input" -ForegroundColor Cyan
dotnet $RuntimePath `
    --assembly $GeneratedDll `
    --entry "TestLibrary_Calculator_Divide_double_double" `
    --input "$TestInputDir\normal.bin" `
    --debug

Write-Host "Exit code: $LASTEXITCODE" -ForegroundColor $(if ($LASTEXITCODE -eq 0) { "Green" } else { "Red" })

# 测试崩溃输入
Write-Host ""
Write-Host "Test 2: Crash input (divide by zero)" -ForegroundColor Cyan
dotnet $RuntimePath `
    --assembly $GeneratedDll `
    --entry "TestLibrary_Calculator_Divide_double_double" `
    --input "$TestInputDir\divide_by_zero.bin" `
    --debug

Write-Host "Exit code: $LASTEXITCODE" -ForegroundColor $(if ($LASTEXITCODE -eq 1) { "Green" } else { "Red" })

# 测试其他入口点
Write-Host ""
Write-Host "Test 3: String processing test" -ForegroundColor Cyan
[byte[]]$stringInput = [System.Text.Encoding]::UTF8.GetBytes("Hello, World!")
[System.IO.File]::WriteAllBytes("$TestInputDir\string.bin", $stringInput)

dotnet $RuntimePath `
    --assembly $GeneratedDll `
    --entry "TestLibrary_StringProcessor_Process_string" `
    --input "$TestInputDir\string.bin" `
    --debug

Write-Host "Exit code: $LASTEXITCODE" -ForegroundColor $(if ($LASTEXITCODE -eq 0) { "Green" } else { "Red" })

# 检查崩溃文件
Write-Host ""
Write-Host "6. Checking crash files..." -ForegroundColor Yellow
$CrashDir = ".\crashes"
if (Test-Path $CrashDir) {
    $CrashFiles = Get-ChildItem $CrashDir -Filter "crash_*"
    if ($CrashFiles.Count -gt 0) {
        Write-Host "Found $($CrashFiles.Count) crash file(s):" -ForegroundColor Green
        $CrashFiles | ForEach-Object {
            Write-Host "  - $($_.Name)" -ForegroundColor Gray
        }
    } else {
        Write-Host "No crash files found" -ForegroundColor Yellow
    }
} else {
    Write-Host "Crash directory not found" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Test completed ===" -ForegroundColor Green