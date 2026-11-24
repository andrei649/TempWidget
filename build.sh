#!/usr/bin/env bash
# Builds everything from Linux: the program, the portable zip, the MSI installer and the source zip.
# Needs: .NET SDK 8+, zip, python3, wixl (msitools).   Usage: ./build.sh ["Publisher name"]
set -euo pipefail
cd "$(dirname "$0")"
VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' TempWidget.csproj)   # single source of truth
PUBLISHER="${1:-TempWidget}"
OUT=bin/Release/net48/win-x64

dotnet build -c Release -v quiet -nologo

rm -rf dist && mkdir -p dist/TempWidget
cp "$OUT"/*.dll "$OUT"/TempWidget.exe "$OUT"/TempWidget.exe.config dist/TempWidget/
cp README.txt PRIVACY.txt THIRD-PARTY-NOTICES.txt dist/TempWidget/
cp redist/PawnIO_setup.exe dist/TempWidget/   # official signed installer, run only on request

(cd dist && zip -qr "TempWidget-$VERSION-portable.zip" TempWidget)

python3 installer/make-wxs.py dist/TempWidget "$VERSION" "$PUBLISHER" Assets/TempWidget.ico > dist/TempWidget.wxs
MSI="dist/TempWidget-$VERSION-x64.msi"
wixl --arch x64 -o "$MSI" dist/TempWidget.wxs
# wixl puts RemoveExistingProducts after our deferred actions, but Windows Installer requires it
# directly after InstallInitialize, or every install fails with error 2613. Renumber, then verify.
msibuild "$MSI" -q "UPDATE \`InstallExecuteSequence\` SET \`Sequence\` = 1501 WHERE \`Action\` = 'RemoveExistingProducts'"
msibuild "$MSI" -q "UPDATE \`InstallExecuteSequence\` SET \`Sequence\` = 1502 WHERE \`Action\` = 'CloseApp'"
msibuild "$MSI" -q "UPDATE \`InstallExecuteSequence\` SET \`Sequence\` = 1503 WHERE \`Action\` = 'RemoveStartupTask'"
ORDER=$(msiinfo export "$MSI" InstallExecuteSequence | tail -n +4 | sort -t$'\t' -k3 -n | cut -f1 | tr -d '\r' | grep -A1 -x InstallInitialize | tail -1)
if [ "$ORDER" != "RemoveExistingProducts" ]; then echo "MSI sequence check failed: '$ORDER' follows InstallInitialize" >&2; exit 1; fi

zip -qr "dist/TempWidget-$VERSION-source.zip" *.cs *.csproj *.txt *.md App.config app.manifest build.sh Start.cmd build-local.ps1 .gitignore Assets installer package redist tests -x 'tests/bin/*' 'tests/obj/*'
ls -la dist
