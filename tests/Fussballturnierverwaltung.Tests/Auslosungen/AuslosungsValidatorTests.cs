using FluentAssertions;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>
/// Prüft alle Auslosungsart × Rundenart-Kombinationen auf Validator-Ebene:
/// welche sind gültig, welche werden korrekt abgelehnt und mit welcher Fehlermeldung.
/// </summary>
public class AuslosungsValidatorTests
{
    private readonly AuslosungsValidator _validator = new();

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private static TurnierRunde Runde(Rundenart art, int? gruppenAnzahl = null) =>
        new() { Id = 1, Name = art.ToString(), Rundenart = art, GruppenAnzahl = gruppenAnzahl };

    private static Mannschaft Team(int id) => new() { Id = id, Name = $"Team {id}" };

    private static LostopfEintrag Eintrag(int id) =>
        new() { Id = id, MannschaftId = id, Mannschaft = Team(id) };

    private static Lostopf Topf(int id, int reihenfolge, params LostopfEintrag[] eintraege) =>
        new() { Id = id, Reihenfolge = reihenfolge, Name = $"Topf {reihenfolge}", Eintraege = [.. eintraege] };

    private static AuslosungsRegel Regel(AuslosungsRegelArt art) =>
        new() { Regelart = art, Beschreibung = art.ToString(), IstAktiv = true };

    private static Auslosung Auslosung(
        Rundenart rundenart,
        Auslosungsart auslosungsart,
        int? gruppenAnzahl = null,
        IEnumerable<Lostopf>? lostoepfe = null,
        IEnumerable<AuslosungsRegel>? regeln = null)
    {
        var a = new Auslosung
        {
            Id            = 1,
            TurnierRunde  = Runde(rundenart, gruppenAnzahl),
            Auslosungsart = auslosungsart,
            Status        = AuslosungsStatus.Vorbereitung
        };
        foreach (var t in lostoepfe ?? []) a.Lostoepfe.Add(t);
        foreach (var r in regeln ?? [])    a.Regeln.Add(r);
        return a;
    }

    private static List<LostopfEintrag> AlleEintraege(Auslosung a) =>
        [.. a.Lostoepfe.SelectMany(l => l.Eintraege)];

    // ══════════════════════════════════════════════════════════════════════════
    // GÜLTIGE KOMBINATIONEN
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Gruppenphase_Zufall_TeilbareDivision_IstGueltig()
    {
        // 8 Teams, 2 Gruppen → 8 / 2 = 4 Teams pro Gruppe → gültig
        var topf = Topf(1, 1, Enumerable.Range(1, 8).Select(Eintrag).ToArray());
        var a    = Auslosung(Rundenart.Gruppenphase, Auslosungsart.Zufall, gruppenAnzahl: 2, lostoepfe: [topf]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void Gruppenphase_NachToepfen_ToepfeKorrekteGroesse_IstGueltig()
    {
        // 4 Gruppen, 2 Töpfe à 4 Teams → jeder Topf hat genau 4 Einträge (= gruppenAnzahl)
        var topf1 = Topf(1, 1, Enumerable.Range(1, 4).Select(Eintrag).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(5, 4).Select(Eintrag).ToArray());
        var a     = Auslosung(Rundenart.Gruppenphase, Auslosungsart.NachToepfen,
                              gruppenAnzahl: 4, lostoepfe: [topf1, topf2]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void Gruppenphase_NachSetzliste_IstGueltig()
    {
        var topf = Topf(1, 1, Enumerable.Range(1, 8).Select(Eintrag).ToArray());
        var a    = Auslosung(Rundenart.Gruppenphase, Auslosungsart.NachSetzliste,
                             gruppenAnzahl: 4, lostoepfe: [topf]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void Gruppenphase_Benutzerdefiniert_IstGueltig()
    {
        var topf = Topf(1, 1, Enumerable.Range(1, 6).Select(Eintrag).ToArray());
        var a    = Auslosung(Rundenart.Gruppenphase, Auslosungsart.Benutzerdefiniert,
                             gruppenAnzahl: 3, lostoepfe: [topf]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void KoRunde_Zufall_IstGueltig()
    {
        var topf = Topf(1, 1, Enumerable.Range(1, 8).Select(Eintrag).ToArray());
        var a    = Auslosung(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void KoRunde_NachToepfen_ZweiToepfe_IstGueltig()
    {
        var topf1 = Topf(1, 1, Enumerable.Range(1, 4).Select(Eintrag).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(5, 4).Select(Eintrag).ToArray());
        var a     = Auslosung(Rundenart.KoRunde, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void KoRunde_NachSetzliste_IstGueltig()
    {
        var topf = Topf(1, 1, Enumerable.Range(1, 8).Select(Eintrag).ToArray());
        var a    = Auslosung(Rundenart.KoRunde, Auslosungsart.NachSetzliste, lostoepfe: [topf]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void KoRunde_GruppensiegerGegenGruppenzweite_IstGueltig()
    {
        var topf1 = Topf(1, 1, Enumerable.Range(1, 4).Select(Eintrag).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(5, 4).Select(Eintrag).ToArray());
        var a     = Auslosung(Rundenart.KoRunde, Auslosungsart.GruppensiegerGegenGruppenzweite,
                              lostoepfe: [topf1, topf2]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void KoRunde_Benutzerdefiniert_IstGueltig()
    {
        var topf = Topf(1, 1, Enumerable.Range(1, 4).Select(Eintrag).ToArray());
        var a    = Auslosung(Rundenart.KoRunde, Auslosungsart.Benutzerdefiniert, lostoepfe: [topf]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void Finale_GruppensiegerGegenGruppenzweite_IstGueltig()
    {
        // Finale ist eine K.O.-Runde → GruppensiegerGegenGruppenzweite erlaubt
        var topf1 = Topf(1, 1, Eintrag(1));
        var topf2 = Topf(2, 2, Eintrag(2));
        var a     = Auslosung(Rundenart.Finale, Auslosungsart.GruppensiegerGegenGruppenzweite,
                              lostoepfe: [topf1, topf2]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void SpielUmPlatzDrei_GruppensiegerGegenGruppenzweite_IstGueltig()
    {
        var topf1 = Topf(1, 1, Eintrag(1));
        var topf2 = Topf(2, 2, Eintrag(2));
        var a     = Auslosung(Rundenart.SpielUmPlatzDrei, Auslosungsart.GruppensiegerGegenGruppenzweite,
                              lostoepfe: [topf1, topf2]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void Liga_Zufall_IstGueltig()
    {
        var topf = Topf(1, 1, Enumerable.Range(1, 6).Select(Eintrag).ToArray());
        var a    = Auslosung(Rundenart.Liga, Auslosungsart.Zufall, lostoepfe: [topf]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void Liga_TopfZuordnung_GleicheToepfe_IstGueltig()
    {
        var topf1 = Topf(1, 1, Eintrag(1), Eintrag(2));
        var topf2 = Topf(2, 2, Eintrag(3), Eintrag(4));
        var a     = Auslosung(Rundenart.Liga, Auslosungsart.TopfZuordnung, lostoepfe: [topf1, topf2]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void SchweizerSystem_Zufall_IstGueltig()
    {
        var topf = Topf(1, 1, Enumerable.Range(1, 8).Select(Eintrag).ToArray());
        var a    = Auslosung(Rundenart.SchweizerSystem, Auslosungsart.Zufall, lostoepfe: [topf]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void TopfLiga_NachToepfen_IstGueltig()
    {
        var topf1 = Topf(1, 1, Enumerable.Range(1, 4).Select(Eintrag).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(5, 4).Select(Eintrag).ToArray());
        var a     = Auslosung(Rundenart.TopfLiga, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UNGÜLTIGE KOMBINATIONEN — Auslosungsart + Rundenart
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Gruppenphase_TopfZuordnung_WirdAbgelehnt()
    {
        var topf1 = Topf(1, 1, Eintrag(1), Eintrag(2));
        var topf2 = Topf(2, 2, Eintrag(3), Eintrag(4));
        var a     = Auslosung(Rundenart.Gruppenphase, Auslosungsart.TopfZuordnung,
                              gruppenAnzahl: 2, lostoepfe: [topf1, topf2]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*UCL-Ligaphase*");
    }

    [Fact]
    public void Gruppenphase_GruppensiegerGegenGruppenzweite_WirdAbgelehnt()
    {
        var topf1 = Topf(1, 1, Enumerable.Range(1, 4).Select(Eintrag).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(5, 4).Select(Eintrag).ToArray());
        var a     = Auslosung(Rundenart.Gruppenphase, Auslosungsart.GruppensiegerGegenGruppenzweite,
                              gruppenAnzahl: 2, lostoepfe: [topf1, topf2]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*K.O.-Runden*");
    }

    [Fact]
    public void Liga_GruppensiegerGegenGruppenzweite_WirdAbgelehnt()
    {
        var topf1 = Topf(1, 1, Enumerable.Range(1, 4).Select(Eintrag).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(5, 4).Select(Eintrag).ToArray());
        var a     = Auslosung(Rundenart.Liga, Auslosungsart.GruppensiegerGegenGruppenzweite,
                              lostoepfe: [topf1, topf2]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*K.O.-Runden*");
    }

    [Fact]
    public void SchweizerSystem_GruppensiegerGegenGruppenzweite_WirdAbgelehnt()
    {
        var topf1 = Topf(1, 1, Enumerable.Range(1, 4).Select(Eintrag).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(5, 4).Select(Eintrag).ToArray());
        var a     = Auslosung(Rundenart.SchweizerSystem, Auslosungsart.GruppensiegerGegenGruppenzweite,
                              lostoepfe: [topf1, topf2]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*K.O.-Runden*");
    }

    [Fact]
    public void KoRunde_TopfZuordnung_WirdAbgelehnt()
    {
        var topf1 = Topf(1, 1, Eintrag(1), Eintrag(2));
        var topf2 = Topf(2, 2, Eintrag(3), Eintrag(4));
        var a     = Auslosung(Rundenart.KoRunde, Auslosungsart.TopfZuordnung, lostoepfe: [topf1, topf2]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*K.O.-Runden*");
    }

    [Fact]
    public void Finale_TopfZuordnung_WirdAbgelehnt()
    {
        var topf1 = Topf(1, 1, Eintrag(1));
        var topf2 = Topf(2, 2, Eintrag(2));
        var a     = Auslosung(Rundenart.Finale, Auslosungsart.TopfZuordnung, lostoepfe: [topf1, topf2]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*K.O.-Runden*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UNGÜLTIGE KOMBINATIONEN — Gruppenphase-Struktur
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Gruppenphase_FehlendaGruppenAnzahl_WirdAbgelehnt()
    {
        var topf = Topf(1, 1, Enumerable.Range(1, 8).Select(Eintrag).ToArray());
        var a    = Auslosung(Rundenart.Gruppenphase, Auslosungsart.Zufall,
                             gruppenAnzahl: null, lostoepfe: [topf]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Gruppenanzahl*");
    }

    [Fact]
    public void Gruppenphase_TeamsNichtGleichmaessigVerteilbar_WirdAbgelehnt()
    {
        // 7 Teams auf 2 Gruppen → 7 % 2 = 1 → nicht teilbar
        var topf = Topf(1, 1, Enumerable.Range(1, 7).Select(Eintrag).ToArray());
        var a    = Auslosung(Rundenart.Gruppenphase, Auslosungsart.Zufall,
                             gruppenAnzahl: 2, lostoepfe: [topf]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*gleichmäßig*");
    }

    [Fact]
    public void Gruppenphase_NachToepfen_TopfZuGross_WirdAbgelehnt()
    {
        // 4 Gruppen, aber Topf 1 hat 5 statt 4 Teams → falsche Topfgröße
        var topf1 = Topf(1, 1, Enumerable.Range(1, 5).Select(Eintrag).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(6, 3).Select(Eintrag).ToArray());
        var a     = Auslosung(Rundenart.Gruppenphase, Auslosungsart.NachToepfen,
                              gruppenAnzahl: 4, lostoepfe: [topf1, topf2]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Topf*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UNGÜLTIGE KOMBINATIONEN — allgemeine Strukturprüfungen
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void KeineTeilnehmer_WirdAbgelehnt()
    {
        var a = Auslosung(Rundenart.KoRunde, Auslosungsart.Zufall);

        var ergebnis = _validator.Validieren(a, []);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Keine Teilnehmer*");
    }

    [Fact]
    public void NurEinTeilnehmer_WirdAbgelehnt()
    {
        var topf = Topf(1, 1, Eintrag(1));
        var a    = Auslosung(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Mindestens 2*");
    }

    [Fact]
    public void LeererLostopf_WirdAbgelehnt()
    {
        var leererTopf = Topf(1, 1);
        var vollerTopf = Topf(2, 2, Eintrag(1), Eintrag(2));
        var a          = Auslosung(Rundenart.KoRunde, Auslosungsart.Zufall,
                                   lostoepfe: [leererTopf, vollerTopf]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*leer*");
    }

    [Fact]
    public void NachToepfen_OhneToepfe_WirdAbgelehnt()
    {
        var topf = Topf(1, 1, Eintrag(1), Eintrag(2));
        var a    = Auslosung(Rundenart.KoRunde, Auslosungsart.NachToepfen, lostoepfe: [topf]);

        // NachToepfen benötigt mindestens 2 Töpfe (paarweise)
        // 1 Topf → NachToepfenAusloesen iteriert paarweise und liefert 0 Ergebnisse,
        // aber Validator prüft "mindestens einen Lostopf" — kein Fehler für 1 Topf allein.
        // Relevant: NachToepfen ohne Topf überhaupt (keine Lostoepfe)
        var ohneToepfe = Auslosung(Rundenart.KoRunde, Auslosungsart.NachToepfen);
        var ergebnis   = _validator.Validieren(ohneToepfe, []);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Keine Teilnehmer*");
    }

    [Fact]
    public void MannschaftInMehrerenToepfen_WirdAbgelehnt()
    {
        // Team 1 ist in Topf 1 UND Topf 2
        var team1 = Eintrag(1);
        var team1Duplikat = new LostopfEintrag { Id = 10, MannschaftId = 1, Mannschaft = Team(1) };
        var topf1 = Topf(1, 1, team1, Eintrag(2));
        var topf2 = Topf(2, 2, team1Duplikat, Eintrag(3));
        var a     = Auslosung(Rundenart.KoRunde, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mehreren Lostöpfen*");
    }

    [Fact]
    public void TopfZuordnung_UngleicheToepfe_WirdAbgelehnt()
    {
        var topf1 = Topf(1, 1, Eintrag(1), Eintrag(2), Eintrag(3));
        var topf2 = Topf(2, 2, Eintrag(4), Eintrag(5));
        var a     = Auslosung(Rundenart.Liga, Auslosungsart.TopfZuordnung, lostoepfe: [topf1, topf2]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*gleich*");
    }

    [Fact]
    public void TopfZuordnung_NurEinTopf_WirdAbgelehnt()
    {
        var topf = Topf(1, 1, Eintrag(1), Eintrag(2));
        var a    = Auslosung(Rundenart.Liga, Auslosungsart.TopfZuordnung, lostoepfe: [topf]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*2 Lostöpfe*");
    }

    [Fact]
    public void Zufall_MehrereToepfe_WirdAbgelehnt()
    {
        var topf1 = Topf(1, 1, Eintrag(1), Eintrag(2));
        var topf2 = Topf(2, 2, Eintrag(3), Eintrag(4));
        var a     = Auslosung(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf1, topf2]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*1 Lostopf*");
    }

    [Fact]
    public void Benutzerdefiniert_MehrereToepfe_WirdAbgelehnt()
    {
        var topf1 = Topf(1, 1, Eintrag(1), Eintrag(2));
        var topf2 = Topf(2, 2, Eintrag(3), Eintrag(4));
        var a     = Auslosung(Rundenart.KoRunde, Auslosungsart.Benutzerdefiniert, lostoepfe: [topf1, topf2]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*1 Lostopf*");
    }

    [Fact]
    public void NachToepfen_PaarungsModus_EinTopf_WirdAbgelehnt()
    {
        // KoRunde = Paarungsmodus: NachToepfen paart Topf 1↔2 usw. — 1 Topf liefert keine Paarung.
        var topf = Topf(1, 1, Eintrag(1), Eintrag(2));
        var a    = Auslosung(Rundenart.KoRunde, Auslosungsart.NachToepfen, lostoepfe: [topf]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mindestens 2 Lostöpfe*");
    }

    [Fact]
    public void NachToepfen_PaarungsModus_UngeradeAnzahlToepfe_WirdAbgelehnt()
    {
        // 3 Töpfe: Topf 1↔2 werden gepaart, Topf 3 bliebe stillschweigend unverlost.
        var topf1 = Topf(1, 1, Eintrag(1), Eintrag(2));
        var topf2 = Topf(2, 2, Eintrag(3), Eintrag(4));
        var topf3 = Topf(3, 3, Eintrag(5), Eintrag(6));
        var a     = Auslosung(Rundenart.KoRunde, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2, topf3]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*gerade Anzahl Lostöpfe*");
    }

    [Fact]
    public void NachToepfen_Gruppenphase_EinTopf_IstGueltig()
    {
        // Gruppenphase-Modus verteilt Topf-für-Topf je 1 Team pro Gruppe — kein Paarungsmodus,
        // 1 Topf ist hier gültig (gleichbedeutend mit Zufall, aber explizit).
        var topf = Topf(1, 1, Enumerable.Range(1, 4).Select(Eintrag).ToArray());
        var a    = Auslosung(Rundenart.Gruppenphase, Auslosungsart.NachToepfen, gruppenAnzahl: 4, lostoepfe: [topf]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void GruppensiegerGegenGruppenzweite_EinTopf_WirdAbgelehnt()
    {
        var topf = Topf(1, 1, Eintrag(1), Eintrag(2));
        var a    = Auslosung(Rundenart.KoRunde, Auslosungsart.GruppensiegerGegenGruppenzweite, lostoepfe: [topf]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mindestens 2 Lostöpfe*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // REGELKONFLIKTE
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RegelKonflikt_GruppensiegerGegenGruppenzweite_UndGesetzteGegenUngesetzte_WirdAbgelehnt()
    {
        var topf = Topf(1, 1, Enumerable.Range(1, 4).Select(Eintrag).ToArray());
        var a    = Auslosung(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf],
            regeln:
            [
                Regel(AuslosungsRegelArt.GruppensiegerGegenGruppenzweite),
                Regel(AuslosungsRegelArt.GesetzteGegenUngesetzte)
            ]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Regelkonflikt*");
    }

    [Fact]
    public void RegelKonflikt_HeimrechtNachLos_UndHeimrechtNachSetzliste_WirdAbgelehnt()
    {
        var topf = Topf(1, 1, Enumerable.Range(1, 4).Select(Eintrag).ToArray());
        var a    = Auslosung(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf],
            regeln:
            [
                Regel(AuslosungsRegelArt.HeimrechtNachLos),
                Regel(AuslosungsRegelArt.HeimrechtNachSetzliste)
            ]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*schließen sich gegenseitig aus*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // VERBAND-QUOTEN (Issue #146/#27)
    // ══════════════════════════════════════════════════════════════════════════

    private static LostopfEintrag EintragMitVerband(int id, int verbandId) =>
        new() { Id = id, MannschaftId = id, Mannschaft = new Mannschaft { Id = id, Name = $"Team {id}", Land = new Land { VerbandId = verbandId } } };

    private static VerbandsQuote Quote(int verbandId, int maxAnzahl, int? minAnzahl = null) =>
        new() { VerbandId = verbandId, MaxAnzahl = maxAnzahl, MinAnzahl = minAnzahl };

    [Fact]
    public void VerbandQuote_InnerhalbMaxUndMin_IstGueltig()
    {
        var topf = Topf(1, 1, [EintragMitVerband(1, verbandId: 10), EintragMitVerband(2, verbandId: 10), EintragMitVerband(3, verbandId: 20)]);
        var a    = Auslosung(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a), [Quote(10, maxAnzahl: 2, minAnzahl: 1)]);

        ergebnis.IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void VerbandQuote_MaxUeberschritten_WirdAbgelehnt()
    {
        var topf = Topf(1, 1, [EintragMitVerband(1, verbandId: 10), EintragMitVerband(2, verbandId: 10), EintragMitVerband(3, verbandId: 10)]);
        var a    = Auslosung(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a), [Quote(10, maxAnzahl: 2)]);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*maximal 2*");
    }

    [Fact]
    public void VerbandQuote_MinUnterschritten_WirdAbgelehnt()
    {
        var topf = Topf(1, 1, [EintragMitVerband(1, verbandId: 10), EintragMitVerband(2, verbandId: 20)]);
        var a    = Auslosung(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf]);

        var ergebnis = _validator.Validieren(a, AlleEintraege(a), [Quote(10, maxAnzahl: 5, minAnzahl: 2)]);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mindestens 2*");
    }

    [Fact]
    public void VerbandQuote_OhneKonfigurierteQuoten_WirdNichtGeprueft()
    {
        var topf = Topf(1, 1, [EintragMitVerband(1, verbandId: 10), EintragMitVerband(2, verbandId: 10), EintragMitVerband(3, verbandId: 10)]);
        var a    = Auslosung(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf]);

        _validator.Validieren(a, AlleEintraege(a)).IstGueltig.Should().BeTrue();
    }
}
