#!/usr/bin/env bash
set -euo pipefail

REPO="ArtemKiyashko/obs-telegram-overlay"
BINARY="obstelegramoverlay_bot"
INSTALL_DIR="${INSTALL_DIR:-$HOME/.local/bin}"

# ── Detect OS / arch ─────────────────────────────────────────────────────────
OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS" in
  Darwin)
    case "$ARCH" in
      arm64)  RID="osx-arm64" ;;
      x86_64) RID="osx-x64"   ;;
      *)      echo "Unsupported macOS architecture: $ARCH"; exit 1 ;;
    esac
    ;;
  Linux)
    case "$ARCH" in
      x86_64)  RID="linux-x64"   ;;
      aarch64) RID="linux-arm64" ;;
      armv7l)  RID="linux-arm"   ;;
      *)       echo "Unsupported Linux architecture: $ARCH"; exit 1 ;;
    esac
    ;;
  *)
    echo "Unsupported OS: $OS. On Windows use install.ps1."
    exit 1
    ;;
esac

# ── Resolve latest release tag ────────────────────────────────────────────────
echo "Fetching latest release for $REPO..."
LATEST_TAG="$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" \
  | grep '"tag_name"' | head -1 | sed 's/.*"tag_name": *"\([^"]*\)".*/\1/')"

if [ -z "$LATEST_TAG" ]; then
  echo "Could not determine latest release tag."
  exit 1
fi

echo "Latest release: $LATEST_TAG"

# ── Download ──────────────────────────────────────────────────────────────────
DOWNLOAD_URL="https://github.com/$REPO/releases/download/$LATEST_TAG/${BINARY}_${RID}"
TMP_FILE="$(mktemp)"

echo "Downloading $BINARY ($RID)..."
curl -fsSL --progress-bar -o "$TMP_FILE" "$DOWNLOAD_URL"

# ── Install ───────────────────────────────────────────────────────────────────
mkdir -p "$INSTALL_DIR"
mv "$TMP_FILE" "$INSTALL_DIR/$BINARY"
chmod +x "$INSTALL_DIR/$BINARY"

echo ""
echo "Installed: $INSTALL_DIR/$BINARY"

# Warn if the directory is not in PATH
if ! echo ":$PATH:" | grep -q ":$INSTALL_DIR:"; then
  echo ""
  echo "Note: $INSTALL_DIR is not in your PATH."
  echo "Add it by putting this in your shell profile (~/.bashrc, ~/.zshrc, etc.):"
  echo "  export PATH=\"\$PATH:$INSTALL_DIR\""
fi

# ── macOS Gatekeeper bypass ───────────────────────────────────────────────────
if [ "$OS" = "Darwin" ]; then
  echo ""
  echo "Removing macOS quarantine attribute..."
  xattr -d com.apple.quarantine "$INSTALL_DIR/$BINARY" 2>/dev/null || true
  echo "Done. You may still need to allow it in System Settings → Privacy & Security on first run."
fi

echo ""
echo "Run: $BINARY --bot-api-token YOUR_TOKEN"
