#!/bin/bash

if [ -z "$DOTNET_VERSION" ]; then
    DOTNET_VERSION="8.0.404"
fi
if [ -z "$DOTNET_ARCH" ]; then
    DOTNET_ARCH="Linux-$(uname -m)"
fi

AFL_VERSION=v4.31c
VERSION=2.0.7
PACKAGE=fuzzer-code-csharp-csharpfuzz

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

cd $SCRIPT_DIR/../..

set -ex

# 构建项目
dotnet build -c Release

# 构建崩溃分析器 (如果有 Rust 版本)
if [ -d "$SCRIPT_DIR/../../CrashAnalyzer" ]; then
    cd $SCRIPT_DIR/../../CrashAnalyzer
    cargo build --release
    cd $SCRIPT_DIR/../..
fi

mkdir -p build
cd build

mkdir -p $PACKAGE

# 克隆并构建 AFL++
if [ ! -d "aflpp" ]; then
    git clone https://github.com/AFLplusplus/AFLplusplus.git aflpp --progress
fi

cd aflpp
git checkout -f $AFL_VERSION
make NO_PYTHON=1 afl-fuzz afl-showmap

cd $SCRIPT_DIR/../../build/$PACKAGE
mkdir -p bin

# 复制 AFL 二进制文件
cp $SCRIPT_DIR/../../build/aflpp/afl-fuzz $SCRIPT_DIR/../../build/aflpp/afl-showmap bin/

# 复制崩溃分析器
if [ -f "$SCRIPT_DIR/../../CrashAnalyzer/target/release/crash-analyzer" ]; then
    cp $SCRIPT_DIR/../../CrashAnalyzer/target/release/crash-analyzer bin/
else
    # 创建占位脚本
    cat > bin/crash-analyzer << 'EOF'
#!/bin/bash
echo "Crash analyzer not implemented yet"
exit 0
EOF
    chmod +x bin/crash-analyzer
fi

# 复制脚本
cp $SCRIPT_DIR/../csharpfuzz* bin/
cp $SCRIPT_DIR/../fuzzer-code-csharp* bin/
cp $SCRIPT_DIR/../fuzzer-replay bin/

# 复制构建输出
cp $SCRIPT_DIR/../../WFuzzAgent/bin/Release/net9.0/WFuzzAgent.dll .
cp $SCRIPT_DIR/../../WFuzzAgent/bin/Release/net9.0/WFuzzAgent.runtimeconfig.json .
cp $SCRIPT_DIR/../../WFuzzDriver/bin/Release/net9.0/WFuzzDriver.dll .
cp $SCRIPT_DIR/../../WFuzzDriver/bin/Release/net9.0/WFuzzDriver.runtimeconfig.json .
cp $SCRIPT_DIR/../../WFuzzEngine/bin/Release/net9.0/WFuzzEngine.dll .
cp $SCRIPT_DIR/../../WFuzz/bin/Release/net9.0/WFuzz.dll wfuzz.dll

# 复制所有依赖
cp $SCRIPT_DIR/../../WFuzzDriver/bin/Release/net9.0/*.dll . 2>/dev/null || true

cat > wfuzz-package.json << EOF
{
  "dependencies" : ["dotnet@$DOTNET_VERSION"],
  "description" : "Wingfuzz Code Fuzzer for C#/.NET",
  "features" : [],
  "name" : "$PACKAGE",
  "platform" : "$DOTNET_ARCH",
  "products" : [ "code" ],
  "version" : "$VERSION",
  "hash": ""
}
EOF

tar cJvf ../$PACKAGE-$VERSION-$DOTNET_ARCH.tar.xz *