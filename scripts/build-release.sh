#!/usr/bin/env bash
# Gera a build WebGL do Unity, valida e cria o zip de release.
# Uso: scripts/build-release.sh [versao]   (ex.: scripts/build-release.sh v1.0.0)
# Com versao: tambem cria a tag local (o push da tag aciona .github/workflows/release.yml).
# Feche o Editor do Unity antes: o batchmode nao abre um projeto que ja esta aberto.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD="$ROOT/WebGLBuild"
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/6000.3.17f1/Unity.app/Contents/MacOS/Unity}"
VERSION="${1:-}"
ZIP="$ROOT/beach-tennis-go-webgl${VERSION:+-$VERSION}.zip"

[ -x "$UNITY" ] || { echo "Unity nao encontrado em $UNITY (defina UNITY_PATH)"; exit 1; }
if pgrep -fl "Unity.*-projectpath $ROOT" >/dev/null 2>&1 || pgrep -fl "Unity.*-projectPath $ROOT" >/dev/null 2>&1; then
  echo "O projeto esta aberto no Editor do Unity. Feche-o e rode de novo."; exit 1
fi

echo "==> Build Unity (WebGL)"
LOG="$(mktemp)"
"$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" -executeMethod BuildScript.BuildWebGL -logFile "$LOG" || true
grep -q "WebGL build succeeded" "$LOG" || { echo "Build falhou. Log: $LOG"; tail -30 "$LOG"; exit 1; }
[ -f "$BUILD/index.html" ] || { echo "index.html ausente em $BUILD"; exit 1; }
ls "$BUILD"/Build/*.wasm* >/dev/null 2>&1 || { echo "wasm ausente em $BUILD/Build"; exit 1; }

echo "==> Zip"
rm -f "$ZIP"
(cd "$BUILD" && zip -qr -X "$ZIP" . -x "*.DS_Store")
echo "    $ZIP ($(du -h "$ZIP" | cut -f1))"

if [ -n "$VERSION" ]; then
  echo "==> Tag $VERSION (local)"
  git -C "$ROOT" tag "$VERSION"
  echo "    Para publicar: git add -A && git commit && git push origin main && git push origin $VERSION"
fi
echo "Pronto."
