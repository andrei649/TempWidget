#!/usr/bin/env bash
# Builds everything from Linux: the program, the portable zip, the MSI installer and the source zip.
# Needs: .NET SDK 8+, zip, python3, wixl (msitools).   Usage: ./build.sh ["Publisher name"]
set -euo pipefail
cd "$(dirname "$0")"
VERSION=2.1.0
PUBLISHER="${1:-TempWidget}"
OUT=bin/Release/net48/win-x64

dotnet build -c Release -v quiet -nologo

rm -rf dist && mkdir -p dist/TempWidget
cp "$OUT"/*.dll "$OUT"/TempWidget.exe "$OUT"/TempWidget.exe.config dist/TempWidget/
cp README.txt PRIVACY.txt THIRD-PARTY-NOTICES.txt dist/TempWidget/
cp redist/PawnIO_setup.exe dist/TempWidget/   # official signed installer, run only on request

(cd dist && zip -qr "TempWidget-$VERSION-portable.zip" TempWidget)

python3 installer/make-wxs.py dist/TempWidget "$VERSION" "$PUBLISHER" Assets/TempWidget.ico > dist/TempWidget.wxs
wixl --arch x64 -o "dist/TempWidget-$VERSION-x64.msi" dist/TempWidget.wxs

zip -qr "dist/TempWidget-$VERSION-source.zip" *.cs *.csproj *.txt App.config app.manifest build.sh STORE.md SELLING.md DEVELOPMENT.md VALIDATION.md .gitignore Assets installer package redist tests -x 'tests/bin/*' 'tests/obj/*'
ls -la dist
