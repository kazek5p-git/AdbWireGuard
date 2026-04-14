#!/usr/bin/env bash
set -euo pipefail

APP_USER="${APP_USER:-kazek}"
APP_GROUP="${APP_GROUP:-$APP_USER}"
APP_ROOT="${APP_ROOT:-/opt/adbwireguard-broker}"
CURRENT_DIR="$APP_ROOT/current"
RELEASES_DIR="$APP_ROOT/releases"
STAMP="$(date +%Y%m%d-%H%M%S)"
TARGET_RELEASE="$RELEASES_DIR/$STAMP"
SERVICE_NAME="${SERVICE_NAME:-adbwireguard-broker.service}"
ENV_FILE="${ENV_FILE:-/etc/adbwireguard-broker.env}"

if [[ ! -f "./AdbWireGuardRelay.dll" ]]; then
  echo "Brak AdbWireGuardRelay.dll w biezacym katalogu." >&2
  exit 1
fi

sudo mkdir -p "$RELEASES_DIR"
sudo mkdir -p "$(dirname "$ENV_FILE")"
sudo rm -rf "$TARGET_RELEASE"
sudo mkdir -p "$TARGET_RELEASE"
sudo cp -R . "$TARGET_RELEASE/"
sudo chown -R "$APP_USER:$APP_GROUP" "$APP_ROOT"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Tworze pusty $ENV_FILE"
  echo "ADBWG_BROKER_HOST_TOKENS=" | sudo tee "$ENV_FILE" > /dev/null
fi

sudo cp "$TARGET_RELEASE/deploy/kazpar/adbwireguard-broker.service" "/etc/systemd/system/$SERVICE_NAME"
sudo ln -sfn "$TARGET_RELEASE" "$CURRENT_DIR"
sudo systemctl daemon-reload
sudo systemctl enable --now "$SERVICE_NAME"

echo
echo "Wdrozenie zakonczone."
echo "Status uslugi:"
sudo systemctl --no-pager --full status "$SERVICE_NAME" || true
echo
echo "Health:"
curl -fsS http://127.0.0.1:5127/healthz || true
