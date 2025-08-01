#!/bin/bash

if [ -z "$DOTNET_VERSION" ]; then
    DOTNET_VERSION="8.0.404"
fi
if [ -z "$DOTNET_ARCH" ]; then
    DOTNET_ARCH="Linux-x86_64"
fi
if [ -z "$DOTNET_URL" ]; then
    case "$(uname -m)" in
        x86_64)
            DOTNET_URL="https://download.visualstudio.microsoft.com/download/pr/4e3b04aa-c015-4e06-a42e-05f9f3c54ed2/74d1bb68e330eea13ecfc47f7cf9aeb7/dotnet-sdk-8.0.404-linux-x64.tar.gz"
            ;;
        aarch64|arm64)
            DOTNET_URL="https://download.visualstudio.microsoft.com/download/pr/7f3a766e-9516-4579-aaf2-2b150caa465c/d58c8a3d0cf8beb8c0fe8d3e0ba37f0a/dotnet-sdk-8.0.404-linux-arm64.tar.gz"
            ;;
        *)
            echo "Unsupported architecture: $(uname -m)"
            exit 1
            ;;
    esac
fi

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

cd $SCRIPT_DIR/../..

if [ -d "$SCRIPT_DIR/../../build/dotnet" ] && [ -f "$SCRIPT_DIR/../../build/dotnet-$DOTNET_VERSION-$DOTNET_ARCH.tar.xz" ]; then
  echo ".NET SDK already exists"
  exit 0
fi

set -ex

mkdir -p build
cd build

curl -Lo dotnet-$DOTNET_VERSION.tar.gz "$DOTNET_URL"

mkdir -p dotnet

cd dotnet

tar xzf ../dotnet-$DOTNET_VERSION.tar.gz

cat > wfuzz-package.json << EOF
{
  "dependencies" : [],
  "description" : ".NET is a free, cross-platform, open source developer platform for building many different types of applications.",
  "features" : [],
  "name" : "dotnet",
  "platform" : "$DOTNET_ARCH",
  "products" : [ "code" ],
  "version" : "$DOTNET_VERSION",
  "hash": ""
}
EOF

tar cJvf ../dotnet-$DOTNET_VERSION-$DOTNET_ARCH.tar.xz *