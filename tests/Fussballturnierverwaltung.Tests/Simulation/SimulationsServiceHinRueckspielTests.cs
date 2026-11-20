using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Issue #346: Simulation von K.-o.-Paarungen mit Hin- und Rückspiel — das Hinspiel kennt kein
/// Entscheidungsverfahren, das Rückspiel nur bei ausgeglichenem Gesamtergebnis.</summary>
public class SimulationsServiceHinRueckspielTests
{
    private const int PAARUNGEN = 30;

    /// <summary>K.-o.-Runde mit Rückspiel und Elfmeterschießen als einzigem Entscheidungsverfahren;
    /// <see cref="PAARUNGEN"/> Paarungen gleich starker Teams (viele ausgeglichene Gesamtergebnisse).</summary>
    private static void SetupAnlegen(TurnierverwaltungDbContext ctx)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Achtelfinale", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, RegelwerkId = 1, MitRueckspiel = true
        });

        for (int i = 0; i < PAARUNGEN; i++)
        {
            int teamA = 2 * i + 1, teamB = 2 * i + 2;
            foreach (var id in new[] { teamA, teamB })
                ctx.Mannschaften.Add(new Mannschaft
                {
                    Id = id, Name = $"Team {id}", Kurzname = $"T{id}", LandId = 1,
                    OffensivRating = 70, MittelfeldRating = 70, DefensivRating = 70,
                    Heimstaerke = 100, Auswaertsstaerke = 100
                });

            int hinId = 2 * i + 1;
            ctx.Spiele.Add(new Spiel
            {
                Id = hinId, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = hinId, Spieltag = 1,
                HeimMannschaftId = teamA, AuswaertsMannschaftId = teamB, Status = SpielStatus.Geplant
            });
            ctx.Spiele.Add(new Spiel
            {
                Id = hinId + 1, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = hinId + 1, Spieltag = 2,
                HeimMannschaftId = teamB, AuswaertsMannschaftId = teamA, Status = SpielStatus.Geplant,
                IstRueckspiel = true, HinspielId = hinId
            });
        }
        ctx.SaveChanges();
    }

    [Fact]
    public async Task RundeSimulierenAsync_HinspielOhneVerfahren_RueckspielEntscheidetGesamtergebnis()
    {
        await using var ctx = SimulationsDienste.NeuerContext();
        SetupAnlegen(ctx);
        var service = SimulationsDienste.Service(ctx, new Zufallsquelle());

        await service.RundeSimulierenAsync(1, seed: 346);

        var spiele = ctx.Spiele.Include(s => s.Ergebnis).Where(s => s.TurnierRundeId == 1).ToList();
        spiele.Should().OnlyContain(s => s.Ergebnis != null);

        foreach (var hin in spiele.Where(s => !s.IstRueckspiel))
        {
            var rueck = spiele.Single(s => s.HinspielId == hin.Id);
            var h = hin.Ergebnis!;
            var r = rueck.Ergebnis!;

            h.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit, "das Hinspiel hat kein Entscheidungsverfahren");
            h.HeimElfmetertore.Should().BeNull();

            int teamA = h.HeimToreRegulaer + r.AuswaertsToreRegulaer;
            int teamB = h.AuswaertsToreRegulaer + r.HeimToreRegulaer;
            if (teamA == teamB)
            {
                r.EntscheidungArt.Should().Be(EntscheidungArt.Elfmeterschiessen);
                r.SiegerMannschaftId.Should().NotBeNull();
            }
            else
            {
                r.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit,
                    "bei entschiedenem Gesamtergebnis gibt es kein Elfmeterschießen im Rückspiel");
                r.SiegerMannschaftId.Should().Be(teamA > teamB ? hin.HeimMannschaftId : hin.AuswaertsMannschaftId);
            }
        }

        // Plausibilitätscheck, dass beide Zweige tatsächlich durchlaufen wurden.
        spiele.Where(s => s.IstRueckspiel).Select(s => s.Ergebnis!.EntscheidungArt).Distinct()
            .Should().Contain([EntscheidungArt.RegulaereSpielzeit, EntscheidungArt.Elfmeterschiessen]);
    }

    // ── Issue #386: Platzhalter-Auflösung einmal je Block ────────────────────────────────

    [Fact]
    public async Task RundeSimulierenAsync_LoestFolgerundenPlatzhalterNachDemBlockAuf()
    {
        await using var ctx = SimulationsDienste.NeuerContext();
        SetupAnlegen(ctx);
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Viertelfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, RegelwerkId = 1
        });
        ctx.Spiele.Add(new Spiel
        {
            Id = 1000, TurnierInstanzId = 1, TurnierRundeId = 2, Spielnummer = 1, Spieltag = 1,
            HeimPlatzhalterName = "Sieger Achtelfinale 1", AuswaertsPlatzhalterName = "Sieger Achtelfinale 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var service = SimulationsDienste.Service(ctx, new Zufallsquelle());

        await service.RundeSimulierenAsync(1, seed: 386);

        // Paarung N = N-tes Hinspiel; den Gesamtsieger trägt dessen Rückspiel (Ids 2 und 4).
        var sieger = ctx.Spiele.Include(s => s.Ergebnis).Where(s => s.Id == 2 || s.Id == 4)
            .ToDictionary(s => s.Id, s => s.Ergebnis!.SiegerMannschaftId);
        var folgespiel = ctx.Spiele.AsNoTracking().Single(s => s.Id == 1000);
        folgespiel.HeimMannschaftId.Should().Be(sieger[2]);
        folgespiel.AuswaertsMannschaftId.Should().Be(sieger[4]);
    }

    // ── Issue #387: Hinspiele / Rückspiele einer K.-o.-Runde getrennt simulieren ─────────

    [Fact]
    public async Task RundenHaelfteSimulierenAsync_Hinspiele_SimuliertNurHinspiele()
    {
        await using var ctx = SimulationsDienste.NeuerContext();
        SetupAnlegen(ctx);
        var service = SimulationsDienste.Service(ctx, new Zufallsquelle());

        int anzahl = await service.RundenHaelfteSimulierenAsync(1, istRueckrunde: false, seed: 387);

        anzahl.Should().Be(PAARUNGEN);
        var spiele = ctx.Spiele.Include(s => s.Ergebnis).Where(s => s.TurnierRundeId == 1).ToList();
        spiele.Where(s => !s.IstRueckspiel).Should().OnlyContain(s => s.Ergebnis != null);
        spiele.Where(s => s.IstRueckspiel).Should().OnlyContain(s => s.Ergebnis == null);
    }

    [Fact]
    public async Task RundenHaelfteSimulierenAsync_Rueckspiele_UeberspringtRueckspieleMitOffenemHinspiel()
    {
        await using var ctx = SimulationsDienste.NeuerContext();
        SetupAnlegen(ctx);
        var service = SimulationsDienste.Service(ctx, new Zufallsquelle());
        // Nur das Hinspiel der ersten Paarung ist gespielt.
        await service.SpielSimulierenAsync(1, new Random(387));

        int anzahl = await service.RundenHaelfteSimulierenAsync(1, istRueckrunde: true, seed: 387);

        anzahl.Should().Be(1, "nur das Rückspiel mit gespieltem Hinspiel ist simulierbar");
        var spiele = ctx.Spiele.Include(s => s.Ergebnis).Where(s => s.TurnierRundeId == 1).ToList();
        spiele.Single(s => s.HinspielId == 1).Ergebnis.Should().NotBeNull();
        spiele.Where(s => s.IstRueckspiel && s.HinspielId != 1).Should().OnlyContain(s => s.Ergebnis == null);
    }

    // ── Issue #671: Risikostufe im Rückspiel nach dem Gesamtstand ─────────────────────────

    /// <summary>Taktikwechsel eines ruhigen Rückspiels (0:0), dessen Heimteam das Hinspiel 0:5 verloren hat (K3, K4;
    /// seit #673 nach dem Entscheidungsmodell, jeder Wurf „noch nicht“, also sieben Minuten nach der Schwelle). Die
    /// Teams aus <c>SetupAnlegen</c> sind gleich stark: offensiv in der 7., alles nach vorn in der 27., aussichtslos
    /// in der 68. – ohne Torwart vorn; der Gast wird in der 67. defensiv (#667) und mauert in der 77.</summary>
    private static readonly (int, bool, string)[] TaktikNachHinspielNullZuFuenfGleichStark =
    [
        (7, true, "Stellt auf Offensive um"),
        (27, true, "Wirft alles nach vorn"),
        (67, false, "Zieht sich etwas zurück und sichert die Führung"),
        (68, true, "Kehrt zur gewohnten Ordnung zurück"),
        (77, false, "Zieht sich zurück und verteidigt die Führung"),
    ];

    /// <summary>Dasselbe mit Heim- und Gastteam aus den Engine-Testdaten (Stärkeanteil 2/3 gegen 1/3): das Heimteam
    /// drückt früher, der Gast mauert früher.</summary>
    private static readonly (int, bool, string)[] TaktikNachHinspielNullZuFuenf =
    [
        (1, true, "Stellt auf Offensive um"),
        (14, true, "Wirft alles nach vorn"),
        (54, false, "Zieht sich etwas zurück und sichert die Führung"),
        (64, false, "Zieht sich zurück und verteidigt die Führung"),
        (68, true, "Kehrt zur gewohnten Ordnung zurück"),
    ];

    private static IEnumerable<(int, bool, string)> Taktikwechsel(SimulationsErgebnis ergebnis) =>
        ergebnis.Ereignisse.Where(e => e.Art == EreignisArt.Taktikwechsel).Select(e => (e.Minute, e.IstHeim, e.Beschreibung));

    [Fact]
    public async Task SpielSimulierenAsync_Rueckspiel_TaktikNachGesamtstand()
    {
        await using var ctx = SimulationsDienste.NeuerContext();
        SetupAnlegen(ctx);
        var hinspiel = ctx.Spiele.Single(s => s.Id == 1);
        hinspiel.Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            SpielId = 1, HeimToreRegulaer = 5, AuswaertsToreRegulaer = 0,
            SiegerMannschaftId = hinspiel.HeimMannschaftId, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit
        });
        ctx.SaveChanges();
        var service = SimulationsDienste.Service(ctx, new Zufallsquelle());

        var ergebnis = await service.SpielSimulierenAsync(2, new Ablauffolge().Zufall());

        (ergebnis.HeimToreRegulaer, ergebnis.AuswaertsToreRegulaer).Should().Be((0, 0));
        Taktikwechsel(ergebnis).Should().Equal(TaktikNachHinspielNullZuFuenfGleichStark);
    }

    [Fact]
    public void RueckspielSimulieren_FreieSimulation_TaktikNachGesamtstand()
    {
        var service = SimulationsDienste.Service(SimulationsDienste.NeuerContext(), new Zufallsquelle());

        var ergebnis = service.RueckspielSimulieren(
            SimulationsEngineTestdaten.HeimTeam(), SimulationsEngineTestdaten.GastTeam(),
            new SimulationsErgebnis(5, 0, null, null, null, null, EntscheidungArt.RegulaereSpielzeit, []), [],
            new Ablauffolge().Zufall());

        Taktikwechsel(ergebnis).Should().Equal(TaktikNachHinspielNullZuFuenf);
    }
}
