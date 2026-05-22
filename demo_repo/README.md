# demo_repo

Tiny self-contained C# project used for screen recordings and demos when no LLM
API key is configured. Open this folder in VS Code / Cursor / Antigravity and:

- Press **AI Refactor** on `UserAuth.cs`. The Roslyn canned refactor collapses
  the nested `if`s into a single boolean expression.
- Press **Run Tests** to invoke `dotnet test` (this project has no tests, so the
  device will report "no results"; add a test project if you want a green run).
- Press **AI Review** after `git diff` shows any change.
- Press **Deploy** to stage + commit + push (only works once you have committed
  this folder as a git repo of its own and configured a remote).

This folder is intentionally NOT included in the parent `DevOS.sln` so it does
not interfere with `dotnet test`.
