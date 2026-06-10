using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.ImportExport;
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
using System.Windows;

namespace Fussballturnierverwaltung.Wpf;

public partial class App
{
    private IHost _host = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();
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

        // Application Services – Stammdaten
        builder.Services.AddScoped<MannschaftService>();
        builder.Services.AddScoped<LandService>();
        builder.Services.AddScoped<VerbandService>();

        // Application Services – Turniere
        builder.Services.AddScoped<TurnierVorlageService>();
        builder.Services.AddScoped<TurnierInstanzService>();
        builder.Services.AddScoped<RegelwerkService>();

        // Application Services – Auslosung & Spielplan
        builder.Services.AddScoped<AuslosungService>();
        builder.Services.AddScoped<SpielErgebnisService>();
        builder.Services.AddScoped<TabellenService>();
        builder.Services.AddScoped<TurnierBaumService>();
        builder.Services.AddScoped<SimulationsService>();
        builder.Services.AddScoped<ImportExportService>();
        builder.Services.AddScoped<ISpielplanGenerator, RoundRobinGenerator>();
        builder.Services.AddScoped<ISpielplanGenerator, KoRundeGenerator>();
        builder.Services.AddScoped<ISpielplanGenerator, KoRundeMitHinRueckspielGenerator>();
        builder.Services.AddScoped<ISpielplanGenerator, TopfLigaGenerator>();
        builder.Services.AddScoped<SpielplanGeneratorFactory>();
        builder.Services.AddScoped<SpielplanService>();

        // ViewModels (Transient = neuer Scope bei jeder Navigation)
        builder.Services.AddTransient<PlatzhalterViewModel>();
        builder.Services.AddTransient<MannschaftenViewModel>();
        builder.Services.AddTransient<LandUndVerbandViewModel>();
        builder.Services.AddTransient<RegelwerkViewModel>();
        builder.Services.AddTransient<TurnierVorlageViewModel>();
        builder.Services.AddTransient<TurnierInstanzViewModel>();
        builder.Services.AddTransient<AuslosungViewModel>();
        builder.Services.AddTransient<SpielplanViewModel>();
        builder.Services.AddTransient<TurnierBaumViewModel>();

        // Navigation & Hauptfenster
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();

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
