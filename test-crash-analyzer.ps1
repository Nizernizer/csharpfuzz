# 测试崩溃分析工具的脚本

param(
    [string]$Configuration = "Release"
)

Write-Host "=== WFuzz Crash Analyzer Test Script ===" -ForegroundColor Green
Write-Host ""

# 构建项目
Write-Host "1. Building CrashAnalyzer..." -ForegroundColor Yellow
dotnet build CrashAnalyzer/CrashAnalyzer.csproj -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# 准备测试崩溃
Write-Host ""
Write-Host "2. Preparing test crashes..." -ForegroundColor Yellow

$CrashDir = ".\TestCrashes"
New-Item -ItemType Directory -Force -Path $CrashDir | Out-Null

# 创建模拟崩溃文件
function Create-TestCrash {
    param(
        [string]$Name,
        [string]$ExceptionType,
        [string]$Message,
        [string]$StackTrace,
        [byte[]]$InputData
    )
    
    $crashFile = "$CrashDir\crash_$Name"
    [System.IO.File]::WriteAllBytes($crashFile, $InputData)
    
    $infoContent = @"
=== WFuzz Crash Report ===
Timestamp: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss") UTC
Test Entry: TestLibrary_Calculator_Divide_double_double
Input Size: $($InputData.Length) bytes

Exception Type: $ExceptionType
Message: $Message

Stack Trace:
$StackTrace

Environment:
OS: Microsoft Windows NT 10.0.19045.0
.NET Version: 9.0.0
Process ID: 12345
Machine Name: TEST-MACHINE
"@
    
    Set-Content -Path "$crashFile.txt" -Value $infoContent
    
    $summary = "$ExceptionType`: $Message`n   at TestLibrary.Calculator.Divide(Double a, Double b)"
    Set-Content -Path "$crashFile.summary" -Value $summary
}

# 创建不同类型的崩溃
Write-Host "Creating test crashes..." -ForegroundColor Cyan

# 除零异常
Create-TestCrash -Name "divzero_001" `
    -ExceptionType "System.DivideByZeroException" `
    -Message "Attempted to divide by zero." `
    -StackTrace @"
   at TestLibrary.Calculator.Divide(Double a, Double b) in C:\Source\TestLibrary\Calculator.cs:line 15
   at WFuzzGen.TestLibrary_Calculator_Divide_double_double.Call(FuzzInput input) in C:\Source\Generated\TestLibrary_Calculator_Divide_double_double.cs:line 25
   at WFuzzRuntime.TestExecutor.ExecuteAsync(ICaller testCaller, Byte[] input, TimeSpan timeout)
"@ `
    -InputData @(0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x24, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00)

# 空引用异常
Create-TestCrash -Name "nullref_001" `
    -ExceptionType "System.NullReferenceException" `
    -Message "Object reference not set to an instance of an object." `
    -StackTrace @"
   at TestLibrary.StringProcessor.Process(String input) in C:\Source\TestLibrary\StringProcessor.cs:line 22
   at WFuzzGen.TestLibrary_StringProcessor_Process_string.Call(FuzzInput input) in C:\Source\Generated\TestLibrary_StringProcessor_Process_string.cs:line 30
   at WFuzzRuntime.TestExecutor.ExecuteAsync(ICaller testCaller, Byte[] input, TimeSpan timeout)
"@ `
    -InputData @(0x00)

# 索引越界异常
Create-TestCrash -Name "indexout_001" `
    -ExceptionType "System.IndexOutOfRangeException" `
    -Message "Index was outside the bounds of the array." `
    -StackTrace @"
   at TestLibrary.Container`1.Get(Int32 index) in C:\Source\TestLibrary\Container.cs:line 18
   at WFuzzGen.TestLibrary_Container_T_Get_int.Call(FuzzInput input) in C:\Source\Generated\TestLibrary_Container_T_Get_int.cs:line 28
   at WFuzzRuntime.TestExecutor.ExecuteAsync(ICaller testCaller, Byte[] input, TimeSpan timeout)
"@ `
    -InputData @(0xFF, 0xFF, 0xFF, 0x7F)

# 堆栈溢出异常（严重）
Create-TestCrash -Name "stackoverflow_001" `
    -ExceptionType "System.StackOverflowException" `
    -Message "Stack overflow." `
    -StackTrace @"
   at TestLibrary.RecursiveClass.Recurse(Int32 depth)
   at TestLibrary.RecursiveClass.Recurse(Int32 depth)
   at TestLibrary.RecursiveClass.Recurse(Int32 depth)
   [... repeated 1000 times ...]
"@ `
    -InputData @(0x00, 0x00, 0x00, 0x80)

# 访问违规（关键）
Create-TestCrash -Name "access_001" `
    -ExceptionType "System.AccessViolationException" `
    -Message "Attempted to read or write protected memory." `
    -StackTrace @"
   at TestLibrary.UnsafeOperations.WriteMemory(IntPtr ptr, Byte[] data) in C:\Source\TestLibrary\UnsafeOperations.cs:line 45
   at WFuzzGen.TestLibrary_UnsafeOperations_WriteMemory.Call(FuzzInput input)
   at WFuzzRuntime.TestExecutor.ExecuteAsync(ICaller testCaller, Byte[] input, TimeSpan timeout)
"@ `
    -InputData @(0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00)

# 创建重复崩溃（用于测试去重）
for ($i = 2; $i -le 5; $i++) {
    Create-TestCrash -Name "divzero_00$i" `
        -ExceptionType "System.DivideByZeroException" `
        -Message "Attempted to divide by zero." `
        -StackTrace @"
   at TestLibrary.Calculator.Divide(Double a, Double b) in C:\Source\TestLibrary\Calculator.cs:line 15
   at WFuzzGen.TestLibrary_Calculator_Divide_double_double.Call(FuzzInput input) in C:\Source\Generated\TestLibrary_Calculator_Divide_double_double.cs:line 25
   at WFuzzRuntime.TestExecutor.ExecuteAsync(ICaller testCaller, Byte[] input, TimeSpan timeout)
"@ `
        -InputData @(0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, [byte]$i)
}

Write-Host "Created $((Get-ChildItem $CrashDir -Filter "crash_*" | Where-Object { !$_.Name.EndsWith(".txt") -and !$_.Name.EndsWith(".summary") }).Count) test crashes" -ForegroundColor Green

# 运行分析
Write-Host ""
Write-Host "3. Running crash analysis..." -ForegroundColor Yellow

$AnalyzerPath = ".\CrashAnalyzer\bin\$Configuration\net9.0\CrashAnalyzer.exe"

# 测试1：基本分析
Write-Host ""
Write-Host "Test 1: Basic analysis" -ForegroundColor Cyan
& $AnalyzerPath analyze $CrashDir --dedupe

# 测试2：生成 JSON 报告
Write-Host ""
Write-Host "Test 2: JSON report" -ForegroundColor Cyan
& $AnalyzerPath analyze $CrashDir --format json --output crashes.json --top 5
if (Test-Path "crashes.json") {
    Write-Host "JSON report saved to crashes.json" -ForegroundColor Green
}

# 测试3：生成 HTML 报告
Write-Host ""
Write-Host "Test 3: HTML report" -ForegroundColor Cyan
& $AnalyzerPath analyze $CrashDir --format html --output crashes.html --top 10
if (Test-Path "crashes.html") {
    Write-Host "HTML report saved to crashes.html" -ForegroundColor Green
    Write-Host "Opening in browser..." -ForegroundColor Yellow
    Start-Process "crashes.html"
}

# 测试4：崩溃分类
Write-Host ""
Write-Host "Test 4: Crash triage" -ForegroundColor Cyan
$GeneratedDll = ".\TestOutput\bin\$Configuration\net9.0\WFuzzGen.Generated.dll"
$TestEntry = "TestLibrary_Calculator_Divide_double_double"
$CrashInput = "$CrashDir\crash_divzero_001"

if (Test-Path $GeneratedDll) {
    & $AnalyzerPath triage $GeneratedDll $TestEntry $CrashInput --debug
} else {
    Write-Host "Generated DLL not found, skipping triage test" -ForegroundColor Yellow
}

# 测试5：崩溃最小化
Write-Host ""
Write-Host "Test 5: Crash minimization" -ForegroundColor Cyan
if (Test-Path $GeneratedDll) {
    # 创建一个较大的崩溃输入
    $largeCrashData = New-Object byte[] 1024
    for ($i = 0; $i -lt 1024; $i++) {
        $largeCrashData[$i] = [byte]($i % 256)
    }
    $largeCrashData[8] = 0  # 确保第二个 double 为 0（导致除零）
    $largeCrashData[9] = 0
    $largeCrashData[10] = 0
    $largeCrashData[11] = 0
    
    $largeCrashFile = "$CrashDir\large_crash"
    [System.IO.File]::WriteAllBytes($largeCrashFile, $largeCrashData)
    
    & $AnalyzerPath minimize $GeneratedDll $TestEntry $largeCrashFile --output "$CrashDir\minimized_crash"
} else {
    Write-Host "Generated DLL not found, skipping minimization test" -ForegroundColor Yellow
}

# 测试6：目录比较
Write-Host ""
Write-Host "Test 6: Directory comparison" -ForegroundColor Cyan

# 创建第二个崩溃目录
$CrashDir2 = ".\TestCrashes2"
New-Item -ItemType Directory -Force -Path $CrashDir2 | Out-Null

# 复制一些崩溃并添加新的
Copy-Item "$CrashDir\crash_divzero_*" $CrashDir2
Copy-Item "$CrashDir\crash_nullref_*" $CrashDir2

# 添加独特的崩溃到第二个目录
Create-TestCrash -Name "outofmem_001" `
    -ExceptionType "System.OutOfMemoryException" `
    -Message "Insufficient memory to continue the execution of the program." `
    -StackTrace @"
   at System.String.Concat(String[] values)
   at TestLibrary.MemoryConsumer.AllocateLargeString(Int32 size)
"@ `
    -InputData @(0xFF, 0xFF, 0xFF, 0xFF)

Move-Item "$CrashDir\crash_outofmem_001*" $CrashDir2

& $AnalyzerPath compare $CrashDir $CrashDir2 --output comparison.txt

if (Test-Path "comparison.txt") {
    Write-Host "Comparison report saved to comparison.txt" -ForegroundColor Green
    Get-Content "comparison.txt" | Select-Object -First 20
}

# 清理
Write-Host ""
Write-Host "7. Cleanup (optional)..." -ForegroundColor Yellow
$cleanup = Read-Host "Clean up test files? (y/n)"
if ($cleanup -eq 'y') {
    Remove-Item $CrashDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $CrashDir2 -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "crashes.json" -ErrorAction SilentlyContinue
    Remove-Item "crashes.html" -ErrorAction SilentlyContinue
    Remove-Item "comparison.txt" -ErrorAction SilentlyContinue
    Write-Host "Test files cleaned up" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Test completed ===" -ForegroundColor Green