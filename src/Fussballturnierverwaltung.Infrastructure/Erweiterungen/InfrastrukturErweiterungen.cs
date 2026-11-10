using Fussballturnierverwaltung.Application.Auslosungen;
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
        services.AddDbContext<TurnierverwaltungDbContext>((sp, options) =>
            options.UseSqlServer(
                    configuration.GetConnectionString("Standard"),
                    sql => sql.MigrationsAssembly(typeof(TurnierverwaltungDbContext).Assembly.FullName))
                .AddInterceptors(sp.GetRequiredService<DbZugriffsDiagnose>()));

        services.AddSingleton(SeedBildQuelle.Eingebettet);
        services.AddScoped<SeedDatenService>();
        services.AddScoped<DatenbankWartungService>();

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
