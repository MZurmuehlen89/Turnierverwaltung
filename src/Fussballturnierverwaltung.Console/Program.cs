using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Einstellungen;
using Serilog;
using Serilog.Events;
using Fussballturnierverwaltung.Application.ImportExport;
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
using Fussballturnierverwaltung.Infrastructure.Seed;
using Fussballturnierverwaltung.Konsole;
using Fussballturnierverwaltung.Konsole.Menues;
using Fussballturnierverwaltung.Konsole.Menues.Auslosungen;
using Fussballturnierverwaltung.Konsole.Menues.Bewegungsdaten;
using Fussballturnierverwaltung.Konsole.Menues.Einstellungen;
using Fussballturnierverwaltung.Konsole.Menues.ImportExport;
using Fussballturnierverwaltung.Konsole.Menues.Simulation;
using Fussballturnierverwaltung.Konsole.Menues.Stammdaten;
using Fussballturnierverwaltung.Konsole.Menues.Statistiken;
using Fussballturnierverwaltung.Konsole.Menues.Turnierbaum;
using Fussballturnierverwaltung.Konsole.Menues.Turniere;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

var logPfad = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "Turnierverwaltung", "logs", "turnierverwaltung-.log");

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog((_, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.File(
        path: logPfad,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));
builder.Services.AddInfrastruktur(builder.Configuration);

// Validierungsservices
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

// Application Services – Auslosungen
builder.Services.AddScoped<AuslosungService>();

// Application Services – Ergebnisse & Tabelle
builder.Services.AddScoped<SpielErgebnisService>();
builder.Services.AddScoped<TabellenService>();

// Application Services – Turnierbaum
builder.Services.AddScoped<TurnierBaumService>();

// Application Services – Simulation
builder.Services.AddScoped<SimulationsService>();

// Application Services – Import/Export
builder.Services.AddScoped<ImportExportService>();

// Application Services – Statistiken
builder.Services.AddScoped<StatistikService>();

// Application Services – Einstellungen (Singleton: liest/schreibt JSON-Datei, kein DB-Kontext)
builder.Services.AddSingleton<EinstellungenService>();

// Application Services – Spielplan
builder.Services.AddScoped<ISpielplanGenerator, RoundRobinGenerator>();
builder.Services.AddScoped<ISpielplanGenerator, KoRundeGenerator>();
builder.Services.AddScoped<ISpielplanGenerator, TopfLigaGenerator>();
builder.Services.AddScoped<ISpielplanGenerator, SchweizerSystemGenerator>();
builder.Services.AddScoped<SpielplanGeneratorFactory>();
builder.Services.AddScoped<SpielplanService>();

// Console Menüs – Blattmenüs
builder.Services.AddScoped<MannschaftMenue>();
builder.Services.AddScoped<LandVerbandMenue>();
builder.Services.AddScoped<RegelwerkMenue>();
builder.Services.AddScoped<TurnierVorlageMenue>();
builder.Services.AddScoped<TurnierInstanzMenue>();
builder.Services.AddScoped<AuslosungMenue>();
builder.Services.AddScoped<TurnierBaumMenue>();
builder.Services.AddScoped<SimulationsMenue>();
builder.Services.AddScoped<SpielplanMenue>();
builder.Services.AddScoped<ImportExportMenue>();
builder.Services.AddScoped<EinstellungenMenue>();
builder.Services.AddScoped<StatistikMenue>();

// Console Menüs – Gruppenmenüs
builder.Services.AddScoped<StammdatenMenue>();
builder.Services.AddScoped<BewegungsdatenMenue>();
builder.Services.AddScoped<HauptMenue>();
builder.Services.AddScoped<Anwendung>();

var host = builder.Build();

// Seed beim ersten Start
using (var scope = host.Services.CreateScope())
{
    var seedService = scope.ServiceProvider.GetRequiredService<SeedDatenService>();
    await seedService.InitialisierenAsync();
}


Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

AnsiConsole.Profile.Encoding = Encoding.UTF8;

// Anwendung starten
using (var scope = host.Services.CreateScope())
{
    var anwendung = scope.ServiceProvider.GetRequiredService<Anwendung>();
    await anwendung.StartenAsync();
}
