using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;

namespace oto
{
    public static class Management
    {
        public static async Task AddNewProcess(List<Manager> processManagers)
        {
            MenuHelper.DisplayHeader("Add New Process");

            var method = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select:")
                    .AddChoices(new[] {
                        "Select a running process,",
                        "Browse for .exe,",
                        "Enter the path manually,",
                        "Back."
                    })
            );

            if (method == "Back.")
                return;

            string? exePath = method switch
            {
                "Select a running process," => await SelectRunningProcess(),
                "Browse for .exe," => MenuHelper.PromptForExecutablePath(),
                "Enter the path manually," => AnsiConsole.Ask<string>("Enter the full path to the executable:"),
                _ => null
            };

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                MenuHelper.ShowError("Invalid executable path.");
                return;
            }

            int interval = MenuHelper.PromptForInterval();
            bool autoRestart = MenuHelper.PromptForAutoRestart();
            bool startMinimized = MenuHelper.PromptForMinimizedStart();

            var manager = new Manager(exePath, interval, autoRestart, startMinimized);
            processManagers.Add(manager);

            _ = Task.Run(async () =>
            {
                try
                {
                    await manager.StartMonitoring();
                }
                catch (Exception ex)
                {
                    MenuHelper.ShowError($"Error monitoring {Path.GetFileName(exePath)}: {ex.Message}");
                }
            });

            MenuHelper.ShowSuccess($"Successfully added {Path.GetFileName(exePath)}");
            await Task.Delay(1000);
        }

        private static Task<string?> SelectRunningProcess()
        {
            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) &&
                                   !p.ProcessName.Equals("oto", StringComparison.OrdinalIgnoreCase))
                    .Select(p =>
                    {
                        try
                        {
                            return new
                            {
                                Process = p,
                                FileName = p.MainModule?.FileName
                            };
                        }
                        catch
                        {
                            return null;
                        }
                    })
                    .Where(p => p != null && p.FileName != null)
                    .ToList();

                if (!processes.Any())
                {
                    MenuHelper.ShowError("No suitable running processes found.");
                    return Task.FromResult<string?>(null);
                }

                var selection = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select:")
                        .PageSize(10)
                        .AddChoices(
                            processes.Select(p =>
                                $"{p!.Process.ProcessName} ({p.Process.Id}) - {p.FileName}")
                        )
                );

                return Task.FromResult<string?>(selection.Split(" - ").Last());
            }
            catch (Exception ex)
            {
                MenuHelper.ShowError($"Error accessing process list: {ex.Message}");
                return Task.FromResult<string?>(null);
            }
        }

        public static async Task DisplayProcesses(List<Manager> processManagers)
        {
            if (!processManagers.Any())
            {
                MenuHelper.ShowError("No active processes to display.");
                MenuHelper.WaitForKeyPress();
                return;
            }

            var table = MenuHelper.CreateProcessTable();
            var prompt = new Panel("[yellow]Press 'Q' to return to the main menu...[/]")
            {
                Border = BoxBorder.None,
                Padding = new Padding(0, 0)
            };

            var headerPanel = new Panel("[yellow]Active Processes[/]")
            {
                Border = BoxBorder.Heavy,
                BorderStyle = Style.Parse("yellow"),
                Padding = new Padding(2, 0),
                Expand = true
            };

            var copyright = new Rule("[yellow dim]© kevinbudz, 2024[/]")
            {
                Border = BoxBorder.Heavy,
                Style = Style.Parse("yellow")
            };
            copyright.RightJustified();

            var layout = new Layout()
                .SplitRows(
                    new Layout("HeaderCopyright").Size(1),
                    new Layout("HeaderTitle").Size(3),
                    new Layout("Table"),
                    new Layout("Prompt").Size(3)
                );

            await AnsiConsole.Live(layout)
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .StartAsync(async ctx =>
                {
                    while (true)
                    {

                        table.Rows.Clear();

                        foreach (var (manager, index) in processManagers.Select((m, i) => (m, i)))
                        {
                            var status = manager.GetStatusInfo();

                            string intervalDisplay = manager.RestartInterval > 0
                                ? $"[yellow]{manager.RestartInterval}s[/]"
                                : "[grey]-[/]";

                            table.AddRow(
                                $"[blue]{index + 1}[/]",
                                $"[yellow]{Path.GetFileName(manager.ExePath)}[/]",
                                status.IsRunning ? "[green]Active[/]" : "[red]Stopped[/]",
                                intervalDisplay,
                                $"[blue]{status.Uptime:hh\\:mm\\:ss}[/]",
                                $"[yellow]{status.MemoryUsageMB:N0} MB[/]"
                            );
                        }

                        layout["HeaderTitle"].Update(headerPanel);
                        layout["HeaderCopyright"].Update(copyright);
                        layout["Table"].Update(table);
                        layout["Prompt"].Update(prompt);

                        ctx.Refresh();

                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            if (key.Key == ConsoleKey.Q)
                                return;
                            if (key.Key == ConsoleKey.R)
                                continue;
                        }

                        await Task.Delay(1000);
                    }
                });
        }

        public static async Task RemoveProcess(List<Manager> processManagers)
        {
            if (!processManagers.Any())
            {
                MenuHelper.ShowError("No processes to remove.");
                return;
            }

            MenuHelper.DisplayHeader("Remove Process");
            var choices = processManagers.Select((p, i) =>
                $"{i + 1}: {Path.GetFileName(p.ExePath)}").ToList();
            choices.Add("Cancel");

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select:")
                    .AddChoices(choices)
            );

            if (selection == "Cancel") return;

            var index = int.Parse(selection.Split(':')[0]) - 1;
            var manager = processManagers[index];

            if (AnsiConsole.Confirm($"Are you sure you want to remove {Path.GetFileName(manager.ExePath)}?"))
            {
                manager.StopProcess();
                processManagers.RemoveAt(index);
                MenuHelper.ShowSuccess("Process removed successfully.");
                await Task.Delay(1500);
            }
        }

        public static async Task ModifyProcessSettings(List<Manager> processManagers)
        {
            if (!processManagers.Any())
            {
                MenuHelper.ShowError("No processes to modify.");
                return;
            }

            MenuHelper.DisplayHeader("Modify Process Settings");
            var choices = processManagers.Select((p, i) =>
                $"{i + 1}: {Path.GetFileName(p.ExePath)}").ToList();

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select:")
                    .AddChoices(choices)
            );

            var index = int.Parse(selection.Split(':')[0]) - 1;
            var manager = processManagers[index];

            var setting = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .AddChoices(new[]
                    {
                        "How often the app restarts,",
                        "Auto-restarting,",
                        "Minimizing app."
                    })
            );

            switch (setting)
            {
                case "How often the app restarts,":
                    manager.RestartInterval = MenuHelper.PromptForInterval();
                    break;
                case "Auto-restarting,":
                    manager.AutoRestart = MenuHelper.PromptForAutoRestart();
                    break;
                case "Minimizing app.":
                    manager.StartMinimized = MenuHelper.PromptForMinimizedStart();
                    break;
            }

            MenuHelper.ShowSuccess("Settings updated successfully.");
            await Task.Delay(1500);
        }
    }
}