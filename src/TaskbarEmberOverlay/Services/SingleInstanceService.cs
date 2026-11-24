using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TaskbarEmberOverlay.Services
{
    public class SingleInstanceService : IDisposable
    {
        private const string MutexName = "Local\\TaskbarEmberOverlay_Mutex_SingleInstance";
        private const string PipeName = "TaskbarEmberOverlay_Pipe";
        private const string ExitEventName = "Local\\TaskbarEmberOverlay_ExitEvent";

        private Mutex? _mutex;
        private EventWaitHandle? _exitEvent;
        private bool _hasHandle;
        private CancellationTokenSource? _cts;
        private Task? _pipeServerTask;
        private RegisteredWaitHandle? _waitHandle;

        public event Action? ExitRequested;
        public event Action? PauseRequested;
        public event Action? ResumeRequested;

        public bool TryAcquire(out bool isFirstInstance)
        {
            try
            {
                _mutex = new Mutex(false, MutexName);
                try
                {
                    _hasHandle = _mutex.WaitOne(TimeSpan.FromMilliseconds(100), false);
                }
                catch (AbandonedMutexException)
                {
                    // Previous process was terminated; take over ownership cleanly
                    _hasHandle = true;
                }

                isFirstInstance = _hasHandle;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SingleInstanceService] Mutex error: {ex.Message}");
                isFirstInstance = false;
                return false;
            }
        }

        public void StartIpcServer()
        {
            _cts = new CancellationTokenSource();
            _pipeServerTask = Task.Run(() => RunServerLoopAsync(_cts.Token));
        }

        private async Task RunServerLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                    await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                    using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                    using var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                    string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string cmd = line.Trim().ToUpperInvariant();
                        switch (cmd)
                        {
                            case "EXIT":
                                await writer.WriteLineAsync("OK_EXITING").ConfigureAwait(false);
                                ExitRequested?.Invoke();
                                break;
                            case "PAUSE":
                                await writer.WriteLineAsync("OK_PAUSED").ConfigureAwait(false);
                                PauseRequested?.Invoke();
                                break;
                            case "RESUME":
                                await writer.WriteLineAsync("OK_RESUMED").ConfigureAwait(false);
                                ResumeRequested?.Invoke();
                                break;
                            case "PING":
                                await writer.WriteLineAsync("PONG").ConfigureAwait(false);
                                break;
                            default:
                                await writer.WriteLineAsync("UNKNOWN_COMMAND").ConfigureAwait(false);
                                break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SingleInstanceService] IPC server loop: {ex.Message}");
                    await Task.Delay(200, ct).ConfigureAwait(false);
                }
                finally
                {
                    if (server != null)
                    {
                        try { server.Dispose(); } catch { }
                    }
                }
            }
        }

        public static async Task<string?> SendCommandAsync(string command, int timeoutMs = 2000)
        {
            // If EXIT command, also signal the named event as fallback
            if (string.Equals(command, "EXIT", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (EventWaitHandle.TryOpenExisting(ExitEventName, out var ev))
                    {
                        ev.Set();
                        ev.Dispose();
                    }
                }
                catch { }
            }

            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                using var cts = new CancellationTokenSource(timeoutMs);

                await client.ConnectAsync(cts.Token).ConfigureAwait(false);

                using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);

                await writer.WriteLineAsync(command).ConfigureAwait(false);
                string? response = await reader.ReadLineAsync(cts.Token).ConfigureAwait(false);
                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SingleInstanceService] SendCommandAsync error: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            _waitHandle?.Unregister(null);
            _exitEvent?.Dispose();

            _cts?.Cancel();
            try { _pipeServerTask?.Wait(500); } catch { }
            _cts?.Dispose();

            if (_hasHandle && _mutex != null)
            {
                try { _mutex.ReleaseMutex(); } catch { }
                _hasHandle = false;
            }
            _mutex?.Dispose();
        }
    }
}
