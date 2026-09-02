#!/usr/bin/env bash
# Builds every plugin and lays each one out into a single flat directory
# per id — plugin.json alongside its built DLLs — which is the layout
# PluginHost.Load actually expects. Needed because Contracts and
# Implementation build into two separate bin/ folders; without this step
# there's nowhere valid to point --plugins at.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STAGE_DIR="${1:-$REPO_ROOT/.stage/plugins}"
CONFIG="${CONFIGURATION:-Debug}"

echo "Building solution ($CONFIG)..."
dotnet build "$REPO_ROOT/LinguaEngine.sln" -c "$CONFIG" >/dev/null

rm -rf "$STAGE_DIR"
mkdir -p "$STAGE_DIR"

for plugin_dir in "$REPO_ROOT"/plugins/*/; do
    id="$(basename "$plugin_dir")"
    [ -f "$plugin_dir/plugin.json" ] || continue

    # engine.foo -> Engine.Foo (the Implementation project's folder name)
    impl_name="$(awk -F. '{for(i=1;i<=NF;i++){$i=toupper(substr($i,1,1)) substr($i,2)}}1' OFS=. <<<"$id")"
    impl_out="$plugin_dir/$impl_name/bin/$CONFIG/net9.0"

    if [ ! -d "$impl_out" ]; then
        echo "warning: no build output for '$id' at $impl_out, skipping" >&2
        continue
    fi

    mkdir -p "$STAGE_DIR/$id"
    cp -r "$impl_out/." "$STAGE_DIR/$id/"
    cp "$plugin_dir/plugin.json" "$STAGE_DIR/$id/"
done

echo "Staged plugins into: $STAGE_DIR"
