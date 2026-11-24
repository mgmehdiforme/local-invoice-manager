#!/usr/bin/env bash
# Invoice Manager — install or update on macOS or Linux (no Docker, no .NET needed).
#   curl -fsSL https://raw.githubusercontent.com/mgmehdiforme/local-invoice-manager/main/install/install.sh | bash
# Your data is kept on update (it lives outside the app folder).
set -euo pipefail
REPO="mgmehdiforme/local-invoice-manager"

case "$(uname -s)" in
  Linux) os=linux ;;
  Darwin) os=osx ;;
  *) echo "Unsupported system: $(uname -s). On Windows use install.ps1."; exit 1 ;;
esac
case "$(uname -m)" in
  x86_64|amd64) arch=x64 ;;
  arm64|aarch64) arch=arm64 ;;
  *) echo "Unsupported processor: $(uname -m)"; exit 1 ;;
esac
rid="$os-$arch"
[ "$rid" = "linux-arm64" ] || [ "$rid" = "linux-x64" ] || [ "$os" = "osx" ] || { echo "No build for $rid"; exit 1; }

if [ "$os" = osx ]; then APP="$HOME/Applications/InvoiceManager"; else APP="${XDG_DATA_HOME:-$HOME/.local/share}/LocalInvoiceManager/app"; fi
BIN="$HOME/.local/bin"
URL="https://github.com/$REPO/releases/latest/download/InvoiceManager-$rid.tar.gz"

echo "Downloading Invoice Manager for $rid…"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
curl -fL --progress-bar "$URL" -o "$tmp/app.tar.gz"
tar -xzf "$tmp/app.tar.gz" -C "$tmp"

# Stop a running copy before replacing it.
pkill -f "$APP/InvoiceManager" 2>/dev/null || true
rm -rf "$APP"
mkdir -p "$(dirname "$APP")" "$BIN"
mv "$tmp/InvoiceManager" "$APP"
chmod +x "$APP/InvoiceManager"
[ "$os" = osx ] && xattr -dr com.apple.quarantine "$APP" 2>/dev/null || true
ln -sf "$APP/InvoiceManager" "$BIN/invoice-manager"

if [ "$os" = linux ]; then
  mkdir -p "${XDG_DATA_HOME:-$HOME/.local/share}/applications"
  cat > "${XDG_DATA_HOME:-$HOME/.local/share}/applications/invoice-manager.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Invoice Manager
Comment=Local invoices, quotations and payments
Exec=$APP/InvoiceManager
Icon=$APP/wwwroot/favicon.svg
Terminal=true
Categories=Office;Finance;
DESKTOP
else
  cat > "$HOME/Applications/Invoice Manager.command" <<CMD
#!/bin/bash
exec "$APP/InvoiceManager"
CMD
  chmod +x "$HOME/Applications/Invoice Manager.command"
fi

echo
echo "Installed to $APP"
echo "Start it any time with: invoice-manager   (or from your applications menu)"
case ":$PATH:" in *":$BIN:"*) ;; *) echo "Tip: add $BIN to your PATH to use the invoice-manager command." ;; esac
echo "Starting… your browser will open http://localhost:8090"
nohup "$APP/InvoiceManager" >/dev/null 2>&1 &
