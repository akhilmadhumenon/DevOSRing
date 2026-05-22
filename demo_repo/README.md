# demo_repo

Small, self-contained C# project used to drive AI Refactor demos. Open this folder
(or any individual file in it) in Cursor / VS Code / Antigravity and press the
**AI Refactor** ring action while the file is the active editor tab.

Each file is hand-crafted to:
- Compile cleanly (no warnings) — so the LLM gets syntactically valid input.
- Contain **no unused usings and no stray whitespace** — so the Roslyn canned
  fallback (used when `LLM_API_KEY` is missing or the LLM call fails) produces
  essentially a no-op diff. Any *substantive* change you see in the diff is
  therefore unambiguously the LLM's work.
- Exercise a different family of refactoring smells so the diffs are visually
  distinct, making it obvious the output is not hardcoded.

## Files

### `UserAuth.cs`
Single-line method. Useful as a smoke test — the LLM will usually leave it
alone or only tighten the doc. Good baseline for "this is what no-op looks like".

### `OrderProcessor.cs` — control-flow smells
- Deeply nested `if/else` with no early returns
- Magic numbers (`1000`, `5`, `25`, `20`, `18` …) sprinkled throughout
- Repeated branches that differ only by the constant returned
- `System.Console.WriteLine` interleaved with business logic (UI/logic mix)
- `int` "status" returned where an `enum`/`record` would be clearer

**Expected LLM refactor**
- Guard clauses up top → eliminate the nested `if` pyramid
- `private const decimal GoldBulkThreshold = 1000m;` (and similar)
- Extract the per-tier branches into small helper methods or a switch expression
- A `DiscountResult` `record`, or at least split logging from calculation
- Optionally introduce a `CustomerTier` enum

### `InventoryReport.cs` — collection / string smells
- Manual `for` loops with index access instead of LINQ
- `string output = "";` + `output = output + "..."` instead of `StringBuilder`
  or interpolation
- Nested `ContainsKey` / `Add` pattern that `Dictionary.TryGetValue` /
  `GroupBy` handles in one line
- `out int totalLowStock` instead of returning a single value object
- `if/else if/else` ladder for the `healthy|watch|reorder` verdict

**Expected LLM refactor**
- A single LINQ pipeline: `items.Where(i => i is not null).GroupBy(i => i.Category).Select(...)`
- `StringBuilder` or interpolated multi-line strings
- A `CategorySummary` record + `(string Report, int TotalLowStock)` tuple
- Switch expression for the verdict

## How to verify the LLM was actually called

After pressing **AI Refactor**, tail the plugin log:

```bash
tail -n 20 "$HOME/Library/Application Support/Logi/LogiPluginService/Logs/plugin_logs/AIRefactor.log"
```

You should see a line shaped like:

```
[AIRefactor] Calling LLM: endpoint=https://api.groq.com/openai/v1, model=llama-3.3-70b-versatile, language=csharp, inputChars=2412, selection=False
[AIRefactor] LLM ok in 1820ms, 2104 chars
[AIRefactor] Diff opened (source=llm, outputChars=2104); user will accept/discard via Cursor Command Palette
```

If you see `source=canned` (or `LLM not configured`/`LLM failed`), the diff in
your editor is the Roslyn fallback — which, on these files, will look almost
identical to the input.

You can also press the **Test LLM** action (also in the AI Refactor plugin) for
an immediate end-to-end LLM round-trip check; the device button will show
`<latency>ms` on success or an HTTP status on failure.

## Why this folder is NOT in `DevOS.sln`

Keeping it out of the solution means `dotnet test` from the repo root only
exercises `DevOSCore.Tests`. The demo files can intentionally be ugly without
polluting CI signals.
