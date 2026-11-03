#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
app="build/Card Reader Guard.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
clang -fobjc-arc -Wall -Wextra -Werror -Wno-deprecated-declarations \
  -mmacosx-version-min=12.0 -arch arm64 -arch x86_64 \
  -framework Cocoa -framework DiskArbitration -framework IOKit -framework UserNotifications \
  main.m -o "$app/Contents/MacOS/CardReaderGuard"
cp Info.plist "$app/Contents/Info.plist"
cp README.md ../LICENSE "$app/Contents/Resources/"
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"
"$app/Contents/MacOS/CardReaderGuard" --self-test
ditto -c -k --sequesterRsrc --keepParent "$app" build/CardReaderGuard-v0.3.0-beta.1-mac-universal.zip
echo "Built universal macOS preview. No storage hardware was ejected during checks."
