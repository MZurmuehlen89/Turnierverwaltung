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

public class FinaleAutomatikTests
{
    // ── Tests: automatische Finale-Generierung ohne Auslosung (Issue #113) ─

    [Fact]
    public async Task Abgleichen_GeneriertFinale_WennHalbfinaleEntschieden()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var dienste = new SpielplanDienste(ctx);

        await dienste.Finale.AbgleichenAsync(1);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1);
        var teilnehmer = new[] { finaleSpiele[0].HeimMannschaftId, finaleSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 1, 3 });
    }

    [Fact]
    public async Task Abgleichen_GeneriertNicht_WennHalbfinaleNochOffen()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: null);
        var dienste = new SpielplanDienste(ctx);

        await dienste.Finale.AbgleichenAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().BeEmpty();
    }

    [Fact]
    public async Task Abgleichen_GeneriertNichtErneut_WennBereitsVorhanden()
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

        await dienste.Finale.AbgleichenAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().HaveCount(1,
            "es darf kein zweites Finalspiel automatisch hinzugefügt werden");
    }

    [Fact]
    public async Task Abgleichen_GeneriertNicht_BeiHalbfinaleMitRueckspiel()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId, hfMitRueckspiel: true);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var dienste = new SpielplanDienste(ctx);

        await dienste.Finale.AbgleichenAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().BeEmpty(
            "Aggregat-Sieger-Ermittlung über zwei Spielbeine ist bewusst nicht Teil dieser Phase");
    }

    [Fact]
    public async Task Abgleichen_EntferntFinale_WennHalbfinalErgebnisZurueckgesetztWird()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var dienste = new SpielplanDienste(ctx);
        await dienste.Finale.AbgleichenAsync(1);
        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().HaveCount(1, "Vorbedingung: Finale wurde generiert");

        // Ergebnis eines Halbfinalspiels zurückgesetzt (z.B. "Ergebnis löschen")
        var spiel2Ergebnis = ctx.SpielErgebnisse.First(e => e.SpielId == 2);
        ctx.SpielErgebnisse.Remove(spiel2Ergebnis);
        var spiel2 = ctx.Spiele.First(s => s.Id == 2);
        spiel2.Status = SpielStatus.Geplant;
        ctx.SaveChanges();

        await dienste.Finale.AbgleichenAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().BeEmpty(
            "das noch nicht gespielte Finale muss entfernt werden, wenn das Halbfinale nicht mehr entschieden ist");
    }

    [Fact]
    public async Task Abgleichen_RegeneriertFinale_WennHalbfinaleAndersAusgeht()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var dienste = new SpielplanDienste(ctx);
        await dienste.Finale.AbgleichenAsync(1);

        // Halbfinale neu simuliert - diesmal gewinnen Team B und Team D statt Team A und Team C
        var erg1 = ctx.SpielErgebnisse.First(e => e.SpielId == 1);
        erg1.SiegerMannschaftId = 2; erg1.HeimToreRegulaer = 0; erg1.AuswaertsToreRegulaer = 1;
        var erg2 = ctx.SpielErgebnisse.First(e => e.SpielId == 2);
        erg2.SiegerMannschaftId = 4; erg2.HeimToreRegulaer = 0; erg2.AuswaertsToreRegulaer = 1;
        ctx.SaveChanges();

        await dienste.Finale.AbgleichenAsync(1);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1);
        var teilnehmer = new[] { finaleSpiele[0].HeimMannschaftId, finaleSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 2, 4 });
    }

    [Fact]
    public async Task Abgleichen_RuehrtBereitsGespieltesFinaleNichtAn()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var dienste = new SpielplanDienste(ctx);
        await dienste.Finale.AbgleichenAsync(1);

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

        await dienste.Finale.AbgleichenAsync(1);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1, "ein bereits gespieltes Finale darf nie automatisch verändert werden");
        finaleSpiele[0].Id.Should().Be(finaleSpielId);
        var teilnehmer = new[] { finaleSpiele[0].HeimMannschaftId, finaleSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 1, 3 }, "die ursprüngliche, bereits gespielte Paarung bleibt erhalten");
    }

    [Fact]
    public async Task Abgleichen_GeneriertSpielUmPlatzDreiMitVerlierern_NichtSiegern()
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

        await dienste.Finale.AbgleichenAsync(1);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1);
        new[] { finaleSpiele[0].HeimMannschaftId, finaleSpiele[0].AuswaertsMannschaftId }
            .Should().BeEquivalentTo(new int?[] { 1, 3 }, "Finale bekommt die Sieger (Team A, Team C)");

        var platzDreiSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == spielUmPlatzDrei.Id).ToList();
        platzDreiSpiele.Should().HaveCount(1);
        new[] { platzDreiSpiele[0].HeimMannschaftId, platzDreiSpiele[0].AuswaertsMannschaftId }
            .Should().BeEquivalentTo(new int?[] { 2, 4 }, "Spiel um Platz 3 bekommt die Verlierer (Team B, Team D), nicht die Sieger");
    }

    // ── Tests: generalisierter Sync-Hook mit expliziten Uebergangsregeln (Issue #130) ─

    [Fact]
    public async Task Abgleichen_RegelbasiertMitZweiUnabhaengigenKoZweigen_GeneriertZielrunde()
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

        await dienste.Finale.AbgleichenAsync(1);

        var zielSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == zielRundeId).ToList();
        zielSpiele.Should().HaveCount(1);
        var teilnehmer = new[] { zielSpiele[0].HeimMannschaftId, zielSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 1, 3 });
    }

    [Fact]
    public async Task Abgleichen_RegelbasiertNurEinZweigEntschieden_GeneriertNicht()
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

        await dienste.Finale.AbgleichenAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == zielRundeId).Should().BeEmpty();
    }

    // ── Tests: gruppenübergreifende Rangfolge-Regel (Issue #184) ────────────────

    [Fact]
    public async Task Abgleichen_AnzahlBesteRegel_GeneriertMitDenBeidenBestenGruppendritten()
    {
        await using var ctx = NeuerContext();
        AnzahlBesteSetupAnlegen(ctx, out int gruppenphaseId, out int zielRundeId);
        AnzahlBesteSpieleHinzufuegen(ctx, gruppenphaseId);
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        await dienste.Finale.AbgleichenAsync(1);

        var zielSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == zielRundeId).ToList();
        zielSpiele.Should().HaveCount(1);
        var teilnehmer = new[] { zielSpiele[0].HeimMannschaftId, zielSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 8, 5 }, "Team 8 (GD -1) und Team 5 (GD -2) sind die beiden besten Gruppendritten, Team 2 (GD -3) fällt raus");
    }

    [Fact]
    public async Task Abgleichen_AnzahlBesteRegel_GeneriertNicht_WennEineGruppeNochOffenIst()
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

        await dienste.Finale.AbgleichenAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == zielRundeId).Should().BeEmpty();
    }
}
