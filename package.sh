#!/bin/sh
set -e
cd "$(dirname "$0")"
./build.sh
SRC=$(sed -n 's/^SRC="\(.*\)"$/\1/p' build.sh)
python3 - $SRC <<'PY'
import sys
parts=[open(f,encoding='utf-8-sig').read() for f in sys.argv[1:]]
src='\n'.join(parts)
src=''.join(c if ord(c)<128 else '\\u%04X'%ord(c) for c in src)
open('LogVice.combined.cs','w',encoding='utf-8',newline='\n').write(src)
w=open('wrapper.ps1',encoding='utf-8-sig').read().replace('\r\n','\n')
assert '__CSHARP__' in w and "'@" not in src.split('\n')[0]
for line in src.split('\n'):
    assert not line.startswith("'@"), line
out=w.replace('__CSHARP__',src).replace('\n','\r\n')
open('LogVice.ps1','w',encoding='utf-8-sig',newline='').write(out)
PY
API=/usr/lib/mono/4.5-api
mcs -langversion:5 -target:library -noconfig -nostdlib -r:$API/mscorlib.dll -r:$API/System.dll -r:$API/System.Core.dll -r:$API/System.Drawing.dll -r:$API/System.Windows.Forms.dll -out:/tmp/combined_check.dll LogVice.combined.cs
wixl -a x64 -o LogVice.msi LogVice.wxs
./patch_msi.sh "$PWD/LogVice.msi"
echo PACKAGE_OK
