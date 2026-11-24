#!/usr/bin/env bash
# Restore from a backup zip (verifies it first and saves a safety backup of the current data).
# Usage: bash scripts/restore.sh /path/to/invoice-backup-....zip
set -euo pipefail
cd "$(dirname "$0")/.."
[ $# -eq 1 ] || { echo "Usage: bash scripts/restore.sh <backup.zip>"; exit 2; }
[ -f "$1" ] || { echo "File not found: $1"; exit 2; }

tmp="/tmp/restore-$(date +%s).zip"
docker cp "$1" "local-invoice-manager:$tmp"
docker exec local-invoice-manager dotnet LocalInvoiceManager.Web.dll verify "$tmp"
read -r -p "Replace ALL current data with this backup? Type yes to continue: " answer
[ "$answer" = "yes" ] || { echo "Cancelled."; exit 1; }
docker exec local-invoice-manager dotnet LocalInvoiceManager.Web.dll restore "$tmp"
docker exec local-invoice-manager rm -f "$tmp" || true
docker restart local-invoice-manager >/dev/null
echo "Done. Open the app again in your browser."
