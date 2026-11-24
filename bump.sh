#!/bin/sh
# Sets a new LogVice version:
#   LogVice.wxs (with a new product code), src/AssemblyInfo.cs and the build date in src/Program.cs,
#   the version and product code in README.md,
#   an At a glance row, a dated empty section and the install table in CHANGELOG.md.
# Usage: ./bump.sh <major.minor.patch>
set -e
cd "$(dirname "$0")"

new="$1"
if ! printf '%s\n' "$new" | grep -Eq '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'; then
  echo "Usage: $0 <major.minor.patch>, for example $0 1.2.0" >&2
  exit 2
fi

current=$(sed -n 's/^ *Version="\([^"]*\)".*/\1/p' LogVice.wxs | head -n 1)
if [ -z "$current" ]; then
  echo "No Version was found in LogVice.wxs." >&2
  exit 1
fi
oldguid=$(sed -n 's/.*<Product Id="\([^"]*\)".*/\1/p' LogVice.wxs | head -n 1)
if [ -z "$oldguid" ]; then
  echo "No product code was found in LogVice.wxs." >&2
  exit 1
fi

# The highest of the two versions comes last when sorted by number, part by part.
highest=$(printf '%s\n%s\n' "$current" "$new" | sort -t. -k1,1n -k2,2n -k3,3n | tail -n 1)
if [ "$new" = "$current" ] || [ "$highest" != "$new" ]; then
  echo "$new is not higher than the current version $current." >&2
  exit 1
fi
if grep -q "^## LogVice $new\$" CHANGELOG.md; then
  echo "CHANGELOG.md already has a section for $new." >&2
  exit 1
fi

if [ -r /proc/sys/kernel/random/uuid ]; then
  guid=$(cat /proc/sys/kernel/random/uuid)
else
  guid=$(uuidgen)
fi
guid=$(printf '%s' "$guid" | tr 'a-f' 'A-F')

sed -i \
  -e "s/<Product Id=\"[^\"]*\"/<Product Id=\"$guid\"/" \
  -e "s/^\( *\)Version=\"[^\"]*\"/\1Version=\"$new\"/" \
  -e "s/DefaultVersion=\"[^\"]*\"/DefaultVersion=\"$new.0\"/" \
  LogVice.wxs
sed -i \
  -e "s/AssemblyVersion(\"[^\"]*\")/AssemblyVersion(\"$new.0\")/" \
  -e "s/AssemblyFileVersion(\"[^\"]*\")/AssemblyFileVersion(\"$new.0\")/" \
  -e "s/AssemblyInformationalVersion(\"[^\"]*\")/AssemblyInformationalVersion(\"$new\")/" \
  src/AssemblyInfo.cs

# src/Program.cs: the build date that About shows.
sed -i "s/BuildDate = \"[^\"]*\"/BuildDate = \"$(LC_ALL=C date +'%-d %B %Y')\"/" src/Program.cs

# README.md: the version badge, the uninstall command and the detection product code.
sed -i \
  -e "s/alt=\"Version [^\"]*\"/alt=\"Version $new\"/" \
  -e "s#badge/version-[0-9.]*-#badge/version-$new-#" \
  -e "s/msiexec \/x {$oldguid} \/qn/msiexec \/x {$guid} \/qn/" \
  -e "s/MSI product code \`{$oldguid}\` (version [0-9.]*)/MSI product code \`{$guid}\` (version $new)/" \
  README.md

date=$(LC_ALL=C date +'%-d %B %Y')
# CHANGELOG.md: the install table first, then the At a glance row and the new section.
sed -i \
  -e "s/Run the $current MSI/Run the $new MSI/" \
  -e "s/msiexec \/x {$oldguid} \/qn/msiexec \/x {$guid} \/qn/" \
  -e "s/add $current as a new app/add $new as a new app/" \
  -e "s/^| Product code ($current) |/| Product code ($new) | \`{$guid}\` |\n&/" \
  CHANGELOG.md
awk -v v="$new" -v old="$current" -v d="$date" -v g="$guid" '
  /^\| \*\*[0-9.]+\*\* \(latest\) \|/ && !row {
    n = split($0, c, " \\| ")
    upgrade = c[3]; sub(/ \|$/, "", upgrade)
    print "| **" v "** (latest) | | " upgrade " |"
    print "| " old " | " substr(c[2], 1, length(c[2])) " | |"
    row = 1
    next
  }
  !done && /^## / {
    print "## LogVice " v; print ""; print d "."; print ""
    print "| | |"; print "|---|---|"
    print "| Product code | `{" g "}` |"
    print "| Upgrade code | `{2A8DF866-E6D4-4A44-ABAC-C64CA0D367B3}` (unchanged) |"
    print "| Files | `LogVice.msi`, `LogVice.exe`, `SHA256SUMS.txt` |"
    print ""
    done = 1
  }
  { print }
' CHANGELOG.md > CHANGELOG.md.new
mv CHANGELOG.md.new CHANGELOG.md

for f in LogVice.wxs src/AssemblyInfo.cs README.md CHANGELOG.md; do
  if ! grep -q "$new" "$f"; then
    echo "$f was not changed. Check it by hand." >&2
    exit 1
  fi
done

echo "LogVice $current -> $new, product code {$guid}"
echo "Changed LogVice.wxs, src/AssemblyInfo.cs, src/Program.cs, README.md and CHANGELOG.md."
echo "Write the changes in CHANGELOG.md, then build, test and commit."
