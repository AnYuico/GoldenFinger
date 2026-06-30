using System.Text;
using GameToolOrchestrator.Core.Models;
using GameToolOrchestrator.Infrastructure.Configuration;

namespace GameToolOrchestrator.Tests;

public sealed class JsonConfigRepositoryTests
{
    [Fact]
    public async Task LoadAsync_DeserializesConfigWithChineseNames()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gto-config-{Guid.NewGuid():N}.json");
        var json = """
        {
          "version": "1.0",
          "execution": {
            "stopOnFailure": true,
            "killOnTimeout": false,
            "waitForExit": true
          },
          "tools": [
            {
              "id": "bettergi",
              "name": "BetterGI",
              "executablePath": "D:\\Tools\\BetterGI\\BetterGI.exe",
              "workingDirectory": "D:\\Tools\\BetterGI",
              "window": {
                "title": "BetterGI",
                "titleMatchMode": "contains"
              }
            }
          ],
          "taskPlans": [
            {
              "id": "daily",
              "name": "每日任务",
              "steps": [
                {
                  "id": "start-bettergi",
                  "name": "启动 BetterGI",
                  "toolId": "bettergi",
                  "order": 1,
                  "completionStrategy": "processExit",
                  "actions": [
                    {
                      "id": "wait-short",
                      "type": "waitSeconds",
                      "name": "等待",
                      "parameters": {
                        "seconds": "1"
                      }
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        await File.WriteAllTextAsync(path, json, Encoding.UTF8);

        var repository = new JsonConfigRepository();
        var config = await repository.LoadAsync(path);

        Assert.Equal("BetterGI", config.Tools[0].Name);
        Assert.Equal("每日任务", config.TaskPlans[0].Name);
        Assert.Equal(CompletionStrategy.ProcessExit, config.TaskPlans[0].Steps[0].CompletionStrategy);
        Assert.Equal("1", config.TaskPlans[0].Steps[0].Actions[0].Parameters["seconds"]);
    }

    [Fact]
    public async Task LoadAsync_DeserializesUiAutomationActionFlatProperties()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gto-config-{Guid.NewGuid():N}.json");
        var json = """
        {
          "version": "1.5",
          "tools": [
            {
              "id": "bettergi",
              "name": "BetterGI",
              "type": "generic-ui",
              "executablePath": "D:\\Tools\\BetterGI\\BetterGI.exe"
            }
          ],
          "taskPlans": [
            {
              "id": "daily",
              "name": "UI 自动化",
              "steps": [
                {
                  "id": "run",
                  "toolId": "bettergi",
                  "order": 1,
                  "actions": [
                    {
                      "id": "wait-window",
                      "type": "waitWindow",
                      "titleContains": "BetterGI",
                      "timeoutSeconds": 30
                    },
                    {
                      "id": "click-start",
                      "type": "clickButton",
                      "windowTitleContains": "BetterGI",
                      "nameContains": "开始",
                      "controlType": "Button",
                      "timeoutSeconds": 10
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        await File.WriteAllTextAsync(path, json, Encoding.UTF8);

        var repository = new JsonConfigRepository();
        var config = await repository.LoadAsync(path);
        var actions = config.TaskPlans[0].Steps[0].Actions;

        Assert.Equal("waitWindow", actions[0].Type);
        Assert.Equal("BetterGI", actions[0].TitleContains);
        Assert.Equal(30, actions[0].TimeoutSeconds);
        Assert.Equal("clickButton", actions[1].Type);
        Assert.Equal("BetterGI", actions[1].WindowTitleContains);
        Assert.Equal("开始", actions[1].NameContains);
        Assert.Equal("Button", actions[1].ControlType);
    }
}
