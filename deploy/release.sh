#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
COMPOSE_FILE="${SCRIPT_DIR}/compose.server.yaml"
ENV_FILE="${SCRIPT_DIR}/.env"

if [[ ! -f "${ENV_FILE}" ]]; then
  echo "deploy/.env fehlt. Bitte zuerst deploy/.env.example nach deploy/.env kopieren und anpassen."
  exit 1
fi

cd "${ROOT_DIR}"

echo "[1/4] Images bauen und Container aktualisieren"
docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" up -d --build

echo "[2/4] Laufende Container anzeigen"
docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" ps

echo "[3/4] Letzte Backend-Logs"
docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" logs backend --tail=50

echo "[4/4] Deployment abgeschlossen"
echo "Web:     http://localhost:${WEB_HTTP_PORT:-8081}"
echo "Backend: http://localhost:${BACKEND_HTTP_PORT:-8080}"
