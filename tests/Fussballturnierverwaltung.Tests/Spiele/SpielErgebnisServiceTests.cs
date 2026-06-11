using FluentAssertions;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
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

namespace Fussballturnierverwaltung.Tests.Spiele;

public class SpielErgebnisServiceTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SpielErgebnisService ServiceFuer(TurnierverwaltungDbContext ctx) =>
        new(new SpielRepository(ctx),
            new SpielErgebnisValidator(),
            NullLogger<SpielErgebnisService>.Instance);

    private static void GrundSetupAnlegen(TurnierverwaltungDbContext ctx, bool auswaertstorRegel)
    {
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Achtelfinale",
            Rundenart = Rundenart.KoRundeMitHinUndRueckspiel,
            RegelwerkId = 1, Reihenfolge = 1,
            AuswaertstorRegelAktiv = auswaertstorRegel
        });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 });
        ctx.SaveChanges();
    }

    private static void HinspielAnlegen(
        TurnierverwaltungDbContext ctx, int spielId, int rundeId,
        int heimId, int auswaertsId,
        int heimTore, int auswaertsTore)
    {
        ctx.Spiele.Add(new Spiel
        {
            Id = spielId, TurnierInstanzId = 1, TurnierRundeId = rundeId,
            Spielnummer = spielId, HeimMannschaftId = heimId, AuswaertsMannschaftId = auswaertsId,
            Status = SpielStatus.Abgeschlossen, IstRueckspiel = false
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = spielId, SpielId = spielId,
            HeimToreRegulaer = heimTore, AuswaertsToreRegulaer = auswaertsTore,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit,
            SiegerMannschaftId = heimTore > auswaertsTore ? heimId
                               : auswaertsTore > heimTore ? auswaertsId
                               : null
        });
        ctx.SaveChanges();
    }

    private static void RueckspielAnlegen(
        TurnierverwaltungDbContext ctx, int spielId, int rundeId,
        int heimId, int auswaertsId, int hinspielId)
    {
        ctx.Spiele.Add(new Spiel
        {
            Id = spielId, TurnierInstanzId = 1, TurnierRundeId = rundeId,
            Spielnummer = spielId, HeimMannschaftId = heimId, AuswaertsMannschaftId = auswaertsId,
            Status = SpielStatus.Geplant, IstRueckspiel = true, HinspielId = hinspielId
        });
        ctx.SaveChanges();
    }

    private static async Task<SpielErgebnis?> ErgebnisFuer(TurnierverwaltungDbContext ctx, int spielId)
        => await ctx.SpielErgebnisse.FirstOrDefaultAsync(e => e.SpielId == spielId);

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task EinfachesSpiel_HeimGewinnt_SiegerWirdGesetzt()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = 1,
            Spielnummer = 1, HeimMannschaftId = 1, AuswaertsMannschaftId = 2,
            Status = SpielStatus.Geplant, IstRueckspiel = false
        });
        ctx.SaveChanges();

        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(1, new ErgebnisErfassenEingabe(2, 1, EntscheidungArt.RegulaereSpielzeit));

        var erg = await ErgebnisFuer(ctx, 1);
        erg.Should().NotBeNull();
        erg!.SiegerMannschaftId.Should().Be(1);
        erg.HeimToreRegulaer.Should().Be(2);
        erg.AuswaertsToreRegulaer.Should().Be(1);
    }

    [Fact]
    public async Task Rueckspiel_AggregateUngleich_GesamtsiegerWirdErmittelt()
    {
        // Hinspiel: Team A (Heim/id=1) 1:0 → Team A führt
        // Rückspiel: Team B (Heim/id=2) 2:0 → Aggregat: A=1, B=2 → Team B gewinnt
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: true);
        HinspielAnlegen(ctx, spielId: 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 1, auswaertsTore: 0);
        RueckspielAnlegen(ctx, spielId: 2, rundeId: 1, heimId: 2, auswaertsId: 1, hinspielId: 1);

        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(2, new ErgebnisErfassenEingabe(2, 0, EntscheidungArt.RegulaereSpielzeit));

        var erg = await ErgebnisFuer(ctx, 2);
        // Aggregat: Team A = hinspiel.Heim(1) + rückspiel.Auswärts(0) = 1
        //           Team B = hinspiel.Auswärts(0) + rückspiel.Heim(2) = 2 → Team B gewinnt
        erg!.SiegerMannschaftId.Should().Be(2, "Team B hat das bessere Aggregat");
        erg.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit);
    }

    [Fact]
    public async Task Rueckspiel_AggregateGleich_AuswaertsToreEntscheiden()
    {
        // Hinspiel: Team A (id=1) heim 0:1 → Team B hat 1 Auswärtstor
        // Rückspiel: Team B (id=2) heim 0:2 → Team A hat 2 Auswärtstore
        // Aggregat: A=0+2=2, B=1+0=1 → Nein, ungleich!
        //
        // Stattdessen: Hinspiel 0:1, Rückspiel 1:0 → Aggregat: A=0+0=0, B=1+1=2 → ungleich
        //
        // Gleich-Aggregat + unterschiedliche Auswärtstore:
        // Hinspiel: A heim 0:1 (B hat 1 Auswärtstor)
        // Rückspiel: B heim 1:2 (A hat 2 Auswärtstore) → Aggregat A=0+2=2, B=1+1=2 → gleich!
        // Auswärtstore: A=2, B=1 → Team A gewinnt via Auswaertstorregel
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: true);
        HinspielAnlegen(ctx, spielId: 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 0, auswaertsTore: 1);
        RueckspielAnlegen(ctx, spielId: 2, rundeId: 1, heimId: 2, auswaertsId: 1, hinspielId: 1);

        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(2, new ErgebnisErfassenEingabe(1, 2, EntscheidungArt.RegulaereSpielzeit));

        var erg = await ErgebnisFuer(ctx, 2);
        // teamA_gesamt = hinspiel.Heim(0) + rückspiel.Auswärts(2) = 2
        // teamB_gesamt = hinspiel.Auswärts(1) + rückspiel.Heim(1) = 2 → gleich
        // teamA_weg = rückspiel.Auswärts(2) > teamB_weg = hinspiel.Auswärts(1) → Team A gewinnt
        erg!.SiegerMannschaftId.Should().Be(1, "Team A hat mehr Auswärtstore (2 vs 1)");
        erg.EntscheidungArt.Should().Be(EntscheidungArt.Auswaertstorregel);
    }

    [Fact]
    public async Task Rueckspiel_OhneAuswaertstorRegel_NormaleEntscheidung()
    {
        // Gleiche Konstellation, aber AuswaertstorRegel ist deaktiviert.
        // → Unentschieden in der Regulärzeit → kein Sieger über Aggregat → EntscheidungsVerfahren nötig
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);  // deaktiviert!
        HinspielAnlegen(ctx, spielId: 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 0, auswaertsTore: 1);
        RueckspielAnlegen(ctx, spielId: 2, rundeId: 1, heimId: 2, auswaertsId: 1, hinspielId: 1);

        var service = ServiceFuer(ctx);
        // Rückspiel 1:2: die BestimmeSieger-Methode ignoriert Aggregat, wertet nur Rückspiel allein aus
        await service.ErgebnisErfassenAsync(2, new ErgebnisErfassenEingabe(1, 2, EntscheidungArt.RegulaereSpielzeit));

        var erg = await ErgebnisFuer(ctx, 2);
        // Kein Auswaertstor-Check → SiegerAbleiten(heim=2, auswaerts=1, 1:2) → AuswaertsId (Team A, id=1) gewinnt
        erg!.SiegerMannschaftId.Should().Be(1, "Ohne Auswaertstorregel entscheidet das Einzelspiel-Ergebnis");
        erg.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit);
    }

    [Fact]
    public async Task Unentschieden_KeinSieger()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = 1,
            Spielnummer = 1, HeimMannschaftId = 1, AuswaertsMannschaftId = 2,
            Status = SpielStatus.Geplant, IstRueckspiel = false
        });
        ctx.SaveChanges();

        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(1, new ErgebnisErfassenEingabe(1, 1, EntscheidungArt.RegulaereSpielzeit));

        var erg = await ErgebnisFuer(ctx, 1);
        erg!.SiegerMannschaftId.Should().BeNull("ein Unentschieden ergibt keinen Sieger");
        erg.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit);
    }
}
