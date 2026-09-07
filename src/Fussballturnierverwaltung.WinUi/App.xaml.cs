using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Einstellungen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Infrastructure.Erweiterungen;
using Fussballturnierverwaltung.Infrastructure.Seed;
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

    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
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

        // Validatoren & Services (Mannschaften, #281; Länder & Verbände, #282; Regelwerke, #283;
        // Turniere/Vorlagen vereinheitlicht, #297)
        builder.Services.AddScoped<MannschaftValidator>();
        builder.Services.AddScoped<LandValidator>();
        builder.Services.AddScoped<VerbandValidator>();
        builder.Services.AddScoped<RegelwerkValidator>();
        builder.Services.AddScoped<TurnierValidator>();
        builder.Services.AddScoped<UebergangsregelValidator>();
        builder.Services.AddScoped<VerbandsQuoteValidator>();
        builder.Services.AddScoped<AuslosungsValidator>();
        builder.Services.AddScoped<RanglisteValidator>();
        builder.Services.AddScoped<RanglistenQuelleValidator>();
        builder.Services.AddScoped<MannschaftService>();
        builder.Services.AddScoped<LandService>();
        builder.Services.AddScoped<VerbandService>();
        builder.Services.AddScoped<RegelwerkService>();
        builder.Services.AddScoped<TurnierInstanzService>();
        builder.Services.AddScoped<TurnierKlonService>();
        builder.Services.AddScoped<UebergangsregelService>();
        builder.Services.AddScoped<VerbandsQuoteService>();
        builder.Services.AddScoped<AuslosungService>();
        builder.Services.AddScoped<TabellenService>();
        builder.Services.AddScoped<ISpielplanGenerator, RoundRobinGenerator>();
        builder.Services.AddScoped<ISpielplanGenerator, KoRundeGenerator>();
        builder.Services.AddScoped<ISpielplanGenerator, TopfLigaGenerator>();
        builder.Services.AddScoped<ISpielplanGenerator, SchweizerSystemGenerator>();
        builder.Services.AddScoped<SpielplanGeneratorFactory>();
        builder.Services.AddScoped<SpielplanService>();
        builder.Services.AddScoped<RanglisteService>();
        builder.Services.AddScoped<RanglistenQuelleService>();
        builder.Services.AddScoped<RanglistenImportService>();

        // RundenViewModel ist der geteilte Datenhalter für die Tabs "Runden" und "Rundenübergänge" (Issue
        // #297) — Scoped statt Transient, damit beide innerhalb derselben Navigations-Scope dieselbe Instanz
        // injiziert bekommen (TurnierlisteViewModel UND RundenUebergaengeViewModel).
        builder.Services.AddScoped<RundenViewModel>();

        // ViewModels (Transient = neuer Scope bei jeder Navigation)
        builder.Services.AddTransient<PlatzhalterViewModel>();
        builder.Services.AddTransient<MannschaftenViewModel>();
        builder.Services.AddTransient<LaenderUndVerbaendeViewModel>();
        builder.Services.AddTransient<RegelwerkeViewModel>();
        builder.Services.AddTransient<TurnierkopfViewModel>();
        builder.Services.AddTransient<RundenUebergaengeViewModel>();
        builder.Services.AddTransient<VorbelegungViewModel>();
        builder.Services.AddTransient<TurnierlisteViewModel>();
        builder.Services.AddTransient<AuslosungViewModel>();
        builder.Services.AddTransient<TurnierdurchfuehrungViewModel>();
        builder.Services.AddTransient<RanglistenViewModel>();

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

        using (var scope = _host.Services.CreateScope())
        {
            var seed = scope.ServiceProvider.GetRequiredService<SeedDatenService>();
            await seed.InitialisierenAsync();
        }

        _window = new MainWindow(
            _host.Services.GetRequiredService<MainViewModel>(),
            _host.Services.GetRequiredService<ThemeService>(),
            _host.Services.GetRequiredService<IDialogService>());
        _window.Activate();
    }
}
