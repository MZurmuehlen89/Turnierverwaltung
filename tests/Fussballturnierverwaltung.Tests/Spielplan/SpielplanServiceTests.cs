using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class SpielplanServiceTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SpielplanService ServiceFuer(TurnierverwaltungDbContext ctx)
    {
        var rundeRepo = new TurnierRundeRepository(ctx);
        var spielRepo = new SpielRepository(ctx);
        var mannschaftRepo = new MannschaftRepository(ctx);
        var baumRepo = new TurnierBaumRepository(ctx);
        var qualifikationsRegelRepo = new QualifikationsRegelRepository(ctx);
        var factory = new SpielplanGeneratorFactory([new KoRundeGenerator(), new RoundRobinGenerator()]);
        return new SpielplanService(
            rundeRepo, spielRepo, mannschaftRepo, baumRepo, qualifikationsRegelRepo,
            factory, new TurnierValidator(), new TabellenService(rundeRepo, spielRepo, new RegelwerkRepository(ctx)),
            NullLogger<SpielplanService>.Instance);
    }

    /// <summary>Legt Regelwerk (mit Entscheidungsverfahren, für KO/Finale Pflicht), Halbfinale- und Finale-Runde
    /// sowie 4 Mannschaften an. Halbfinale-Spiele werden NICHT angelegt (Aufgabe der einzelnen Tests).</summary>
    private static void GrundSetupAnlegen(
        TurnierverwaltungDbContext ctx, out int hfRundeId, out int finaleRundeId, bool hfMitRueckspiel = false)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var hf = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1, MitRueckspiel = hfMitRueckspiel
        };
        var finale = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 2,
            Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(hf, finale);

        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });

        ctx.SaveChanges();
        hfRundeId = hf.Id;
        finaleRundeId = finale.Id;
    }

    private static void HalbfinalSpielAnlegen(
        TurnierverwaltungDbContext ctx, int id, int rundeId,
        int heimId, int auswaertsId, int? siegerMannschaftId)
    {
        ctx.Spiele.Add(new Spiel
        {
            Id = id, TurnierInstanzId = 1, TurnierRundeId = rundeId,
            Spielnummer = id, HeimMannschaftId = heimId, AuswaertsMannschaftId = auswaertsId,
            Status = siegerMannschaftId.HasValue ? SpielStatus.Abgeschlossen : SpielStatus.Geplant,
            IstRueckspiel = false
        });
        if (siegerMannschaftId.HasValue)
        {
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = id, SpielId = id,
                HeimToreRegulaer = siegerMannschaftId == heimId ? 1 : 0,
                AuswaertsToreRegulaer = siegerMannschaftId == auswaertsId ? 1 : 0,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit,
                SiegerMannschaftId = siegerMannschaftId
            });
        }
        ctx.SaveChanges();
    }

    // ── Tests: automatische Finale-Generierung ohne Auslosung (Issue #113) ─

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_GeneriertFinale_WennHalbfinaleEntschieden()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var service = ServiceFuer(ctx);

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1);
        var teilnehmer = new[] { finaleSpiele[0].HeimMannschaftId, finaleSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 1, 3 });
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_GeneriertNicht_WennHalbfinaleNochOffen()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: null);
        var service = ServiceFuer(ctx);

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().BeEmpty();
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_GeneriertNichtErneut_WennBereitsVorhanden()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        // Finale wurde bereits (z.B. manuell) generiert
        ctx.Spiele.Add(new Spiel
        {
            Id = 99, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId,
            Spielnummer = 1, HeimMannschaftId = 1, AuswaertsMannschaftId = 3, Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var service = ServiceFuer(ctx);

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().HaveCount(1,
            "es darf kein zweites Finalspiel automatisch hinzugefügt werden");
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_GeneriertNicht_BeiHalbfinaleMitRueckspiel()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId, hfMitRueckspiel: true);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var service = ServiceFuer(ctx);

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().BeEmpty(
            "Aggregat-Sieger-Ermittlung über zwei Spielbeine ist bewusst nicht Teil dieser Phase");
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_EntferntFinale_WennHalbfinalErgebnisZurueckgesetztWird()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var service = ServiceFuer(ctx);
        await service.FinaleAutomatischGenerierenWennBereitAsync(1);
        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().HaveCount(1, "Vorbedingung: Finale wurde generiert");

        // Ergebnis eines Halbfinalspiels zurückgesetzt (z.B. "Ergebnis löschen")
        var spiel2Ergebnis = ctx.SpielErgebnisse.First(e => e.SpielId == 2);
        ctx.SpielErgebnisse.Remove(spiel2Ergebnis);
        var spiel2 = ctx.Spiele.First(s => s.Id == 2);
        spiel2.Status = SpielStatus.Geplant;
        ctx.SaveChanges();

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().BeEmpty(
            "das noch nicht gespielte Finale muss entfernt werden, wenn das Halbfinale nicht mehr entschieden ist");
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_RegeneriertFinale_WennHalbfinaleAndersAusgeht()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var service = ServiceFuer(ctx);
        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        // Halbfinale neu simuliert - diesmal gewinnen Team B und Team D statt Team A und Team C
        var erg1 = ctx.SpielErgebnisse.First(e => e.SpielId == 1);
        erg1.SiegerMannschaftId = 2; erg1.HeimToreRegulaer = 0; erg1.AuswaertsToreRegulaer = 1;
        var erg2 = ctx.SpielErgebnisse.First(e => e.SpielId == 2);
        erg2.SiegerMannschaftId = 4; erg2.HeimToreRegulaer = 0; erg2.AuswaertsToreRegulaer = 1;
        ctx.SaveChanges();

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1);
        var teilnehmer = new[] { finaleSpiele[0].HeimMannschaftId, finaleSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 2, 4 });
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_RuehrtBereitsGespieltesFinaleNichtAn()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var service = ServiceFuer(ctx);
        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        // Finale wurde bereits gespielt
        var finaleSpielId = ctx.Spiele.First(s => s.TurnierRundeId == finaleRundeId).Id;
        ctx.Spiele.First(s => s.Id == finaleSpielId).Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = finaleSpielId, SpielId = finaleSpielId,
            HeimToreRegulaer = 2, AuswaertsToreRegulaer = 1,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = 1
        });
        ctx.SaveChanges();

        // Halbfinale nachträglich geändert
        var erg2 = ctx.SpielErgebnisse.First(e => e.SpielId == 2);
        erg2.SiegerMannschaftId = 4;
        ctx.SaveChanges();

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1, "ein bereits gespieltes Finale darf nie automatisch verändert werden");
        finaleSpiele[0].Id.Should().Be(finaleSpielId);
        var teilnehmer = new[] { finaleSpiele[0].HeimMannschaftId, finaleSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 1, 3 }, "die ursprüngliche, bereits gespielte Paarung bleibt erhalten");
    }

    // ── Tests: automatische Generierung aus abgeschlossener Auslosung (Issue #19) ─

    private static void LigaGrundSetupAnlegen(TurnierverwaltungDbContext ctx, out int rundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        var runde = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Liga", Reihenfolge = 1,
            Rundenart = Rundenart.Liga, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 4,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.Add(runde);
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });
        ctx.SaveChanges();
        rundeId = runde.Id;
    }

    [Fact]
    public async Task GenerierenAusAuslosungAsync_LigaRunde_GeneriertSpielplanAusMannschaftIds()
    {
        await using var ctx = NeuerContext();
        LigaGrundSetupAnlegen(ctx, out int rundeId);
        var ergebnisse = new List<AuslosungsErgebnis>
        {
            new() { PaarungsNummer = 1, HeimMannschaftId = 1 },
            new() { PaarungsNummer = 2, HeimMannschaftId = 2 },
            new() { PaarungsNummer = 3, HeimMannschaftId = 3 },
            new() { PaarungsNummer = 4, HeimMannschaftId = 4 },
        };
        var service = ServiceFuer(ctx);

        await service.GenerierenAusAuslosungAsync(rundeId, ergebnisse);

        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == rundeId).ToList();
        spiele.Should().NotBeEmpty("bei 4 Teams muss ein Round-Robin-Spielplan entstehen");
        var beteiligteTeams = spiele
            .SelectMany(s => new[] { s.HeimMannschaftId, s.AuswaertsMannschaftId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        beteiligteTeams.Should().BeEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    [Fact]
    public async Task GenerierenAusAuslosungAsync_GruppenphaseRunde_GeneriertProGruppe()
    {
        await using var ctx = NeuerContext();
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        var runde = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 4,
            RegelwerkId = 1, GruppenAnzahl = 2
        };
        ctx.TurnierRunden.Add(runde);
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });
        ctx.SaveChanges();
        // Gruppe 1: Team A + Team B, Gruppe 2: Team C + Team D
        var ergebnisse = new List<AuslosungsErgebnis>
        {
            new() { PaarungsNummer = 1, HeimMannschaftId = 1 },
            new() { PaarungsNummer = 1, HeimMannschaftId = 2 },
            new() { PaarungsNummer = 2, HeimMannschaftId = 3 },
            new() { PaarungsNummer = 2, HeimMannschaftId = 4 },
        };
        var service = ServiceFuer(ctx);

        await service.GenerierenAusAuslosungAsync(runde.Id, ergebnisse);

        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == runde.Id).ToList();
        spiele.Should().NotBeEmpty();
        spiele.Should().OnlyContain(s =>
            (s.HeimMannschaftId == 1 && s.AuswaertsMannschaftId == 2) ||
            (s.HeimMannschaftId == 2 && s.AuswaertsMannschaftId == 1) ||
            (s.HeimMannschaftId == 3 && s.AuswaertsMannschaftId == 4) ||
            (s.HeimMannschaftId == 4 && s.AuswaertsMannschaftId == 3),
            "Gruppen dürfen sich in den generierten Paarungen nicht mischen");
    }

    [Fact]
    public async Task GenerierenAusAuslosungAsync_KoRundeMitPlatzhaltern_GeneriertPlatzhalterSpiele()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out _);
        var ergebnisse = new List<AuslosungsErgebnis>
        {
            new() { PaarungsNummer = 1, HeimPlatzhalterName = "Sieger Gruppe A", AuswaertsPlatzhalterName = "2. Gruppe B" },
            new() { PaarungsNummer = 2, HeimPlatzhalterName = "Sieger Gruppe B", AuswaertsPlatzhalterName = "2. Gruppe A" },
        };
        var service = ServiceFuer(ctx);

        await service.GenerierenAusAuslosungAsync(hfRundeId, ergebnisse);

        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == hfRundeId).ToList();
        spiele.Should().HaveCount(2);
        spiele.Should().OnlyContain(s => s.HeimMannschaftId == null && s.AuswaertsMannschaftId == null);
        spiele.Select(s => s.HeimPlatzhalterName).Should().Contain(["Sieger Gruppe A", "Sieger Gruppe B"]);
    }

    // ── Tests: Freitext-Platzhalter-Auflösung (Regressionstests für Issue #130) ─

    private static void GruppenphaseUndKoSetupAnlegen(
        TurnierverwaltungDbContext ctx, out int gruppenphaseRundeId, out int koRundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 4,
            RegelwerkId = 1, GruppenAnzahl = 2
        };
        var ko = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(gruppenphase, ko);

        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });

        // Gruppe A (Team A/B): Team A gewinnt → Rang 1. Gruppe B (Team C/D): Team C gewinnt → Rang 1.
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 1, SpielId = 1, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.Spiele.Add(new Spiel
        {
            Id = 2, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 2,
            HeimMannschaftId = 3, AuswaertsMannschaftId = 4, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 2, SpielId = 2, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });

        ctx.SaveChanges();
        gruppenphaseRundeId = gruppenphase.Id;
        koRundeId = ko.Id;
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_LoestGruppensiegerUndGruppenrangAuf()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out _, out int koRundeId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = koRundeId, Spielnummer = 10,
            HeimPlatzhalterName = "Sieger Gruppe A", AuswaertsPlatzhalterName = "2. Gruppe B",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var service = ServiceFuer(ctx);

        var anzahl = await service.PlatzhalterAufloesenAsync(koRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().Be(1);      // Sieger Gruppe A = Team A
        spiel.AuswaertsMannschaftId.Should().Be(4); // 2. Gruppe B = Team D
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_LoestKoSiegerUndVerliererAuf()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = "Sieger HF 1", AuswaertsPlatzhalterName = "Verlierer HF 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var service = ServiceFuer(ctx);

        var anzahl = await service.PlatzhalterAufloesenAsync(finaleRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 20);
        spiel.HeimMannschaftId.Should().Be(1);      // Sieger HF 1 = Team A
        spiel.AuswaertsMannschaftId.Should().Be(4); // Verlierer HF 2 = Team D
    }

    // ── Tests: regelbasierte Auflösung über QualifikationsRegel (Issue #130) ─

    [Fact]
    public async Task QualifikationsRegelQualifikantenAsync_GruppenphaseQuelle_LoestTabellenraengeAuf()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out int gruppenphaseRundeId, out int koRundeId);
        var gruppenphase = ctx.TurnierRunden.First(r => r.Id == gruppenphaseRundeId);
        var regel = new QualifikationsRegel
        {
            QuellRundeId = gruppenphaseRundeId, QuellRunde = gruppenphase, ZielRundeId = koRundeId,
            GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 2
        };
        var service = ServiceFuer(ctx);

        var qualifikanten = await service.QualifikationsRegelQualifikantenAsync(regel);

        qualifikanten[1].Should().Be(1); // Platz 1 Gruppe A = Team A
        qualifikanten[2].Should().Be(2); // Platz 2 Gruppe A = Team B
    }

    [Fact]
    public async Task QualifikationsRegelPlatzAufloesenAsync_LigaQuelle_LoestTabellenrangAuf()
    {
        await using var ctx = NeuerContext();
        LigaGrundSetupAnlegen(ctx, out int ligaRundeId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = ligaRundeId, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 1, SpielId = 1, HeimToreRegulaer = 3, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.Spiele.Add(new Spiel
        {
            Id = 2, TurnierInstanzId = 1, TurnierRundeId = ligaRundeId, Spielnummer = 2,
            HeimMannschaftId = 3, AuswaertsMannschaftId = 4, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 2, SpielId = 2, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.SaveChanges();

        var ligaRunde = ctx.TurnierRunden.First(r => r.Id == ligaRundeId);
        var regel = new QualifikationsRegel
        {
            QuellRundeId = ligaRundeId, QuellRunde = ligaRunde, ZielRundeId = 999,
            GruppeIndex = null, PlatzierungVon = 1, PlatzierungBis = 1
        };
        var service = ServiceFuer(ctx);

        var platz1 = await service.QualifikationsRegelPlatzAufloesenAsync(regel, 1);

        platz1.Should().Be(1); // Team A: 3 Punkte, Tordifferenz +3 → Rang 1
    }

    [Fact]
    public async Task QualifikationsRegelPlatzAufloesenAsync_KoRundeQuelle_LoestSiegerUndVerliererAuf()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        var hfRunde = ctx.TurnierRunden.First(r => r.Id == hfRundeId);
        var regelSieger = new QualifikationsRegel
        {
            QuellRundeId = hfRundeId, QuellRunde = hfRunde, ZielRundeId = finaleRundeId,
            GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1
        };
        var regelVerlierer = new QualifikationsRegel
        {
            QuellRundeId = hfRundeId, QuellRunde = hfRunde, ZielRundeId = finaleRundeId,
            GruppeIndex = 1, PlatzierungVon = 2, PlatzierungBis = 2
        };
        var service = ServiceFuer(ctx);

        var sieger = await service.QualifikationsRegelPlatzAufloesenAsync(regelSieger, 1);
        var verlierer = await service.QualifikationsRegelPlatzAufloesenAsync(regelVerlierer, 2);

        sieger.Should().Be(1);
        verlierer.Should().Be(2);
    }

    [Fact]
    public async Task QualifikationsRegelPlatzAufloesenAsync_NochNichtEntschieden_LiefertNull()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: null);
        var hfRunde = ctx.TurnierRunden.First(r => r.Id == hfRundeId);
        var regel = new QualifikationsRegel
        {
            QuellRundeId = hfRundeId, QuellRunde = hfRunde, ZielRundeId = finaleRundeId,
            GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1
        };
        var service = ServiceFuer(ctx);

        var sieger = await service.QualifikationsRegelPlatzAufloesenAsync(regel, 1);

        sieger.Should().BeNull();
    }

    // ── Tests: generalisierter Sync-Hook mit expliziten QualifikationsRegeln (Issue #130) ─

    private static void ZweiKoZweigeSetupAnlegen(
        TurnierverwaltungDbContext ctx, out int zweig1Id, out int zweig2Id, out int zielRundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var zweig1 = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Zweig 1", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1, RegelwerkId = 1
        };
        var zweig2 = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Zweig 2", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1, RegelwerkId = 1
        };
        // Zielrunde ist bewusst eine KoRunde (nicht Rundenart.Finale) - zeigt dass der Sync-Hook
        // jetzt jede Zielrunde mit zwei Eingangsteams bedient, nicht nur klassische Finalrunden.
        var ziel = new TurnierRunde
        {
            Id = 3, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1, RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(zweig1, zweig2, ziel);

        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });

        ctx.QualifikationsRegeln.AddRange(
            new QualifikationsRegel { QuellRundeId = zweig1.Id, ZielRundeId = ziel.Id, GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1 },
            new QualifikationsRegel { QuellRundeId = zweig2.Id, ZielRundeId = ziel.Id, GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1 });

        ctx.SaveChanges();
        zweig1Id = zweig1.Id;
        zweig2Id = zweig2.Id;
        zielRundeId = ziel.Id;
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_RegelbasiertMitZweiUnabhaengigenKoZweigen_GeneriertZielrunde()
    {
        await using var ctx = NeuerContext();
        ZweiKoZweigeSetupAnlegen(ctx, out int zweig1Id, out int zweig2Id, out int zielRundeId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = zweig1Id, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 1, SpielId = 1, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = 1 });
        ctx.Spiele.Add(new Spiel
        {
            Id = 2, TurnierInstanzId = 1, TurnierRundeId = zweig2Id, Spielnummer = 2,
            HeimMannschaftId = 3, AuswaertsMannschaftId = 4, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 2, SpielId = 2, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = 3 });
        ctx.SaveChanges();
        var service = ServiceFuer(ctx);

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        var zielSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == zielRundeId).ToList();
        zielSpiele.Should().HaveCount(1);
        var teilnehmer = new[] { zielSpiele[0].HeimMannschaftId, zielSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 1, 3 });
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_RegelbasiertNurEinZweigEntschieden_GeneriertNicht()
    {
        await using var ctx = NeuerContext();
        ZweiKoZweigeSetupAnlegen(ctx, out int zweig1Id, out int zweig2Id, out int zielRundeId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = zweig1Id, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 1, SpielId = 1, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = 1 });
        ctx.Spiele.Add(new Spiel
        {
            Id = 2, TurnierInstanzId = 1, TurnierRundeId = zweig2Id, Spielnummer = 2,
            HeimMannschaftId = 3, AuswaertsMannschaftId = 4, Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var service = ServiceFuer(ctx);

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == zielRundeId).Should().BeEmpty();
    }
}
