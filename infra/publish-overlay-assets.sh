#!/usr/bin/env bash

set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <storage-account-name> <resource-group>"
  exit 1
fi

storage_account_name="$1"
resource_group="$2"

script_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

az storage account show --name "$storage_account_name" --resource-group "$resource_group" >/dev/null

pushd "$repo_root/overlay" >/dev/null
npm install
npm run build
popd >/dev/null

az storage blob upload \
  --account-name "$storage_account_name" \
  --auth-mode login \
  --container-name '$web' \
  --name 'overlay-assets/overlay.js' \
  --file "$repo_root/overlay/dist/overlay.js" \
  --overwrite true

az storage blob upload \
  --account-name "$storage_account_name" \
  --auth-mode login \
  --container-name '$web' \
  --name 'overlay-assets/overlay.css' \
  --file "$repo_root/overlay/dist/overlay.css" \
  --overwrite true

az storage blob upload \
  --account-name "$storage_account_name" \
  --auth-mode login \
  --container-name '$web' \
  --name 'overlay-assets/overlay-template.html' \
  --file "$repo_root/overlay/dist/overlay-template.html" \
  --overwrite true

az storage blob upload \
  --account-name "$storage_account_name" \
  --auth-mode login \
  --container-name '$web' \
  --name '404.html' \
  --file "$repo_root/overlay/dist/index.html" \
  --overwrite true

echo "Published overlay assets to https://${storage_account_name}.z22.web.core.windows.net/overlay-assets/"