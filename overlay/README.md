# Overlay Frontend

Frontend bundle for OBS browser source.

## Commands

- `npm run dev` - local development server
- `npm run build` - production build to `dist/`
- `npm run lint` - ESLint checks

## Static publish artifacts

After build, these files are published to Azure Storage static website:

- `dist/overlay.js` -> `overlay-assets/overlay.js`
- `dist/overlay.css` -> `overlay-assets/overlay.css`
- `dist/overlay-template.html` -> `overlay-assets/overlay-template.html`

The bot then creates per-overlay entrypoints under `/overlays/{overlayId}/` by copying `overlay-template.html` and generating `config.json`.

Publishing to Azure Storage is handled by `overlay/azure-pipeline.yml`.
