#!/bin/sh
set -e
cd "$(dirname "$0")"
./build.sh
wixl -a x64 -o LogVice.msi LogVice.wxs
./patch_msi.sh "$PWD/LogVice.msi"
echo PACKAGE_OK
