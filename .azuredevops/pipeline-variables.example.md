# Azure DevOps Pipeline Variables (Example)

Repository now uses 3 pipelines (one per project):

- `bot/azure-pipeline.yml` (validate + deploy bot)
- `overlay/azure-pipeline.yml` (deploy overlay)
- `infra/azure-pipeline.yml` (deploy infra)

## Variable Groups used by pipelines

Pipelines reference these Azure DevOps Variable Groups by name:

- `obs-overlay-shared`
- `obs-overlay-bot`

## obs-overlay-shared

Required variables:

- `azureServiceConnection`: Service connection name with access to target subscription/resource group
- `resourceGroup`: Resource group for deployment

## Bot deploy pipeline variables

For group `obs-overlay-bot` (used by `bot/azure-pipeline.yml`):

- `functionAppName`: optional override for Function App name
- `telegramBotToken`: Telegram bot token (mark as secret, optional)
- `telegramManagementChatId`: Telegram management chat id (optional)

If `functionAppName` is not set, bot pipeline resolves it automatically from the latest infra deployment output `functionAppName` in the target resource group.

## Overlay deploy pipeline variables

`overlay/azure-pipeline.yml` resolves `storageAccountName` automatically from the latest infra deployment output in the target resource group.

Optional override:

- `storageAccountName`: set as pipeline variable only when manual override is needed

## Infra deploy pipeline variables

`infra/azure-pipeline.yml` uses static `bicepPrefix: obstg` in YAML.
If needed later, you can move it back to a variable group.

Notes:

- Keep `telegramBotToken` secret.
- `bot/azure-pipeline.yml` validates `bot/` changes for pushes/PRs in `main`/`develop`, and deploys only from `main`.
- Deploy pipelines run only from `main` and only when related paths are changed.
