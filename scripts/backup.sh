#!/usr/bin/env bash
# Create a backup now, and optionally copy it to another folder or disk.
# Usage: bash scripts/backup.sh [/media/usb/invoice-backups]
set -euo pipefail
cd "$(dirname "$0")/.."
set -a; [ -f .env ] && . ./.env; set +a

name=$(docker exec local-invoice-manager dotnet LocalInvoiceManager.Web.dll backup | tail -1)
BACKUP_PATH="${BACKUP_DIR:-../backups}"
[[ "$BACKUP_PATH" = /* ]] || BACKUP_PATH="deployment/$BACKUP_PATH"
echo "Backup created: $BACKUP_PATH/$name"

if [ $# -ge 1 ]; then
  mkdir -p "$1"
  cp "$BACKUP_PATH/$name" "$1/"
  echo "Copied to: $1/$name"
fi
