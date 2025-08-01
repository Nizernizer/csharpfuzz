#!/bin/bash

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

cd $SCRIPT_DIR/../..
./Scripts/build/build-dotnet-runtime.sh
./Scripts/build/build-cli-csharp.sh
./Scripts/build/build-fuzzer-csharpfuzz.sh