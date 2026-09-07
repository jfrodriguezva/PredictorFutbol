using System.IO;
using System.Text.Json;

namespace SportsPredictor.Desktop;

/// <summary>
/// Where to find each service and how to start it. Defaults assume the installed
/// layout (see installer/README.md): Api/, Frontend/, Ml/ folders sitting next to
/// this executable. For running straight out of the dev repo, a
/// "launcher.settings.local.json" file (gitignored) next to the exe can override
/// any of these — see desktop/README.md.
/// </summary>
public sealed class LauncherSettings
{
    public string ApiWorkingDirectory { get; set; } = "Api";
    public string ApiExecutable { get; set; } = "SportsPredictor.Api.exe";
    public string ApiArguments { get; set; } = "";
    public int ApiPort { get; set; } = 20050;

    /// <summary>
    /// Relative to the base directory (next to this launcher exe) — NOT relative to
    /// ApiWorkingDirectory, because that differs between the dev repo layout
    /// (backend/SportsPredictor.Api) and the installed layout (Api/), and the
    /// connection string baked into appsettings.json only knows one of those.
    /// The launcher resolves this to an absolute path and passes it to the API via
    /// the ConnectionStrings__SportsPredictorDb environment variable, overriding
    /// whatever relative path is in appsettings.json.
    /// </summary>
    public string DatabasePath { get; set; } = "database/sportspredictor.db";

    public string FrontendWorkingDirectory { get; set; } = "Frontend";
    /// <summary>"node server.js" for a Next.js standalone build, or "npm" + "run dev"/"run start" for dev mode.</summary>
    public string FrontendCommand { get; set; } = "node";
    public string FrontendArguments { get; set; } = "server.js";
    public int FrontendPort { get; set; } = 20051;

    public string MlWorkingDirectory { get; set; } = "Ml";
    public string MlPythonExecutable { get; set; } = @".venv\Scripts\python.exe";
    public string MlArguments { get; set; } = "-m uvicorn app.main:app --port 8001 --host 127.0.0.1";
    public int MlPort { get; set; } = 8001;

    public static LauncherSettings Load(string baseDirectory)
    {
        var settings = new LauncherSettings();

        settings = TryOverlay(settings, Path.Combine(baseDirectory, "launcher.settings.json"));
        // Local, gitignored override — e.g. to point at the dev repo's ml/.venv, frontend/, backend/ folders directly.
        settings = TryOverlay(settings, Path.Combine(baseDirectory, "launcher.settings.local.json"));

        return settings;
    }

    /// <summary>A malformed settings file must never crash the app — fall back to whatever was valid before it.</summary>
    private static LauncherSettings TryOverlay(LauncherSettings current, string path)
    {
        if (!File.Exists(path))
        {
            return current;
        }

        try
        {
            return JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(path)) ?? current;
        }
        catch (JsonException)
        {
            return current;
        }
    }
}
