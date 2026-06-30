# GameToolOrchestrator 手动验证指南

这份文档用于验证真实 Windows 桌面工具流程。验证时请保持当前用户桌面可见，不要锁屏，不要断开远程桌面，不要手动移动或遮挡目标窗口。

## 验证前准备

1. 安装 .NET 8 SDK。
2. 确认目标工具和 GameToolOrchestrator 使用同一权限级别运行。默认都不要以管理员身份运行。
3. 准备一个真实目标工具，例如 BetterGI 或任意带可见窗口和按钮的本地 `.exe`。
4. 确认目标工具启动后会显示窗口，并且任务完成后会自动退出，或者配置足够长的 `waitProcessExit` 超时。
5. 先运行：

```powershell
dotnet build GameToolOrchestrator.sln
dotnet test GameToolOrchestrator.sln
```

## 验证 sample-config.json

1. 打开 `sample-config.json`。
2. 修改目标 tool 的 `executablePath` 为真实 `.exe` 路径。
3. 修改 `workingDirectory` 为该工具所在目录。
4. 确认 `waitWindow.titleContains` 能匹配目标窗口标题的一部分。
5. 确认 `clickButton` 优先使用 `automationId`，其次 `nameEquals`，最后 `nameContains`。
6. 暂时不要配置 `clickByCoordinate`，当前阶段未实现。

## 验证 WPF 启动

运行：

```powershell
dotnet run --project src\GameToolOrchestrator.Wpf
```

检查：

1. 窗口能保持打开。
2. 如果找不到默认配置，界面仍可用。
3. `logs/wpf-startup.log` 已写入启动信息。
4. `logs/wpf-crash.log` 没有新增异常。

## 验证窗口诊断

1. 手动启动目标工具。
2. 在 WPF 点击“Refresh Window List”。
3. 在日志区确认能看到目标窗口标题、进程名和进程 ID。
4. 如果看不到窗口，确认目标工具窗口没有最小化，并且当前桌面未锁屏。

也可以使用 ConsoleRunner：

```powershell
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- inspect-windows
```

## 验证控件树导出

1. 在 WPF 的 `Tree Title` 输入目标窗口标题片段。
2. 点击“Export Control Tree”。
3. 在日志区查看控件树摘要。
4. 记录目标按钮的 `AutomationId`、`Name`、`ControlType`、`IsEnabled`、`IsOffscreen`。

ConsoleRunner 等价命令：

```powershell
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- inspect-window --title-contains BetterGI --max-depth 4
```

## 验证 selector

推荐顺序：

1. 优先使用 `automationId`。
2. 其次使用 `nameEquals`。
3. 最后使用 `nameContains`。

ConsoleRunner 示例：

```powershell
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- test-selector --window-title-contains BetterGI --automation-id StartButton
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- test-selector --window-title-contains BetterGI --name-contains 开始
```

检查输出中的匹配控件数量和控件摘要。

## 验证 test-selector 默认不点击

运行不带 `--click` 的命令：

```powershell
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- test-selector --window-title-contains BetterGI --name-contains 开始
```

确认：

1. 输出匹配控件数量。
2. 不触发真实点击。
3. 目标工具状态没有变化。

## 验证 test-selector --click 才真实点击

确认 selector 匹配正确后，再运行：

```powershell
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- test-selector --window-title-contains BetterGI --name-contains 开始 --click
```

确认：

1. 日志提示即将点击的控件。
2. 目标工具按钮被真实点击。
3. 如果点击失败，查看控件树并调整 selector。

## 验证 WPF 中“测试 Selector”和“测试并点击”

1. 手动启动目标工具。
2. 在 WPF 输入 `Window Title`。
3. 输入 `AutomationId` 或 `Name Contains`。
4. 点击“Test Selector”，确认只输出匹配结果，不点击。
5. 确认无误后点击“Test And Click”，确认按钮被真实点击。

## 验证完整 TaskPlan 执行

1. 在 WPF 选择配置文件。
2. 选择目标 TaskPlan。
3. 点击“Start”。
4. 观察 Step 状态变化。
5. 任务结束后查看日志中的执行结果摘要。
6. 确认目标进程按预期退出后，下一个 step 才开始。

ConsoleRunner 示例：

```powershell
dotnet run --project src\GameToolOrchestrator.ConsoleRunner -- --config sample-config.json --plan bettergi-daily
```

## 验证取消执行

1. 选择一个会运行较久的 TaskPlan。
2. 点击“Start”。
3. 等待进入执行中状态后点击“Cancel”。
4. 确认 UI 日志记录用户取消。
5. 确认不会默认强杀目标进程。

## 验证失败日志

可以故意配置错误 selector 来验证失败日志：

1. 把 `waitWindow.titleContains` 改成不存在的标题。
2. 或把 `clickButton.nameContains` 改成不存在的按钮文本。
3. 执行 TaskPlan。
4. 查看日志中的窗口列表、控件树摘要、失败 action、执行结果摘要和下一步建议。

## 验证保存配置

1. 在 WPF 修改 `executablePath` 或 `workingDirectory`。
2. 修改某个 Step 的 Enabled。
3. 修改某个 Action 的 Enabled。
4. 窗口标题或状态栏应显示“未保存”。
5. 点击“Save Config”。
6. 确认日志显示保存成功和文件路径。
7. 打开 JSON 文件确认字段真实更新。

## 验证重新加载配置

1. 保存配置后点击“Refresh Config”。
2. 确认 UI 展示与 JSON 文件一致。
3. 修改字段但不保存，再点击“Refresh Config”。
4. WPF 应提示是否放弃未保存修改。

## Action Editor Verification

Use this section to verify editing existing action parameters. Add/copy/delete action verification is covered below. This stage still does not support drag/drop sorting and does not implement `clickByCoordinate`.

1. Select a `waitWindow` action.
2. Change `titleContains` to the real target window title fragment.
3. Click `Save Config`.
4. Open the JSON file and confirm `titleContains` changed.
5. Click `Refresh Config` and confirm the editor still shows the saved value.

For `clickButton`:

1. Select a `clickButton` action.
2. Change `nameContains`, or fill `automationId` / `nameEquals` based on the exported control tree.
3. Use `用当前 Selector 测试` and confirm it only lists matching controls.
4. Confirm the target tool is not clicked during the default test.
5. Use `测试并点击` only after the selector is correct, and confirm it performs the real click.
6. Save and reload the config, then confirm the selector values remain.

For `waitProcessExit`:

1. Select a `waitProcessExit` action.
2. Change `timeoutSeconds` or `timeoutMinutes`.
3. Save the config.
4. Reload the same config and confirm the timeout value remains.
5. If both fields are set, remember that top-level `timeoutMinutes` takes precedence over top-level `timeoutSeconds`.

For validation:

1. Select a `waitWindow` action and clear both `titleEquals` and `titleContains`.
2. Click `Save Config`.
3. Confirm save is blocked and the log panel shows a validation error.
4. Select a `clickButton` action and clear `automationId`, `nameEquals`, and `nameContains`.
5. Click `Save Config`.
6. Confirm save is blocked and the log panel explains that at least one selector is required.

## Configuration Object Editor Verification

Use this section to verify WPF add/copy/delete operations. This stage still does not support drag/drop sorting and does not implement `clickByCoordinate`.

For tools:

1. Click `Add Tool`.
2. Confirm a new tool such as `tool-1` is selected.
3. Fill `executablePath` and `workingDirectory` if you have real paths.
4. Click `Save Config`, then `Refresh Config`.
5. Confirm the tool still exists after reload.
6. Select the new tool and click `Copy`.
7. Confirm a copy such as `tool-1-copy` appears.
8. Modify the copy path and confirm the original tool path is unchanged.
9. Delete an unreferenced tool and save/reload.
10. Try deleting a tool referenced by a Step, such as `bettergi`; confirm deletion is blocked and the log lists the referencing `TaskPlan/Step`.

For task plans:

1. Click `Add` in the Task Plans panel.
2. Confirm a new plan such as `plan-1` is selected and has no steps.
3. Click `Save Config`, then reload and confirm it remains.
4. Select the new plan and click `Copy`.
5. Confirm the copy has independent steps after you add or edit steps.
6. Delete the copied plan, save, and reload.

For steps:

1. Select a TaskPlan and a Tool.
2. Click `Add Step`.
3. Confirm the new Step uses the selected `toolId` and `order = max + 1`.
4. Click `Copy` for the selected Step.
5. Confirm the copy is appended with `order = max + 1`.
6. Click `Normalize Order`.
7. Confirm current TaskPlan steps are ordered `1, 2, 3...`.
8. Delete a Step, save, and reload.

For actions:

1. Select a Step.
2. Choose `waitWindow` in the action type dropdown and click `Add Action`.
3. Try saving before filling `titleEquals` or `titleContains`; confirm validation blocks save.
4. Fill `titleContains`, save, and reload.
5. Choose `clickButton` and click `Add Action`.
6. Try saving before filling `automationId`, `nameEquals`, or `nameContains`; confirm validation blocks save.
7. Fill `nameContains`, then use the selector test button to verify it without clicking.
8. Copy the action and confirm the copy is appended to the end of the action list.
9. Modify the copy and confirm the original action is unchanged.
10. Delete the copied action, save, and reload.

## 常见问题排查

- `waitWindow` 失败：点击“Refresh Window List”确认窗口标题，调整 `titleEquals` 或 `titleContains`。
- `clickButton` 失败：点击“Export Control Tree”，优先查找稳定的 `AutomationId`，再尝试 `nameEquals` 或 `nameContains`。
- `waitProcessExit` 超时：确认目标工具是否会自动退出，或调大 `timeoutSeconds` / `timeoutMinutes`。
- UI Automation 无法访问控件：确认目标工具没有以更高权限运行。若目标工具是管理员权限，编排器也需要以管理员身份运行。
- 保存失败：检查配置文件是否只读、是否被编辑器占用、目录是否有写权限。
- WPF 启动异常：查看 `logs/wpf-startup.log` 和 `logs/wpf-crash.log`。
- 需要反馈问题：点击 WPF 日志区的“复制诊断信息”，分享前先检查其中的本机路径是否可以公开。
