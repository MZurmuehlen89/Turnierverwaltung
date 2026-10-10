using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Einstellungen;
using Fussballturnierverwaltung.Application.ImportExport;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Statistiken;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Infrastructure.Erweiterungen;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Wartung;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;

namespace Fussballturnierverwaltung.WinUi;

/// <summary>Fundament (#280): DI-/Seed-Aufbau analog zu Fussballturnierverwaltung.Wpf\App.xaml.cs. Fach-
/// Services/Validatoren/ViewModels werden erst mit der jeweiligen View (#281–#290) ergänzt.</summary>
public partial class App : global::Microsoft.UI.Xaml.Application
{
    private Window? _window;
    private IHost _host = null!;

    public App()
    {
        InitializeComponent();

        // Diagnose für sporadische, nicht-reproduzierbare Abstürze beim App-Beenden (z.B. gemeldeter
        // JIT-Debugger-Dialog mit COMException 0x8000FFFF ohne Stacktrace) — schreibt UNABHÄNGIG von
        // Serilog (das bei einem harten nativen Absturz während des Shutdowns evtl. nicht mehr flusht)
        // synchron+direkt in eine eigene Datei, damit der nächste Vorfall einen echten Stacktrace
        // hinterlässt statt nur den generischen OS-Dialog.
        UnhandledException += (_, e) => CrashDiagnoseSchreiben("Application.UnhandledException", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            CrashDiagnoseSchreiben("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
            CrashDiagnoseSchreiben("TaskScheduler.UnobservedTaskException", e.Exception);
    }

    private static void CrashDiagnoseSchreiben(string quelle, Exception? ex)
    {
        try
        {
            var pfad = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Turnierverwaltung", "logs", "crash-diagnose.log");
            Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
            File.AppendAllText(pfad,
                $"{DateTimeOffset.Now:O} [{quelle}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* Diagnose darf den eigentlichen Absturz nicht verdecken oder selbst werfen */ }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var logPfad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Turnierverwaltung", "logs", "turnierverwaltung-.log");

        // Vor dem Host: Serilog braucht den Schalter schon beim Aufbau (#413).
        var ablaufprotokoll = new Ablaufprotokoll(new EinstellungenService());

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(ablaufprotokoll);
        builder.Services.AddSerilog((_, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .MinimumLevel.Override(Ablaufprotokoll.KATEGORIE, ablaufprotokoll.Schwelle)
            .WriteTo.File(
                path: logPfad,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));
        builder.Services.AddInfrastruktur(builder.Configuration);

        // Validatoren & Services (Mannschaften, #281; Länder & Verbände, #282; Regelwerke, #283;
        // Turniere/Vorlagen vereinheitlicht, #297)
        builder.Services.AddScoped<MannschaftValidator>();
        builder.Services.AddScoped<LandValidator>();
        builder.Services.AddScoped<VerbandValidator>();
        builder.Services.AddScoped<RegelwerkValidator>();
        builder.Services.AddScoped<TurnierValidator>();
        builder.Services.AddScoped<UebergangsregelValidator>();
        builder.Services.AddScoped<VerbandsQuoteValidator>();
        builder.Services.AddScoped<LandesQuoteValidator>();
        builder.Services.AddScoped<AuslosungsValidator>();
        builder.Services.AddScoped<RanglisteValidator>();
        builder.Services.AddScoped<RanglistenQuelleValidator>();
        builder.Services.AddScoped<SpielErgebnisValidator>();
        builder.Services.AddScoped<ImportValidator>();
        builder.Services.AddScoped<MannschaftService>();
        builder.Services.AddScoped<LandService>();
        builder.Services.AddScoped<VerbandService>();
        builder.Services.AddScoped<RegelwerkService>();
        builder.Services.AddScoped<TurnierInstanzService>();
        builder.Services.AddScoped<TurnierKlonService>();
        builder.Services.AddScoped<UebergangsregelService>();
        builder.Services.AddScoped<VerbandsQuoteService>();
        builder.Services.AddScoped<LandesQuoteService>();
        builder.Services.AddScoped<AuslosungService>();
        builder.Services.AddScoped<TabellenService>();
        builder.Services.AddScoped<StatistikService>();
        builder.Services.AddScoped<ISpielplanGenerator, RoundRobinGenerator>();
        builder.Services.AddScoped<ISpielplanGenerator, KoRundeGenerator>();
        builder.Services.AddScoped<ISpielplanGenerator, TopfLigaGenerator>();
        builder.Services.AddScoped<ISpielplanGenerator, SchweizerSystemGenerator>();
        builder.Services.AddScoped<SpielplanGeneratorFactory>();
        builder.Services.AddScoped<SpielplanService>();
        builder.Services.AddScoped<IPlatzhalterKaskadeService>(sp => sp.GetRequiredService<SpielplanService>());
        builder.Services.AddScoped<SpielErgebnisService>();
        builder.Services.AddScoped<TurnierBaumService>();
        builder.Services.AddScoped<SimulationsService>();
        builder.Services.AddScoped<RanglisteService>();
        builder.Services.AddScoped<RanglistenQuelleService>();
        builder.Services.AddScoped<RanglistenImportService>();
        builder.Services.AddScoped<ImportExportService>();

        // RundenViewModel ist Datenhalter für Runden und Übergangsregeln (Issue #297/#348) — Scoped, damit
        // TurnierlisteViewModel innerhalb derselben Navigations-Scope dieselbe Instanz erhält.
        builder.Services.AddTransient<RundenFormularViewModel>();
        builder.Services.AddScoped<RundenViewModel>();

        // ViewModels (Transient = neuer Scope bei jeder Navigation)
        builder.Services.AddTransient<PlatzhalterViewModel>();
        builder.Services.AddTransient<MannschaftenViewModel>();
        builder.Services.AddTransient<LaenderUndVerbaendeViewModel>();
        builder.Services.AddTransient<RegelwerkeViewModel>();
        builder.Services.AddTransient<AuslosungsfilterViewModel>();
        builder.Services.AddTransient<TitelGastgeberViewModel>();
        builder.Services.AddTransient<TurnierQuotenViewModel>();
        builder.Services.AddTransient<TurnierkopfViewModel>();
        builder.Services.AddTransient<VorbelegungViewModel>();
        builder.Services.AddTransient<TurnierlisteViewModel>();
        builder.Services.AddTransient<AuslosungViewModel>();
        builder.Services.AddTransient<SpielplanViewModel>();
        builder.Services.AddTransient<RundenTabellenViewModel>();
        builder.Services.AddTransient<TurnierBaumViewModel>();
        builder.Services.AddTransient<SpielplanUndBaumViewModel>();
        builder.Services.AddTransient<SimulationsViewModel>();
        builder.Services.AddTransient<StatistikViewModel>();
        builder.Services.AddTransient<TurnierdurchfuehrungViewModel>();
        builder.Services.AddTransient<RanglistenViewModel>();
        builder.Services.AddTransient<EinzelspielSimulationsViewModel>();
        builder.Services.AddTransient<ImportExportViewModel>();
        builder.Services.AddTransient<DatenbankwartungViewModel>();

        // Navigation & Hauptfenster
        // AusgewaehlteTurnierInstanzService (#299) haelt die Instanzauswahl geteilt zwischen "Turniere" und
        // "Turnierdurchführung" — zwingend Singleton, da NavigationService bei jeder Navigation den Scope
        // disposed und ein Scoped-Service die Auswahl beim Menüwechsel sofort verlieren würde.
        builder.Services.AddSingleton<AusgewaehlteTurnierInstanzService>();
        builder.Services.AddSingleton<EinstellungenService>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<MainViewModel>();

        _host = builder.Build();
        _host.Services.GetRequiredService<DbZugriffsDiagnose>().KollisionenUeberwachen();

        if (!await DatenbankVorbereitenAsync()) return;

        _window = new MainWindow(
            _host.Services.GetRequiredService<MainViewModel>(),
            _host.Services.GetRequiredService<ThemeService>(),
            _host.Services.GetRequiredService<IDialogService>());
        _window.Activate();
    }

    /// <summary>Issue #370: legt die Datenbank bei Bedarf an, spielt fehlende Migrationen ein und ergänzt fehlende
    /// Stammdaten. Schlägt das fehl (z. B. SQL Server nicht erreichbar), gibt es noch kein Hauptfenster und damit
    /// keinen ContentDialog — daher native Meldung und sauberes Beenden statt Absturz.</summary>
    private async Task<bool> DatenbankVorbereitenAsync()
    {
        using var scope = _host.Services.CreateScope();
        var wartung = scope.ServiceProvider.GetRequiredService<DatenbankWartungService>();
        try
        {
            await wartung.PruefenUndErgaenzenAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Datenbank konnte beim Start nicht vorbereitet werden.");
            var (server, datenbank) = wartung.Verbindungsziel();
            var logOrdner = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Turnierverwaltung", "logs");
            NativeMeldung.FehlerAnzeigen(
                "Turnierverwaltung – Datenbankfehler",
                $"Die Datenbank konnte nicht geöffnet oder eingerichtet werden.\n\n"
                + $"Server: {server}\nDatenbank: {datenbank}\n\n"
                + $"Ursache: {ex.GetBaseException().Message}\n\n"
                + $"Bitte prüfen, ob der SQL Server läuft und erreichbar ist. Details stehen im Log:\n{logOrdner}\n\n"
                + "Die Anwendung wird beendet.");
            Exit();
            return false;
        }
    }
}
