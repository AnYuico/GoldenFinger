# GameToolOrchestrator

GameToolOrchestrator is a local Windows task orchestrator. The current MVP can read JSON configuration, launch local `.exe` tools in order, run basic UI Automation actions through FlaUI, wait for process exit, and write diagnostic logs.

## Structure

```text
src/
  GameToolOrchestrator.Core/            # Domain models, interfaces, execution engine, non-UI actions
  GameToolOrchestrator.Infrastructure/  # JSON, Serilog, Process, FlaUI automation
  GameToolOrchestrator.ConsoleRunner/   # CLI runner
  GameToolOrchestrator.Wpf/             # WPF desktop UI, MVVM
tests/
  GameToolOrchestrator.Tests/           # xUnit tests with mocks
```

## Run ConsoleRunner

Edit `sample-config.json` first, especially `executablePath` and `workingDirectory`.

```powershell
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- sample-config.json bettergi-daily
```

Explicit arguments also work:

```powershell
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- --config sample-config.json --plan bettergi-daily
```

Exit codes: `0` succeeded, `1` failed, `2` cancelled, `3` timed out.

## Run WPF

```powershell
dotnet run --project src\GameToolOrchestrator.Wpf
```

The WPF app tries to load `config.json` or `sample-config.json` from the app/current directory. You can also browse to a JSON config file from the top bar.

Current WPF features:

- Load and save JSON config.
- Show task plans, steps, selected tool details, and actions.
- Edit a tool's `executablePath` and `workingDirectory`.
- Enable or disable a step.
- Enable or disable an action.
- Show an unsaved state in the window title/status after editable fields change.
- Run the selected task plan through the existing Core execution engine.
- Cancel a running task through `CancellationToken`.
- Show step status, current tool/action, and live logs.
- Edit existing action parameters for `waitSeconds`, `waitProcessExit`, `waitWindow`, and `clickButton`.
- Add, copy, and delete tools.
- Add, copy, and delete task plans.
- Add, copy, delete, and normalize steps.
- Add, copy, and delete supported actions.
- Copy a diagnostic bundle from the log panel for issue reports.
- Open the `logs/` folder.
- Open the currently loaded config folder.
- Append an execution summary after each task run.
- Refresh visible desktop windows.
- Export a target window control tree by `titleContains`.
- Test a selector without clicking.
- Explicitly test a selector and click.

Recommended WPF flow:

1. Choose `config.json`.
2. Use **Refresh Window List** to confirm the target window title.
3. Use **Export Control Tree** to inspect the button selector.
4. Save config changes.
5. Run the selected task plan.
6. Review logs and failure diagnostics.

WPF save behavior:

- The current editor saves `Tool.executablePath`, `Tool.workingDirectory`, `Step.Enabled`, `Action.Enabled`, supported action parameters, and the supported add/copy/delete structure changes listed below.
- After any editable field or structure changes, the window shows an unsaved marker.
- Save is disabled while a task is running. Browse/Load/Refresh config and add/copy/delete operations are also disabled while a task is running.
- If you reload or switch config while there are unsaved changes, WPF asks whether to discard those changes first.
- Save failures, such as read-only files, missing permissions, or invalid paths, are shown in the log panel and should not close the app. Check `logs/wpf-crash.log` only if the app reports an unhandled exception.

WPF action editor:

- `waitSeconds`: edits `enabled`, `seconds`, and `timeoutSeconds`.
- `waitProcessExit`: edits `enabled`, `timeoutSeconds`, and `timeoutMinutes`. For top-level fields, `timeoutMinutes` takes precedence over `timeoutSeconds`; parameter dictionary timeout values still take precedence if manually present in JSON.
- `waitWindow`: edits `enabled`, `titleEquals`, `titleContains`, and `timeoutSeconds`. At least one of `titleEquals` / `titleContains` is required. If both are filled, `titleEquals` wins.
- `clickButton`: edits `enabled`, `windowTitleContains`, `automationId`, `nameEquals`, `nameContains`, `controlType`, and `timeoutSeconds`. At least one selector is required. Selector priority is `automationId > nameEquals > nameContains`; `controlType` defaults to `Button`.
- Numeric fields must be positive when filled. Validation errors are shown in the UI and block save without crashing.
- In the `clickButton` editor, **Use Current Selector Test** only matches controls and never clicks. **Test And Click** performs the real click.

WPF config structure editor:

- **Add Tool** creates a `generic-ui` tool with a unique id such as `tool-1`, empty paths, `launchTimeoutSeconds = 30`, and `taskTimeoutMinutes = 180`.
- **Copy Tool** deep-copies the selected tool and gives it a unique id such as `bettergi-copy`.
- **Delete Tool** is blocked when any `TaskStep` still references that `toolId`; the log lists the referencing `TaskPlan/Step`.
- **Add TaskPlan** creates a unique id such as `plan-1`, `name = New Task Plan`, `stopOnFailure = true`, and no steps.
- **Copy TaskPlan** deep-copies steps so the original and copy can be edited independently.
- **Add Step** uses the currently selected tool and assigns `order = max(order) + 1`.
- **Copy Step** appends the copy with `order = max(order) + 1`.
- **Normalize Order** sorts the current task plan's steps by existing `order` and rewrites them as `1, 2, 3...`.
- **Add Action** supports `waitSeconds`, `waitProcessExit`, `waitWindow`, and `clickButton`.
- **Copy Action** appends the copy to the end of the current step's action list.
- Delete operations ask for confirmation before removing unreferenced objects.
- New `waitWindow` and `clickButton` actions may require selector fields before save; validation errors block save and keep the editor open.

WPF feedback tools:

- **Copy diagnostics** copies the current log panel plus config path, selected task plan, selected step, selected action, app version, current working directory, and `AppContext.BaseDirectory`. Review local paths before sharing the copied text.
- **Open logs folder** creates and opens the `logs/` directory.
- **Open config folder** opens the folder containing the currently loaded config file.
- Task completion writes an execution summary with step counts, elapsed time, cancelled state, and the first failing step when present.

For a complete real-tool checklist, see [docs/manual-verification.md](docs/manual-verification.md).

## WPF Startup Troubleshooting

The WPF app writes startup diagnostics to:

```text
logs/wpf-startup.log
```

If an unhandled exception occurs, details are written to:

```text
logs/wpf-crash.log
```

The startup log records the current working directory, `AppContext.BaseDirectory`, every attempted `config.json` / `sample-config.json` path, whether a default config was found, whether `MainWindow` and `MainViewModel` were created, and whether the default config loaded successfully.

If no default config is found, the WPF window should still open. The task/step/action lists will be empty, Start will be disabled, and the log panel will show:

```text
未找到默认配置文件，请手动选择 config.json。
```

If the JSON file is malformed, the window should also stay open and show the load error in the log panel. Use Browse to select a fixed config file.

## Desktop Diagnostics

These commands do not require WPF. They are intended for selector calibration on the current visible Windows desktop.

List visible desktop windows:

```powershell
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- inspect-windows
```

Inspect one window and export a control tree summary:

```powershell
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- inspect-window --title-contains BetterGI
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- inspect-window --title-equals "BetterGI" --max-depth 5
```

Test a selector without clicking:

```powershell
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- test-selector --window-title-contains BetterGI --name-contains "\u5f00\u59cb"
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- test-selector --window-title-contains BetterGI --automation-id StartButton
```

Only `--click` performs a real click:

```powershell
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- test-selector --window-title-contains BetterGI --name-contains "\u5f00\u59cb" --click
```

Run one configured action by zero-based action index:

```powershell
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- run-action --config sample-config.json --tool bettergi --action-index 1
```

`run-action` does not launch the full task plan. It is mainly useful for debugging `waitWindow` and `clickButton` while the target tool is already open.

Recommended calibration flow:

1. `inspect-windows`: find the exact or partial window title.
2. `inspect-window`: export the target window control tree.
3. `test-selector`: verify `automationId`, `nameEquals`, or `nameContains`.
4. Update `sample-config.json`.
5. Run the full task plan.

## Supported Actions

- `waitSeconds`: wait for a configured duration.
- `waitProcessExit`: wait for the launched process to exit.
- `waitWindow`: wait for a visible desktop window using `titleEquals` or `titleContains`.
- `clickButton`: find and click a Button inside a target window.

For `clickButton`, prefer selectors in this order:

1. `automationId`
2. `nameEquals`
3. `nameContains`

`nameContains` is useful for localized UI text. In JSON, Chinese text can also be written with Unicode escapes, for example `"\u5f00\u59cb"` for the text "start" in Chinese.

When choosing selectors from a control tree:

- Prefer `AutomationId` when it is present and stable.
- Use `nameEquals` when the visible label is exact and stable.
- Use `nameContains` for localized or dynamic labels, but keep the fragment specific enough.
- Keep `controlType = "Button"` for button clicks unless the tree shows a different real control type.

## Diagnostics

When `waitWindow` or `clickButton` fails, logs include:

- Current visible window titles.
- Target window control tree summary.
- Element `Name`, `AutomationId`, `ControlType`, `IsEnabled`, and `IsOffscreen`.
- Control tree depth is capped at 4 levels by default.

Check the `logs/` directory after failures. The control tree is the main tool for finding a better `automationId`, `nameEquals`, or `nameContains` selector.

Failure triage:

- `waitWindow` failed: use **Refresh Window List** or `inspect-windows` to confirm the actual window title, then adjust `titleEquals` / `titleContains`.
- `clickButton` failed: use **Export Control Tree** / `inspect-window`, then verify with **Test Selector** / `test-selector` before clicking.
- `waitProcessExit` timed out: check whether the target tool exits automatically, or increase `timeoutSeconds` / `timeoutMinutes`.
- Permission errors: run the orchestrator and target tool at the same privilege level. If the target is elevated, run the orchestrator as administrator.

## Current Limits

- The WPF UI is intentionally basic and uses native controls only.
- WPF does not implement task execution or FlaUI logic directly; it calls Core and Infrastructure services.
- WPF does not currently edit `TaskPlan.Enabled`; it only edits `Step.Enabled` and `Action.Enabled`.
- WPF does not yet edit ids, names, arguments, launch timeout fields, or every advanced config field.
- WPF does not support drag/drop ordering; use **Normalize Order** after manually editing order in JSON if needed.
- WPF does not support adding `clickByCoordinate`.
- `clickByCoordinate` is intentionally deferred; prefer UI Automation selectors first.
- Only `completionStrategy = processExit` is implemented.
- Default `killOnTimeout = false`, so timed-out processes are left running unless explicitly enabled later.
- UI Automation depends on the current visible desktop session. It is not designed for locked screens, Windows services, Session 0, or disconnected Remote Desktop sessions.
- Avoid moving, minimizing, or covering the target window while automation is running.
- The project does not read process memory, inject DLLs, bypass anti-cheat, perform hidden control, or operate on game process internals.

## Permissions

The MVP assumes the orchestrator and target tools run under the current user. If a tool requires administrator privileges, set `requiresAdministrator = true`; the launcher will fail early with a clear message when the orchestrator is not elevated. To control elevated software, run GameToolOrchestrator as administrator.
