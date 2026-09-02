#!/usr/bin/env bash
# Stages every plugin, then runs samples/WindowDemo windowed. Extra args
# are forwarded to `engine run` — e.g. ./run-sample.sh --screenshot out.png
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIG="${CONFIGURATION:-Debug}"
STAGE_DIR="$REPO_ROOT/.stage/plugins"

"$REPO_ROOT/scripts/stage-plugins.sh" "$STAGE_DIR"

cd "$REPO_ROOT/samples/WindowDemo"
exec dotnet "$REPO_ROOT/src/Engine.Host/bin/$CONFIG/net9.0/Engine.Host.dll" run --windowed \
    --plugins "$STAGE_DIR" --project project.json --scene scene.json "$@"
