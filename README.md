# OBS Telegram Overlay

Production-grade scaffold for a private Telegram-to-OBS overlay built around cheap Azure primitives:

- `bot/`: Azure Functions on .NET 8 isolated worker
- `overlay/`: React + Vite client rendered as an OBS Browser Source
- `infra/`: Bicep + deployment helpers for storage static website, SignalR, and Functions

## Architecture

1. Telegram sends webhook updates to the .NET Azure Function.
2. The bot normalizes messages and looks up active overlays for the source chat in Blob Storage.
3. Each overlay gets its own SignalR identity equal to its secret `overlayId`.
4. The overlay path is physically materialized in Azure Storage static website hosting as `/overlays/{overlayId}/index.html` and `/overlays/{overlayId}/config.json`.
5. The bot revokes an overlay by deleting those blobs and marking the overlay record as revoked.

This avoids cross-chat leakage in two places:

- The static URL itself only exists while the overlay is active.
- SignalR negotiate only returns a token for that specific `overlayId`, and the bot publishes messages only to that same logical user.

## Why Storage Static Website

Azure Storage static website hosting is cheaper than duplicating deployments in Static Web Apps for every secret overlay URL. The shared React bundle is uploaded once to `/overlay-assets`, while the bot creates only tiny per-overlay entrypoints and config files.

## Revocation Model

- New page loads fail immediately after revoke because `/overlays/{overlayId}/` is deleted.
- New SignalR sessions fail immediately because `negotiate` refuses revoked overlays.
- Existing SignalR sessions are bounded by token lifetime. The current default is `10` minutes and uses `CloseOnAuthenticationExpiration`, so long-lived leaked sessions are cut off on token expiry and cannot renegotiate after revocation.

## Repository Layout

- `bot/`: webhook, overlay registry, SignalR negotiate, Telegram command handling
- `overlay/`: OBS-friendly client bundle with browser TTS and reconnect support
- `infra/main.bicep`: Azure baseline deployment
- `infra/publish-overlay-assets.sh`: uploads `overlay.js` and `overlay.css` to the storage static website
- `obs-telegram-overlay.code-workspace`: VS Code multi-root workspace file

## Telegram Management Commands

Configure `Bot:TelegramManagementChatId` and send commands from that chat:

- `/overlay-create <chatId>`
- `/overlay-revoke <overlayId>`
- `/overlay-list <chatId>`

`chatId` is the Telegram chat whose messages should be mirrored into the overlay.

## Local Development

1. Update `bot/local.settings.json` with your Telegram bot token, management chat ID, SignalR connection string, and public base URLs.
2. Start the Functions app:

   ```bash
   cd bot
   func start
   ```

3. Build the frontend bundle:

   ```bash
   cd overlay
   npm install
   npm run build
   ```

4. Use `infra/publish-overlay-assets.sh` after provisioning Azure Storage static website hosting.

## Azure Deployment

Example Bicep deployment:

```bash
az deployment group create \
  --resource-group <rg> \
  --template-file infra/main.bicep \
  --parameters @infra/main.parameters.example.json
```

Then publish the overlay assets:

```bash
./infra/enable-static-website.sh <storage-account-name> <resource-group>
./infra/publish-overlay-assets.sh <storage-account-name> <resource-group>
```

## Cost Notes

- Start with `Free_F1` SignalR for low traffic and testing.
- Storage static website hosting is extremely cheap and scales well for many overlay paths.
- Functions Consumption keeps idle cost near zero.
- The next likely paid upgrade is SignalR, not storage or React hosting.