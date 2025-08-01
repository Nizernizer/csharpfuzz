# 运行脚本以构建并运行WFuzzGen项目

Write-Host "构建并运行WFuzzGen..." -ForegroundColor Green

# 构建
Write-Host "构建项目..." -ForegroundColor Yellow
dotnet build WFuzz.sln
if ($LASTEXITCODE -ne 0) {
    Write-Host "构建失败!" -ForegroundColor Red
    exit 1
}

# 创建输出目录
$outputDir = "generated"
if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDir | Out-Null

# 运行
Write-Host "分析 TestLibrary..." -ForegroundColor Yellow
dotnet run --project WFuzzGen -- TestLibrary\bin\Debug\net9.0\TestLibrary.dll $outputDir --namespace TestLibrary

if ($LASTEXITCODE -eq 0) {
    Write-Host "成功! 生成的文件列表如下：" -ForegroundColor Green
    Get-ChildItem $outputDir
} else {
    Write-Host "运行失败!" -ForegroundColor Red
}
