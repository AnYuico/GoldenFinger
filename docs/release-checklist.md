# GameToolOrchestrator Release Checklist

Version: `0.3.2`

Release date: `2026-06-30`

## Build And Test

1. Run build:

   ```powershell
   dotnet build GameToolOrchestrator.sln --no-restore -m:1
   ```

2. Run tests:

   ```powershell
   dotnet test GameToolOrchestrator.sln --no-build --no-restore -m:1
   ```

3. Confirm all tests pass.

## Publish Packages

1. Publish WPF:

   ```powershell
   .\scripts\publish-wpf.ps1
   ```

2. Publish ConsoleRunner:

   ```powershell
   .\scripts\publish-console.ps1
   ```

3. Confirm these executables exist:

   ```text
   artifacts\publish\GameToolOrchestrator.Wpf-win-x64\GameToolOrchestrator.Wpf.exe
   artifacts\publish\GameToolOrchestrator.ConsoleRunner-win-x64\GameToolOrchestrator.ConsoleRunner.exe
   ```

## WPF Published App

1. Start the published WPF exe.
2. Confirm the app window stays open.
3. Confirm `sample-config.json` beside the exe can be loaded.
4. Confirm `logs/wpf-startup.log` is generated.
5. Click Copy Diagnostics and confirm the copied text contains `Version: 0.3.2`.
6. Temporarily remove or rename `config.json` / `sample-config.json` and confirm WPF still opens safely.

## ConsoleRunner Published App

1. Run:

   ```powershell
   .\artifacts\publish\GameToolOrchestrator.ConsoleRunner-win-x64\GameToolOrchestrator.ConsoleRunner.exe inspect-windows
   ```

2. Confirm it prints visible desktop windows without launching a task plan.

## Publish Directory Contents

For each publish directory, confirm these files/folders exist:

- Executable.
- `README.md`.
- `sample-config.json`.
- `docs\manual-verification.md`.
- `docs\release-checklist.md`.
- `logs\`.

## Archive

1. Compress the publish output directory, for example:

   ```powershell
   Compress-Archive -Path artifacts\publish\GameToolOrchestrator.Wpf-win-x64 -DestinationPath artifacts\publish\GameToolOrchestrator.Wpf-0.3.2-win-x64.zip -Force
   ```

2. Record:

   ```text
   Version: 0.3.2
   Release date: 2026-06-30
   Commit:
   Notes:
   ```
