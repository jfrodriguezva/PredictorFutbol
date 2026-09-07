using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace SportsPredictor.Desktop;

public enum ServiceKind
{
    MlService,
    Api,
    Frontend,
}

public sealed record ServiceStatusChangedEventArgs(ServiceKind Service, string Message);

/// <summary>
/// Starts and stops the three backend processes in the order the chain requires
/// (WPF → Next.js → API C# → Python): Python first (nothing depends on it being
/// slow to import sklearn/xgboost), then the API (needs the ML service reachable
/// for training/prediction calls), then the Next.js frontend last (it only needs
/// the API's URL, not the other way around). Each step waits for a health check
/// before starting the next, so the dashboard never opens against a half-started backend.
/// </summary>
public sealed class ServiceLauncher : IDisposable
{
    private readonly LauncherSettings _settings;
    private readonly string _baseDirectory;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly List<Process> _processes = [];

    public event EventHandler<ServiceStatusChangedEventArgs>? StatusChanged;

    public ServiceLauncher(LauncherSettings settings, string baseDirectory)
    {
        _settings = settings;
        _baseDirectory = baseDirectory;
    }

    public async Task<bool> StartAllAsync(CancellationToken cancellationToken)
    {
        if (!await StartAndWaitAsync(
                ServiceKind.MlService,
                _settings.MlWorkingDirectory,
                _settings.MlPythonExecutable,
                _settings.MlArguments,
                $"http://127.0.0.1:{_settings.MlPort}/health",
                cancellationToken))
        {
            return false;
        }

        var databaseFullPath = Path.GetFullPath(Path.Combine(_baseDirectory, _settings.DatabasePath));
        Directory.CreateDirectory(Path.GetDirectoryName(databaseFullPath)!);

        if (!await StartAndWaitAsync(
                ServiceKind.Api,
                _settings.ApiWorkingDirectory,
                _settings.ApiExecutable,
                _settings.ApiArguments,
                $"http://127.0.0.1:{_settings.ApiPort}/health",
                cancellationToken,
                extraEnv: new Dictionary<string, string>
                {
                    ["ASPNETCORE_URLS"] = $"http://localhost:{_settings.ApiPort}",
                    ["ConnectionStrings__SportsPredictorDb"] = $"Data Source={databaseFullPath}",
                }))
        {
            return false;
        }

        if (!await StartAndWaitAsync(
                ServiceKind.Frontend,
                _settings.FrontendWorkingDirectory,
                _settings.FrontendCommand,
                _settings.FrontendArguments,
                $"http://127.0.0.1:{_settings.FrontendPort}",
                cancellationToken,
                extraEnv: new Dictionary<string, string> { ["PORT"] = _settings.FrontendPort.ToString() }))
        {
            return false;
        }

        return true;
    }

    private async Task<bool> StartAndWaitAsync(
        ServiceKind kind,
        string workingDirectoryRelative,
        string executable,
        string arguments,
        string healthUrl,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? extraEnv = null)
    {
        RaiseStatus(kind, "Starting…");

        var workingDirectory = Path.Combine(_baseDirectory, workingDirectoryRelative);
        var executablePath = Path.IsPathRooted(executable) ? executable : executable;

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : _baseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (extraEnv is not null)
        {
            foreach (var (key, value) in extraEnv)
            {
                startInfo.Environment[key] = value;
            }
        }

        try
        {
            var process = Process.Start(startInfo);
            if (process is null)
            {
                RaiseStatus(kind, "Failed to start.");
                return false;
            }

            _processes.Add(process);
        }
        catch (Exception ex)
        {
            RaiseStatus(kind, $"Failed to start: {ex.Message}");
            return false;
        }

        RaiseStatus(kind, "Waiting for it to become ready…");
        var ready = await WaitForHealthyAsync(healthUrl, cancellationToken);
        RaiseStatus(kind, ready ? "Ready." : "Did not respond in time.");
        return ready;
    }

    private async Task<bool> WaitForHealthyAsync(string url, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
                // Not up yet — keep polling until the deadline.
            }

            await Task.Delay(1000, cancellationToken);
        }

        return false;
    }

    private void RaiseStatus(ServiceKind kind, string message) =>
        StatusChanged?.Invoke(this, new ServiceStatusChangedEventArgs(kind, message));

    public void Dispose()
    {
        foreach (var process in _processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best effort — the process may have already exited on its own.
            }
            finally
            {
                process.Dispose();
            }
        }

        _httpClient.Dispose();
    }
}
