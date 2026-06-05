# Azure DevOps Pipeline Variables (Example)

Repository now uses 3 pipelines (one per project):

- `bot/azure-pipeline.yml` (validate + deploy bot)
- `overlay/azure-pipeline.yml` (deploy overlay)
- `infra/azure-pipeline.yml` (deploy infra)

## Variable Groups used by pipelines

Pipelines reference these Azure DevOps Variable Groups by name:

- `obs-overlay-shared`
- `obs-overlay-bot`
- `obs-overlay-overlay`
- `obs-overlay-infra`

## obs-overlay-shared

Required variables:

- `azureServiceConnection`: Service connection name with access to target subscription/resource group
- `resourceGroup`: Resource group for deployment

## Bot deploy pipeline variables

For group `obs-overlay-bot` (used by `bot/azure-pipeline.yml`):

- `functionAppName`: existing Function App name
- `telegramBotToken`: Telegram bot token (mark as secret, optional)
- `telegramManagementChatId`: Telegram management chat id (optional)

## Overlay deploy pipeline variables

For group `obs-overlay-overlay` (used by `overlay/azure-pipeline.yml`):

- `storageAccountName`: existing Storage Account name with static website enabled

## Infra deploy pipeline variables

For group `obs-overlay-infra` (used by `infra/azure-pipeline.yml`):

- `location`: Azure region (for example `westeurope`)
- `bicepPrefix`: short resource prefix (for example `obstg`)

Notes:

- Keep `telegramBotToken` secret.
- `bot/azure-pipeline.yml` validates `bot/` changes for pushes/PRs in `main`/`develop`, and deploys only from `main`.
- Deploy pipelines run only from `main` and only when related paths are changed.
