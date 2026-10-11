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

public class PlatzhalterKaskadeServiceTests
{
    // ── Tests: Kaskaden-Reset für über Auslosung aufgelöste Folgerunden (Issue #88) ─

    [Fact]
    public async Task Planen_ErkenntStaleKoSpiel_WennGruppenphaseErgebnisSichAendert()
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

        var plan = await dienste.Kaskade.PlanenAsync(1, new HashSet<int>());

        plan.HatAuswirkungen.Should().BeTrue();
        plan.Schritte.Should().ContainSingle(s => s.RundeId == koRundeId);
    }

    [Fact]
    public async Task Ausfuehren_GeneriertKoRundeMitNeuemGruppensieger()
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

        var plan = await dienste.Kaskade.PlanenAsync(1, new HashSet<int>());
        await dienste.Kaskade.AusfuehrenAsync(plan);

        var koSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId).ToList();
        koSpiele.Should().HaveCount(1);
        koSpiele[0].HeimMannschaftId.Should().Be(2, "Sieger Gruppe A ist jetzt Team B");
        koSpiele[0].AuswaertsMannschaftId.Should().Be(4, "2. Gruppe B bleibt unverändert Team D");
    }

    [Fact]
    public async Task RundenSpielplanAusAuslosungErsetzen_ErzeugtSpielplanGenauEinmal()
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
    public async Task Kaskade_LoeschtBereitsGespieltesFinaleUndGeneriertNeu_WennHalbfinaleSichAendert()
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

        var plan = await dienste.Kaskade.PlanenAsync(1, new HashSet<int>());

        plan.HatAuswirkungen.Should().BeTrue();
        var schritt = plan.Schritte.Should().ContainSingle(s => s.RundeId == finaleRundeId).Subject
            .Should().BeOfType<KaskadenSpielSchritt>().Subject;
        schritt.SpielId.Should().Be(finaleSpielId);
        schritt.HatteErgebnis.Should().BeTrue(
            "das bereits erfasste Finale-Ergebnis basiert auf einer jetzt ungültigen Halbfinale-Paarung");

        await dienste.Kaskade.AusfuehrenAsync(plan);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1);
        finaleSpiele[0].Id.Should().Be(finaleSpielId, "dasselbe Spiel wird korrigiert statt gelöscht+neu angelegt");
        ctx.SpielErgebnisse.Any(e => e.SpielId == finaleSpielId).Should().BeFalse("das ungültige Ergebnis wurde entfernt");
        finaleSpiele[0].HeimMannschaftId.Should().Be(2, "Sieger HF 1 ist jetzt Team B");
    }

    [Fact]
    public async Task Kaskade_RuehrtUnbetroffenesSpielDerselbenRundeNichtAn()
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

        var plan = await dienste.Kaskade.PlanenAsync(1, new HashSet<int>());
        var spielSchritt = plan.Schritte.Should().ContainSingle().Subject.Should().BeOfType<KaskadenSpielSchritt>().Subject;
        spielSchritt.SpielId.Should().Be(hf1Id, "nur HF1 hängt von Gruppe 1 ab");

        await dienste.Kaskade.AusfuehrenAsync(plan);

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
    public async Task Planen_KeineAuswirkung_WennRundeOhneAuslosungGeneriertWurde()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var dienste = new SpielplanDienste(ctx);
        // Erzeugt das Finale automatisch OHNE Auslosung (Issue #113/#125-Mechanismus)
        await dienste.Finale.AbgleichenAsync(1);

        var plan = await dienste.Kaskade.PlanenAsync(1, new HashSet<int>());

        plan.HatAuswirkungen.Should().BeFalse(
            "die Finale-Runde wurde ohne Auslosung generiert — Issue #88 deckt nur Auslosungs-basierte Runden ab");
    }

    [Fact]
    public async Task Ausfuehren_IstIdempotent_NachAusfuehrungKeineWeiterenAuswirkungen()
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

        var plan = await dienste.Kaskade.PlanenAsync(1, new HashSet<int>());
        await dienste.Kaskade.AusfuehrenAsync(plan);

        var planNachAusfuehrung = await dienste.Kaskade.PlanenAsync(1, new HashSet<int>());

        planNachAusfuehrung.HatAuswirkungen.Should().BeFalse();
    }

    // ── Tests: Platzhalter-Gruppenphase in der Kaskade (Issue #395) ─────────

    [Fact]
    public async Task Kaskade_PlatzhalterGruppenphase_OhneAenderung_KeineAuswirkung()
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

        var plan = await dienste.Kaskade.PlanenAsync(1, new HashSet<int>());

        plan.HatAuswirkungen.Should().BeFalse("an der Quali hat sich nichts geändert");
    }

    [Fact]
    public async Task Kaskade_PlatzhalterGruppenphase_KorrigiertNurSpieleDesBetroffenenTeams()
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

        var plan = await dienste.Kaskade.PlanenAsync(1, new HashSet<int>());
        var betroffen = ctx.Spiele
            .Where(s => s.TurnierRundeId == gruppeId && (s.HeimMannschaftId == 1 || s.AuswaertsMannschaftId == 1))
            .Select(s => s.Id).ToList();
        betroffen.Should().HaveCount(3);
        plan.Schritte.OfType<KaskadenSpielSchritt>().Select(s => s.SpielId)
            .Should().BeEquivalentTo(betroffen, "nur die drei Spiele von „Sieger Quali 1“ hängen an Quali-Spiel 1");

        await dienste.Kaskade.AusfuehrenAsync(plan);

        var gruppenSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == gruppeId).ToList();
        gruppenSpiele.Should().NotContain(s => s.HeimMannschaftId == 1 || s.AuswaertsMannschaftId == 1);
        gruppenSpiele.Count(s => s.HeimMannschaftId == 2 || s.AuswaertsMannschaftId == 2).Should().Be(3);
        gruppenSpiele.Should().OnlyContain(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue);
        ctx.SpielErgebnisse.Count(e => betroffen.Contains(e.SpielId)).Should().Be(0);
        ctx.SpielErgebnisse.Count(e => e.SpielId > 4 && !betroffen.Contains(e.SpielId))
            .Should().Be(3, "die Spiele ohne „Sieger Quali 1“ behalten ihr Ergebnis");
    }

    // ── Tests: gespeicherte Gruppennummer (Issue #397) ──────────────────────

    [Fact]
    public async Task RundenSpielplanLoeschen_SetztStaleFolgerundenPlatzhalterZurueck_WennQuellrundeNeuGeneriertWird()
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
        await dienste.Kaskade.RundenSpielplanLoeschenAsync(gruppenphaseRundeId);

        var koSpielNachher = ctx.Spiele.First(s => s.TurnierRundeId == koRundeId);
        koSpielNachher.HeimMannschaftId.Should().BeNull(
            "Gruppenphase hat keine Spiele mehr — 'Sieger Gruppe A' ist nicht mehr auflösbar");
        koSpielNachher.HeimPlatzhalterName.Should().Be("Sieger Gruppe A");
        koSpielNachher.AuswaertsMannschaftId.Should().BeNull(
            "Gruppenphase hat keine Spiele mehr — '2. Gruppe B' ist nicht mehr auflösbar");
        koSpielNachher.AuswaertsPlatzhalterName.Should().Be("2. Gruppe B");
    }

    // ── Tests: K.o.-Runden mit Hin-/Rückspiel und Platzhaltern (Issue #346) ─

    [Fact]
    public async Task Kaskade_KoRundeMitRueckspiel_KorrigiertHinUndRueckspielJederPaarung()
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

        var plan = await dienste.Kaskade.PlanenAsync(1, new HashSet<int>());
        plan.Schritte.Should().HaveCount(4, "Hin- und Rückspiel beider Paarungen sind betroffen");
        await dienste.Kaskade.AusfuehrenAsync(plan);

        var teams = ctx.Spiele.Where(s => s.TurnierRundeId == koRundeId).OrderBy(s => s.Spielnummer)
            .Select(s => new { s.HeimMannschaftId, s.AuswaertsMannschaftId }).ToList();
        teams.Should().Equal(
            new { HeimMannschaftId = (int?)2, AuswaertsMannschaftId = (int?)4 },   // Hinspiel: Sieger Gr. A – 2. Gr. B
            new { HeimMannschaftId = (int?)3, AuswaertsMannschaftId = (int?)1 },   // Hinspiel: Sieger Gr. B – 2. Gr. A
            new { HeimMannschaftId = (int?)4, AuswaertsMannschaftId = (int?)2 },   // Rückspiele gespiegelt
            new { HeimMannschaftId = (int?)1, AuswaertsMannschaftId = (int?)3 });
    }
}
