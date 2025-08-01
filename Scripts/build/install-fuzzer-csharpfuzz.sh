#!/bin/bash

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
PACKAGE=fuzzer-code-csharp-csharpfuzz

set -ex

tarfile=$(ls $SCRIPT_DIR/../../build/$PACKAGE-*-$(uname -s)-$(uname -m).tar.xz 2>/dev/null | tail -n 1)

if [ -z "$tarfile" ]; then
    echo "No tarfile found, please build it first"
    exit 1
fi

pathenv=$(wfuzz env | grep '^WFUZZ_CURRENT_SERVER_PREFIX')

eval "export $pathenv"

if [ -z "$WFUZZ_CURRENT_SERVER_PREFIX" ]; then
    echo "WFUZZ_CURRENT_SERVER_PREFIX not set"
    exit 1
fi

mkdir -p $WFUZZ_CURRENT_SERVER_PREFIX/$PACKAGE/
cd $WFUZZ_CURRENT_SERVER_PREFIX/$PACKAGE
tar xvf $tarfile