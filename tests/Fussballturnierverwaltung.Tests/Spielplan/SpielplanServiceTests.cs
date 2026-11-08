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
using static Fussballturnierverwaltung.Tests.Spielplan.SpielplanTestdaten;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class SpielplanServiceTests
{
    // ── Tests: automatische Generierung aus abgeschlossener Auslosung (Issue #19) ─

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

    // ── Tests: Schweizer-Rundenfolge über mehrere Spieltage (Issue #329 Teil E) ─

    [Fact]
    public async Task SchweizerRundenfolge_NaechsteRunde_GeneriertFolgeSpieltagAusAktuellerTabelle()
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

        await dienste.Schweizer.NaechsteRundeGenerierenAsync(rundeId);

        var alleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == rundeId).ToList();
        alleSpiele.Should().HaveCount(4);
        alleSpiele.Where(s => s.Spieltag == 2).Should().HaveCount(2);
    }

    [Fact]
    public async Task SchweizerRundenfolge_NaechsteRunde_FreilosTeamOhneVorherigesSpiel_WirdWiederEingeplant()
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

        await dienste.Schweizer.NaechsteRundeGenerierenAsync(rundeId);

        var spieltag2 = ctx.Spiele.Where(s => s.TurnierRundeId == rundeId && s.Spieltag == 2).ToList();
        spieltag2.Should().HaveCount(2);
        spieltag2.SelectMany(s => new[] { s.HeimMannschaftId, s.AuswaertsMannschaftId })
            .Should().Contain(5, "Team 5 muss nach seinem Freilos in Spieltag 2 wieder eingeplant werden");
    }

    [Fact]
    public async Task SchweizerRundenfolge_NaechsteRunde_FreilosTeamWirdNachSeinemTabellenplatzGepaart()
    {
        // #608: Team 7 (Freilos, 0 Punkte, 0:0) steht vor den Verlierern von Spieltag 1 (0 Punkte, 0:1) und wird
        // deshalb mit dem dritten Sieger gepaart. Vorher kam es ohne Tabellenplatz ans Ende und traf einen Verlierer.
        await using var ctx = NeuerContext();
        SchweizerSystemGrundSetupAnlegen(ctx, out int rundeId);
        ctx.TurnierRunden.Single().EingangsMannschaftsanzahl = 7;
        ctx.TurnierRunden.Single().AusgangsMannschaftsanzahl = 7;
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 5, Name = "Team E", Kurzname = "TE", LandId = 1 },
            new Mannschaft { Id = 6, Name = "Team F", Kurzname = "TF", LandId = 1 },
            new Mannschaft { Id = 7, Name = "Team G", Kurzname = "TG", LandId = 1 });
        await ctx.SaveChangesAsync();

        var auslosung = await SchweizerAuslosungAnlegen(ctx, rundeId, (1, 2), (3, 4), (5, 6), (7, null));
        var dienste = new SpielplanDienste(ctx);
        await dienste.Spielplan.GenerierenAusAuslosungAsync(rundeId, [.. auslosung.Ergebnisse]);

        var spieltag1 = ctx.Spiele.Where(s => s.TurnierRundeId == rundeId).ToList();
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
        var sieger = spieltag1.Select(s => s.HeimMannschaftId!.Value).ToList();

        await dienste.Schweizer.NaechsteRundeGenerierenAsync(rundeId);

        var spielVon7 = ctx.Spiele.Single(s => s.TurnierRundeId == rundeId && s.Spieltag == 2
            && (s.HeimMannschaftId == 7 || s.AuswaertsMannschaftId == 7));
        int gegner = spielVon7.HeimMannschaftId == 7 ? spielVon7.AuswaertsMannschaftId!.Value : spielVon7.HeimMannschaftId!.Value;
        sieger.Should().Contain(gegner, "das Freilos-Team steht mit 0:0 vor den Verlierern von Spieltag 1");
    }

    // ── Tests: Platzhalter-Gruppenphase in der Kaskade (Issue #395) ─────────

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

        var plan = await dienste.Kaskade.PlanenAsync(1, new HashSet<int>());
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

        var plan = await dienste.Kaskade.PlanenAsync(1, new HashSet<int>());
        plan.HatAuswirkungen.Should().BeFalse();
    }

    // ── Tests: gespeicherte Gruppennummer (Issue #397) ──────────────────────

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

        tabellen[0].Tabelle.Should().OnlyContain(e => !e.SicherPositioniert && e.BesterRang == null,
            "in Gruppe 1 sind noch zwei Teilnehmer unbekannt");
        tabellen[1].Tabelle.Should().OnlyContain(e => e.SicherPositioniert, "Gruppe 2 ist vollständig gespielt");
    }

    // ── Tests: K.o.-Runden mit Hin-/Rückspiel und Platzhaltern (Issue #346) ─

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

    // ── Tests: Hin- und Rückspiele mit festen Teams speichern (#504) ─────────

    [Fact]
    public async Task GenerierenAusAuslosungAsync_LigaMitRueckspiel_VerknuepftJedesRueckspielMitSeinemHinspiel()
    {
        await using var ctx = NeuerContext();
        LigaGrundSetupAnlegen(ctx, out int rundeId);
        ctx.TurnierRunden.Single(r => r.Id == rundeId).MitRueckspiel = true;
        ctx.SaveChanges();
        var ergebnisse = Enumerable.Range(1, 4)
            .Select(id => new AuslosungsErgebnis { PaarungsNummer = id, HeimMannschaftId = id }).ToList();

        await new SpielplanDienste(ctx).Spielplan.GenerierenAusAuslosungAsync(rundeId, ergebnisse);

        RueckspieleVerknuepftPruefen(ctx.Spiele.Where(s => s.TurnierRundeId == rundeId).ToList(), erwarteteSpiele: 12);
    }

    [Fact]
    public async Task GenerierenAusAuslosungAsync_GruppenMitFestenTeamsUndRueckspiel_VerknuepftJedesRueckspielMitSeinemHinspiel()
    {
        await using var ctx = NeuerContext();
        LigaGrundSetupAnlegen(ctx, out int rundeId);
        var runde = ctx.TurnierRunden.Single(r => r.Id == rundeId);
        (runde.Rundenart, runde.GruppenAnzahl, runde.MitRueckspiel) = (Rundenart.Gruppenphase, 2, true);
        ctx.SaveChanges();
        var ergebnisse = new List<AuslosungsErgebnis>
        {
            new() { PaarungsNummer = 1, HeimMannschaftId = 1 },
            new() { PaarungsNummer = 1, HeimMannschaftId = 2 },
            new() { PaarungsNummer = 2, HeimMannschaftId = 3 },
            new() { PaarungsNummer = 2, HeimMannschaftId = 4 },
        };

        await new SpielplanDienste(ctx).Spielplan.GenerierenAusAuslosungAsync(rundeId, ergebnisse);

        RueckspieleVerknuepftPruefen(ctx.Spiele.Where(s => s.TurnierRundeId == rundeId).ToList(), erwarteteSpiele: 4);
    }

    /// <summary>Jedes Hinspiel hat genau ein Rückspiel, das auf es verweist und Heim und Gast tauscht.</summary>
    private static void RueckspieleVerknuepftPruefen(IReadOnlyList<Spiel> spiele, int erwarteteSpiele)
    {
        spiele.Should().HaveCount(erwarteteSpiele);
        var hinspiele   = spiele.Where(s => !s.IstRueckspiel).ToList();
        var rueckspiele = spiele.Where(s => s.IstRueckspiel).ToList();
        hinspiele.Should().HaveCount(erwarteteSpiele / 2).And.OnlyContain(s => s.HinspielId == null);
        rueckspiele.Select(s => s.HinspielId).Should().BeEquivalentTo(hinspiele.Select(s => (int?)s.Id));
        foreach (var rueck in rueckspiele)
        {
            var hin = hinspiele.Single(s => s.Id == rueck.HinspielId);
            (rueck.HeimMannschaftId, rueck.AuswaertsMannschaftId).Should().Be((hin.AuswaertsMannschaftId, hin.HeimMannschaftId));
        }
    }
}
