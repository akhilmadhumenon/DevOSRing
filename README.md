# DevOSRing

Four Loupedeck / Logi Plugin Service plugins that turn the **MX Master 4 Actions
Ring** into a developer-workflow surface, paired with a companion VS Code /
Cursor / Antigravity extension that supplies real workspace context to every
press.

| Sector       | Plugin                                          | One press does this                                                            |
|--------------|-------------------------------------------------|--------------------------------------------------------------------------------|
| AI Refactor  | [`AIRefactorPlugin`](AIRefactorPlugin)          | LLM-refactor the active file or selection, open the diff inside the IDE       |
| Run Tests    | [`TestActionPlugin`](TestActionPlugin)          | Auto-detect the project's test runner, execute it, stream status to the button |
| AI Review    | [`ReviewActionPlugin`](ReviewActionPlugin)      | Review the active selection / file / git diff with an LLM as a Markdown panel  |
| Deploy       | [`GitCommitPushPlugin`](GitCommitPushPlugin)    | Stage all changes, write an AI commit message, push (auto-creates upstream)    |

A fifth diagnostic action — **Test LLM** — ships inside the AI Refactor plugin
and verifies that the configured LLM endpoint is reachable from the
LogiPluginService process. Use it once during setup.

---

## Language scope

The plugins are **language-agnostic when an LLM API key is configured** — AI
Refactor and AI Review work on whatever language Cursor / VS Code identifies for
the active editor (Python, TypeScript, Go, Rust, Java, etc.), Run Tests
auto-detects seven ecosystems (see [Run Tests](#run-tests)), and Deploy is
git-only.

Three things are currently **C#-specific**:

1. The **LLM-less canned refactor fallback** for AI Refactor uses Roslyn to
   collapse nested `if`s and simplify boolean returns. Other languages get a
   whitespace tidy instead. Configure an LLM key to refactor any language.
2. The **bundled [`demo_repo/`](demo_repo)** is a small C# project. It exists
   purely to provide a known-clean baseline for screen recordings so that any
   substantive diff is unambiguously the LLM's output, not a pre-baked snippet.
3. The plugin binaries themselves are compiled C# / .NET 8 (this is just the
   Loupedeck SDK requirement; it has no bearing on what code you can refactor).

If you only intend to demo without an LLM key, today that demo is best done on
a C# file. Everything else is polyglot.

---

## Architecture

```
+------------------+    HTTP 127.0.0.1:<ephemeral>   +---------------------+
| MX Master 4      |  bearer-token auth              | VS Code / Cursor /  |
| Actions Ring     |  ~/.devos/companion.json        | Antigravity         |
|        |         |  + heartbeat every 5s           |        |            |
|        v         |                                 |        v            |
| Logi Plugin Svc  |     <----- HTTP ----->          | devos-companion ext |
|        |         |     auto-wake if offline        +---------------------+
|        v         |                                 (active file, selection,
|  4 x .lplug4     |                                  git root, branch, diff,
|  DevOSCore.dll   |---- OpenAI-compatible HTTPS --->  notifications, webviews)
+------------------+         (optional)              [ OpenAI / Anthropic / ]
                                                     [ Azure  / OpenRouter / ]
                                                     [ Groq   / Ollama /v1  ]
```

- The companion is a tiny **VS Code extension** (works in any VS Code-derived
  IDE: VS Code, Cursor, Antigravity). It binds a loopback HTTP server on an
  ephemeral port, generates a bearer token, and writes both into
  `~/.devos/companion.json` with mode `0600`. It updates `heartbeatAt` every 5 s
  so plugins can tell "alive" from "stale".
- The .NET plugins read that file, ping `/v1/ping` to verify liveness, then call
  `/v1/context`, `/v1/diff`, `/v1/apply`, `/v1/review`, `/v1/notify`.
- If the discovery file is missing or stale, the plugin auto-launches Cursor
  via `open -a Cursor` (mac) / registry-probed `Cursor.exe` (Windows) /
  `cursor` on `PATH` (Linux), then polls `/v1/ping` for up to 15 s.
- LLM calls go directly from the plugin to the configured endpoint over HTTPS.
  The companion never proxies LLM traffic.

A deeper write-up lives in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

---

## Repo layout

```
DevOSRing/
├── DevOSCore/                  # Shared .NET 8 library all 4 plugins reference
│   └── src/
│       ├── Companion/          # CompanionClient + CompanionLauncher (auto-wake)
│       ├── Llm/                # OpenAI-compatible client + canned fallbacks
│       │   └── Canned/         # Roslyn refactor, diff-aware review, conventional commit
│       ├── Process/            # ProcessRunner + BinaryResolver (PATH-augmenting)
│       ├── Git/                # `git` CLI wrapper
│       ├── Tests/              # ProjectDetector + TestResultParser
│       ├── Plugin/             # namespace DevOSRing.Core.Hosting — PluginActionBase, log, images
│       └── Settings/           # Typed wrappers over Plugin.SetPluginSetting
├── DevOSCore.Tests/            # xUnit tests (currently 32 / 32 green)
├── AIRefactorPlugin/           # .lplug4 #1  (assembly: AIRefactorPluginV2.dll)
├── TestActionPlugin/           # .lplug4 #2  (assembly: TestActionPluginV2.dll)
├── ReviewActionPlugin/         # .lplug4 #3  (assembly: ReviewActionPluginV2.dll)
├── GitCommitPushPlugin/        # .lplug4 #4  (assembly: GitCommitPushPluginV2.dll)
├── devos-companion/            # VS Code / Cursor / Antigravity extension (TypeScript)
├── scripts/
│   ├── build/build-all.sh|ps1  # one-shot: builds, tests, packs .lplug4 + .vsix
│   └── setup-llm.sh            # reads .env → writes ~/.devos/llm.json
├── demo_repo/                  # Tiny C# project used for AI Refactor demos
├── docs/ARCHITECTURE.md
├── Directory.Build.props       # Locates PluginApi.dll and plugin install dir
├── DevOS.sln                   # Includes all 5 .csprojs (4 plugins + Core + Tests)
├── .env.example                # Template for LLM credentials
└── dist/                       # Build artefacts (.lplug4, .vsix)
```

> The `V2` suffix is intentional. The Logi Options+ host strips trailing
> numerics from plugin display names internally, so the `V2` assembly name is
> what lets v2 builds load alongside an existing v1 install during testing.

---

## Prerequisites

| Tool                          | Min version | Purpose                                                                | macOS install                      | Windows install                                 |
|-------------------------------|------------|------------------------------------------------------------------------|------------------------------------|-------------------------------------------------|
| **.NET SDK**                  | 8.0       | Build `DevOSCore` and the four plugins                                  | `brew install --cask dotnet-sdk`   | <https://dot.net/download>                       |
| **Node.js + npm**             | 20.x      | Build the companion extension                                           | `brew install node@20`             | <https://nodejs.org>                             |
| **Logi Options+**             | latest    | Ships `LogiPluginService` and `PluginApi.dll`                           | <https://logi.com/options-plus>    | <https://logi.com/options-plus>                  |
| **VS Code / Cursor / Antigravity** | any   | Host for the companion extension                                        | <https://cursor.com>               | <https://cursor.com>                             |
| **MX Master 4** (or any LoupedeckCT-family device) | n/a | Actions Ring surface                                  | hardware                           | hardware                                         |
| **`git`**                     | 2.x       | Deploy action                                                           | included with Xcode CLT            | <https://git-scm.com>                            |
| **`logiplugintool`** *(opt.)* | latest    | Pack `.lplug4` from a folder; build script falls back to `zip` if absent | bundled with Logi Plugin SDK       | bundled with Logi Plugin SDK                     |

`PluginApi.dll` is auto-located by [`Directory.Build.props`](Directory.Build.props):

- macOS: `/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/PluginApi.dll`
- Windows: `C:\Program Files\Logi\LogiPluginService\PluginApi.dll`

If those paths don't exist on your machine, install (or reinstall) Logi Options+
— the file is shipped inside it.

---

## Setup — from `git clone` to a working button in ~5 minutes

### 1. Clone and build everything

```bash
git clone <this-repo> DevOSRing
cd DevOSRing
./scripts/build/build-all.sh        # build-all.ps1 on Windows
```

The script:
1. Restores NuGet packages and `npm install`s the companion's deps
2. Builds `DevOS.sln` in Release (DevOSCore + 4 plugins + tests)
3. Runs the xUnit suite (must be 32 / 32 green to proceed)
4. Packs each plugin into `dist/<short>.lplug4` (via `logiplugintool` if
   installed, otherwise a `.zip` with the same layout)
5. Packs the companion into `dist/devos-companion.vsix`

> The first run downloads NuGet packages and can take ~1 minute. Subsequent
> incremental builds are sub-second.

### 2. Configure your LLM key

The plugins work without an LLM (canned fallbacks), but for production-quality
output you'll want a key. Any OpenAI-compatible endpoint is supported.

```bash
cp .env.example .env
# edit .env and fill in LLM_API_KEY (and optionally LLM_ENDPOINT / LLM_MODEL)
./scripts/setup-llm.sh
```

`setup-llm.sh` writes `~/.devos/llm.json` with mode `0600`. The script infers
sensible defaults from the key prefix (`gsk_*` → Groq, `sk-or*` → OpenRouter,
`sk-*` → OpenAI), so for many providers just setting `LLM_API_KEY` is enough.

Tested endpoints:

| Provider     | `LLM_ENDPOINT`                                          | Example `LLM_MODEL`             |
|--------------|---------------------------------------------------------|---------------------------------|
| OpenAI       | `https://api.openai.com/v1`                             | `gpt-4o-mini`                   |
| Azure OpenAI | `https://<resource>.openai.azure.com/openai/deployments/<deployment>` | (your deployment name) |
| Groq         | `https://api.groq.com/openai/v1`                        | `llama-3.3-70b-versatile`       |
| OpenRouter   | `https://openrouter.ai/api/v1`                          | `openai/gpt-4o-mini`            |
| Ollama       | `http://localhost:11434/v1`                             | `llama3.1:8b`                   |
| LM Studio    | `http://localhost:1234/v1`                              | (whatever you loaded)           |

> `.env` is git-ignored. The key never leaves your machine except to call the
> configured LLM endpoint directly.

Settings resolution order (highest wins): per-plugin `Plugin.SetPluginSetting`
override → `~/.devos/llm.json` → environment variables (`LLM_API_KEY`,
`LLM_ENDPOINT`, `LLM_MODEL`).

### 3. Install the companion extension into your IDE

```bash
# Cursor
"/Applications/Cursor.app/Contents/Resources/app/bin/cursor" \
    --install-extension dist/devos-companion-1.0.0.vsix

# VS Code
code --install-extension dist/devos-companion-1.0.0.vsix

# Antigravity
antigravity --install-extension dist/devos-companion-1.0.0.vsix
```

Then **launch (or reload) your IDE once**. The extension activates on
`onStartupFinished`, writes `~/.devos/companion.json`, and starts the 5-second
heartbeat.

Verify:

```bash
cat ~/.devos/companion.json
# {"port":51839,"token":"...","pid":12345,"version":"1.0.0","ide":"Cursor",
#  "state":"ready","startedAt":1716364800000,"heartbeatAt":1716364805000}
```

### 4. Install the four plugins into Logi Plugin Service

The `.lplug4` files are regular Logi Options+ plugins:

```bash
# If logiplugintool is on PATH:
logiplugintool install dist/AIRefactor.lplug4
logiplugintool install dist/TestAction.lplug4
logiplugintool install dist/ReviewAction.lplug4
logiplugintool install dist/GitCommitPush.lplug4

# Otherwise double-click each .lplug4 in Finder / Explorer.
```

For active **development** (so edits to plugin source hot-reload into Logi
Options+ without re-packing), the per-plugin `dotnet build` instead drops a
`.link` file pointing at the build output:

```bash
dotnet build AIRefactorPlugin/src/AIRefactorPlugin.csproj -c Release
# → ~/Library/Application Support/Logi/LogiPluginService/Plugins/AIRefactorPluginV2.link
```

The `.link` is just a text file containing the absolute path to
`bin/Release/`. Set `-p:DEVOS_SKIP_LINK=true` to skip this step when packaging
for distribution.

### 5. Restart LogiPluginService

The service caches plugin assemblies in memory. Bounce it after a fresh install
or after rebuilding a plugin:

```bash
pkill -x LogiPluginService    # mac — auto-respawns on next event
```

On Windows: open Task Manager → kill `LogiPluginService.exe` (it respawns
when you next interact with Logi Options+).

### 6. Bind the actions

Open **Logi Options+**, select the MX Master 4, navigate to the **Actions
Ring**, and bind one of the four DevOS actions to each of the four sectors.
The actions appear under the `DevOS` category.

### 7. Verify end-to-end with the diagnostic action

The AI Refactor plugin also exposes a **Test LLM** action under the `DevOS`
category. Press it (you can temporarily bind it to one sector for the test).
The button should show `<latency>ms` and Cursor should pop a notification:

```
DevOS LLM ok: llama-3.3-70b-versatile via https://api.groq.com/openai/v1
replied "PONG" in 312ms
```

If that works, your full stack — companion → plugin → LLM — is wired up.

---

## What each action does

### AI Refactor

`AIRefactorPlugin/src/Actions/AIRefactorAction.cs`

| State                    | Trigger                                                                                     |
|--------------------------|---------------------------------------------------------------------------------------------|
| `Reading file`           | Companion handshake succeeded; pulling the active file path + content                       |
| `Calling AI`             | LLM request in flight (logged with endpoint, model, language, input chars)                  |
| `AI diff` / `Canned diff`| Diff view opened in Cursor; user accepts via Cmd+Shift+P → *DevOS: Apply Pending Refactor*  |
| `No active file`         | The IDE editor has no active file — bring focus into the editor and retry                   |

The refactored content is opened in a side-by-side diff view inside the IDE.
The companion's diff route returns *immediately* to the plugin (so the device
button is never blocked); the accept/discard flow lives entirely in the IDE.

### Run Tests

`TestActionPlugin/src/Actions/TestAction.cs`

Auto-detects the workspace's test runner in this order (first match wins):

| Signal in workspace root                                | Runner invoked                              |
|---------------------------------------------------------|---------------------------------------------|
| `*.sln` or any nested `*.csproj`                        | `dotnet test --nologo --verbosity quiet`    |
| `Cargo.toml`                                            | `cargo test --quiet`                        |
| `go.mod`                                                | `go test ./...`                             |
| `pom.xml`                                               | `mvn -q test`                               |
| `build.gradle` / `build.gradle.kts`                     | `gradle test --console=plain`               |
| `pytest.ini` / `pyproject.toml` / `conftest.py` / `setup.cfg` | `pytest -q`                          |
| `pnpm-lock.yaml`                                        | `pnpm test --silent`                        |
| `yarn.lock`                                             | `yarn test --silent`                        |
| `package.json`                                          | `npm test --silent`                         |

Three-phase notification UX:

1. **Started** — IDE toast: `DevOS: Run Tests started — Dotnet: dotnet test in <workspace> (timeout 10m)`
2. **Running** — device button label ticks every 2 s with elapsed time (`Running 2s`, `Running 4s`, …)
3. **Completed** — IDE toast: `DevOS: tests PASSED — 32/32 in 2.1s — Dotnet: …` (or `FAILED — X failed / Y passed …`). The full stdout/stderr tail also pops up as a Markdown webview.

`BinaryResolver` is consulted for runner binaries (`dotnet`, `pytest`, `npm`,
…) because the LogiPluginService process inherits a stripped PATH on macOS.

### AI Review

`ReviewActionPlugin/src/Actions/ReviewAction.cs`

Picks a target in this order:

1. Non-empty **selection** in the active editor (reviewed in isolation, with
   file path + language as context)
2. **Active file** as a whole
3. Workspace **git diff** (staged ➜ unstaged)
4. Otherwise: surface "nothing to review"

The result renders as a Markdown webview inside the IDE (Summary / Risks /
Suggestions sections). Without an LLM key, the git-diff path falls back to a
deterministic diff-aware summary; the file / selection paths cannot fall back
and will tell the user the LLM is required.

### Deploy

`GitCommitPushPlugin/src/Actions/DeployAction.cs`

1. Verifies the workspace is a git repo (uses `ctx.GitRoot` or `ctx.WorkspaceRoot`)
2. `git add -A`
3. Generates a commit message — LLM (Conventional Commits format) if configured,
   deterministic fallback otherwise
4. `git commit -m "<message>"`
5. `git push` — sets upstream automatically with `--set-upstream origin <branch>`
   on first push of a branch
6. Surfaces the commit hash and pushed-to-branch on the button + IDE toast

The action does *not* open pull requests or kick off CI by design — that
remains the user's flow.

### Test LLM (diagnostic, lives in the AI Refactor plugin)

`AIRefactorPlugin/src/Actions/TestLlmAction.cs`

One press → sends a tiny "respond with PONG" prompt to the configured LLM →
shows latency on the button + a notification with the full settings (endpoint,
model, masked key length) and the raw reply. Useful for verifying network
reachability from inside the LogiPluginService process (which has its own PATH
and proxy settings — different from your shell).

---

## Companion auto-wake

If the companion isn't running when you press a button, the plugin will:

1. Read `~/.devos/companion.json` and ping `/v1/ping` (bearer-auth'd, 2 s timeout)
2. If that fails: launch the IDE (Cursor → VS Code → Antigravity, preferring
   the one that last ran the companion) via `open -g -a` on macOS
3. Poll the discovery file + ping every 500 ms for up to 15 s while the device
   button shows `Waking IDE` / `Still waking`
4. Once the companion comes online, continue the original action automatically

The companion writes `state: "stopped"` (rather than deleting the file) on
`deactivate()`, so a window reload no longer causes a "companion offline"
gap — the new extension instance overwrites the file with `state: "ready"` as
soon as it activates.

If after 15 s the companion still isn't responding, the button shows
`Open Cursor` and the log records:

```
[Review] companion still offline after auto-wake — open Cursor and reload the window
```

The most common cause of that state is having installed a fresh VSIX into a
running IDE: the old extension was disposed but the new one only activates on
the next window startup. **Cmd+Shift+P → Developer: Reload Window** in Cursor
fixes it, one time.

---

## Demo mode (no LLM, no live repo)

[`demo_repo/`](demo_repo) is a self-contained tiny C# project with three files
crafted so that the canned Roslyn fallback produces an essentially-empty diff —
making any *substantive* change unambiguously the LLM's work:

| File                | Smell family                                                            |
|---------------------|-------------------------------------------------------------------------|
| `UserAuth.cs`       | One-liner — baseline "no-op" example                                    |
| `OrderProcessor.cs` | Nested `if/else`, magic numbers, mixed logging + business logic         |
| `InventoryReport.cs`| Manual `for` loops + string concat that should be LINQ + `StringBuilder`|

Open this folder in Cursor / VS Code / Antigravity, focus one of the `.cs`
files, press the **AI Refactor** ring action. Tail the log to verify which path
was taken:

```bash
tail -n 20 "$HOME/Library/Application Support/Logi/LogiPluginService/Logs/plugin_logs/AIRefactor.log"
# [AIRefactor] Calling LLM: endpoint=…, model=…, language=csharp, inputChars=2412, selection=False
# [AIRefactor] LLM ok in 1820ms, 2104 chars
# [AIRefactor] Diff opened (source=llm, outputChars=2104); user will accept/discard via Cursor Command Palette
```

`source=llm` means the LLM was called; `source=canned` means the Roslyn fallback.

---

## Troubleshooting

| Symptom                                              | Likely cause                                                                                 | Fix                                                                                                       |
|------------------------------------------------------|----------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------|
| Button flashes `Open Cursor`                         | Just installed a new VSIX into a running IDE; the new extension hasn't activated yet         | Cmd+Shift+P → **Developer: Reload Window** in Cursor (one-time)                                           |
| Button flashes `Companion off` then auto-recovers    | Working as intended — the auto-wake brought the IDE up                                       | None                                                                                                      |
| Button flashes `401`                                 | Companion regenerated its bearer token; the plugin's cached info is stale                    | Press the button again — `EnsureConnectedAsync` re-pings and refreshes                                    |
| `Test LLM` shows `HTTP 401` / `403`                  | `LLM_API_KEY` is wrong or expired                                                            | Update `.env`, rerun `./scripts/setup-llm.sh`                                                             |
| `Test LLM` shows `Timeout`                           | LLM endpoint unreachable from the LogiPluginService process (different network / proxy)      | Verify with `curl` from the same machine; if behind a proxy, set `HTTPS_PROXY` for the service           |
| `Run Tests` shows `Launch fail` for `dotnet`         | macOS apps inherit a stripped PATH; `BinaryResolver` couldn't find the executable            | Install dotnet to a standard location (`brew install --cask dotnet-sdk`) or symlink into `/usr/local/bin` |
| `dotnet build` cannot find `PluginApi.dll`           | Logi Options+ not installed, or installed to a non-default location                          | Reinstall Logi Options+, or edit `Directory.Build.props` to point at your install                         |
| `logiplugintool` not found during build              | Logi Plugin SDK isn't installed                                                              | Optional — `build-all.sh` falls back to a plain `.zip` with the same layout                              |
| Plugin doesn't appear in Logi Options+ after install | Service hasn't reloaded its plugin cache                                                     | `pkill -x LogiPluginService` (mac) / kill `LogiPluginService.exe` in Task Manager (Windows)              |
| Refactor button shows `No active file`               | The editor has no focused document (e.g., focus is on the file tree)                         | Click into the editor tab and retry                                                                       |

### Logs

| Component              | Path (macOS)                                                                                       |
|------------------------|----------------------------------------------------------------------------------------------------|
| AI Refactor plugin     | `~/Library/Application Support/Logi/LogiPluginService/Logs/plugin_logs/AIRefactor.log`            |
| Run Tests plugin       | `~/Library/Application Support/Logi/LogiPluginService/Logs/plugin_logs/TestAction.log`            |
| AI Review plugin       | `~/Library/Application Support/Logi/LogiPluginService/Logs/plugin_logs/ReviewAction.log`          |
| Deploy plugin          | `~/Library/Application Support/Logi/LogiPluginService/Logs/plugin_logs/GitCommitPush.log`         |
| Companion extension    | Cursor's `Output → DevOS Companion` channel                                                        |

On Windows replace `~/Library/Application Support/Logi/LogiPluginService` with
`%LocalAppData%\Logi\LogiPluginService`.

---

## Development workflow

```bash
# One-shot full build + test + package
./scripts/build/build-all.sh

# Iterate on a single plugin (drops a .link file → no repack needed)
dotnet build AIRefactorPlugin/src/AIRefactorPlugin.csproj -c Release
pkill -x LogiPluginService

# Iterate on the companion extension
cd devos-companion
npm run build && npm run package
"/Applications/Cursor.app/Contents/Resources/app/bin/cursor" \
    --install-extension ../dist/devos-companion-1.0.0.vsix
# then Cmd+Shift+P → Developer: Reload Window in Cursor

# Run unit tests only
dotnet test DevOSCore.Tests/DevOSCore.Tests.csproj -c Release --no-build
```

The xUnit suite covers:

- `CompanionLauncher` — auto-wake (HTTP stub) + `IsLive` staleness logic
- `CannedRefactor` — Roslyn passes + whitespace tidying
- `CannedCommitMessage` — Conventional Commits generation from diffs
- `CannedReview` — diff-aware Markdown summary
- `GitOps` — `git` CLI integration against a temp repo
- `LlmExtraction` — code-block extraction from LLM responses
- `PluginDiscovery` — `MetadataLoadContext` introspection of each built plugin
- `ProjectDetector` — runner detection for all 7 ecosystems
- `TestResultParser` — pass/fail/skip extraction for dotnet, pytest, jest, cargo

32 / 32 must be green before any plugin is packaged.

---

## Security notes

- The companion's HTTP server binds **only to `127.0.0.1`** on an ephemeral port.
- Every request is bearer-authenticated with a 256-bit token regenerated on each
  extension activation.
- `~/.devos/companion.json` and `~/.devos/llm.json` are both written with mode
  `0600` inside a `0700` directory.
- LLM credentials never transit the companion — plugins call the configured
  endpoint directly over HTTPS.
- `.env` is git-ignored (see `.gitignore`); use `.env.example` as the template.

---

## License

MIT — see [`LICENSE`](LICENSE).
