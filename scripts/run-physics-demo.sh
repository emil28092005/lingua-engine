#!/usr/bin/env bash
# Stages the shared engine plugins, builds+places the sample's own
# game-specific plugin (physics-demo-game, listed in this project's own
# pluginPaths rather than the shared engine catalog — it's this game's
# logic, not a reusable engine plugin), then runs samples/PhysicsDemo.
# Extra args are forwarded to `engine run`.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIG="${CONFIGURATION:-Debug}"
STAGE_DIR="$REPO_ROOT/.stage/plugins"
GAME_DIR="$REPO_ROOT/samples/PhysicsDemo/GamePlugins/physics-demo-game"

"$REPO_ROOT/scripts/stage-plugins.sh" "$STAGE_DIR"

cp "$GAME_DIR/bin/$CONFIG/net9.0/"*.dll "$GAME_DIR/" 2>/dev/null || true
cp "$GAME_DIR/bin/$CONFIG/net9.0/"*.pdb "$GAME_DIR/" 2>/dev/null || true

cd "$REPO_ROOT/samples/PhysicsDemo"
exec dotnet "$REPO_ROOT/src/Engine.Host/bin/$CONFIG/net9.0/Engine.Host.dll" run --windowed \
    --plugins "$STAGE_DIR" --project project.json --scene scene.json "$@"
