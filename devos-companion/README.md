# DevOS Companion

VS Code / Cursor / Antigravity extension that exposes the active editor's file,
selection, workspace, and git state to the DevOSRing plugins running on the
MX Master 4 Actions Ring.

## What it does

On activation it:

1. Generates a random 32-byte bearer token.
2. Binds a tiny HTTP server to `127.0.0.1` (ephemeral port by default).
3. Writes the port + token + pid + version to `~/.devos/companion.json` (mode `0600`).

The DevOSRing .NET plugins discover the server via that file, authenticate with
the bearer token, and call:

| Method | Path             | Used by                |
|--------|------------------|------------------------|
| GET    | `/v1/ping`       | health-check           |
| GET    | `/v1/context`    | all 4 plugins          |
| POST   | `/v1/diff`       | AI Refactor            |
| POST   | `/v1/apply`      | AI Refactor            |
| POST   | `/v1/review`     | AI Review / Tests      |
| POST   | `/v1/notify`     | all 4 plugins          |

The server only listens on the loopback interface and rejects any request without
the matching `Authorization: Bearer ...` header.

## Install

```bash
# In VS Code
code --install-extension dist/devos-companion-1.0.0.vsix

# In Cursor
cursor --install-extension dist/devos-companion-1.0.0.vsix

# In Antigravity (uses the same CLI signature as VS Code)
antigravity --install-extension dist/devos-companion-1.0.0.vsix
```

Or drag the `.vsix` onto the Extensions panel.

## Commands

| Command                          | What it does                              |
|----------------------------------|-------------------------------------------|
| `DevOS: Restart Companion Server`| Regenerate token, re-listen, rewrite disk |
| `DevOS: Show Companion Status`   | Show port + discovery file path           |

## Settings

| Key          | Default | Description                                    |
|--------------|---------|------------------------------------------------|
| `devos.port` | `0`     | Fixed loopback port. `0` = ephemeral (default).|

## Development

```bash
npm install
npm run build       # tsc
npm run watch
npm test            # vitest
npm run package     # builds ../dist/devos-companion-<version>.vsix
```
