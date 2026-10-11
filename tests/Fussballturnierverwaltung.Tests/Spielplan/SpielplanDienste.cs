using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Spielplan;

/// <summary>Baut die Spielplan-Dienste für einen Test so zusammen wie die App — an einer Stelle statt in jeder
/// Testklasse, die einen von ihnen braucht.</summary>
internal sealed class SpielplanDienste
{
    public SpielplanDienste(TurnierverwaltungDbContext ctx)
    {
        var rundeRepo = new TurnierRundeRepository(ctx);
        var spielRepo = new SpielRepository(ctx);
        var tabellen  = new TabellenService(rundeRepo, spielRepo, new RegelwerkRepository(ctx));
        var generatoren = new SpielplanGeneratorFactory(
            [new KoRundeGenerator(), new RoundRobinGenerator(), new TopfLigaGenerator(), new SchweizerSystemGenerator()]);

        Quellverknuepfung = new KoQuellverknuepfung(rundeRepo, spielRepo, NullLogger<KoQuellverknuepfung>.Instance);
        Spielplan = new SpielplanService(
            rundeRepo, spielRepo, new MannschaftRepository(ctx), new TurnierBaumRepository(ctx),
            new UebergangsregelRepository(ctx), generatoren, new TurnierValidator(),
            tabellen, new TabellenZwischenspeicher(tabellen, spielRepo), Quellverknuepfung,
            new AuslosungRepository(ctx), NullLogger<SpielplanService>.Instance);
    }

    public SpielplanService Spielplan { get; }
    public KoQuellverknuepfung Quellverknuepfung { get; }
}
