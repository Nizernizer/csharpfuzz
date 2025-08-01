#!/bin/bash

if [ -z "$DOTNET_VERSION" ]; then
    DOTNET_VERSION="8.0.404"
fi
if [ -z "$DOTNET_ARCH" ]; then
    DOTNET_ARCH="Linux-$(uname -m)"
fi

SDK_VERSION=2.0.7

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

cd $SCRIPT_DIR/../..

# 构建项目
dotnet build -c Release

mkdir -p build
cd build

mkdir -p wfuzz-build-code-csharp
cd wfuzz-build-code-csharp

mkdir -p bin

# 复制脚本
cp $SCRIPT_DIR/../wfuzz-build-code-csharp bin/wfuzz-build-code-csharp
cp $SCRIPT_DIR/../wfuzz-build-code-csharp-* bin/ 2>/dev/null || true

# 复制构建输出
cp $SCRIPT_DIR/../../WFuzzGen/bin/Release/net9.0/WFuzzGen.dll .
cp $SCRIPT_DIR/../../WFuzzGen/bin/Release/net9.0/WFuzzGen.runtimeconfig.json .
cp $SCRIPT_DIR/../../WFuzzGen/bin/Release/net9.0/*.dll . 2>/dev/null || true
cp $SCRIPT_DIR/../../WFuzz/bin/Release/net9.0/WFuzz.dll .

cat > wfuzz-package.json << EOF
{
  "dependencies" : ["dotnet@$DOTNET_VERSION", "wfuzz-utils@2.0.5"],
  "description" : "Wingfuzz Code Analyzer SDK for C#/.NET",
  "features" : [],
  "name" : "wfuzz-build-code-csharp",
  "platform" : "$DOTNET_ARCH",
  "products" : [ "code" ],
  "version" : "$SDK_VERSION",
  "hash": ""
}
EOF

tar cJvf ../wfuzz-build-code-csharp-$SDK_VERSION-$DOTNET_ARCH.tar.xz *