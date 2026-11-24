#!/usr/bin/env bash
# Local Invoice Manager — build from source and run with Docker Compose (Linux or macOS).
# Most people should use install/install.sh (no Docker) instead; see README.md.
# Usage: bash scripts/install.sh
set -euo pipefail
cd "$(dirname "$0")/.."
COMPOSE=(docker compose -f deployment/docker-compose.yml --env-file .env)

say() { printf '\n\033[1m%s\033[0m\n' "$*"; }

command -v docker >/dev/null || { echo "Docker is not installed. Install Docker (https://docs.docker.com/get-docker/) or use install/install.sh, which needs no Docker."; exit 1; }
docker compose version >/dev/null 2>&1 || { echo "Docker Compose v2 is missing: sudo apt install docker-compose-v2"; exit 1; }
docker info >/dev/null 2>&1 || { echo "Cannot talk to Docker. Add yourself to the docker group: sudo usermod -aG docker \$USER (then log out and in)"; exit 1; }

if [ ! -f .env ]; then
  say "Creating .env"
  cp .env.example .env
  tz=$(timedatectl show -p Timezone --value 2>/dev/null || readlink /etc/localtime 2>/dev/null | sed 's|.*zoneinfo/||' || true)
  sed -i.bak "s/^APP_UID=.*/APP_UID=$(id -u)/; s/^APP_GID=.*/APP_GID=$(id -g)/; s|^APP_TIMEZONE=.*|APP_TIMEZONE=${tz:-UTC}|" .env && rm -f .env.bak
fi
set -a; . ./.env; set +a

BACKUP_PATH="${BACKUP_DIR:-../backups}"
[[ "$BACKUP_PATH" = /* ]] || BACKUP_PATH="deployment/$BACKUP_PATH"
mkdir -p "$BACKUP_PATH"

if ss -ltn 2>/dev/null | grep -q ":${APP_PORT:-8090} " && ! docker ps --format '{{.Names}}' | grep -qx local-invoice-manager; then
  echo "Port ${APP_PORT:-8090} is already used by another program. Change APP_PORT in .env and run again."; exit 1
fi

say "Building and starting (first time takes a few minutes)…"
"${COMPOSE[@]}" up -d --build

say "Waiting for the app to become healthy…"
for _ in $(seq 1 60); do
  status=$(docker inspect -f '{{.State.Health.Status}}' local-invoice-manager 2>/dev/null || echo starting)
  [ "$status" = healthy ] && break
  sleep 2
done
if [ "${status:-}" != healthy ]; then
  echo "The app did not become healthy. Logs:"; "${COMPOSE[@]}" logs --tail 50; exit 1
fi

say "Ready: http://localhost:${APP_PORT:-8090}"
echo "Backups are saved in: $(cd "$BACKUP_PATH" && pwd)"
