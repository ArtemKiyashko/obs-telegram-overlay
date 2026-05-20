#!/usr/bin/env bash

set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <storage-account-name> <resource-group>"
  exit 1
fi

storage_account_name="$1"
resource_group="$2"

az storage account show --name "$storage_account_name" --resource-group "$resource_group" >/dev/null

az storage blob service-properties update \
  --account-name "$storage_account_name" \
  --auth-mode login \
  --static-website \
  --index-document index.html \
  --404-document 404.html

echo "Enabled static website hosting for ${storage_account_name}."