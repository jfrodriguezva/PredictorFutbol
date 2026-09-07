using System.IO;
using System.Windows;

namespace SportsPredictor.Desktop;

/// <summary>
/// Orchestrates the WPF → Next.js → API C# → Python startup chain: launches the three
/// backend processes in dependency order, shows live status while waiting on their
/// health checks, then hosts the Next.js dashboard in an embedded WebView2 — the user
/// never has to open a separate browser tab or run anything by hand.
/// </summary>
public partial class MainWindow : Window
{
    private ServiceLauncher? _launcher;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var baseDirectory = AppContext.BaseDirectory;
        var settings = LauncherSettings.Load(baseDirectory);
        _launcher = new ServiceLauncher(settings, baseDirectory);
        _launcher.StatusChanged += Launcher_StatusChanged;

        var allReady = await _launcher.StartAllAsync(CancellationToken.None);

        if (!allReady)
        {
            ErrorText.Text =
                "One or more services did not start. Check that Api/, Frontend/, and Ml/ exist next to this " +
                "executable (see desktop/README.md), then restart the app.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        await Browser.EnsureCoreWebView2Async();
        Browser.CoreWebView2.Navigate($"http://localhost:{settings.FrontendPort}");

        StatusPanel.Visibility = Visibility.Collapsed;
        Browser.Visibility = Visibility.Visible;
    }

    private void Launcher_StatusChanged(object? sender, ServiceStatusChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var target = e.Service switch
            {
                ServiceKind.MlService => MlStatusText,
                ServiceKind.Api => ApiStatusText,
                ServiceKind.Frontend => FrontendStatusText,
                _ => null,
            };

            if (target is not null)
            {
                var label = e.Service switch
                {
                    ServiceKind.MlService => "ML service",
                    ServiceKind.Api => "API",
                    ServiceKind.Frontend => "Dashboard",
                    _ => e.Service.ToString(),
                };
                target.Text = $"{label}: {e.Message}";
            }
        });
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _launcher?.Dispose();
    }
}
