using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Application.Zufall;
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
        var mannschaftRepo = new MannschaftRepository(ctx);
        var auslosungRepo = new AuslosungRepository(ctx);
        var uebergangsregelRepo = new UebergangsregelRepository(ctx);
        var tabellen  = new TabellenService(rundeRepo, spielRepo, new RegelwerkRepository(ctx), new RanglisteRepository(ctx));
        var generatoren = new SpielplanGeneratorFactory(
            [new KoRundeGenerator(), new RoundRobinGenerator(), new TopfLigaGenerator(new Zufallsquelle()), new SchweizerSystemGenerator()]);

        Quellverknuepfung = new KoQuellverknuepfung(rundeRepo, spielRepo, NullLogger<KoQuellverknuepfung>.Instance);
        var zwischenspeicher = new TabellenZwischenspeicher(tabellen, spielRepo);
        Aufloesung = new PlatzhalterAufloesung(
            rundeRepo, spielRepo, mannschaftRepo, tabellen, zwischenspeicher, Quellverknuepfung,
            NullLogger<PlatzhalterAufloesung>.Instance);
        Spielplan = new SpielplanService(
            rundeRepo, spielRepo, mannschaftRepo,
            generatoren, new TurnierValidator(), Quellverknuepfung, Aufloesung,
            NullLogger<SpielplanService>.Instance);
        Schweizer = new SchweizerRundenfolge(auslosungRepo, tabellen, Spielplan);
        Finale = new FinaleAutomatik(
            rundeRepo, uebergangsregelRepo, auslosungRepo, spielRepo, Aufloesung, Spielplan,
            NullLogger<FinaleAutomatik>.Instance);
        Kaskade = new PlatzhalterKaskadeService(
            rundeRepo, spielRepo, auslosungRepo, zwischenspeicher, Aufloesung, Spielplan, Finale,
            NullLogger<PlatzhalterKaskadeService>.Instance);
        Qualifikanten = new QualifikantenAufloesung(Aufloesung, zwischenspeicher, rundeRepo, mannschaftRepo);
    }

    public SpielplanService Spielplan { get; }
    public SchweizerRundenfolge Schweizer { get; }
    public KoQuellverknuepfung Quellverknuepfung { get; }
    public PlatzhalterAufloesung Aufloesung { get; }
    public FinaleAutomatik Finale { get; }
    public PlatzhalterKaskadeService Kaskade { get; }
    public QualifikantenAufloesung Qualifikanten { get; }
}
