using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.ImportExport;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Infrastructure.Erweiterungen;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Fussballturnierverwaltung.Wpf;

public partial class App
{
    private IHost _host = null!;
    private ILogger<App>? _logger;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogCritical(e.Exception, "Unbehandelter Fehler auf dem UI-Thread");
        e.Handled = true;
        ZeigeFehlerdialog(e.Exception, kannFortfahren: true);
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        _logger?.LogCritical(ex, "Schwerwiegender Fehler im AppDomain (IsTerminating={IsTerminating})", e.IsTerminating);
        ZeigeFehlerdialog(ex, kannFortfahren: !e.IsTerminating);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unbeobachtete Task-Exception");
        e.SetObserved();
    }

    private void ZeigeFehlerdialog(Exception? ex, bool kannFortfahren)
    {
        var meldung = ex?.Message ?? "Unbekannter Fehler";
        var text = kannFortfahren
            ? $"Es ist ein unerwarteter Fehler aufgetreten:\n\n{meldung}\n\nMöchten Sie die Anwendung neu starten?"
            : $"Ein schwerwiegender Fehler ist aufgetreten:\n\n{meldung}\n\nDie Anwendung muss beendet werden. Möchten Sie danach neu starten?";

        var ergebnis = MessageBox.Show(text, "Unerwarteter Fehler", MessageBoxButton.YesNo, MessageBoxImage.Error);

        if (ergebnis == MessageBoxResult.Yes)
            NeustartAnfordern();
        else if (!kannFortfahren)
            Shutdown(1);
    }

    private static void NeustartAnfordern()
    {
        var pfad = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(pfad))
            Process.Start(pfad);
        Current.Shutdown(0);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logPfad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Turnierverwaltung", "logs", "turnierverwaltung-.log");

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSerilog((_, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .WriteTo.File(
                path: logPfad,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));
        builder.Services.AddInfrastruktur(builder.Configuration);

        // Validierung
        builder.Services.AddScoped<MannschaftValidator>();
        builder.Services.AddScoped<LandValidator>();
        builder.Services.AddScoped<VerbandValidator>();
        builder.Services.AddScoped<TurnierValidator>();
        builder.Services.AddScoped<RegelwerkValidator>();
        builder.Services.AddScoped<RankingRegelSetValidator>();
        builder.Services.AddScoped<SpielErgebnisValidator>();
        builder.Services.AddScoped<AuslosungsValidator>();
        builder.Services.AddScoped<ImportValidator>();
        builder.Services.AddScoped<RanglisteValidator>();

        // Application Services – Stammdaten
        builder.Services.AddScoped<MannschaftService>();
        builder.Services.AddScoped<LandService>();
        builder.Services.AddScoped<VerbandService>();

        // Application Services – Turniere
        builder.Services.AddScoped<TurnierVorlageService>();
        builder.Services.AddScoped<TurnierInstanzService>();
        builder.Services.AddScoped<RegelwerkService>();
        builder.Services.AddScoped<RanglisteService>();

        // Application Services – Auslosung & Spielplan
        builder.Services.AddScoped<AuslosungService>();
        builder.Services.AddScoped<SpielErgebnisService>();
        builder.Services.AddScoped<TabellenService>();
        builder.Services.AddScoped<TurnierBaumService>();
        builder.Services.AddScoped<SimulationsService>();
        builder.Services.AddScoped<ImportExportService>();
        builder.Services.AddScoped<ISpielplanGenerator, RoundRobinGenerator>();
        builder.Services.AddScoped<ISpielplanGenerator, KoRundeGenerator>();
        builder.Services.AddScoped<ISpielplanGenerator, TopfLigaGenerator>();
        builder.Services.AddScoped<SpielplanGeneratorFactory>();
        builder.Services.AddScoped<SpielplanService>();

        // ViewModels (Transient = neuer Scope bei jeder Navigation)
        builder.Services.AddTransient<PlatzhalterViewModel>();
        builder.Services.AddTransient<MannschaftenViewModel>();
        builder.Services.AddTransient<LandUndVerbandViewModel>();
        builder.Services.AddTransient<RegelwerkViewModel>();
        builder.Services.AddTransient<RanglisteViewModel>();
        builder.Services.AddTransient<TurnierVorlageViewModel>();
        builder.Services.AddTransient<TurnierInstanzViewModel>();
        builder.Services.AddTransient<AuslosungViewModel>();
        builder.Services.AddTransient<SpielplanViewModel>();
        builder.Services.AddTransient<TurnierBaumViewModel>();
        builder.Services.AddTransient<SimulationsViewModel>();
        builder.Services.AddTransient<TurnierBereichViewModel>();
        builder.Services.AddTransient<ImportExportViewModel>();
        builder.Services.AddTransient<RundenEditorViewModel>();

        // Navigation & Hauptfenster
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        _logger = _host.Services.GetRequiredService<ILogger<App>>();

        using (var scope = _host.Services.CreateScope())
        {
            var seed = scope.ServiceProvider.GetRequiredService<SeedDatenService>();
            await seed.InitialisierenAsync();
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
