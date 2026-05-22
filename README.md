# DevOSRing

Four production-ready Loupedeck / Logi Plugin Service plugins that turn the
MX Master 4 **Actions Ring** into a developer-workflow surface, paired with a
companion VS Code / Cursor / Antigravity extension that supplies real workspace
context to every press.

| Sector       | Plugin                                  | What it does                                                                 |
|--------------|------------------------------------------|------------------------------------------------------------------------------|
| AI Refactor  | [`AIRefactorPlugin`](AIRefactorPlugin)   | LLM-refactor the active file or selection; open the diff in the IDE         |
| Run Tests    | [`TestActionPlugin`](TestActionPlugin)   | Auto-detect project type and run the right test command; show counts        |
| AI Review    | [`ReviewActionPlugin`](ReviewActionPlugin)| Review the current `git diff` with an LLM; render as Markdown in a webview |
| Deploy       | [`GitCommitPushPlugin`](GitCommitPushPlugin) | Stage all changes, AI commit message, push to origin (auto-upstream)    |

All four plugins are **fully real** when an LLM API key is configured, and
fall back to deterministic, diff-aware canned output when it isn't, so demos
always work and zero-config users still get useful behaviour.

## Architecture

```
+------------------+    HTTP 127.0.0.1:<ephemeral>   +---------------------+
| MX Master 4      |  bearer-token auth              | VS Code / Cursor /  |
| Actions Ring     |  ~/.devos/companion.json        | Antigravity         |
|        |         |                                 |        |            |
|        v         |                                 |        v            |
| Logi Plugin Svc  |     <----- HTTP ----->          | devos-companion ext |
|        |         |                                 +---------------------+
|        v         |                                 (active file, selection,
|  4 x .lplug4     |                                  git root, branch, diff,
|  DevOSCore.dll   |---- OpenAI-compatible HTTPS --->  notifications, webviews)
+------------------+         (optional)              [ OpenAI / Anthropic / ]
                                                     [ Azure / OpenRouter / ]
                                                     [ Ollama /v1 endpoint  ]
```

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for a deeper walk-through.

## Repo layout

```
DevOSRing/
├── DevOSCore/                  # Shared .NET 8 library all 4 plugins link
│   └── src/
│       ├── Companion/          # HTTP client for the VS Code extension
│       ├── Llm/                # OpenAI-compatible client + canned fallbacks
│       ├── Process/            # Cross-platform child-process runner
│       ├── Git/                # `git` CLI wrapper
│       ├── Tests/              # Project detector + test result parser
│       ├── Plugin/             # PluginActionBase, image renderer, logging
│       └── Settings/           # Typed wrappers over Plugin.SetPluginSetting
├── DevOSCore.Tests/            # xUnit tests for the shared library
├── AIRefactorPlugin/           # .lplug4 #1
├── TestActionPlugin/           # .lplug4 #2
├── ReviewActionPlugin/         # .lplug4 #3
├── GitCommitPushPlugin/        # .lplug4 #4
├── devos-companion/            # VS Code / Cursor / Antigravity extension
├── scripts/build/              # build-all.sh / .ps1 → dist/*.lplug4 + .vsix
├── demo_repo/                  # Tiny C# project used for "no LLM key" demos
└── dist/                       # Build artefacts
```

## Prerequisites

| Tool               | Purpose                          | Notes                                            |
|--------------------|----------------------------------|--------------------------------------------------|
| .NET SDK 8         | Build plugins + DevOSCore        | `dotnet --version` ≥ 8.0                          |
| Node 20            | Build companion extension        | `node --version` ≥ 20                             |
| Logi Plugin Service| Run the plugins                  | Bundled with Logi Options+                       |
| `logiplugintool`   | Pack `.lplug4` artefacts         | Optional; build script falls back to plain zip   |
| MX Master 4 mouse  | Bind actions to the Actions Ring | Or any LoupedeckCtFamily / Razer Stream device   |
| VS Code / Cursor / Antigravity | Run the companion extension | Any IDE that consumes VS Code extensions    |

The plugins reference `PluginApi.dll` from:

- macOS: `/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/PluginApi.dll`
- Windows: `C:\Program Files\Logi\LogiPluginService\PluginApi.dll`

Both paths are set automatically in [`Directory.Build.props`](Directory.Build.props).

## Quick start

```bash
git clone <this-repo>
cd DevOSRing

# 1. Build everything and produce dist/*.lplug4 + dist/devos-companion.vsix
./scripts/build/build-all.sh          # or build-all.ps1 on Windows

# 2. Install the companion in your IDE
code --install-extension dist/devos-companion.vsix
# (or `cursor --install-extension ...`, or `antigravity --install-extension ...`)

# 3. Install the 4 plugins (each .lplug4 is a regular Logi Options+ plugin)
#    Either double-click in Finder/Explorer or:
logiplugintool install dist/AIRefactor.lplug4
logiplugintool install dist/TestAction.lplug4
logiplugintool install dist/ReviewAction.lplug4
logiplugintool install dist/GitCommitPush.lplug4

# 4. Open Logi Options+, bind the four actions to the MX Master 4 Actions Ring.
#    Open a project in VS Code/Cursor/Antigravity, press a sector, watch it work.
```

## LLM configuration

Each plugin exposes an **Open LLM Settings** button (group: `DevOS Settings`) that
opens `~/.devos/llm.json`:

```json
{
  "endpoint": "https://api.openai.com/v1",
  "model": "gpt-4o-mini",
  "apiKey": "sk-...",
  "systemPrompt": ""
}
```

Endpoints that work out of the box:

| Provider    | Endpoint                                                |
|-------------|---------------------------------------------------------|
| OpenAI      | `https://api.openai.com/v1`                             |
| Azure OpenAI| `https://<resource>.openai.azure.com/openai/deployments/<deployment>` |
| OpenRouter  | `https://openrouter.ai/api/v1`                          |
| Ollama      | `http://localhost:11434/v1`                             |
| LM Studio   | `http://localhost:1234/v1`                              |

The API key is stored encrypted via `Plugin.SetPluginSetting(..., isSecure: true)`.

If `apiKey` is empty, all 4 actions still work; AI Refactor falls back to a
Roslyn-based cleanup, AI Review falls back to a diff-aware Markdown summary,
Deploy uses a deterministic Conventional Commits message, and Tests doesn't need
the LLM at all.

## Demo mode (no LLM, no live repo)

[`demo_repo/`](demo_repo) is a self-contained tiny C# project used for screen
recordings: open it in your IDE, press AI Refactor on `UserAuth.cs`, watch the
Roslyn-based cleanup collapse the nested `if`s into a single boolean.

## Troubleshooting

| Symptom                                       | What to check                                                          |
|-----------------------------------------------|------------------------------------------------------------------------|
| Button flashes "Companion offline"            | Reload your IDE window; confirm `~/.devos/companion.json` exists       |
| Button flashes "401" on every press           | Restart the IDE (regenerates the token)                                |
| `dotnet build` cannot find `PluginApi.dll`    | Install Logi Options+; confirm `Directory.Build.props` path is correct |
| `logiplugintool` not found                    | Install Logi Plugin Service dev tools; build script falls back to zip  |
| Refactor button shows "No active file"        | Bring focus into the editor in the IDE                                 |

## License

MIT — see [`LICENSE`](LICENSE).
