#!/usr/bin/env bash
# Builds the self-contained downloads (no .NET or Docker needed on the target computer).
# Usage: bash scripts/publish-native.sh [rid ...]      default: all of win-x64 osx-arm64 osx-x64 linux-x64 linux-arm64
#   VERSION=1.2.0 OUT=dist SKIP_FRONTEND=1 bash scripts/publish-native.sh linux-x64
# Output: $OUT/InvoiceManager-<rid>.zip (Windows) or .tar.gz (macOS, Linux).
set -euo pipefail
cd "$(dirname "$0")/.."
ROOT=$(pwd)
VERSION=${VERSION:-0.0.0-dev}
OUT=$(mkdir -p "${OUT:-dist}" && cd "${OUT:-dist}" && pwd)
RIDS=("$@")
[ ${#RIDS[@]} -eq 0 ] && RIDS=(win-x64 osx-arm64 osx-x64 linux-x64 linux-arm64)

if [ -z "${SKIP_FRONTEND:-}" ]; then
  echo "== Building the web UI"
  (cd frontend && npm ci --no-audit --no-fund && npm run build)
fi
[ -f frontend/dist/index.html ] || { echo "frontend/dist is missing; build the UI first"; exit 1; }

WORK=$(mktemp -d)
# A runtime-specific publish rewrites the NuGet lock files; put the committed ones back afterwards.
LOCKS=$(find backend -name packages.lock.json -not -path "*/bin/*" -not -path "*/obj/*")
for f in $LOCKS; do cp "$f" "$WORK/$(echo "$f" | tr / _)"; done
cleanup() { for f in $LOCKS; do cp "$WORK/$(echo "$f" | tr / _)" "$f"; done; rm -rf "$WORK"; }
trap cleanup EXIT

for rid in "${RIDS[@]}"; do
  echo "== Publishing $rid"
  app="$WORK/$rid/InvoiceManager"
  dotnet publish backend/src/LocalInvoiceManager.Web/LocalInvoiceManager.Web.csproj \
    -c Release -r "$rid" --self-contained true -o "$app" --nologo \
    -p:UseAppHost=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=none -p:Version="${VERSION%%-*}" -p:InformationalVersion="$VERSION"
  # A friendlier executable name (renaming a single-file app is safe).
  if [[ "$rid" == win-* ]]; then mv "$app/LocalInvoiceManager.Web.exe" "$app/InvoiceManager.exe"
  else mv "$app/LocalInvoiceManager.Web" "$app/InvoiceManager"; fi
  cp -R frontend/dist "$app/wwwroot"
  cp "$ROOT/LICENSE" "$app/" 2>/dev/null || true
  cat > "$app/README.txt" <<TXT
Invoice Manager $VERSION ($rid)

Start: run InvoiceManager (InvoiceManager.exe on Windows). Your browser opens http://localhost:8090.
Stop:  close the window or press Ctrl+C.
Data:  Windows %LOCALAPPDATA%\\LocalInvoiceManager, macOS ~/Library/Application Support/LocalInvoiceManager,
       Linux ~/.local/share/LocalInvoiceManager (set LIM_HOME to use another folder).
Options: --port 9000   --no-browser
Docs:  https://github.com/mgmehdiforme/local-invoice-manager
TXT
  name="InvoiceManager-$rid"   # stable name: releases/latest/download/InvoiceManager-<rid>.* always works
  if [[ "$rid" == win-* ]]; then
    (cd "$WORK/$rid" && rm -f "$OUT/$name.zip" && zip -qr "$OUT/$name.zip" InvoiceManager)
  else
    chmod +x "$app/InvoiceManager"
    tar -C "$WORK/$rid" -czf "$OUT/$name.tar.gz" InvoiceManager
  fi
  echo "   -> $OUT/$name.$([[ "$rid" == win-* ]] && echo zip || echo tar.gz)"
done
