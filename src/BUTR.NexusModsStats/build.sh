#!/bin/sh

set -e

echo "🔍 Detecting architecture..."
arch=$(uname -m)
echo "🧠 Detected: $arch"

case "$arch" in
  aarch64)
    RID="linux-musl-arm64"
    ;;
  x86_64)
    RID="linux-musl-x64"
    ;;
  *)
    echo "❌ Unsupported architecture: $arch"
    exit 1
    ;;
esac

echo "🚀 Using Runtime Identifier: $RID"
echo "📦 Running dotnet publish..."

dotnet publish -c Release -r $RID -o /app/publish

echo "✅ Publish complete!"