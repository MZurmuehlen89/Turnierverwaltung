using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Einstellungen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Fussballturnierverwaltung.Infrastructure.Wartung;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fussballturnierverwaltung.Infrastructure.Erweiterungen;

public static class InfrastrukturErweiterungen
{
    public static IServiceCollection AddInfrastruktur(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Diagnose für DbContext-Kollisionen/langsame Abfragen (#317/#338-Folgefund) — Singleton, da sie ihren
        // Zustand pro DbContext-Instanz selbst verwaltet; KollisionenUeberwachen() startet der WinUi-Startpfad.
        services.AddSingleton<DbZugriffsDiagnose>();
        // DB-Anteil je Vorgang im Ablaufprotokoll (#49) — Singleton aus demselben Grund: der Zustand hängt je Befehl/Verbindung/Kontext an der Instanz.
        services.AddSingleton<DbAnteilErfassung>();
        // Verbindung aus den Einstellungen, eine Vorgabe aus der Konfiguration hat Vorrang (GUI-Tests, #49).
        services.TryAddSingleton<EinstellungenService>();
        services.AddSingleton(sp => new Verbindungseinstellung(sp.GetRequiredService<EinstellungenService>(), configuration));
        services.AddDbContext<TurnierverwaltungDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<Verbindungseinstellung>().Verbindungszeichenfolge())
                .AddInterceptors(sp.GetRequiredService<DbZugriffsDiagnose>(), sp.GetRequiredService<DbAnteilErfassung>()));

        services.AddSingleton(SeedBildQuelle.Eingebettet);
        services.AddScoped<SeedDatenService>();
        services.AddScoped<DatenbankWartungService>();
        // Je Aufruf neu gesucht: Wer die Werkzeuge nach der Fehlermeldung installiert, muss die App nicht neu starten.
        services.AddTransient(_ => PostgresWerkzeuge.Standard());
        services.AddScoped<DatenbankSicherungService>();

        // Stammdaten-Repositories
        services.AddScoped<IMannschaftRepository, MannschaftRepository>();
        services.AddScoped<ILandRepository, LandRepository>();
        services.AddScoped<IVerbandRepository, VerbandRepository>();

        // Turniere-Repositories
        services.AddScoped<ITurnierInstanzRepository, TurnierInstanzRepository>();
        services.AddScoped<IRegelwerkRepository, RegelwerkRepository>();
        services.AddScoped<IRankingRegelSetRepository, RankingRegelSetRepository>();
        services.AddScoped<IRankingRegelRepository, RankingRegelRepository>();

        // Spielplan-Repositories
        services.AddScoped<ISpielRepository, SpielRepository>();
        services.AddScoped<ITurnierRundeRepository, TurnierRundeRepository>();
        services.AddScoped<IUebergangsregelRepository, UebergangsregelRepository>();
        services.AddScoped<IVerbandsQuoteRepository, VerbandsQuoteRepository>();
        services.AddScoped<ILandesQuoteRepository, LandesQuoteRepository>();

        // Auslosungs-Repository
        services.AddScoped<IAuslosungRepository, AuslosungRepository>();

        // Turnierbaum-Repository
        services.AddScoped<ITurnierBaumRepository, TurnierBaumRepository>();

        // Simulations-Repository
        services.AddScoped<ISimulationsLaufRepository, SimulationsLaufRepository>();

        // Ranglisten-Repository
        services.AddScoped<IRanglisteRepository, RanglisteRepository>();

        return services;
    }
}
