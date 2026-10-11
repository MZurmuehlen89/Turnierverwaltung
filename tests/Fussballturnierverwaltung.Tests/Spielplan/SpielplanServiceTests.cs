using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
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

    /// <summary>Legt Regelwerk (mit Entscheidungsverfahren, für KO/Finale Pflicht), Halbfinale- und Finale-Runde
    /// sowie 4 Mannschaften an. Halbfinale-Spiele werden NICHT angelegt (Aufgabe der einzelnen Tests).</summary>
    private static void GrundSetupAnlegen(
        TurnierverwaltungDbContext ctx, out int hfRundeId, out int finaleRundeId, bool hfMitRueckspiel = false,
        string hfName = "Halbfinale")
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var hf = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = hfName, Reihenfolge = 1,
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
        var dienste = new SpielplanDienste(ctx);

        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

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
        var dienste = new SpielplanDienste(ctx);

        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

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
        var dienste = new SpielplanDienste(ctx);

        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

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
        var dienste = new SpielplanDienste(ctx);

        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

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
        var dienste = new SpielplanDienste(ctx);
        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);
        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().HaveCount(1, "Vorbedingung: Finale wurde generiert");

        // Ergebnis eines Halbfinalspiels zurückgesetzt (z.B. "Ergebnis löschen")
        var spiel2Ergebnis = ctx.SpielErgebnisse.First(e => e.SpielId == 2);
        ctx.SpielErgebnisse.Remove(spiel2Ergebnis);
        var spiel2 = ctx.Spiele.First(s => s.Id == 2);
        spiel2.Status = SpielStatus.Geplant;
        ctx.SaveChanges();

        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

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
        var dienste = new SpielplanDienste(ctx);
        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

        // Halbfinale neu simuliert - diesmal gewinnen Team B und Team D statt Team A und Team C
        var erg1 = ctx.SpielErgebnisse.First(e => e.SpielId == 1);
        erg1.SiegerMannschaftId = 2; erg1.HeimToreRegulaer = 0; erg1.AuswaertsToreRegulaer = 1;
        var erg2 = ctx.SpielErgebnisse.First(e => e.SpielId == 2);
        erg2.SiegerMannschaftId = 4; erg2.HeimToreRegulaer = 0; erg2.AuswaertsToreRegulaer = 1;
        ctx.SaveChanges();

        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

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
        var dienste = new SpielplanDienste(ctx);
        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

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

        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1, "ein bereits gespieltes Finale darf nie automatisch verändert werden");
        finaleSpiele[0].Id.Should().Be(finaleSpielId);
        var teilnehmer = new[] { finaleSpiele[0].HeimMannschaftId, finaleSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 1, 3 }, "die ursprüngliche, bereits gespielte Paarung bleibt erhalten");
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_GeneriertSpielUmPlatzDreiMitVerlierern_NichtSiegern()
    {
        // Regressionstest für Issue #199: die Heuristik lieferte für JEDE Zielrunde mit zwei
        // Eingangsteams die Sieger der Vorrunde — "Spiel um Platz 3" muss stattdessen die Verlierer
        // bekommen.
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        var spielUmPlatzDrei = new TurnierRunde
        {
            Id = 3, TurnierInstanzId = 1, Name = "Spiel um Platz 3", Reihenfolge = 2,
            // Reihenfolge bewusst zwischen Halbfinale (1) und Finale, wie in den Turniervorlagen üblich.
            Rundenart = Rundenart.SpielUmPlatzDrei, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.Add(spielUmPlatzDrei);
        ctx.TurnierRunden.First(r => r.Id == finaleRundeId).Reihenfolge = 3;
        ctx.SaveChanges();
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var dienste = new SpielplanDienste(ctx);

        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1);
        new[] { finaleSpiele[0].HeimMannschaftId, finaleSpiele[0].AuswaertsMannschaftId }
            .Should().BeEquivalentTo(new int?[] { 1, 3 }, "Finale bekommt die Sieger (Team A, Team C)");

        var platzDreiSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == spielUmPlatzDrei.Id).ToList();
        platzDreiSpiele.Should().HaveCount(1);
        new[] { platzDreiSpiele[0].HeimMannschaftId, platzDreiSpiele[0].AuswaertsMannschaftId }
            .Should().BeEquivalentTo(new int?[] { 2, 4 }, "Spiel um Platz 3 bekommt die Verlierer (Team B, Team D), nicht die Sieger");
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
        var dienste = new SpielplanDienste(ctx);

        await dienste.Spielplan.GenerierenAusAuslosungAsync(rundeId, ergebnisse);

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
        var dienste = new SpielplanDienste(ctx);

        await dienste.Spielplan.GenerierenAusAuslosungAsync(runde.Id, ergebnisse);

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
        var dienste = new SpielplanDienste(ctx);

        await dienste.Spielplan.GenerierenAusAuslosungAsync(hfRundeId, ergebnisse);

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
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().Be(1);      // Sieger Gruppe A = Team A
        spiel.AuswaertsMannschaftId.Should().Be(4); // 2. Gruppe B = Team D
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_LoestNurAufloesbareSeiteAufWennAndereSeiteNochOffenIst()
    {
        // Issue #206 (TV-Präsentation-Stil): Gruppe A ist entschieden (einziges Spiel der 2er-Gruppe
        // gespielt), Gruppe B ist noch offen (Spiel C-D noch nicht gespielt). Das Halbfinale soll
        // trotzdem die bereits bekannte Heim-Seite ("Sieger Gruppe A") schreiben, während die
        // Auswärts-Seite ("2. Gruppe B") als Platzhaltertext stehen bleibt, statt beide Seiten erst
        // gemeinsam zu schreiben, wenn auch Gruppe B entschieden ist.
        await using var ctx = NeuerContext();
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

        // Gruppe A (Team A/B): entschieden, Team A gewinnt.
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 1, SpielId = 1, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });

        // Gruppe B (Team C/D): noch offen, Spiel noch nicht gespielt.
        ctx.Spiele.Add(new Spiel
        {
            Id = 2, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 2,
            HeimMannschaftId = 3, AuswaertsMannschaftId = 4, Status = SpielStatus.Geplant
        });

        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = ko.Id, Spielnummer = 10,
            HeimPlatzhalterName = "Sieger Gruppe A", AuswaertsPlatzhalterName = "2. Gruppe B",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(ko.Id);

        anzahl.Should().Be(1, "das Spiel wurde teilweise aufgelöst und zählt als Fortschritt");
        var spiel = ctx.Spiele.AsNoTracking().First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().Be(1, "Sieger Gruppe A = Team A, bereits entschieden");
        spiel.HeimPlatzhalterName.Should().BeNull();
        spiel.AuswaertsMannschaftId.Should().BeNull("Gruppe B ist noch offen");
        spiel.AuswaertsPlatzhalterName.Should().Be("2. Gruppe B", "Platzhaltertext bleibt stehen, bis Gruppe B entschieden ist");

        // Gruppe B jetzt abschließen (Team C gewinnt) — ein zweiter Aufruf muss die restliche Seite
        // nachziehen, ohne die bereits aufgelöste Heim-Seite anzufassen.
        ctx.Spiele.First(s => s.Id == 2).Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 2, SpielId = 2, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        await ctx.SaveChangesAsync();

        var anzahlNachher = await dienste.Aufloesung.RundeAufloesenAsync(ko.Id);

        anzahlNachher.Should().Be(1, "die zweite, nachziehende Auflösung zählt erneut als Fortschritt");
        var spielNachher = ctx.Spiele.AsNoTracking().First(s => s.Id == 10);
        spielNachher.HeimMannschaftId.Should().Be(1, "bereits vorher aufgelöste Heim-Seite bleibt unverändert stehen");
        spielNachher.AuswaertsMannschaftId.Should().Be(4, "2. Gruppe B = Team D, jetzt auch entschieden");
        spielNachher.AuswaertsPlatzhalterName.Should().BeNull();
    }

    [Fact]
    public async Task PlatzhalterVorschauAsync_LiefertOptimistischenNamenOhneZuPersistieren()
    {
        // Issue #208: Team A führt Gruppe A aktuell auf Rang 2 (3 Punkte, alle Spiele gespielt),
        // Team C ist uneinholbar Erster (7 Punkte). Rang 2 ist NICHT SicherPositioniert (ein
        // Unentschieden im offenen Spiel B-D hält Team A auf Rang 2, ein Sieg verdrängt es auf
        // Rang 3) — die reguläre Auflösung darf daher nur die Auswaerts-Seite (sicherer Rang 1)
        // schreiben, nicht die Heim-Seite (unsicherer Rang 2). Die Vorschau soll für beide Seiten
        // den aktuell führenden Namen liefern, ohne zusätzlich etwas zu persistieren.
        await using var ctx = NeuerContext();
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 4,
            RegelwerkId = 1, GruppenAnzahl = 1
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

        void Spiel(int id, int heim, int auswaerts, int? heimTore, int? auswaertsTore, int? sieger = null)
        {
            ctx.Spiele.Add(new Spiel
            {
                Id = id, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = id,
                HeimMannschaftId = heim, AuswaertsMannschaftId = auswaerts,
                Status = heimTore.HasValue ? SpielStatus.Abgeschlossen : SpielStatus.Geplant
            });
            if (heimTore.HasValue && auswaertsTore.HasValue)
                ctx.SpielErgebnisse.Add(new SpielErgebnis
                {
                    Id = id, SpielId = id, HeimToreRegulaer = heimTore.Value, AuswaertsToreRegulaer = auswaertsTore.Value,
                    EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = sieger
                });
        }
        Spiel(1, 1, 2, 1, 1);                    // A - B 1:1
        Spiel(2, 3, 4, 2, 1, sieger: 3);          // C schlägt D
        Spiel(3, 4, 1, 0, 0);                     // D - A 0:0
        Spiel(4, 2, 3, 0, 1, sieger: 3);          // C schlägt B
        Spiel(5, 1, 3, 1, 1);                     // A - C 1:1
        Spiel(6, 4, 2, null, null);                // D - B noch offen
        await ctx.SaveChangesAsync();

        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = ko.Id, Spielnummer = 10,
            HeimPlatzhalterName = "2. Gruppe A", AuswaertsPlatzhalterName = "Sieger Gruppe A",
            Status = SpielStatus.Geplant
        });
        await ctx.SaveChangesAsync();
        var dienste = new SpielplanDienste(ctx);

        var regulaer = await dienste.Aufloesung.RundeAufloesenAsync(ko.Id);
        regulaer.Should().Be(1, "nur die Auswaerts-Seite (Sieger Gruppe A, sicher positioniert) darf regulär aufgelöst werden");
        var spielNachRegulaer = ctx.Spiele.AsNoTracking().First(s => s.Id == 10);
        spielNachRegulaer.HeimMannschaftId.Should().BeNull(
            "Rang 2 ist noch nicht SicherPositioniert, die reguläre Auflösung darf diese Seite nicht schreiben");
        spielNachRegulaer.AuswaertsMannschaftId.Should().Be(3, "Team C ist uneinholbar Erster und darf regulär aufgelöst werden");

        var vorschau = await dienste.Aufloesung.VorschauAsync(ko.Id);

        vorschau.Should().ContainKey(10);
        vorschau[10].Heim.Should().Be("Team A", "Team A führt Rang 2 aktuell, auch wenn das noch nicht sicher ist");
        vorschau[10].Auswaerts.Should().BeNull(
            "Auswaerts ist bereits regulär (persistent) aufgelöst, dafür liefert die Vorschau keinen Zusatzwert mehr");
        ctx.Spiele.AsNoTracking().First(s => s.Id == 10).HeimMannschaftId.Should().BeNull(
            "die Vorschau darf weiterhin nichts in die Datenbank geschrieben haben");
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_LoestGruppensiegerErstAufWennRangSicherPositioniertIst()
    {
        // 3-Team-Gruppe (Top 2 qualifizieren): Team C ist bereits abgehaengt, aber das direkte Duell A vs. B
        // ist noch offen und entscheidet erst, wer Rang 1 ("Sieger Gruppe A") wird (Issue #202 - der aktuelle
        // Tabellenrang allein reicht nicht, der Rang muss SicherPositioniert sein).
        await using var ctx = NeuerContext();
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 3, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1, GruppenAnzahl = 1
        };
        var ko = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(gruppenphase, ko);

        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 });

        // A schlägt C 5:0, B schlägt C 5:0 (C rechnerisch raus) - A gegen B (Sieger = Gruppenerster) noch offen.
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 3, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 1, SpielId = 1, HeimToreRegulaer = 5, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.Spiele.Add(new Spiel
        {
            Id = 2, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 2,
            HeimMannschaftId = 2, AuswaertsMannschaftId = 3, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 2, SpielId = 2, HeimToreRegulaer = 5, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        var abSpiel = new Spiel
        {
            Id = 3, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 3,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Geplant
        };
        ctx.Spiele.Add(abSpiel);

        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = ko.Id, Spielnummer = 10,
            HeimPlatzhalterName = "Sieger Gruppe A", AuswaertsMannschaftId = 3,
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahlVorher = await dienste.Aufloesung.RundeAufloesenAsync(ko.Id);

        anzahlVorher.Should().Be(0, "wer Gruppenerster wird, entscheidet erst das noch offene Duell A vs. B");
        ctx.Spiele.First(s => s.Id == 10).HeimMannschaftId.Should().BeNull();

        // Jetzt A vs B abschließen (A gewinnt) - Rang 1 ist jetzt final.
        abSpiel.Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 3, SpielId = 3, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        await ctx.SaveChangesAsync();

        var anzahlNachher = await dienste.Aufloesung.RundeAufloesenAsync(ko.Id);

        anzahlNachher.Should().Be(1);
        ctx.Spiele.First(s => s.Id == 10).HeimMannschaftId.Should().Be(1, "Sieger Gruppe A = Team A");
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_ReproduziertRealeGruppeAusManuellemTest()
    {
        // Reproduziert 1:1 die reale Gruppe "Gruppenphase 2" / "Gruppe A" aus dem manuellen #202-Test
        // (Arsenal/Dortmund/Polen/PSG, echtes FIFA-Regelwerk mit DirekterVergleich-Kaskade). Polen hat alle
        // 3 Spiele gespielt (9 Punkte), PSG (3 Punkte, 2 Spiele) kann selbst mit einem Sieg gegen Dortmund
        // maximal 6 Punkte erreichen - Polen ist damit rein rechnerisch bereits als Gruppenerster fixiert.
        await using var ctx = NeuerContext();
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });

        var regelSet = new RankingRegelSet { Id = 1, Name = "FIFA" };
        var regeln = new[]
        {
            new RankingRegel { Id = 1, Regelart = RankingRegelArt.Punkte,                       Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 2, Regelart = RankingRegelArt.Tordifferenz,                  Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 3, Regelart = RankingRegelArt.ErzielteTore,                  Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 4, Regelart = RankingRegelArt.DirekterVergleichPunkte,       Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 5, Regelart = RankingRegelArt.DirekterVergleichTordifferenz, Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 6, Regelart = RankingRegelArt.DirekterVergleichErzielteTore, Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 7, Regelart = RankingRegelArt.DirekterVergleichAuswaertstore,Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 8, Regelart = RankingRegelArt.FairplayWertung,               Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 9, Regelart = RankingRegelArt.Losentscheid,                  Sortierrichtung = Sortierrichtung.Aufsteigend },
        };
        var eintraege = regeln.Select((r, i) => new RankingRegelSetEintrag
            { Id = i + 1, RankingRegelSetId = 1, RankingRegelId = r.Id, Reihenfolge = i + 1 }).ToArray();
        ctx.RankingRegelSets.Add(regelSet);
        ctx.RankingRegeln.AddRange(regeln);
        ctx.RankingRegelSetEintraege.AddRange(eintraege);

        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "FIFA-Regelwerk", RankingRegelSetId = 1 });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase 2", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1, GruppenAnzahl = 1
        };
        var halbfinale = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(gruppenphase, halbfinale);

        const int arsenal = 67, dortmund = 61, polen = 15, psg = 69;
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = arsenal,  Name = "Arsenal FC",           Kurzname = "ARS", LandId = 1 },
            new Mannschaft { Id = dortmund, Name = "Borussia Dortmund",    Kurzname = "BVB", LandId = 1 },
            new Mannschaft { Id = polen,    Name = "Polen",                Kurzname = "POL", LandId = 1 },
            new Mannschaft { Id = psg,      Name = "Paris Saint-Germain",  Kurzname = "PSG", LandId = 1 });
        ctx.SaveChanges();

        void Ergebnis(int id, int spielnr, int heim, int auswaerts, int heimTore, int auswaertsTore)
        {
            ctx.Spiele.Add(new Spiel
            {
                Id = id, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = spielnr,
                HeimMannschaftId = heim, AuswaertsMannschaftId = auswaerts, Status = SpielStatus.Abgeschlossen
            });
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = id, SpielId = id, HeimToreRegulaer = heimTore, AuswaertsToreRegulaer = auswaertsTore,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit
            });
        }

        Ergebnis(1, 49, arsenal, dortmund, 1, 1); // Arsenal - Dortmund 1:1
        Ergebnis(2, 50, polen, psg, 2, 1);        // Polen schlägt PSG 2:1
        Ergebnis(3, 53, psg, arsenal, 3, 1);      // PSG schlägt Arsenal 3:1
        Ergebnis(4, 54, dortmund, polen, 1, 2);   // Polen schlägt Dortmund 1:2
        Ergebnis(5, 57, arsenal, polen, 1, 3);    // Polen schlägt Arsenal 1:3
        ctx.Spiele.Add(new Spiel                  // PSG - Dortmund noch offen
        {
            Id = 6, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = 58,
            HeimMannschaftId = psg, AuswaertsMannschaftId = dortmund, Status = SpielStatus.Geplant
        });

        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = halbfinale.Id, Spielnummer = 61,
            HeimPlatzhalterName = "1. Gruppe A", AuswaertsMannschaftId = arsenal, Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);
        var tabellenService = new TabellenService(new TurnierRundeRepository(ctx), new SpielRepository(ctx), new RegelwerkRepository(ctx));

        var gruppen = await tabellenService.GruppenTabellenBerechnenAsync(gruppenphase.Id);
        var polenEintrag = gruppen[0].Tabelle.First(e => e.Mannschaft.Id == polen);
        polenEintrag.Punkte.Should().Be(9);
        polenEintrag.Rang.Should().Be(1);
        polenEintrag.Qualifikationsstatus.Should().Be(Qualifikationsstatus.SicherQualifiziert);
        polenEintrag.SicherPositioniert.Should().BeTrue(
            "PSG kann mit maximal 6 Punkten Polens 9 Punkte nicht mehr einholen - unabhängig von der Tordifferenz");

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(halbfinale.Id);

        anzahl.Should().Be(1, "Polen ist bereits als Gruppenerster fixiert und sollte übernommen werden");
        ctx.Spiele.First(s => s.Id == 10).HeimMannschaftId.Should().Be(polen, "1. Gruppe A = Polen");
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
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 20);
        spiel.HeimMannschaftId.Should().Be(1);      // Sieger HF 1 = Team A
        spiel.AuswaertsMannschaftId.Should().Be(4); // Verlierer HF 2 = Team D
    }

    /// <summary>Regressionstest für einen Nutzerfund: keine mitgelieferte Turniervorlage benennt ihre K.o.-
    /// Runden mit einer Kurzform ("VF"/"HF"/"AF") — sie heißen immer ausgeschrieben ("Viertelfinale",
    /// "Halbfinale", ...). <see cref="TeilnehmerPoolHilfsmittel"/> (WinUi) bzw. dessen WPF-Pendant erzeugen
    /// Platzhaltertext ausschließlich aus dem ECHTEN Rundennamen ("Sieger Halbfinale 1"), nie aus einer
    /// Kurzform — der obige Test <see cref="PlatzhalterAufloesenAsync_LoestKoSiegerUndVerliererAuf"/> verdeckte
    /// das, weil er den Platzhaltertext von Hand mit der Kurzform "HF" schreibt statt mit dem echten
    /// Rundennamen "Halbfinale" der Testrunde. In der Praxis lösten sich K.o.-Platzhalter dadurch nie auf,
    /// weder einzeln noch simuliert, weder produktiv noch in der Vorschau.</summary>
    [Fact]
    public async Task PlatzhalterAufloesenAsync_LoestKoSiegerAufMitAusgeschriebenemRundennamen()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = "Sieger Halbfinale 1", AuswaertsPlatzhalterName = "Verlierer Halbfinale 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 20);
        spiel.HeimMannschaftId.Should().Be(1);      // Sieger Halbfinale 1 = Team A
        spiel.AuswaertsMannschaftId.Should().Be(4); // Verlierer Halbfinale 2 = Team D
    }

    /// <summary>Regressionstest Issue #368: Rundennamen mit Leerzeichen/Bindestrich ("Playoff-Runde" der
    /// UEFA-Wettbewerbe, "1. Runde" im DFB-Pokal) scheiterten an der K.o.-Platzhalter-Regex — die Sieger
    /// wurden nie in die Folgerunde übernommen.</summary>
    [Theory]
    [InlineData("Playoff-Runde")]
    [InlineData("1. Runde")]
    public async Task PlatzhalterAufloesenAsync_LoestKoSiegerAufBeiRundennamenMitLeerzeichenOderBindestrich(string rundenName)
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId, hfName: rundenName);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = $"Sieger {rundenName} 1", AuswaertsPlatzhalterName = $"Verlierer {rundenName} 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 20);
        spiel.HeimMannschaftId.Should().Be(1);
        spiel.AuswaertsMannschaftId.Should().Be(4);
    }

    // ── Tests: K.o.-Quellverknüpfung (Issue #241) ──────────────────────────

    /// <summary>Regressionstest Issue #368: ohne Quellverknüpfung fiel der Turnierbaum auf die sequentielle
    /// Alt-Zuordnung zurück und verband z.B. die CL-Playoff-Runde falsch mit dem Achtelfinale.</summary>
    [Theory]
    [InlineData("Playoff-Runde")]
    [InlineData("1. Runde")]
    public async Task SpielQuellenAlleTransitionsVerknuepfenAsync_SetztQuellIdsBeiRundennamenMitLeerzeichenOderBindestrich(string rundenName)
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId, hfName: rundenName);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: null);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: null);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = $"Sieger {rundenName} 2", AuswaertsPlatzhalterName = $"Verlierer {rundenName} 1",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        await dienste.Quellverknuepfung.AlleRundenVerknuepfenAsync(1);

        var finale = ctx.Spiele.First(s => s.Id == 20);
        finale.HeimQuellSpielId.Should().Be(2);
        finale.HeimQuellErgebnis.Should().Be(SpielQuellErgebnisTyp.Sieger);
        finale.AuswaertsQuellSpielId.Should().Be(1);
        finale.AuswaertsQuellErgebnis.Should().Be(SpielQuellErgebnisTyp.Verlierer);
    }

    [Fact]
    public async Task SpielQuellenAlleTransitionsVerknuepfenAsync_SetztQuellIdsFuerSiegerUndVerlierer()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: null);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: null);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = "Sieger HF 1", AuswaertsPlatzhalterName = "Verlierer HF 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        // Verknüpfung ist rein strukturell (Text-basiert) und braucht kein Ergebnis der HF-Spiele.
        await dienste.Quellverknuepfung.AlleRundenVerknuepfenAsync(1);

        var finale = ctx.Spiele.First(s => s.Id == 20);
        finale.HeimQuellSpielId.Should().Be(1);
        finale.HeimQuellErgebnis.Should().Be(SpielQuellErgebnisTyp.Sieger);
        finale.AuswaertsQuellSpielId.Should().Be(2);
        finale.AuswaertsQuellErgebnis.Should().Be(SpielQuellErgebnisTyp.Verlierer);
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_NutztIdFastpathOhneRegex_WennQuellIdBereitsGesetzt()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        // Kein (bzw. ein zur Regex nicht passender) Platzhaltertext — die ID-Felder sind die einzige
        // Quelle, aus der aufgelöst werden kann.
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimQuellSpielId = 1, HeimQuellErgebnis = SpielQuellErgebnisTyp.Sieger,
            AuswaertsQuellSpielId = 2, AuswaertsQuellErgebnis = SpielQuellErgebnisTyp.Verlierer,
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 20);
        spiel.HeimMannschaftId.Should().Be(1);      // Sieger HF 1 = Team A
        spiel.AuswaertsMannschaftId.Should().Be(4); // Verlierer HF 2 = Team D
    }

    // ── Tests: regelbasierte Auflösung über Uebergangsregel (Issue #130) ─

    [Fact]
    public async Task UebergangsregelQualifikantenAsync_GruppenphaseQuelle_LoestTabellenraengeAuf()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out int gruppenphaseRundeId, out int koRundeId);
        var gruppenphase = ctx.TurnierRunden.First(r => r.Id == gruppenphaseRundeId);
        var regel = new Uebergangsregel
        {
            QuellRundeId = gruppenphaseRundeId, QuellRunde = gruppenphase, ZielRundeId = koRundeId,
            GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 2
        };
        var dienste = new SpielplanDienste(ctx);

        var qualifikanten = await dienste.Aufloesung.UebergangsregelQualifikantenAsync(regel);

        qualifikanten[1].Should().Be(1); // Platz 1 Gruppe A = Team A
        qualifikanten[2].Should().Be(2); // Platz 2 Gruppe A = Team B
    }

    [Fact]
    public async Task UebergangsregelPlatzAufloesenAsync_LigaQuelle_LoestTabellenrangAuf()
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
        var regel = new Uebergangsregel
        {
            QuellRundeId = ligaRundeId, QuellRunde = ligaRunde, ZielRundeId = 999,
            GruppeIndex = null, PlatzierungVon = 1, PlatzierungBis = 1
        };
        var dienste = new SpielplanDienste(ctx);

        var platz1 = await dienste.Aufloesung.UebergangsregelPlatzAufloesenAsync(regel, 1);

        platz1.Should().Be(1); // Team A: 3 Punkte, Tordifferenz +3 → Rang 1
    }

    private static void SchweizerSystemGrundSetupAnlegen(TurnierverwaltungDbContext ctx, out int rundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        var runde = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Ligaphase", Reihenfolge = 1,
            Rundenart = Rundenart.SchweizerSystem, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 4,
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
    public async Task UebergangsregelPlatzAufloesenAsync_SchweizerSystemQuelle_LoestTabellenrangAuf()
    {
        // Issue #25-Nachlieferung: Ligaphase im CL-2024+-Format nutzt SchweizerSystem statt Liga
        // (Teilrunde, nicht volle Punktrunde) — Rangauflösung muss denselben Tabellenweg gehen wie Liga.
        await using var ctx = NeuerContext();
        SchweizerSystemGrundSetupAnlegen(ctx, out int ligaphaseRundeId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = ligaphaseRundeId, Spielnummer = 1, Spieltag = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 1, SpielId = 1, HeimToreRegulaer = 3, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.Spiele.Add(new Spiel
        {
            Id = 2, TurnierInstanzId = 1, TurnierRundeId = ligaphaseRundeId, Spielnummer = 2, Spieltag = 1,
            HeimMannschaftId = 3, AuswaertsMannschaftId = 4, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 2, SpielId = 2, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.SaveChanges();

        var ligaphaseRunde = ctx.TurnierRunden.First(r => r.Id == ligaphaseRundeId);
        // Issue #343: fest aufgelöst wird nur ein gesicherter Rang — mit nur einem Spieltag ist die Runde komplett.
        ligaphaseRunde.KonfigurationJson = "{\"anzahlSpieltage\":1}";
        ctx.SaveChanges();
        var regel = new Uebergangsregel
        {
            QuellRundeId = ligaphaseRundeId, QuellRunde = ligaphaseRunde, ZielRundeId = 999,
            GruppeIndex = null, PlatzierungVon = 1, PlatzierungBis = 1
        };
        var dienste = new SpielplanDienste(ctx);

        var platz1 = await dienste.Aufloesung.UebergangsregelPlatzAufloesenAsync(regel, 1);

        platz1.Should().Be(1); // Team A: 3 Punkte, Tordifferenz +3 → Rang 1
    }

    // ── Tests: Schweizer-Rundenfolge über mehrere Spieltage (Issue #329 Teil E) ─

    private static async Task<Auslosung> SchweizerAuslosungAnlegen(
        TurnierverwaltungDbContext ctx, int rundeId, params (int? Heim, int? Auswaerts)[] paarungen)
    {
        var auslosung = new Auslosung
        {
            TurnierInstanzId = 1, TurnierRundeId = rundeId, Name = "Auslosung",
            Auslosungsart = Auslosungsart.Manuell, Status = AuslosungsStatus.Abgeschlossen
        };
        ctx.Auslosungen.Add(auslosung);
        await ctx.SaveChangesAsync();

        int nr = 1;
        foreach (var (heim, auswaerts) in paarungen)
            ctx.AuslosungsErgebnisse.Add(new AuslosungsErgebnis
            {
                AuslosungId = auslosung.Id, PaarungsNummer = nr++,
                HeimMannschaftId = heim, AuswaertsMannschaftId = auswaerts, IstFreilos = auswaerts is null
            });
        await ctx.SaveChangesAsync();
        return auslosung;
    }

    [Fact]
    public async Task SchweizerNaechsteRundeGenerierenAsync_GeneriertFolgeSpieltagAusAktuellerTabelle()
    {
        await using var ctx = NeuerContext();
        SchweizerSystemGrundSetupAnlegen(ctx, out int rundeId); // 4 Teams
        var auslosung = await SchweizerAuslosungAnlegen(ctx, rundeId, (1, 2), (3, 4));
        var dienste = new SpielplanDienste(ctx);
        await dienste.Spielplan.GenerierenAusAuslosungAsync(rundeId, [.. auslosung.Ergebnisse]);

        var spieltag1 = ctx.Spiele.Where(s => s.TurnierRundeId == rundeId).ToList();
        spieltag1.Should().HaveCount(2);
        foreach (var s in spieltag1)
        {
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                SpielId = s.Id, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit
            });
            s.Status = SpielStatus.Abgeschlossen;
        }
        await ctx.SaveChangesAsync();

        await dienste.Spielplan.SchweizerNaechsteRundeGenerierenAsync(rundeId);

        var alleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == rundeId).ToList();
        alleSpiele.Should().HaveCount(4);
        alleSpiele.Where(s => s.Spieltag == 2).Should().HaveCount(2);
    }

    [Fact]
    public async Task SchweizerNaechsteRundeGenerierenAsync_FreilosTeamOhneVorherigesSpiel_WirdWiederEingeplant()
    {
        // Regressionstest: Team 5 sitzt Spieltag 1 aus (Freilos, kein Spiel-Datensatz) und hat zum Zeitpunkt
        // der Spieltag-2-Generierung noch KEIN gespieltes Spiel — TabellenService allein würde es daher aus
        // der Teamliste verlieren (Tabelle baut sich nur aus gespielten Spielen auf). Die Teamliste muss aus
        // der Auslosung kommen, damit Team 5 hier wieder eingeplant wird.
        await using var ctx = NeuerContext();
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        var runde = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Ligaphase", Reihenfolge = 1,
            Rundenart = Rundenart.SchweizerSystem, EingangsMannschaftsanzahl = 5, AusgangsMannschaftsanzahl = 5,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.Add(runde);
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 },
            new Mannschaft { Id = 5, Name = "Team E", Kurzname = "TE", LandId = 1 });
        await ctx.SaveChangesAsync();
        int rundeId = runde.Id;

        var auslosung = await SchweizerAuslosungAnlegen(ctx, rundeId, (1, 2), (3, 4), (5, null));
        var dienste = new SpielplanDienste(ctx);
        await dienste.Spielplan.GenerierenAusAuslosungAsync(rundeId, [.. auslosung.Ergebnisse]);

        var spieltag1 = ctx.Spiele.Where(s => s.TurnierRundeId == rundeId).ToList();
        spieltag1.Should().HaveCount(2, "bei 5 Teams sitzt eines aus, ohne eigenen Spiel-Datensatz");
        spieltag1.SelectMany(s => new[] { s.HeimMannschaftId, s.AuswaertsMannschaftId })
            .Should().NotContain(5, "Team 5 hat laut Freilos-Auswahl in Spieltag 1 ausgesetzt");

        foreach (var s in spieltag1)
        {
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                SpielId = s.Id, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit
            });
            s.Status = SpielStatus.Abgeschlossen;
        }
        await ctx.SaveChangesAsync();

        await dienste.Spielplan.SchweizerNaechsteRundeGenerierenAsync(rundeId);

        var spieltag2 = ctx.Spiele.Where(s => s.TurnierRundeId == rundeId && s.Spieltag == 2).ToList();
        spieltag2.Should().HaveCount(2);
        spieltag2.SelectMany(s => new[] { s.HeimMannschaftId, s.AuswaertsMannschaftId })
            .Should().Contain(5, "Team 5 muss nach seinem Freilos in Spieltag 2 wieder eingeplant werden");
    }

    [Fact]
    public async Task UebergangsregelPlatzAufloesenAsync_KoRundeQuelle_LoestSiegerUndVerliererAuf()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        var hfRunde = ctx.TurnierRunden.First(r => r.Id == hfRundeId);
        var regelSieger = new Uebergangsregel
        {
            QuellRundeId = hfRundeId, QuellRunde = hfRunde, ZielRundeId = finaleRundeId,
            GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1
        };
        var regelVerlierer = new Uebergangsregel
        {
            QuellRundeId = hfRundeId, QuellRunde = hfRunde, ZielRundeId = finaleRundeId,
            GruppeIndex = 1, PlatzierungVon = 2, PlatzierungBis = 2
        };
        var dienste = new SpielplanDienste(ctx);

        var sieger = await dienste.Aufloesung.UebergangsregelPlatzAufloesenAsync(regelSieger, 1);
        var verlierer = await dienste.Aufloesung.UebergangsregelPlatzAufloesenAsync(regelVerlierer, 2);

        sieger.Should().Be(1);
        verlierer.Should().Be(2);
    }

    [Fact]
    public async Task UebergangsregelPlatzAufloesenAsync_NochNichtEntschieden_LiefertNull()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: null);
        var hfRunde = ctx.TurnierRunden.First(r => r.Id == hfRundeId);
        var regel = new Uebergangsregel
        {
            QuellRundeId = hfRundeId, QuellRunde = hfRunde, ZielRundeId = finaleRundeId,
            GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1
        };
        var dienste = new SpielplanDienste(ctx);

        var sieger = await dienste.Aufloesung.UebergangsregelPlatzAufloesenAsync(regel, 1);

        sieger.Should().BeNull();
    }

    // ── Tests: generalisierter Sync-Hook mit expliziten Uebergangsregeln (Issue #130) ─

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

        ctx.Uebergangsregeln.AddRange(
            new Uebergangsregel { QuellRundeId = zweig1.Id, ZielRundeId = ziel.Id, GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1 },
            new Uebergangsregel { QuellRundeId = zweig2.Id, ZielRundeId = ziel.Id, GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1 });

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
        var dienste = new SpielplanDienste(ctx);

        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

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
        var dienste = new SpielplanDienste(ctx);

        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == zielRundeId).Should().BeEmpty();
    }

    // ── Tests: gruppenübergreifende Rangfolge-Regel (Issue #184) ────────────────

    private static void AnzahlBesteSetupAnlegen(
        TurnierverwaltungDbContext ctx, out int gruppenphaseId, out int zielRundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 9, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1, GruppenAnzahl = 3
        };
        var ziel = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 2,
            Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1, RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(gruppenphase, ziel);

        for (int i = 1; i <= 9; i++)
            ctx.Mannschaften.Add(new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 });

        ctx.Uebergangsregeln.Add(new Uebergangsregel
        {
            QuellRundeId = gruppenphase.Id, ZielRundeId = ziel.Id,
            GruppeIndex = null, PlatzierungVon = 3, PlatzierungBis = 3, AnzahlBeste = 2
        });

        ctx.SaveChanges();
        gruppenphaseId = gruppenphase.Id;
        zielRundeId = ziel.Id;
    }

    private static void AnzahlBesteSpieleHinzufuegen(TurnierverwaltungDbContext ctx, int rundeId)
    {
        // Drei Gruppen zu je drei Teams, Gruppendritte mit unterschiedlicher Tordifferenz (-3/-2/-1) -
        // identisches Szenario wie TabellenServiceTests.UebergreifendeRangfolgeBerechnenAsync_*.
        void Spiel(int id, int heim, int aus, int heimTore, int ausTore, int? sieger)
        {
            ctx.Spiele.Add(new Spiel
            {
                Id = id, TurnierInstanzId = 1, TurnierRundeId = rundeId, Spielnummer = id,
                HeimMannschaftId = heim, AuswaertsMannschaftId = aus, Status = SpielStatus.Abgeschlossen
            });
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = id, SpielId = id, HeimToreRegulaer = heimTore, AuswaertsToreRegulaer = ausTore,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = sieger
            });
        }

        Spiel(1, heim: 1, aus: 2, heimTore: 3, ausTore: 0, sieger: 1);
        Spiel(2, heim: 2, aus: 3, heimTore: 1, ausTore: 1, sieger: null); // Gruppe 1, 3. = Team 2 (GD -3)
        Spiel(3, heim: 4, aus: 5, heimTore: 2, ausTore: 0, sieger: 4);
        Spiel(4, heim: 5, aus: 6, heimTore: 0, ausTore: 0, sieger: null); // Gruppe 2, 3. = Team 5 (GD -2)
        Spiel(5, heim: 7, aus: 8, heimTore: 1, ausTore: 0, sieger: 7);
        Spiel(6, heim: 8, aus: 9, heimTore: 2, ausTore: 2, sieger: null); // Gruppe 3, 3. = Team 8 (GD -1)
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_AnzahlBesteRegel_GeneriertMitDenBeidenBestenGruppendritten()
    {
        await using var ctx = NeuerContext();
        AnzahlBesteSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

        var zielSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == zielRundeId).ToList();
        zielSpiele.Should().HaveCount(1);
        var teilnehmer = new[] { zielSpiele[0].HeimMannschaftId, zielSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 8, 5 }, "Team 8 (GD -1) und Team 5 (GD -2) sind die beiden besten Gruppendritten, Team 2 (GD -3) fällt raus");
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_AnzahlBesteRegel_GeneriertNicht_WennEineGruppeNochOffenIst()
    {
        await using var ctx = NeuerContext();
        AnzahlBesteSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        ctx.SaveChanges();
        // Gruppe 3 (Spiel 6) wieder auf offen setzen - alle Gruppen müssen fertig sein (konservatives Gate).
        var spiel6 = ctx.Spiele.First(s => s.Id == 6);
        spiel6.Status = SpielStatus.Geplant;
        ctx.SpielErgebnisse.Remove(ctx.SpielErgebnisse.First(e => e.SpielId == 6));
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == zielRundeId).Should().BeEmpty();
    }

    // ── Tests: gruppenübergreifende Platzhalter-Auflösung in der Auslosung (Issue #213) ─

    private static void DreiGruppenUndZielrundeSetupAnlegen(
        TurnierverwaltungDbContext ctx, out int gruppenphaseRundeId, out int zielRundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 9, AusgangsMannschaftsanzahl = 9,
            RegelwerkId = 1, GruppenAnzahl = 3
        };
        var ziel = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Achtelfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(gruppenphase, ziel);

        for (int i = 1; i <= 9; i++)
            ctx.Mannschaften.Add(new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 });

        ctx.SaveChanges();
        gruppenphaseRundeId = gruppenphase.Id;
        zielRundeId = ziel.Id;
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_LoestBestenDrittenUeberMehrereGruppenAuf()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = zielRundeId, Spielnummer = 10,
            HeimPlatzhalterName = "1. bester 3. Platz", AuswaertsPlatzhalterName = "2. bester 3. Platz",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().Be(8, "Team 8 (GD -1) ist der beste Gruppendritte");
        spiel.AuswaertsMannschaftId.Should().Be(5, "Team 5 (GD -2) ist der zweitbeste Gruppendritte");
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_BestePlatzierungBleibtOffenSolangeGruppenphaseNichtVollstaendigIst()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        ctx.SaveChanges();
        // Spiel 6 (Gruppe 3) wieder auf offen setzen - alle Gruppen müssen fertig sein (konservatives Gate).
        var spiel6 = ctx.Spiele.First(s => s.Id == 6);
        spiel6.Status = SpielStatus.Geplant;
        ctx.SpielErgebnisse.Remove(ctx.SpielErgebnisse.First(e => e.SpielId == 6));
        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = zielRundeId, Spielnummer = 10,
            HeimPlatzhalterName = "1. bester 3. Platz", AuswaertsPlatzhalterName = "2. bester 3. Platz",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(0);
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().BeNull();
        spiel.AuswaertsMannschaftId.Should().BeNull();
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_BestePlatzierungLoestNurAufloesbareSeiteAufWennAngeforderterRangNichtExistiert()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        ctx.Spiele.Add(new Spiel
        {
            // Nur 3 Gruppendritte existieren (3 Gruppen) - Rang 5 ist nicht auflösbar.
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = zielRundeId, Spielnummer = 10,
            HeimPlatzhalterName = "1. bester 3. Platz", AuswaertsPlatzhalterName = "5. bester 3. Platz",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(1, "die Heim-Seite ist auflösbar, unabhängig von der nicht auflösbaren Auswärts-Seite (Issue #206)");
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().Be(8, "Team 8 (GD -1) ist der beste Gruppendritte");
        spiel.AuswaertsMannschaftId.Should().BeNull("Rang 5 existiert bei nur 3 Gruppen nicht");
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_BestePlatzierungUndGruppensiegerAufDenBeidenSpielseitenUnabhaengigVoneinanderAufloesbar()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = zielRundeId, Spielnummer = 10,
            HeimPlatzhalterName = "Sieger Gruppe 1", AuswaertsPlatzhalterName = "1. bester 3. Platz",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().Be(1, "Team 1 gewinnt Gruppe 1");
        spiel.AuswaertsMannschaftId.Should().Be(8, "Team 8 (GD -1) ist der beste Gruppendritte");
    }

    // ── Tests: Gruppendritte per Kombinationstabelle (Issue #372) ──────────────

    // Drei Gruppen, zwei beste Dritte (Copa-América-12-Format): qualifiziert sind Gruppe B (Team 5) und
    // Gruppe C (Team 8). Die Tabelle ordnet die Plätze bewusst NICHT in Ranglisten-Reihenfolge zu.
    private static void KombinationstabelleSetzen(TurnierverwaltungDbContext ctx, int zielRundeId)
    {
        var tabelle = new Kombinationstabelle(
            ["3. Gruppe A/B", "3. Gruppe B/C"],
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["AB"] = ["A", "B"],
                ["AC"] = ["A", "C"],
                ["BC"] = ["B", "C"],
            });
        ctx.TurnierRunden.First(r => r.Id == zielRundeId).KombinationstabelleJson = tabelle.ZuJson();
        ctx.Spiele.Add(new Spiel
        {
            Id = 10, TurnierInstanzId = 1, TurnierRundeId = zielRundeId, Spielnummer = 10,
            HeimPlatzhalterName = "3. Gruppe A/B", AuswaertsPlatzhalterName = "3. Gruppe B/C",
            Status = SpielStatus.Geplant
        });
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_LoestGruppendrittenPerKombinationstabelleAuf()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        KombinationstabelleSetzen(ctx, zielRundeId);
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(1);
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().Be(5, "Zeile BC ordnet '3. Gruppe A/B' dem Dritten aus Gruppe B zu");
        spiel.AuswaertsMannschaftId.Should().Be(8, "Zeile BC ordnet '3. Gruppe B/C' dem Dritten aus Gruppe C zu");
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_KombinationstabelleBleibtOffenSolangeGruppenphaseNichtVollstaendigIst()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        KombinationstabelleSetzen(ctx, zielRundeId);
        ctx.SaveChanges();
        var spiel6 = ctx.Spiele.First(s => s.Id == 6);
        spiel6.Status = SpielStatus.Geplant;
        ctx.SpielErgebnisse.Remove(ctx.SpielErgebnisse.First(e => e.SpielId == 6));
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(0);
        var spiel = ctx.Spiele.First(s => s.Id == 10);
        spiel.HeimMannschaftId.Should().BeNull();
        spiel.AuswaertsMannschaftId.Should().BeNull();
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_KombinationsplatzOhneTabelleBleibtOffen()
    {
        await using var ctx = NeuerContext();
        DreiGruppenUndZielrundeSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        KombinationstabelleSetzen(ctx, zielRundeId);
        ctx.TurnierRunden.First(r => r.Id == zielRundeId).KombinationstabelleJson = null;
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        var anzahl = await dienste.Aufloesung.RundeAufloesenAsync(zielRundeId);

        anzahl.Should().Be(0);
    }

    // ── Tests: Kaskaden-Reset für über Auslosung aufgelöste Folgerunden (Issue #88) ─

    private static async Task<Auslosung> AuslosungMitPlatzhalternAnlegen(
        TurnierverwaltungDbContext ctx, int rundeId,
        params (string? Heim, string? Auswaerts)[] paarungen)
    {
        var auslosung = new Auslosung
        {
            TurnierInstanzId = 1, TurnierRundeId = rundeId, Name = "Auslosung",
            Auslosungsart = Auslosungsart.Manuell, Status = AuslosungsStatus.Abgeschlossen
        };
        ctx.Auslosungen.Add(auslosung);
        await ctx.SaveChangesAsync();

        int nr = 1;
        foreach (var (heim, auswaerts) in paarungen)
            ctx.AuslosungsErgebnisse.Add(new AuslosungsErgebnis
            {
                AuslosungId = auslosung.Id, PaarungsNummer = nr++,
                HeimPlatzhalterName = heim, AuswaertsPlatzhalterName = auswaerts
            });
        await ctx.SaveChangesAsync();
        return auslosung;
    }

    [Fact]
    public async Task PlatzhalterKaskadePlanenAsync_ErkenntStaleKoSpiel_WennGruppenphaseErgebnisSichAendert()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out _, out int koRundeId);
        var auslosung = await AuslosungMitPlatzhalternAnlegen(
            ctx, koRundeId, ("Sieger Gruppe A", "2. Gruppe B"));
        var dienste = new SpielplanDienste(ctx);
        await dienste.Spielplan.GenerierenAusAuslosungAsync(koRundeId, [.. auslosung.Ergebnisse]);
        await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        var koSpielVorher = ctx.Spiele.First(s => s.TurnierRundeId == koRundeId);
        koSpielVorher.HeimMannschaftId.Should().Be(1, "Vorbedingung: Sieger Gruppe A = Team A");

        // Gruppenspiel neu entschieden: Team B gewinnt Gruppe A statt Team A
        var gruppenErgebnis = ctx.SpielErgebnisse.First(e => e.SpielId == 1);
        gruppenErgebnis.HeimToreRegulaer = 0;
        gruppenErgebnis.AuswaertsToreRegulaer = 1;
        await ctx.SaveChangesAsync();

        var plan = await dienste.Kaskade.PlatzhalterKaskadePlanenAsync(1, new HashSet<int>());

        plan.HatAuswirkungen.Should().BeTrue();
        plan.Schritte.Should().ContainSingle(s => s.RundeId == koRundeId);
    }

    [Fact]
    public async Task PlatzhalterKaskadeAusfuehrenAsync_GeneriertKoRundeMitNeuemGruppensieger()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out _, out int koRundeId);
        var auslosung = await AuslosungMitPlatzhalternAnlegen(
            ctx, koRundeId, ("Sieger Gruppe A", "2. Gruppe B"));
        var dienste = new SpielplanDienste(ctx);
        await dienste.Spielplan.GenerierenAusAuslosungAsync(koRundeId, [.. auslosung.Ergebnisse]);
        await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        var gruppenErgebnis = ctx.SpielErgebnisse.First(e => e.SpielId == 1);
        gruppenErgebnis.HeimToreRegulaer = 0;
        gruppenErgebnis.AuswaertsToreRegulaer = 1;
        await ctx.SaveChangesAsync();

        var plan = await dienste.Kaskade.PlatzhalterKaskadePlanenAsync(1, new HashSet<int>());
        await dienste.Kaskade.PlatzhalterKaskadeAusfuehrenAsync(plan);

        var koSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId).ToList();
        koSpiele.Should().HaveCount(1);
        koSpiele[0].HeimMannschaftId.Should().Be(2, "Sieger Gruppe A ist jetzt Team B");
        koSpiele[0].AuswaertsMannschaftId.Should().Be(4, "2. Gruppe B bleibt unverändert Team D");
    }

    [Fact]
    public async Task RundenSpielplanAusAuslosungErsetzenAsync_ErzeugtSpielplanGenauEinmal()
    {
        // Issue #411: Abschließen einer Platzhalter-Runde — ob die Runde leer ist oder schon einen Spielplan
        // hat, am Ende steht genau ein Spiel je Paarung, mit den bereits feststehenden Teilnehmern.
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out _, out int koRundeId);
        var auslosung = await AuslosungMitPlatzhalternAnlegen(
            ctx, koRundeId, ("Sieger Gruppe A", "2. Gruppe B"));
        var dienste = new SpielplanDienste(ctx);

        await dienste.Kaskade.RundenSpielplanAusAuslosungErsetzenAsync(koRundeId, [.. auslosung.Ergebnisse]);
        await dienste.Kaskade.RundenSpielplanAusAuslosungErsetzenAsync(koRundeId, [.. auslosung.Ergebnisse]);

        var koSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId).ToList();
        koSpiele.Should().HaveCount(1);
        koSpiele[0].HeimMannschaftId.Should().Be(1, "Sieger Gruppe A = Team A");
        koSpiele[0].AuswaertsMannschaftId.Should().Be(4, "2. Gruppe B = Team D");
    }

    [Fact]
    public async Task PlatzhalterKaskade_LoeschtBereitsGespieltesFinaleUndGeneriertNeu_WennHalbfinaleSichAendert()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var auslosung = await AuslosungMitPlatzhalternAnlegen(
            ctx, finaleRundeId, ("Sieger HF 1", "Sieger HF 2"));
        var dienste = new SpielplanDienste(ctx);
        await dienste.Spielplan.GenerierenAusAuslosungAsync(finaleRundeId, [.. auslosung.Ergebnisse]);
        await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        var finaleSpielId = ctx.Spiele.First(s => s.TurnierRundeId == finaleRundeId).Id;
        ctx.Spiele.First(s => s.Id == finaleSpielId).Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = finaleSpielId, SpielId = finaleSpielId,
            HeimToreRegulaer = 2, AuswaertsToreRegulaer = 1,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = 1
        });
        await ctx.SaveChangesAsync();

        // Halbfinalspiel 1 nachträglich anders entschieden
        var hf1 = ctx.SpielErgebnisse.First(e => e.SpielId == 1);
        hf1.SiegerMannschaftId = 2;
        hf1.HeimToreRegulaer = 0;
        hf1.AuswaertsToreRegulaer = 1;
        await ctx.SaveChangesAsync();

        var plan = await dienste.Kaskade.PlatzhalterKaskadePlanenAsync(1, new HashSet<int>());

        plan.HatAuswirkungen.Should().BeTrue();
        var schritt = plan.Schritte.Should().ContainSingle(s => s.RundeId == finaleRundeId).Subject
            .Should().BeOfType<KaskadenSpielSchritt>().Subject;
        schritt.SpielId.Should().Be(finaleSpielId);
        schritt.HatteErgebnis.Should().BeTrue(
            "das bereits erfasste Finale-Ergebnis basiert auf einer jetzt ungültigen Halbfinale-Paarung");

        await dienste.Kaskade.PlatzhalterKaskadeAusfuehrenAsync(plan);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1);
        finaleSpiele[0].Id.Should().Be(finaleSpielId, "dasselbe Spiel wird korrigiert statt gelöscht+neu angelegt");
        ctx.SpielErgebnisse.Any(e => e.SpielId == finaleSpielId).Should().BeFalse("das ungültige Ergebnis wurde entfernt");
        finaleSpiele[0].HeimMannschaftId.Should().Be(2, "Sieger HF 1 ist jetzt Team B");
    }

    [Fact]
    public async Task PlatzhalterKaskade_RuehrtUnbetroffenesSpielDerselbenRundeNichtAn()
    {
        // Regressionstest: Eine Runde mit zwei unabhängigen Platzhalter-Paarungen (z.B. zwei
        // Halbfinal-Spiele aus vier verschiedenen Gruppen) darf beim Zurücksetzen einer einzelnen
        // Quell-Gruppe nur das tatsächlich betroffene Spiel anfassen — das andere, davon unabhängige
        // Spiel muss inklusive seines bereits erfassten Ergebnisses unverändert bleiben.
        await using var ctx = NeuerContext();
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 8, AusgangsMannschaftsanzahl = 8,
            RegelwerkId = 1, GruppenAnzahl = 4
        };
        var hf = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(gruppenphase, hf);
        ctx.Mannschaften.AddRange(Enumerable.Range(1, 8)
            .Select(i => new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 }));

        // Gruppe 1: Team1/Team2 (Team1 gewinnt). Gruppe 2: Team3/Team4 (Team3 gewinnt).
        // Gruppe 3: Team5/Team6 (Team5 gewinnt). Gruppe 4: Team7/Team8 (Team7 gewinnt).
        for (int g = 0; g < 4; g++)
        {
            int heim = g * 2 + 1, ausw = g * 2 + 2;
            ctx.Spiele.Add(new Spiel
            {
                Id = heim, TurnierInstanzId = 1, TurnierRundeId = gruppenphase.Id, Spielnummer = heim,
                HeimMannschaftId = heim, AuswaertsMannschaftId = ausw, Status = SpielStatus.Abgeschlossen
            });
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = heim, SpielId = heim, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit
            });
        }
        await ctx.SaveChangesAsync();

        var auslosung = await AuslosungMitPlatzhalternAnlegen(
            ctx, hf.Id,
            ("Sieger Gruppe 1", "2. Gruppe 2"),   // HF1: Team1 vs Team4
            ("Sieger Gruppe 3", "2. Gruppe 4"));  // HF2: Team5 vs Team8
        var dienste = new SpielplanDienste(ctx);
        await dienste.Spielplan.GenerierenAusAuslosungAsync(hf.Id, [.. auslosung.Ergebnisse]);
        await dienste.Aufloesung.RundeAufloesenAsync(hf.Id);

        var hfSpieleVorher = ctx.Spiele.Where(s => s.TurnierRundeId == hf.Id).OrderBy(s => s.Spielnummer).ToList();
        hfSpieleVorher.Should().HaveCount(2);
        var hf1Id = hfSpieleVorher[0].Id;
        var hf2Id = hfSpieleVorher[1].Id;

        // Beide Halbfinal-Spiele werden ausgetragen.
        ctx.Spiele.First(s => s.Id == hf1Id).Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = 100, SpielId = hf1Id, HeimToreRegulaer = 2, AuswaertsToreRegulaer = 0,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = 1
        });
        ctx.Spiele.First(s => s.Id == hf2Id).Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = 101, SpielId = hf2Id, HeimToreRegulaer = 3, AuswaertsToreRegulaer = 1,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = 5
        });
        await ctx.SaveChangesAsync();

        // Nur Gruppe 1 wird neu entschieden (Team2 gewinnt statt Team1) — betrifft ausschließlich HF1.
        var gruppe1Ergebnis = ctx.SpielErgebnisse.First(e => e.SpielId == 1);
        gruppe1Ergebnis.HeimToreRegulaer = 0;
        gruppe1Ergebnis.AuswaertsToreRegulaer = 1;
        await ctx.SaveChangesAsync();

        var plan = await dienste.Kaskade.PlatzhalterKaskadePlanenAsync(1, new HashSet<int>());
        var spielSchritt = plan.Schritte.Should().ContainSingle().Subject.Should().BeOfType<KaskadenSpielSchritt>().Subject;
        spielSchritt.SpielId.Should().Be(hf1Id, "nur HF1 hängt von Gruppe 1 ab");

        await dienste.Kaskade.PlatzhalterKaskadeAusfuehrenAsync(plan);

        var hf1 = ctx.Spiele.First(s => s.Id == hf1Id);
        hf1.HeimMannschaftId.Should().Be(2, "Sieger Gruppe 1 ist jetzt Team 2");
        hf1.AuswaertsMannschaftId.Should().Be(4, "2. Gruppe 2 bleibt unverändert Team 4");
        ctx.SpielErgebnisse.Any(e => e.SpielId == hf1Id).Should().BeFalse("das alte HF1-Ergebnis basierte auf der ungültigen Paarung");

        var hf2 = ctx.Spiele.First(s => s.Id == hf2Id);
        hf2.HeimMannschaftId.Should().Be(5, "HF2 ist von Gruppe 1 unabhängig und muss unverändert bleiben");
        hf2.AuswaertsMannschaftId.Should().Be(8);
        var hf2Ergebnis = ctx.SpielErgebnisse.FirstOrDefault(e => e.SpielId == hf2Id);
        hf2Ergebnis.Should().NotBeNull("das gültige HF2-Ergebnis darf nicht angefasst werden");
        hf2Ergebnis!.HeimToreRegulaer.Should().Be(3);
    }

    [Fact]
    public async Task PlatzhalterKaskadePlanenAsync_KeineAuswirkung_WennRundeOhneAuslosungGeneriertWurde()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var dienste = new SpielplanDienste(ctx);
        // Erzeugt das Finale automatisch OHNE Auslosung (Issue #113/#125-Mechanismus)
        await dienste.Finale.FinaleAutomatischGenerierenWennBereitAsync(1);

        var plan = await dienste.Kaskade.PlatzhalterKaskadePlanenAsync(1, new HashSet<int>());

        plan.HatAuswirkungen.Should().BeFalse(
            "die Finale-Runde wurde ohne Auslosung generiert — Issue #88 deckt nur Auslosungs-basierte Runden ab");
    }

    [Fact]
    public async Task PlatzhalterKaskadeAusfuehrenAsync_IstIdempotent_NachAusfuehrungKeineWeiterenAuswirkungen()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out _, out int koRundeId);
        var auslosung = await AuslosungMitPlatzhalternAnlegen(
            ctx, koRundeId, ("Sieger Gruppe A", "2. Gruppe B"));
        var dienste = new SpielplanDienste(ctx);
        await dienste.Spielplan.GenerierenAusAuslosungAsync(koRundeId, [.. auslosung.Ergebnisse]);
        await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        var gruppenErgebnis = ctx.SpielErgebnisse.First(e => e.SpielId == 1);
        gruppenErgebnis.HeimToreRegulaer = 0;
        gruppenErgebnis.AuswaertsToreRegulaer = 1;
        await ctx.SaveChangesAsync();

        var plan = await dienste.Kaskade.PlatzhalterKaskadePlanenAsync(1, new HashSet<int>());
        await dienste.Kaskade.PlatzhalterKaskadeAusfuehrenAsync(plan);

        var planNachAusfuehrung = await dienste.Kaskade.PlatzhalterKaskadePlanenAsync(1, new HashSet<int>());

        planNachAusfuehrung.HatAuswirkungen.Should().BeFalse();
    }

    // ── Tests: Platzhalter-Gruppenphase in der Kaskade (Issue #395) ─────────

    /// <summary>Runde 1 „Quali“ (K.o., 4 entschiedene Spiele, Heimteams 1/3/5/7 gewinnen), Runde 2
    /// Gruppenphase mit einer Gruppe und abgeschlossener Gruppen-Auslosung (ein Eintrag pro Slot).</summary>
    private static Task<int> QualiUndPlatzhalterGruppeAnlegen(
        TurnierverwaltungDbContext ctx, bool mitRueckspiel, params (int? MannschaftId, string? Platzhalter)[] slots)
        => QualiUndPlatzhalterGruppenAnlegen(ctx, mitRueckspiel, entschiedeneQualiSpiele: 4,
            [.. slots.Select(x => (1, x.MannschaftId, x.Platzhalter))]);

    /// <summary>Wie <see cref="QualiUndPlatzhalterGruppeAnlegen"/>, aber mit mehreren Gruppen (Gruppe = PaarungsNummer)
    /// und nur den ersten <paramref name="entschiedeneQualiSpiele"/> Quali-Spielen entschieden.</summary>
    private static async Task<int> QualiUndPlatzhalterGruppenAnlegen(
        TurnierverwaltungDbContext ctx, bool mitRueckspiel, int entschiedeneQualiSpiele,
        (int Gruppe, int? MannschaftId, string? Platzhalter)[] slots)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });
        ctx.TurnierRunden.AddRange(
            new TurnierRunde
            {
                Id = 1, TurnierInstanzId = 1, Name = "Quali", Reihenfolge = 1, Rundenart = Rundenart.KoRunde,
                EingangsMannschaftsanzahl = 8, AusgangsMannschaftsanzahl = 4, RegelwerkId = 1
            },
            new TurnierRunde
            {
                Id = 2, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 2, Rundenart = Rundenart.Gruppenphase,
                EingangsMannschaftsanzahl = slots.Length, AusgangsMannschaftsanzahl = 2, RegelwerkId = 1,
                GruppenAnzahl = slots.Select(x => x.Gruppe).Distinct().Count(), MitRueckspiel = mitRueckspiel
            });
        ctx.Mannschaften.AddRange(Enumerable.Range(1, 10)
            .Select(i => new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 }));
        for (int q = 1; q <= 4; q++)
        {
            int heim = q * 2 - 1;
            bool entschieden = q <= entschiedeneQualiSpiele;
            ctx.Spiele.Add(new Spiel
            {
                Id = q, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = q,
                HeimMannschaftId = heim, AuswaertsMannschaftId = heim + 1,
                Status = entschieden ? SpielStatus.Abgeschlossen : SpielStatus.Geplant
            });
            if (entschieden) ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = q, SpielId = q, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = heim
            });
        }
        var auslosung = new Auslosung
        {
            TurnierInstanzId = 1, TurnierRundeId = 2, Name = "Auslosung",
            Auslosungsart = Auslosungsart.Manuell, Status = AuslosungsStatus.Abgeschlossen
        };
        ctx.Auslosungen.Add(auslosung);
        await ctx.SaveChangesAsync();

        foreach (var (gruppe, mannschaftId, platzhalter) in slots)
            ctx.AuslosungsErgebnisse.Add(new AuslosungsErgebnis
            {
                AuslosungId = auslosung.Id, PaarungsNummer = gruppe,
                HeimMannschaftId = mannschaftId, HeimPlatzhalterName = platzhalter
            });
        await ctx.SaveChangesAsync();

        var dienste = new SpielplanDienste(ctx);
        await dienste.Spielplan.GenerierenAusAuslosungAsync(2, [.. ctx.AuslosungsErgebnisse]);
        return 2;
    }

    [Fact]
    public async Task PlatzhalterKaskade_PlatzhalterGruppenphase_OhneAenderung_KeineAuswirkung()
    {
        // Regressionstest #395: vorher setzte die Kaskade hier ohne jede Änderung 4 von 6 Auswärts-Seiten
        // auf leer, weil sie Team-Einträge der Gruppen-Auslosung positional mit Spielen abglich.
        await using var ctx = NeuerContext();
        int gruppeId = await QualiUndPlatzhalterGruppeAnlegen(ctx, mitRueckspiel: false,
            (null, "Sieger Quali 1"), (null, "Sieger Quali 2"), (null, "Sieger Quali 3"), (null, "Sieger Quali 4"));
        var dienste = new SpielplanDienste(ctx);

        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == gruppeId).ToList();
        spiele.Should().HaveCount(6);
        spiele.Should().OnlyContain(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue,
            "die Quali ist entschieden, alle Platzhalter sind beim Generieren aufgelöst worden");

        var plan = await dienste.Kaskade.PlatzhalterKaskadePlanenAsync(1, new HashSet<int>());

        plan.HatAuswirkungen.Should().BeFalse("an der Quali hat sich nichts geändert");
    }

    [Fact]
    public async Task PlatzhalterKaskade_PlatzhalterGruppenphase_KorrigiertNurSpieleDesBetroffenenTeams()
    {
        await using var ctx = NeuerContext();
        int gruppeId = await QualiUndPlatzhalterGruppeAnlegen(ctx, mitRueckspiel: false,
            (null, "Sieger Quali 1"), (null, "Sieger Quali 2"), (null, "Sieger Quali 3"), (null, "Sieger Quali 4"));
        var dienste = new SpielplanDienste(ctx);

        // Alle Gruppenspiele ausgetragen
        foreach (var s in ctx.Spiele.Where(s => s.TurnierRundeId == gruppeId).ToList())
        {
            s.Status = SpielStatus.Abgeschlossen;
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = 100 + s.Id, SpielId = s.Id, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 1,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit
            });
        }
        await ctx.SaveChangesAsync();

        // Quali-Spiel 1 nachträglich anders entschieden: Team 2 statt Team 1
        var quali1 = ctx.SpielErgebnisse.First(e => e.SpielId == 1);
        quali1.HeimToreRegulaer = 0;
        quali1.AuswaertsToreRegulaer = 1;
        quali1.SiegerMannschaftId = 2;
        await ctx.SaveChangesAsync();

        var plan = await dienste.Kaskade.PlatzhalterKaskadePlanenAsync(1, new HashSet<int>());
        var betroffen = ctx.Spiele
            .Where(s => s.TurnierRundeId == gruppeId && (s.HeimMannschaftId == 1 || s.AuswaertsMannschaftId == 1))
            .Select(s => s.Id).ToList();
        betroffen.Should().HaveCount(3);
        plan.Schritte.OfType<KaskadenSpielSchritt>().Select(s => s.SpielId)
            .Should().BeEquivalentTo(betroffen, "nur die drei Spiele von „Sieger Quali 1“ hängen an Quali-Spiel 1");

        await dienste.Kaskade.PlatzhalterKaskadeAusfuehrenAsync(plan);

        var gruppenSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == gruppeId).ToList();
        gruppenSpiele.Should().NotContain(s => s.HeimMannschaftId == 1 || s.AuswaertsMannschaftId == 1);
        gruppenSpiele.Count(s => s.HeimMannschaftId == 2 || s.AuswaertsMannschaftId == 2).Should().Be(3);
        gruppenSpiele.Should().OnlyContain(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue);
        ctx.SpielErgebnisse.Count(e => betroffen.Contains(e.SpielId)).Should().Be(0);
        ctx.SpielErgebnisse.Count(e => e.SpielId > 4 && !betroffen.Contains(e.SpielId))
            .Should().Be(3, "die Spiele ohne „Sieger Quali 1“ behalten ihr Ergebnis");
    }

    [Fact]
    public async Task GenerierenAusAuslosungAsync_GemischteGruppe_BehaeltPlatzhalterTeams()
    {
        // Regressionstest #395: vorher entstand aus 2 festen Teams + 2 Platzhaltern eine Zweiergruppe.
        await using var ctx = NeuerContext();
        int gruppeId = await QualiUndPlatzhalterGruppeAnlegen(ctx, mitRueckspiel: false,
            (9, null), (10, null), (null, "Sieger Quali 1"), (null, "Sieger Quali 2"));
        var dienste = new SpielplanDienste(ctx);

        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == gruppeId).ToList();
        spiele.Should().HaveCount(6, "Vierergruppe mit Hinrunde = 6 Spiele");
        var teams = spiele.SelectMany(s => new[] { s.HeimMannschaftId, s.AuswaertsMannschaftId }).Distinct();
        teams.Should().BeEquivalentTo(new int?[] { 9, 10, 1, 3 });

        var plan = await dienste.Kaskade.PlatzhalterKaskadePlanenAsync(1, new HashSet<int>());
        plan.HatAuswirkungen.Should().BeFalse();
    }

    [Fact]
    public async Task GenerierenAusAuslosungAsync_PlatzhalterGruppeMitRueckspiel_VerknuepftHinspiel()
    {
        await using var ctx = NeuerContext();
        int gruppeId = await QualiUndPlatzhalterGruppeAnlegen(ctx, mitRueckspiel: true,
            (null, "Sieger Quali 1"), (null, "Sieger Quali 2"), (null, "Sieger Quali 3"));
        var dienste = new SpielplanDienste(ctx);

        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == gruppeId).ToList();
        spiele.Should().HaveCount(6);
        foreach (var rueck in spiele.Where(s => s.IstRueckspiel))
        {
            var hin = spiele.Single(s => s.Id == rueck.HinspielId);
            (rueck.HeimMannschaftId, rueck.AuswaertsMannschaftId).Should().Be((hin.AuswaertsMannschaftId, hin.HeimMannschaftId));
        }

        var plan = await dienste.Kaskade.PlatzhalterKaskadePlanenAsync(1, new HashSet<int>());
        plan.HatAuswirkungen.Should().BeFalse();
    }

    // ── Tests: gespeicherte Gruppennummer (Issue #397) ──────────────────────

    /// <summary>Gruppe 1: „Sieger Quali 1“ (= Team 1), „Sieger Quali 3“, „Sieger Quali 4“ (beide offen) —
    /// Team 1 spielt nur gegen offene Plätze. Gruppe 2: „Sieger Quali 2“ (= Team 3), Team 9, Team 10.</summary>
    private static Task<int> TeilaufgeloesteGruppenAnlegen(TurnierverwaltungDbContext ctx) =>
        QualiUndPlatzhalterGruppenAnlegen(ctx, mitRueckspiel: false, entschiedeneQualiSpiele: 2,
        [
            (1, null, "Sieger Quali 1"), (1, null, "Sieger Quali 3"), (1, null, "Sieger Quali 4"),
            (2, null, "Sieger Quali 2"), (2, 9, null), (2, 10, null)
        ]);

    private static TabellenService TabellenServiceFuer(TurnierverwaltungDbContext ctx) =>
        new(new TurnierRundeRepository(ctx), new SpielRepository(ctx), new RegelwerkRepository(ctx));

    [Fact]
    public async Task GenerierenAusAuslosungAsync_Gruppenphase_SpeichertGruppenNummer()
    {
        await using var ctx = NeuerContext();
        int gruppeId = await TeilaufgeloesteGruppenAnlegen(ctx);

        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == gruppeId).ToList();
        spiele.Should().HaveCount(6);
        spiele.Count(s => s.GruppenNummer == 1).Should().Be(3);
        spiele.Count(s => s.GruppenNummer == 2).Should().Be(3);
        spiele.Where(s => s.GruppenNummer == 2)
            .SelectMany(s => new[] { s.HeimMannschaftId, s.AuswaertsMannschaftId })
            .Should().OnlyContain(id => id == 3 || id == 9 || id == 10);
    }

    [Fact]
    public async Task GruppenTabellenBerechnenAsync_TeilaufgeloesteGruppe_VollstaendigMitOffenenPlaetzen()
    {
        // Regressionstest #397: vorher fehlte Gruppe 1 ganz (Team 1 hat kein Spiel gegen ein bekanntes Team),
        // und Gruppe 2 erschien als „Gruppe 1“.
        await using var ctx = NeuerContext();
        int gruppeId = await TeilaufgeloesteGruppenAnlegen(ctx);

        var tabellen = await TabellenServiceFuer(ctx).GruppenTabellenBerechnenAsync(gruppeId);

        tabellen.Select(t => t.GruppenNummer).Should().Equal(1, 2);
        tabellen[0].Tabelle.Select(e => e.Mannschaft.Id).Should().Equal(1);
        tabellen[0].OffenePlaetze.Should().BeEquivalentTo("Sieger Quali 3", "Sieger Quali 4");
        tabellen[1].Tabelle.Select(e => e.Mannschaft.Id).Should().BeEquivalentTo([3, 9, 10]);
        tabellen[1].OffenePlaetze.Should().BeEmpty();
    }

    [Fact]
    public async Task GruppenTabellenBerechnenAsync_GruppeMitOffenenPlaetzen_KeinRangSicher()
    {
        // Regressionstest #397: Spiele gegen noch offene Plätze fehlten in der Status-Berechnung — sobald die
        // Spiele unter bekannten Teams gespielt waren, galt die Gruppe fälschlich als fertig.
        await using var ctx = NeuerContext();
        int gruppeId = await TeilaufgeloesteGruppenAnlegen(ctx);
        foreach (var s in ctx.Spiele.Where(s => s.TurnierRundeId == gruppeId).ToList()
                     .Where(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue))
        {
            s.Status = SpielStatus.Abgeschlossen;
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = 100 + s.Id, SpielId = s.Id, HeimToreRegulaer = s.Id % 3, AuswaertsToreRegulaer = 0,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit
            });
        }
        await ctx.SaveChangesAsync();

        var tabellen = await TabellenServiceFuer(ctx).GruppenTabellenBerechnenAsync(gruppeId);

        tabellen[0].Tabelle.Should().OnlyContain(e => !e.SicherPositioniert && e.Qualifikationsstatus == Qualifikationsstatus.Offen,
            "in Gruppe 1 sind noch zwei Teilnehmer unbekannt");
        tabellen[1].Tabelle.Should().OnlyContain(e => e.SicherPositioniert, "Gruppe 2 ist vollständig gespielt");
    }

    [Fact]
    public async Task LoeschenAsync_SetztStaleFolgerundenPlatzhalterZurueck_WennQuellrundeNeuGeneriertWird()
    {
        // Regressionstest für Issue #306: Beim Reset+Redo einer Auslosung (z.B. Gruppenphase) löscht die
        // aufrufende UI-Schicht den alten Spielplan der Quellrunde (LoeschenAsync) vor der Neugenerierung.
        // Genau in diesem Moment wird "Sieger Gruppe A" unauflösbar — eine bereits aufgelöste Folgerunde
        // (hier: Halbfinale) muss dadurch automatisch auf Platzhaltertext zurückgesetzt werden, statt die
        // alten, jetzt ungültigen Mannschafts-IDs zu behalten.
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out int gruppenphaseRundeId, out int koRundeId);
        var auslosung = await AuslosungMitPlatzhalternAnlegen(
            ctx, koRundeId, ("Sieger Gruppe A", "2. Gruppe B"));
        var dienste = new SpielplanDienste(ctx);
        await dienste.Spielplan.GenerierenAusAuslosungAsync(koRundeId, [.. auslosung.Ergebnisse]);
        await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        var koSpielVorher = ctx.Spiele.First(s => s.TurnierRundeId == koRundeId);
        koSpielVorher.HeimMannschaftId.Should().Be(1, "Vorbedingung: Sieger Gruppe A = Team A");
        koSpielVorher.AuswaertsMannschaftId.Should().Be(4, "Vorbedingung: 2. Gruppe B = Team D");

        // Auslosung-Redo: die UI löscht den bestehenden Gruppenphase-Spielplan vor der Neugenerierung.
        await dienste.Kaskade.LoeschenAsync(gruppenphaseRundeId);

        var koSpielNachher = ctx.Spiele.First(s => s.TurnierRundeId == koRundeId);
        koSpielNachher.HeimMannschaftId.Should().BeNull(
            "Gruppenphase hat keine Spiele mehr — 'Sieger Gruppe A' ist nicht mehr auflösbar");
        koSpielNachher.HeimPlatzhalterName.Should().Be("Sieger Gruppe A");
        koSpielNachher.AuswaertsMannschaftId.Should().BeNull(
            "Gruppenphase hat keine Spiele mehr — '2. Gruppe B' ist nicht mehr auflösbar");
        koSpielNachher.AuswaertsPlatzhalterName.Should().Be("2. Gruppe B");
    }

    // ── Tests: Liga-Platzhalter "Platz N (Runde)" mit Sicherheits-Gate (Issue #343) ─

    /// <summary>Ligaphase (4 Teams, Einfachrunde, 3 Spieltage) + Halbfinale mit Platzhaltern
    /// "Platz 1/4 (Ligaphase)" und "Platz 2/3 (Ligaphase)". Spieltag 1: A–B, C–D; 2: A–C, B–D; 3: A–D, B–C.</summary>
    private static void LigaMitFolgeKoRundeAnlegen(
        TurnierverwaltungDbContext ctx, out int ligaRundeId, out int koRundeId,
        Rundenart ligaRundenart = Rundenart.Liga)
    {
        LigaGrundSetupAnlegen(ctx, out ligaRundeId);
        var liga = ctx.TurnierRunden.Single(r => r.Id == 1);
        liga.Name = "Ligaphase";
        liga.Rundenart = ligaRundenart;
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });
        var ko = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.Add(ko);
        ctx.Spiele.AddRange(
            new Spiel { Id = 101, TurnierInstanzId = 1, TurnierRundeId = 2, Spielnummer = 101,
                        HeimPlatzhalterName = "Platz 1 (Ligaphase)", AuswaertsPlatzhalterName = "Platz 4 (Ligaphase)" },
            new Spiel { Id = 102, TurnierInstanzId = 1, TurnierRundeId = 2, Spielnummer = 102,
                        HeimPlatzhalterName = "Platz 2 (Ligaphase)", AuswaertsPlatzhalterName = "Platz 3 (Ligaphase)" });

        (int Id, int Spieltag, int Heim, int Auswaerts)[] paarungen =
            [(1, 1, 1, 2), (2, 1, 3, 4), (3, 2, 1, 3), (4, 2, 2, 4), (5, 3, 1, 4), (6, 3, 2, 3)];
        foreach (var (id, spieltag, heim, auswaerts) in paarungen)
            ctx.Spiele.Add(new Spiel
            {
                Id = id, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = id, Spieltag = spieltag,
                HeimMannschaftId = heim, AuswaertsMannschaftId = auswaerts, Status = SpielStatus.Geplant
            });
        ctx.SaveChanges();
        koRundeId = ko.Id;
    }

    private static void LigaErgebnisSetzen(TurnierverwaltungDbContext ctx, int spielId, int heimTore, int auswaertsTore)
    {
        var spiel = ctx.Spiele.Single(s => s.Id == spielId);
        spiel.Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = spielId, SpielId = spielId, HeimToreRegulaer = heimTore, AuswaertsToreRegulaer = auswaertsTore,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit,
            SiegerMannschaftId = heimTore > auswaertsTore ? spiel.HeimMannschaftId
                               : auswaertsTore > heimTore ? spiel.AuswaertsMannschaftId : null
        });
        ctx.SaveChanges();
    }

    /// <summary>Spieltag 1+2: A gewinnt beide (6 Pkt.), alle übrigen Spiele Unentschieden
    /// (B 1, C 1, D 2) — A ist vor dem letzten Spieltag uneinholbar Erster (D max. 5).</summary>
    private static void ZweiSpieltageMitUneinholbaremTabellenfuehrer(TurnierverwaltungDbContext ctx)
    {
        LigaErgebnisSetzen(ctx, 1, 2, 0); // A–B
        LigaErgebnisSetzen(ctx, 2, 1, 1); // C–D
        LigaErgebnisSetzen(ctx, 3, 3, 0); // A–C
        LigaErgebnisSetzen(ctx, 4, 0, 0); // B–D
    }

    /// <summary>Letzter Spieltag: A–D 1:0, B–C 2:1 → Endstand A 9, B 4, D 2, C 1.</summary>
    private static void LetztenSpieltagSpielen(TurnierverwaltungDbContext ctx)
    {
        LigaErgebnisSetzen(ctx, 5, 1, 0);
        LigaErgebnisSetzen(ctx, 6, 2, 1);
    }

    [Theory]
    [InlineData(Rundenart.Liga)]
    [InlineData(Rundenart.TopfLiga)]
    public async Task PlatzhalterAufloesenAsync_LigaKomplettGespielt_LoestPlatzNAufTabellenrangAuf(Rundenart ligaRundenart)
    {
        await using var ctx = NeuerContext();
        LigaMitFolgeKoRundeAnlegen(ctx, out _, out int koRundeId, ligaRundenart);
        ZweiSpieltageMitUneinholbaremTabellenfuehrer(ctx);
        LetztenSpieltagSpielen(ctx);
        var dienste = new SpielplanDienste(ctx);

        await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId).OrderBy(s => s.Id).ToList();
        (spiele[0].HeimMannschaftId, spiele[0].AuswaertsMannschaftId).Should().Be((1, 3), "Platz 1 = A, Platz 4 = C");
        (spiele[1].HeimMannschaftId, spiele[1].AuswaertsMannschaftId).Should().Be((2, 4), "Platz 2 = B, Platz 3 = D");
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_LigaVorLetztemSpieltag_LoestNurUneinholbarenRangAuf()
    {
        await using var ctx = NeuerContext();
        LigaMitFolgeKoRundeAnlegen(ctx, out _, out int koRundeId);
        ZweiSpieltageMitUneinholbaremTabellenfuehrer(ctx);
        var dienste = new SpielplanDienste(ctx);

        await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId).OrderBy(s => s.Id).ToList();
        spiele[0].HeimMannschaftId.Should().Be(1, "A kann mit 6 Punkten von niemandem mehr eingeholt werden");
        spiele[0].AuswaertsMannschaftId.Should().BeNull("Platz 4 hängt noch vom letzten Spieltag ab");
        spiele[1].HeimMannschaftId.Should().BeNull();
        spiele[1].AuswaertsMannschaftId.Should().BeNull();
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_LigaNachErstemSpieltag_LoestNichtsAufAberVorschauZeigtStand()
    {
        await using var ctx = NeuerContext();
        LigaMitFolgeKoRundeAnlegen(ctx, out _, out int koRundeId);
        LigaErgebnisSetzen(ctx, 1, 2, 0); // A–B
        LigaErgebnisSetzen(ctx, 2, 1, 0); // C–D
        var dienste = new SpielplanDienste(ctx);

        var aufgeloest = await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);
        var vorschau = await dienste.Aufloesung.VorschauAsync(koRundeId);

        aufgeloest.Should().Be(0, "nach einem Spieltag ist noch kein Tabellenplatz gesichert");
        ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId)
            .Should().OnlyContain(s => s.HeimMannschaftId == null && s.AuswaertsMannschaftId == null);
        vorschau.Should().HaveCount(2, "die optimistische Vorschau zeigt den aktuellen Tabellenstand");
        vorschau[101].Heim.Should().NotBeNull();
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_SchweizerSystemOhneSpieltagAnzahl_LoestNichtFestAuf()
    {
        await using var ctx = NeuerContext();
        LigaMitFolgeKoRundeAnlegen(ctx, out _, out int koRundeId, Rundenart.SchweizerSystem);
        ZweiSpieltageMitUneinholbaremTabellenfuehrer(ctx);
        LetztenSpieltagSpielen(ctx);
        var dienste = new SpielplanDienste(ctx);

        var aufgeloest = await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        aufgeloest.Should().Be(0,
            "ohne AnzahlSpieltage ist unbekannt, ob noch Schweizer Spieltage folgen — kein Rang gilt als gesichert");
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_SchweizerSystemAlleSpieltageGespielt_LoestAllesAuf()
    {
        await using var ctx = NeuerContext();
        LigaMitFolgeKoRundeAnlegen(ctx, out _, out int koRundeId, Rundenart.SchweizerSystem);
        ctx.TurnierRunden.Single(r => r.Id == 1).KonfigurationJson = "{\"anzahlSpieltage\":3}";
        ctx.SaveChanges();
        ZweiSpieltageMitUneinholbaremTabellenfuehrer(ctx);
        LetztenSpieltagSpielen(ctx);
        var dienste = new SpielplanDienste(ctx);

        await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId)
            .Should().OnlyContain(s => s.HeimMannschaftId != null && s.AuswaertsMannschaftId != null);
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_SchweizerSystemNochNichtGenerierterSpieltag_ZaehltAlsOffen()
    {
        await using var ctx = NeuerContext();
        LigaMitFolgeKoRundeAnlegen(ctx, out _, out int koRundeId, Rundenart.SchweizerSystem);
        ctx.TurnierRunden.Single(r => r.Id == 1).KonfigurationJson = "{\"anzahlSpieltage\":4}";
        ctx.SaveChanges();
        ZweiSpieltageMitUneinholbaremTabellenfuehrer(ctx);
        LetztenSpieltagSpielen(ctx);
        var dienste = new SpielplanDienste(ctx);

        await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        // Spieltag 4 ist noch nicht generiert: A (9, max. 12) bleibt uneinholbar vor B (max. 7), alle anderen
        // Plätze sind noch offen.
        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId).OrderBy(s => s.Id).ToList();
        spiele[0].HeimMannschaftId.Should().Be(1);
        spiele[0].AuswaertsMannschaftId.Should().BeNull();
        spiele[1].HeimMannschaftId.Should().BeNull();
        spiele[1].AuswaertsMannschaftId.Should().BeNull();
    }

    // ── Tests: K.o.-Runden mit Hin-/Rückspiel und Platzhaltern (Issue #346) ─

    private static void KoSpielAnlegen(
        TurnierverwaltungDbContext ctx, int id, int rundeId, int heimId, int auswaertsId,
        int? siegerMannschaftId, bool istRueckspiel = false, int? hinspielId = null)
    {
        ctx.Spiele.Add(new Spiel
        {
            Id = id, TurnierInstanzId = 1, TurnierRundeId = rundeId,
            Spielnummer = id, HeimMannschaftId = heimId, AuswaertsMannschaftId = auswaertsId,
            Status = SpielStatus.Abgeschlossen, IstRueckspiel = istRueckspiel, HinspielId = hinspielId
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = id, SpielId = id,
            HeimToreRegulaer = siegerMannschaftId == heimId ? 1 : 0,
            AuswaertsToreRegulaer = siegerMannschaftId == auswaertsId ? 1 : 0,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit,
            SiegerMannschaftId = siegerMannschaftId
        });
        ctx.SaveChanges();
    }

    /// <summary>Halbfinale mit Hin-/Rückspiel: Paarung 1 A–B (Hinspiel-Sieger B, Gesamtsieger A),
    /// Paarung 2 C–D (Gesamtsieger D). Spielnummern 1–4, Rückspiele direkt nach ihrem Hinspiel.</summary>
    private static void HalbfinaleMitRueckspielenAnlegen(TurnierverwaltungDbContext ctx, int hfRundeId)
    {
        KoSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 2);
        KoSpielAnlegen(ctx, 2, hfRundeId, heimId: 2, auswaertsId: 1, siegerMannschaftId: 1, istRueckspiel: true, hinspielId: 1);
        KoSpielAnlegen(ctx, 3, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 4);
        KoSpielAnlegen(ctx, 4, hfRundeId, heimId: 4, auswaertsId: 3, siegerMannschaftId: 4, istRueckspiel: true, hinspielId: 3);
    }

    [Fact]
    public async Task GenerierenAusAuslosungAsync_KoRundeMitRueckspielUndPlatzhaltern_ErzeugtGespiegelteRueckspiele()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out _, hfMitRueckspiel: true);
        var ergebnisse = new List<AuslosungsErgebnis>
        {
            new() { PaarungsNummer = 1, HeimPlatzhalterName = "Sieger Gruppe A", AuswaertsPlatzhalterName = "2. Gruppe B" },
            new() { PaarungsNummer = 2, HeimPlatzhalterName = "Sieger Gruppe B", AuswaertsPlatzhalterName = "2. Gruppe A" },
        };
        var dienste = new SpielplanDienste(ctx);

        await dienste.Spielplan.GenerierenAusAuslosungAsync(hfRundeId, ergebnisse);

        var spiele = ctx.Spiele.Where(s => s.TurnierRundeId == hfRundeId).OrderBy(s => s.Spielnummer).ToList();
        spiele.Should().HaveCount(4, "je Paarung Hin- und Rückspiel");
        spiele.Select(s => s.IstRueckspiel).Should().Equal(false, false, true, true);

        var (hin1, rueck1) = (spiele[0], spiele[2]);
        hin1.HeimPlatzhalterName.Should().Be("Sieger Gruppe A");
        hin1.Spieltag.Should().Be(1);
        rueck1.HeimPlatzhalterName.Should().Be("2. Gruppe B", "das Rückspiel ist gespiegelt");
        rueck1.AuswaertsPlatzhalterName.Should().Be("Sieger Gruppe A");
        rueck1.Spieltag.Should().Be(2);
        rueck1.HinspielId.Should().Be(hin1.Id);
        rueck1.Spielnummer.Should().Be(hin1.Spielnummer + 2, "erst alle Hinspiele, dann alle Rückspiele");
        spiele[3].HinspielId.Should().Be(spiele[1].Id);
    }

    [Fact]
    public async Task PlatzhalterAufloesenAsync_KoSiegerAusHinUndRueckspiel_NimmtGesamtsiegerJePaarung()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId, hfMitRueckspiel: true);
        HalbfinaleMitRueckspielenAnlegen(ctx, hfRundeId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = "Sieger Halbfinale 1", AuswaertsPlatzhalterName = "Sieger Halbfinale 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        var finale = ctx.Spiele.First(s => s.Id == 20);
        // Früher: "Sieger Halbfinale 1" = Hinspiel-Sieger B, "Sieger Halbfinale 2" = Rückspiel der 1. Paarung.
        finale.HeimMannschaftId.Should().Be(1, "Team A gewinnt Paarung 1 über beide Spiele");
        finale.AuswaertsMannschaftId.Should().Be(4, "Team D gewinnt Paarung 2");
    }

    [Fact]
    public async Task SpielQuellenAlleTransitionsVerknuepfenAsync_HinUndRueckspiel_VerweistAufRueckspielDerPaarung()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId, hfMitRueckspiel: true);
        HalbfinaleMitRueckspielenAnlegen(ctx, hfRundeId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = "Sieger Halbfinale 1", AuswaertsPlatzhalterName = "Verlierer Halbfinale 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        await dienste.Quellverknuepfung.AlleRundenVerknuepfenAsync(1);
        await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        var finale = ctx.Spiele.First(s => s.Id == 20);
        finale.HeimQuellSpielId.Should().Be(2, "das Rückspiel der 1. Paarung trägt den Gesamtsieger");
        finale.AuswaertsQuellSpielId.Should().Be(4);
        finale.HeimMannschaftId.Should().Be(1);
        finale.AuswaertsMannschaftId.Should().Be(3, "Verlierer der 2. Paarung = Team C");
    }

    [Fact]
    public async Task PlatzhalterKaskade_KoRundeMitRueckspiel_KorrigiertHinUndRueckspielJederPaarung()
    {
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out _, out int koRundeId);
        ctx.TurnierRunden.First(r => r.Id == koRundeId).MitRueckspiel = true;
        ctx.SaveChanges();
        var auslosung = await AuslosungMitPlatzhalternAnlegen(
            ctx, koRundeId, ("Sieger Gruppe A", "2. Gruppe B"), ("Sieger Gruppe B", "2. Gruppe A"));
        var dienste = new SpielplanDienste(ctx);
        await dienste.Spielplan.GenerierenAusAuslosungAsync(koRundeId, [.. auslosung.Ergebnisse]);
        await dienste.Aufloesung.RundeAufloesenAsync(koRundeId);

        // Gruppenspiel neu entschieden: Team B gewinnt Gruppe A statt Team A
        var gruppenErgebnis = ctx.SpielErgebnisse.First(e => e.SpielId == 1);
        gruppenErgebnis.HeimToreRegulaer = 0;
        gruppenErgebnis.AuswaertsToreRegulaer = 1;
        await ctx.SaveChangesAsync();

        var plan = await dienste.Kaskade.PlatzhalterKaskadePlanenAsync(1, new HashSet<int>());
        plan.Schritte.Should().HaveCount(4, "Hin- und Rückspiel beider Paarungen sind betroffen");
        await dienste.Kaskade.PlatzhalterKaskadeAusfuehrenAsync(plan);

        var teams = ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId).OrderBy(s => s.Spielnummer)
            .Select(s => new { s.HeimMannschaftId, s.AuswaertsMannschaftId }).ToList();
        teams.Should().Equal(
            new { HeimMannschaftId = (int?)2, AuswaertsMannschaftId = (int?)4 },   // Hinspiel: Sieger Gr. A – 2. Gr. B
            new { HeimMannschaftId = (int?)3, AuswaertsMannschaftId = (int?)1 },   // Hinspiel: Sieger Gr. B – 2. Gr. A
            new { HeimMannschaftId = (int?)4, AuswaertsMannschaftId = (int?)2 },   // Rückspiele gespiegelt
            new { HeimMannschaftId = (int?)1, AuswaertsMannschaftId = (int?)3 });
    }

    [Fact]
    public async Task GenerierenAusAuslosungAsync_VorrundeBereitsEntschieden_TraegtFeststehendeTeamsSofortEin()
    {
        // Issue #346-Folgefund: eine nach komplett gespielter Gruppenphase neu generierte K.o.-Runde (hier mit
        // Rückspielen) zeigte zunächst nur Platzhalter, bis ein weiteres Ergebnis die Auflösung anstieß.
        await using var ctx = NeuerContext();
        GruppenphaseUndKoSetupAnlegen(ctx, out _, out int koRundeId);
        ctx.TurnierRunden.First(r => r.Id == koRundeId).MitRueckspiel = true;
        ctx.SaveChanges();
        var auslosung = await AuslosungMitPlatzhalternAnlegen(
            ctx, koRundeId, ("Sieger Gruppe A", "2. Gruppe B"), ("Sieger Gruppe B", "2. Gruppe A"));
        var dienste = new SpielplanDienste(ctx);

        await dienste.Spielplan.GenerierenAusAuslosungAsync(koRundeId, [.. auslosung.Ergebnisse]);

        var teams = ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId).OrderBy(s => s.Spielnummer)
            .Select(s => new { s.HeimMannschaftId, s.AuswaertsMannschaftId }).ToList();
        teams.Should().Equal(
            new { HeimMannschaftId = (int?)1, AuswaertsMannschaftId = (int?)4 },
            new { HeimMannschaftId = (int?)3, AuswaertsMannschaftId = (int?)2 },
            new { HeimMannschaftId = (int?)4, AuswaertsMannschaftId = (int?)1 },
            new { HeimMannschaftId = (int?)2, AuswaertsMannschaftId = (int?)3 });
    }
}
