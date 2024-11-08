using System;
using System.Collections.Generic;
using System.IO;
using Spectre.Console;

namespace oto
{
    public static class Utility
    {
        public static void CleanupProcesses(List<Manager> processManagers)
        {
            AnsiConsole.Status()
                .Start("Cleaning up processes...", ctx =>
                {
                    foreach (var manager in processManagers.ToList())
                    {
                        ctx.Status($"Stopping {Path.GetFileName(manager.ExePath)}...");
                        manager.StopProcess();
                        processManagers.Remove(manager);
                    }
                });
        }
    }
}