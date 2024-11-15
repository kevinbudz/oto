using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace oto
{
    public class Manager
    {
        public string ExePath { get; }
        public int RestartInterval { get; set; }
        public bool AutoRestart { get; set; }
        public bool StartMinimized { get; set; }
        public bool IsRunning
        {
            get
            {
                try
                {

                    var processName = Path.GetFileNameWithoutExtension(ExePath);
                    var processes = Process.GetProcessesByName(processName);

                    if (_process != null && !_process.HasExited)
                    {
                        try
                        {
                            var _ = _process.Id;
                            return true;
                        }
                        catch
                        {
                            _process = null;
                        }
                    }

                    if (processes.Length > 0)
                    {

                        foreach (var proc in processes)
                        {
                            try
                            {
                                if (proc.MainModule?.FileName?.Equals(ExePath, StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    _process = proc;
                                    return true;
                                }
                            }
                            catch
                            {

                                continue;
                            }
                        }
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }

        private Process? _process;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private DateTime _startTime;
        private DateTime? _lastRestartAttempt;
        private const int MIN_RESTART_DELAY = 30;

        public Manager(string exePath, int restartInterval, bool autoRestart, bool startMinimized)
        {
            ExePath = exePath;
            RestartInterval = restartInterval;
            AutoRestart = autoRestart;
            StartMinimized = startMinimized;
            _startTime = DateTime.Now;

            TryAttachToExistingProcess();
        }

        private void TryAttachToExistingProcess()
        {
            try
            {
                var processName = Path.GetFileNameWithoutExtension(ExePath);
                var processes = Process.GetProcessesByName(processName);

                foreach (var proc in processes)
                {
                    try
                    {
                        if (proc.MainModule?.FileName?.Equals(ExePath, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            _process = proc;
                            _startTime = DateTime.Now - TimeSpan.FromMilliseconds(proc.TotalProcessorTime.TotalMilliseconds);
                            break;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                    finally
                    {
                        if (proc != _process)
                        {
                            proc.Dispose();
                        }
                    }
                }
            }
            catch
            {

            }
        }

        public async Task StartMonitoring()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, _cancellationTokenSource.Token);

                    if (!IsRunning && AutoRestart)
                    {
                        if (_lastRestartAttempt == null ||
                            (DateTime.Now - _lastRestartAttempt.Value).TotalSeconds >= MIN_RESTART_DELAY)
                        {
                            MenuHelper.ShowWarning($"Process {Path.GetFileName(ExePath)} is not running. Attempting restart...");
                            StartProcess(false);
                        }
                    }
                    else if (IsRunning && RestartInterval > 0)
                    {
                        var runningTime = DateTime.Now - _startTime;
                        if (runningTime.TotalSeconds >= RestartInterval)
                        {
                            MenuHelper.ShowWarning($"Performing scheduled restart of {Path.GetFileName(ExePath)}");
                            RestartProcess();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    MenuHelper.ShowError($"Error monitoring {Path.GetFileName(ExePath)}: {ex.Message}");
                    await Task.Delay(5000, _cancellationTokenSource.Token);
                }
            }
        }

        public void StartProcess(bool isInitialStart)
        {
            try
            {
                // Ensure any existing process is killed before starting a new one
                KillExistingProcess();

                _process = new Process
                {
                    StartInfo = new ProcessStartInfo(ExePath)
                    {
                        UseShellExecute = true,
                        WindowStyle = StartMinimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal
                    },
                    EnableRaisingEvents = true
                };

                _process.Start();
                _startTime = DateTime.Now;
                _lastRestartAttempt = DateTime.Now;

                if (!isInitialStart)
                {
                    MenuHelper.ShowSuccess($"Successfully restarted {Path.GetFileName(ExePath)}");
                }
            }
            catch (Exception ex)
            {
                MenuHelper.ShowError($"Failed to start {Path.GetFileName(ExePath)}: {ex.Message}");
            }
        }

        private void KillExistingProcess()
        {
            try
            {
                var processName = Path.GetFileNameWithoutExtension(ExePath);
                foreach (var proc in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        if (proc.MainModule?.FileName?.Equals(ExePath, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            proc.Kill();
                            proc.WaitForExit();
                        }
                    }
                    catch (Exception ex)
                    {
                        MenuHelper.ShowWarning($"Could not kill {proc.ProcessName} (PID {proc.Id}): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MenuHelper.ShowError($"Error killing existing processes for {ExePath}: {ex.Message}");
            }
        }

        public void StopProcess()
        {
            _cancellationTokenSource.Cancel();
            if (_process != null && !_process.HasExited)
            {
                try
                {
                    _process.CloseMainWindow();
                    if (!_process.WaitForExit(5000))
                    {
                        MenuHelper.ShowWarning($"Forcing {Path.GetFileName(ExePath)} to close...");
                        _process.Kill();
                    }
                }
                catch (Exception ex)
                {
                    MenuHelper.ShowError($"Error stopping {Path.GetFileName(ExePath)}: {ex.Message}");
                }
            }
            _process?.Dispose();
        }

        public void RestartProcess()
        {
            if (IsRunning)
            {
                StopProcess();
                Thread.Sleep(2000);
            }
            StartProcess(false);
        }

        public (bool IsRunning, TimeSpan Uptime, double MemoryUsageMB) GetStatusInfo()
        {
            if (!IsRunning)
                return (false, TimeSpan.Zero, 0);

            try
            {
                if (_process != null)
                {
                    _process.Refresh();
                    var uptime = DateTime.Now - _startTime;
                    var memoryMB = _process.WorkingSet64 / 1024.0 / 1024.0;

                    return (true, uptime, memoryMB);
                }
                return (false, TimeSpan.Zero, 0);
            }
            catch
            {
                return (false, TimeSpan.Zero, 0);
            }
        }

        ~Manager()
        {
            StopProcess();
        }
    }
}