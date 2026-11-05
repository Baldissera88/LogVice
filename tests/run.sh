#!/bin/sh
set -e
cd "$(dirname "$0")/.."
API=/usr/lib/mono/4.5-api
[ -f LogVice.exe ] || ./build.sh
mcs -langversion:5 -nostdlib -noconfig -warnaserror+ \
  -r:$API/mscorlib.dll -r:$API/System.dll -r:$API/System.Core.dll -r:LogVice.exe \
  -out:tests/LogViceTests.exe tests/LogViceTests.cs
cp LogVice.exe tests/
mono tests/LogViceTests.exe tests/data "$@"
