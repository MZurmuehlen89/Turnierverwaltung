using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Turnierbaum;

public class TurnierBaumServiceTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (TurnierBaumService Baum, SpielplanService Spielplan) ServiceFuer(TurnierverwaltungDbContext ctx)
    {
        var rundeRepo = new TurnierRundeRepository(ctx);
        var spielRepo = new SpielRepository(ctx);
        var mannschaftRepo = new MannschaftRepository(ctx);
        var baumRepo = new TurnierBaumRepository(ctx);
        var uebergangsregelRepo = new UebergangsregelRepository(ctx);
        var factory = new SpielplanGeneratorFactory([new KoRundeGenerator(), new RoundRobinGenerator()]);
        var spielplanService = new SpielplanService(
            rundeRepo, spielRepo, mannschaftRepo, baumRepo, uebergangsregelRepo,
            factory, new TurnierValidator(), new TabellenService(rundeRepo, spielRepo, new RegelwerkRepository(ctx)),
            new AuslosungRepository(ctx), NullLogger<SpielplanService>.Instance);
        var instanzRepo = new TurnierInstanzRepository(ctx);
        var baumService = new TurnierBaumService(
            baumRepo, instanzRepo, spielRepo, spielplanService, NullLogger<TurnierBaumService>.Instance);
        return (baumService, spielplanService);
    }

    /// <summary>Legt Regelwerk, TurnierInstanz, 16 Mannschaften sowie Achtelfinale (8 Spiele, alle mit
    /// echten Mannschaften) und Viertelfinale (Runde ohne Spiele, Aufgabe der einzelnen Tests) an.</summary>
    private static void AchtelfinaleUndViertelfinaleAnlegen(
        TurnierverwaltungDbContext ctx, out int afRundeId, out int vfRundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });
        ctx.TurnierInstanzen.Add(new TurnierInstanz { Id = 1, Name = "Test-Turnier", SaisonOderJahr = "2026" });

        var af = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Achtelfinale", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 16, AusgangsMannschaftsanzahl = 8,
            RegelwerkId = 1
        };
        var vf = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Viertelfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 8, AusgangsMannschaftsanzahl = 4,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(af, vf);

        for (int i = 1; i <= 16; i++)
            ctx.Mannschaften.Add(new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 });

        for (int i = 1; i <= 8; i++)
        {
            ctx.Spiele.Add(new Spiel
            {
                Id = i, TurnierInstanzId = 1, TurnierRundeId = af.Id, Spielnummer = i,
                HeimMannschaftId = i * 2 - 1, AuswaertsMannschaftId = i * 2,
                Status = SpielStatus.Geplant
            });
        }

        ctx.SaveChanges();
        afRundeId = af.Id;
        vfRundeId = vf.Id;
    }

    // ── Regressionstest für den gemeldeten #241-Bug ────────────────────────

    [Fact]
    public async Task GenerierenAsync_VerknuepftAlleReferenziertenSlots_BeiGemischterEbeneMitDirekteinstieg()
    {
        await using var ctx = NeuerContext();
        AchtelfinaleUndViertelfinaleAnlegen(ctx, out int afRundeId, out int vfRundeId);

        // 3 der 4 Viertelfinale-Spiele beziehen sich per Platzhalter auf konkrete Achtelfinale-Spiele
        // (6 von 8 AF-Spielen referenziert), das vierte ist ein Direkteinstieg (2 real bekannte Teams,
        // keine AF-Quelle) — reproduziert den gemeldeten Bug: eine gemischte Ebene aus K.o.-Siegern und
        // Direkteinstiegen, bei der vorher (TurnierBaumService.VerkuepfenPerPlatzhalter, "anyLinked" pro
        // gesamten Übergang statt pro Slot) nur ein Bruchteil der Kind-Ebene verknüpft wurde.
        ctx.Spiele.AddRange(
            new Spiel { Id = 101, TurnierInstanzId = 1, TurnierRundeId = vfRundeId, Spielnummer = 101,
                HeimPlatzhalterName = "Sieger AF 1", AuswaertsPlatzhalterName = "Sieger AF 2", Status = SpielStatus.Geplant },
            new Spiel { Id = 102, TurnierInstanzId = 1, TurnierRundeId = vfRundeId, Spielnummer = 102,
                HeimPlatzhalterName = "Sieger AF 3", AuswaertsPlatzhalterName = "Sieger AF 4", Status = SpielStatus.Geplant },
            new Spiel { Id = 103, TurnierInstanzId = 1, TurnierRundeId = vfRundeId, Spielnummer = 103,
                HeimPlatzhalterName = "Sieger AF 5", AuswaertsPlatzhalterName = "Sieger AF 6", Status = SpielStatus.Geplant },
            new Spiel { Id = 104, TurnierInstanzId = 1, TurnierRundeId = vfRundeId, Spielnummer = 104,
                HeimMannschaftId = 15, AuswaertsMannschaftId = 16, Status = SpielStatus.Geplant });
        ctx.SaveChanges();

        var (baumService, _) = ServiceFuer(ctx);

        await baumService.GenerierenAsync(1);
        var geladen = await baumService.AbrufenAsync(1);

        geladen.Should().NotBeNull();
        var afKnoten = geladen!.Knoten.Where(k => k.Runde == 1).OrderBy(k => k.Spiel!.Spielnummer).ToList();
        var vfKnoten = geladen.Knoten.Where(k => k.Runde == 2).OrderBy(k => k.Spiel!.Spielnummer).ToList();

        afKnoten.Should().HaveCount(8);
        vfKnoten.Should().HaveCount(4);

        // Alle 6 referenzierten AF-Spiele (1-6) müssen verknüpft sein — nicht nur ein Teil davon.
        for (int i = 0; i < 6; i++)
            afKnoten[i].ElternKnotenId.Should().NotBeNull($"AF-Spiel {i + 1} wird per Platzhalter referenziert");

        // Die beiden nicht referenzierten AF-Spiele (7, 8) bleiben korrekt unverknüpft.
        afKnoten[6].ElternKnotenId.Should().BeNull();
        afKnoten[7].ElternKnotenId.Should().BeNull();

        // Sieger-Seite (Heim-Platzhalter) → linkes Kind, Auswärts-Platzhalter → rechtes Kind.
        vfKnoten[0].LinkerKindKnotenId.Should().Be(afKnoten[0].Id);
        vfKnoten[0].RechterKindKnotenId.Should().Be(afKnoten[1].Id);
        vfKnoten[1].LinkerKindKnotenId.Should().Be(afKnoten[2].Id);
        vfKnoten[1].RechterKindKnotenId.Should().Be(afKnoten[3].Id);
        vfKnoten[2].LinkerKindKnotenId.Should().Be(afKnoten[4].Id);
        vfKnoten[2].RechterKindKnotenId.Should().Be(afKnoten[5].Id);

        // Der Direkteinstieg (Spiel 104) hat korrekt keine Kind-Verknüpfung.
        vfKnoten[3].LinkerKindKnotenId.Should().BeNull();
        vfKnoten[3].RechterKindKnotenId.Should().BeNull();
    }

    [Fact]
    public async Task GenerierenAsync_NutztSequentiellenFallback_WennKeineQuellIdsVorhanden()
    {
        await using var ctx = NeuerContext();
        AchtelfinaleUndViertelfinaleAnlegen(ctx, out int afRundeId, out int vfRundeId);

        // Alt-Daten-Szenario: Viertelfinale-Spiele ohne jegliche Platzhalter-Historie (z.B. Turnier vor
        // der #241-Migration), alle Slots bereits mit realen Teams befüllt.
        for (int i = 1; i <= 4; i++)
        {
            ctx.Spiele.Add(new Spiel
            {
                Id = 100 + i, TurnierInstanzId = 1, TurnierRundeId = vfRundeId, Spielnummer = 100 + i,
                HeimMannschaftId = i * 2 - 1, AuswaertsMannschaftId = i * 2, Status = SpielStatus.Geplant
            });
        }
        ctx.SaveChanges();

        var (baumService, _) = ServiceFuer(ctx);

        await baumService.GenerierenAsync(1);
        var geladen = await baumService.AbrufenAsync(1);

        var afKnoten = geladen!.Knoten.Where(k => k.Runde == 1).OrderBy(k => k.Spiel!.Spielnummer).ToList();
        var vfKnoten = geladen.Knoten.Where(k => k.Runde == 2).OrderBy(k => k.Spiel!.Spielnummer).ToList();

        // Sequentieller Fallback: VF-Spiel j verknüpft mit AF-Spielen 2j und 2j+1.
        vfKnoten[0].LinkerKindKnotenId.Should().Be(afKnoten[0].Id);
        vfKnoten[0].RechterKindKnotenId.Should().Be(afKnoten[1].Id);
        vfKnoten[3].LinkerKindKnotenId.Should().Be(afKnoten[6].Id);
        vfKnoten[3].RechterKindKnotenId.Should().Be(afKnoten[7].Id);
    }

    [Fact]
    public async Task SiegerFortsteiernAsync_PropagiertSiegerAutomatischUeberIdVerknuepfung()
    {
        await using var ctx = NeuerContext();
        AchtelfinaleUndViertelfinaleAnlegen(ctx, out int afRundeId, out int vfRundeId);

        ctx.Spiele.Add(new Spiel
        {
            Id = 101, TurnierInstanzId = 1, TurnierRundeId = vfRundeId, Spielnummer = 101,
            HeimPlatzhalterName = "Sieger AF 1", AuswaertsPlatzhalterName = "Sieger AF 2", Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();

        var (baumService, _) = ServiceFuer(ctx);
        await baumService.GenerierenAsync(1);

        // AF-Spiel 1 (Team 1 vs Team 2) entscheiden: Team 1 gewinnt.
        var afSpiel1 = ctx.Spiele.First(s => s.Id == 1);
        afSpiel1.Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = 1, SpielId = 1, HeimToreRegulaer = 2, AuswaertsToreRegulaer = 0,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = 1
        });
        ctx.SaveChanges();

        int fortgeschrieben = await baumService.SiegerFortsteiernAsync(1);

        fortgeschrieben.Should().BeGreaterThan(0);
        ctx.Spiele.First(s => s.Id == 101).HeimMannschaftId.Should().Be(1, "Sieger AF 1 = Team 1");
    }
}
