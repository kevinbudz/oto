using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Spectre.Console;

namespace oto
{
    class Program
    {
        static async Task Main(string[] args)
        {
            List<Manager> processManagers = new();

            AppDomain.CurrentDomain.ProcessExit += (s, e) => Utility.CleanupProcesses(processManagers);
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Utility.CleanupProcesses(processManagers);
                Environment.Exit(0);
            };

            while (true)
            {
                AnsiConsole.Clear();
                MenuHelper.DisplayHeader("What would you like to do?");

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select:")
                        .AddChoices(new[] {
                            "Add an application,",
                            "View active processes,",
                            "Remove a process,",
                            "Edit process configurations,",
                            "Close."
                        })
                );

                try
                {
                    switch (choice)
                    {
                        case "Add an application,":
                            await Management.AddNewProcess(processManagers);
                            break;

                        case "View active processes,":
                            await Management.DisplayProcesses(processManagers);
                            break;

                        case "Remove a process,":
                            await Management.RemoveProcess(processManagers);
                            break;

                        case "Edit process configurations,":
                            await Management.ModifyProcessSettings(processManagers);
                            break;

                        case "Close.":
                            AnsiConsole.MarkupLine("[red]Exiting the application...[/]");
                            Utility.CleanupProcesses(processManagers);
                            return;
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]An error occurred: {ex.Message}[/]");
                    AnsiConsole.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                }
            }
        }
    }
}