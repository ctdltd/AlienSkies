#!/usr/bin/env bash
# Builds "Alien Skies.saver" for macOS (Apple silicon + Intel). Run on a Mac with:
#   * the .NET 10 SDK            https://dotnet.microsoft.com/download
#   * Xcode or its Command Line Tools (xcode-select --install): clang, lipo, codesign
#
# Usage: src/AlienSkies.Mac/build-saver.sh [options]
#   --data DIR      bundle full catalogs (stars.csv, exoplanets.csv, constellations.csv from
#                   tools/build_catalog.py) instead of the small samples built into the library
#   --arch LIST     space-separated subset of "arm64 x64" (default both, merged into one universal bundle)
#   --smoke         after building, load the library like the screen saver does and render test frames
#   --install       copy the result to ~/Library/Screen Savers
#
# Signing (optional; without it the bundle is ad-hoc signed, which is fine on the Mac that built it):
#   SIGN_IDENTITY="Developer ID Application: Certification Tool and Die, Ltd. (TEAMID)"
#   NOTARY_PROFILE=<name saved with: xcrun notarytool store-credentials>   (also notarizes and staples)
#
# Output: artifacts/mac/Alien Skies.saver and artifacts/mac/AlienSkies-macOS.zip
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"
out="$root/artifacts/mac"
data=""
archs="arm64 x64"
smoke=0
install=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --data) data="$2"; shift 2 ;;
    --arch) archs="$2"; shift 2 ;;
    --smoke) smoke=1; shift ;;
    --install) install=1; shift ;;
    -h|--help) sed -n '2,20p' "$0"; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

[[ "$(uname)" == "Darwin" ]] || { echo "This script must run on macOS." >&2; exit 1; }
for tool in dotnet clang lipo codesign; do
  command -v "$tool" >/dev/null || { echo "Missing $tool (install the .NET 10 SDK and Xcode Command Line Tools)." >&2; exit 1; }
done

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/Directory.Build.props" | head -1)"
version="${version:-0.0.0}"
min_macos="$(/usr/libexec/PlistBuddy -c 'Print :LSMinimumSystemVersion' "$here/Saver/Info.plist")"

rm -rf "$out"
mkdir -p "$out"

# 1. The C# core, compiled ahead of time to a native dylib for each architecture.
dylibs=()
clang_archs=()
for arch in $archs; do
  rid="osx-$arch"
  echo "==> dotnet publish ($rid)"
  dotnet publish "$here/AlienSkies.Mac.csproj" -c Release -r "$rid" -o "$out/publish-$arch" \
    -p:DebugType=none -p:StripSymbols=true -nologo
  dylibs+=("$out/publish-$arch/AlienSkiesCore.dylib")
  clang_archs+=(-arch "$([[ $arch == x64 ]] && echo x86_64 || echo arm64)")
done
first_arch="${archs%% *}"

# 2. The bundle.
bundle="$out/Alien Skies.saver"
contents="$bundle/Contents"
mkdir -p "$contents/MacOS" "$contents/Frameworks" "$contents/Resources"
sed "s/@VERSION@/$version/g" "$here/Saver/Info.plist" > "$contents/Info.plist"

echo "==> lipo AlienSkiesCore.dylib"
lipo -create "${dylibs[@]}" -output "$contents/Frameworks/AlienSkiesCore.dylib"
# SkiaSharp's macOS library is already universal.
cp "$out/publish-$first_arch/libSkiaSharp.dylib" "$contents/Frameworks/"

echo "==> clang AlienSkiesView.m"
clang "${clang_archs[@]}" -mmacosx-version-min="$min_macos" -fobjc-arc -O2 -Wall \
  -bundle -framework ScreenSaver -framework Cocoa \
  -o "$contents/MacOS/AlienSkies" "$here/Saver/AlienSkiesView.m"

if [[ -n "$data" ]]; then
  mkdir -p "$contents/Resources/data"
  for f in stars.csv exoplanets.csv constellations.csv; do
    [[ -f "$data/$f" ]] && cp "$data/$f" "$contents/Resources/data/"
  done
fi

# 3. Signing: inside out (libraries first, then the bundle).
identity="${SIGN_IDENTITY:--}"
sign_flags=(--force --sign "$identity")
[[ "$identity" != "-" ]] && sign_flags+=(--options runtime --timestamp)
echo "==> codesign (${identity})"
codesign "${sign_flags[@]}" "$contents/Frameworks/libSkiaSharp.dylib" "$contents/Frameworks/AlienSkiesCore.dylib"
codesign "${sign_flags[@]}" "$bundle"
codesign --verify --strict --verbose=1 "$bundle"

zip="$out/AlienSkies-macOS.zip"
(cd "$out" && ditto -c -k --keepParent "Alien Skies.saver" "$zip")

# 4. Notarization (needs a Developer ID identity and an App Store Connect account).
if [[ -n "${NOTARY_PROFILE:-}" ]]; then
  [[ "$identity" != "-" ]] || { echo "Notarizing needs SIGN_IDENTITY." >&2; exit 1; }
  echo "==> notarytool submit"
  xcrun notarytool submit "$zip" --keychain-profile "$NOTARY_PROFILE" --wait
  xcrun stapler staple "$bundle"
  rm -f "$zip"
  (cd "$out" && ditto -c -k --keepParent "Alien Skies.saver" "$zip")
fi

if [[ $smoke -eq 1 ]]; then
  echo "==> smoke test"
  clang -O1 -o "$out/smoke" "$here/Smoke/smoke.c"
  home="$(mktemp -d)"
  HOME="$home" "$out/smoke" "$contents/Frameworks/AlienSkiesCore.dylib" "$contents/Resources" \
    "$contents/Frameworks" "$out/smoke-frame.ppm"
fi

if [[ $install -eq 1 ]]; then
  dest="$HOME/Library/Screen Savers"
  mkdir -p "$dest"
  rm -rf "$dest/Alien Skies.saver"
  cp -R "$bundle" "$dest/"
  echo "Installed to $dest. Pick it in System Settings > Screen Saver."
fi

echo "Built $bundle"
