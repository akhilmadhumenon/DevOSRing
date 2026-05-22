#!/usr/bin/env bash
# Builds DevOSCore, all 4 plugins, runs xunit tests, packs each plugin
# into a .lplug4, and packs the companion extension into a .vsix.
# Output goes to dist/.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DIST="$REPO_ROOT/dist"
CONFIG="${CONFIG:-Release}"

PLUGINS=("AIRefactor:AIRefactorPlugin" "TestAction:TestActionPlugin" "ReviewAction:ReviewActionPlugin" "GitCommitPush:GitCommitPushPlugin")

log()  { printf "\033[1;34m[devos] %s\033[0m\n" "$*"; }
fail() { printf "\033[1;31m[devos] %s\033[0m\n" "$*" >&2; exit 1; }

command -v dotnet >/dev/null   || fail "dotnet CLI not found"
command -v node >/dev/null     || fail "node not found (needed for companion)"
command -v npm >/dev/null      || fail "npm not found"

mkdir -p "$DIST"

log "Building DevOS.sln ($CONFIG)..."
( cd "$REPO_ROOT" && DEVOS_SKIP_LINK=true dotnet build -c "$CONFIG" --nologo -v minimal DevOS.sln )

log "Running unit tests..."
( cd "$REPO_ROOT" && dotnet test -c "$CONFIG" --nologo --no-build --verbosity quiet DevOS.sln )

for entry in "${PLUGINS[@]}"; do
    short="${entry%%:*}"
    proj="${entry##*:}"
    bin="$REPO_ROOT/$proj/bin/$CONFIG"
    if [[ ! -d "$bin" ]]; then fail "missing $bin"; fi

    if command -v logiplugintool >/dev/null; then
        log "Packing $short via logiplugintool..."
        logiplugintool pack "$bin" "$DIST/$short.lplug4"
    else
        log "logiplugintool not on PATH; producing $short.zip with the same layout..."
        ( cd "$bin/.." && zip -qr "$DIST/$short.lplug4" bin metadata )
    fi
done

log "Building devos-companion VSIX..."
( cd "$REPO_ROOT/devos-companion" && {
    if [[ ! -d node_modules ]]; then npm install --silent; fi
    npm run build --silent
    if [[ -x "$(command -v npx)" ]]; then
        npx --yes @vscode/vsce package --out "$DIST/devos-companion.vsix" >/dev/null
    else
        fail "npx not found; cannot run @vscode/vsce"
    fi
  } )

log "Artefacts:"
ls -lh "$DIST"
