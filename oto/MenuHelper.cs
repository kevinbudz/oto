using Spectre.Console;

namespace oto
{
    public static class MenuHelper
    {
        public static void DisplayHeader(string message)
        {
            AnsiConsole.Clear();

            var header = new Rule("[yellow dim]© kevinbudz, 2024[/]");
            header.RightJustified();
            header.Style = Style.Parse("yellow");
            header.Border = BoxBorder.Heavy;
            AnsiConsole.Write(header);

            // Add the message panel below the header !!
            if (!string.IsNullOrEmpty(message))
            {
                var panel = new Panel($"[bold yellow]{message}[/]")
                {
                    Border = BoxBorder.Rounded,
                    BorderStyle = Style.Parse("yellow"),
                    Padding = new Padding(2, 1),
                    Expand = true
                };
                AnsiConsole.Write(panel);
            }
        }

        public static void ShowSuccess(string message)
        {
            AnsiConsole.MarkupLine($"[green]✓ {message}[/]");
        }

        public static void ShowError(string message)
        {
            AnsiConsole.MarkupLine($"[red]✗ {message}[/]");
        }

        public static void ShowWarning(string message)
        {
            AnsiConsole.MarkupLine($"[yellow]! {message}[/]");
        }

        public static Table CreateProcessTable()
        {
            return new Table()
                .Border(TableBorder.Rounded)
                .Expand()
                .AddColumns(
                    "[bold]ID[/]",
                    "[bold]Process[/]",
                    "[bold]Status[/]",
                    "[bold]Interval[/]",
                    "[bold]Uptime[/]",
                    "[bold]Memory[/]"
                );
        }

        public static string? PromptForExecutablePath()
        {
            ShowWarning("Enter the path to the executable:");
            return Console.ReadLine();
        }

        public static int PromptForInterval()
        {
            if (!AnsiConsole.Confirm("Enable scheduled periodic restarts? (Not recommended for most applications)", false))
            {
                return 0;
            }

            return AnsiConsole.Prompt(
                new TextPrompt<int>("[yellow]How often should the application be restarted (in seconds)?[/]\n" +
                                   "Note: Minimum recommended value is 3600 (1 hour)")
                    .ValidationErrorMessage("[red]Please enter a valid number greater than 0.[/]")
                    .Validate(interval =>
                    {
                        if (interval < 300) // 5 minutes
                        {
                            AnsiConsole.MarkupLine("[yellow]Warning: Short restart intervals may cause stability issues.[/]");
                        }
                        return interval > 0;
                    })
            );
        }

        public static bool PromptForAutoRestart()
        {
            return AnsiConsole.Confirm(
                "Enable crash recovery? (Automatically restart if the application stops unexpectedly)",
                true
            );
        }

        public static bool PromptToContinue(string message = "Continue?")
        {
            return AnsiConsole.Confirm(message, true);
        }

        public static bool PromptForMinimizedStart()
        {
            return AnsiConsole.Confirm("Start process minimized?", false);
        }

        public static void WaitForKeyPress(string message = "Press any key to continue...")
        {
            AnsiConsole.WriteLine(message);
            Console.ReadKey(true);
        }
    }
}