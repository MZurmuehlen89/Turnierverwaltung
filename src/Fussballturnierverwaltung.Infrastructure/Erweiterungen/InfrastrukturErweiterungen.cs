using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Ranglisten;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Fussballturnierverwaltung.Infrastructure.Seed;
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
        services.AddDbContext<TurnierverwaltungDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("Standard"),
                sql => sql.MigrationsAssembly(typeof(TurnierverwaltungDbContext).Assembly.FullName)));

        services.AddScoped<SeedDatenService>();

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
        services.AddScoped<IRanglistenQuelleRepository, RanglistenQuelleRepository>();

        // Ranglisten-Import (Issue #120)
        services.AddScoped<IRanglistenHtmlAuswerter, RanglistenHtmlAuswerter>();
        services.AddHttpClient<IHtmlHerunterlader, HttpHtmlHerunterlader>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Turnierverwaltung/1.0 (+https://github.com/MZurmuehlen89/Turnierverwaltung)");
        });

        return services;
    }
}
