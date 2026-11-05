using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class TurnierValidatorTests
{
    private readonly TurnierValidator _validator = new();

    private static TurnierRundeEingabe Runde(
        string name, Rundenart art, int eingang, int ausgang,
        bool mitRueckspiel = false, bool auswaerts = false, int? gruppenAnzahl = null,
        bool eingangUeberUebergangsregeln = false) =>
        new(name, art, eingang, ausgang, RegelwerkId: 1,
            MitRueckspiel: mitRueckspiel, AuswaertstorRegelAktiv: auswaerts,
            GruppenAnzahl: gruppenAnzahl,
            EingangUeberUebergangsregeln: eingangUeberUebergangsregeln);

    [Fact]
    public void GueltigeRundenKette_IstGueltig()
    {
        var runden = new[]
        {
            Runde("Gruppenphase",  Rundenart.Gruppenphase, eingang: 32, ausgang: 16, gruppenAnzahl: 8),
            Runde("Achtelfinale",  Rundenart.KoRunde,      eingang: 16, ausgang: 8),
            Runde("Viertelfinale", Rundenart.KoRunde,      eingang: 8,  ausgang: 4),
            Runde("Halbfinale",    Rundenart.KoRunde,      eingang: 4,  ausgang: 2),
            Runde("Finale",        Rundenart.Finale,        eingang: 2,  ausgang: 1),
        };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeTrue();
    }

    // Feste Gruppenköpfe (#487): nur Gruppen, die es gibt, und keine Gruppe doppelt vergeben.
    [Theory]
    [InlineData(new[] { 1, 9 }, null, "*Gruppe, die es bei 8 Gruppen nicht gibt*")]
    [InlineData(new[] { 1 }, 1, "*Gruppe A*zugleich*")]
    public void Gruppenkoepfe_Widerspruch_IstUngueltig(int[] gastgeber, int? titelverteidiger, string fehler)
    {
        var runde = Runde("Gruppenphase", Rundenart.Gruppenphase, eingang: 32, ausgang: 16, gruppenAnzahl: 8) with
        {
            GastgeberGruppen = gastgeber, TitelverteidigerGruppe = titelverteidiger
        };

        _validator.RundenValidieren([runde]).Fehler.Should().ContainMatch(fehler);
    }

    [Fact]
    public void RundenUebergang_FalscheAnzahl_IstUngueltig()
    {
        // Runde 1 liefert 15, Runde 2 erwartet 16
        var runden = new[]
        {
            Runde("Gruppenphase", Rundenart.Gruppenphase, eingang: 32, ausgang: 15, gruppenAnzahl: 8),
            Runde("Achtelfinale", Rundenart.KoRunde,      eingang: 16, ausgang: 8),
        };

        var ergebnis = _validator.RundenValidieren(runden);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*16*15*");
    }

    [Fact]
    public void AuswaertstorRegel_OhneHinRueckspiel_IstUngueltig()
    {
        var runden = new[]
        {
            // MitRueckspiel=false, aber AuswaertstorRegelAktiv=true
            Runde("Viertelfinale", Rundenart.KoRunde, eingang: 8, ausgang: 4, mitRueckspiel: false, auswaerts: true),
        };

        var ergebnis = _validator.RundenValidieren(runden);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Auswärtstorregel*keine Hin- und Rückspiele*");
    }

    [Fact]
    public void AuswaertstorRegel_BeiHinRueckspiel_IstGueltig()
    {
        var runden = new[]
        {
            Runde("Halbfinale H/R", Rundenart.KoRunde, eingang: 4, ausgang: 2, mitRueckspiel: true, auswaerts: true),
            Runde("Finale",         Rundenart.Finale,  eingang: 2, ausgang: 1),
        };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void FinaleOhneEinenSieger_IstUngueltig()
    {
        var runden = new[]
        {
            Runde("Finale", Rundenart.Finale, eingang: 2, ausgang: 2), // 2 Ausgänge statt 1
        };

        var ergebnis = _validator.RundenValidieren(runden);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Finale*1 Sieger*");
    }

    [Fact]
    public void KeineRunden_IstUngueltig()
    {
        var ergebnis = _validator.RundenValidieren([]);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mindestens eine Runde*");
    }

    [Fact]
    public void KoRundeMitAusgang_GroesserAlsEingang_IstUngueltig()
    {
        var runden = new[]
        {
            Runde("Halbfinale", Rundenart.KoRunde, eingang: 4, ausgang: 6), // weder Hälfte noch voll
        };

        var ergebnis = _validator.RundenValidieren(runden);

        ergebnis.IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void KoRundeMitAusgangGleichEingang_IstGueltig()
    {
        // Issue #334: Ausgang == Eingang ist der Split-Sonderfall (Sieger UND Verlierer steigen auf, in
        // getrennte Zielrunden, z.B. Halbfinale → Finale + Spiel um Platz 3 über explizite Übergangsregeln).
        var runden = new[]
        {
            Runde("Halbfinale", Rundenart.KoRunde, eingang: 4, ausgang: 4),
        };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void KoGeschwisterpaar_SpielUmPlatzDreiVorFinale_IstGueltig()
    {
        // FIFA-WM-Struktur: Halbfinale speist SOWOHL Spiel um Platz 3 (Verlierer) ALS AUCH Finale (Sieger).
        var runden = new[]
        {
            Runde("Halbfinale",       Rundenart.KoRunde,          eingang: 4, ausgang: 2),
            Runde("Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, eingang: 2, ausgang: 1),
            Runde("Finale",           Rundenart.Finale,           eingang: 2, ausgang: 1),
        };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void KoGeschwisterpaar_FinaleVorSpielUmPlatzDrei_IstGueltig()
    {
        // Umgekehrte Reihenfolge der Geschwisterrunden muss ebenfalls erkannt werden.
        var runden = new[]
        {
            Runde("Halbfinale",       Rundenart.KoRunde,          eingang: 4, ausgang: 2),
            Runde("Finale",           Rundenart.Finale,           eingang: 2, ausgang: 1),
            Runde("Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, eingang: 2, ausgang: 1),
        };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void EingangUeberUebergangsregeln_WirdNichtGegenVorrundeGeprueft()
    {
        // Eingang (2) passt nicht zum Ausgang der Vorrunde (16) — aber die Runde wird über
        // Übergangsregeln gespeist, daher darf die Kettenprüfung hier nicht greifen.
        var runden = new[]
        {
            Runde("Liga",   Rundenart.Liga, eingang: 16, ausgang: 16),
            Runde("Finale", Rundenart.Finale, eingang: 2, ausgang: 1, eingangUeberUebergangsregeln: true),
        };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeTrue();
    }

    [Theory]
    [InlineData(Rundenart.KoRunde)]
    [InlineData(Rundenart.Finale)]
    [InlineData(Rundenart.SpielUmPlatzDrei)]
    public void KoRundenart_UngeradeEingangsanzahl_IstUngueltig(Rundenart art)
    {
        var runden = new[] { Runde("Runde", art, eingang: 5, ausgang: 2) };

        var ergebnis = _validator.RundenValidieren(runden);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*gerade Eingangsmannschaftsanzahl*");
    }

    [Theory]
    [InlineData(Rundenart.KoRunde)]
    [InlineData(Rundenart.Finale)]
    [InlineData(Rundenart.SpielUmPlatzDrei)]
    public void KoRundenart_AusgangUngleichHalberEingang_IstUngueltig(Rundenart art)
    {
        // Eingang gerade, Ausgang aber nicht exakt die Hälfte
        var runden = new[] { Runde("Runde", art, eingang: 8, ausgang: 3) };

        var ergebnis = _validator.RundenValidieren(runden);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*entweder der Hälfte*");
    }

    [Theory]
    [InlineData(Rundenart.KoRunde)]
    [InlineData(Rundenart.Finale)]
    [InlineData(Rundenart.SpielUmPlatzDrei)]
    public void KoRundenart_AusgangGleichHalberEingang_IstGueltig(Rundenart art)
    {
        // eingang: 2 / ausgang: 1 passt gleichzeitig zur "Finale liefert genau 1 Sieger"-Regel
        var runden = new[] { Runde("Runde", art, eingang: 2, ausgang: 1) };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeTrue();
    }

    // ── BasisdatenValidieren (Issue #294 — Nachfolger von TurnierVorlageValidatorTests, vereinheitlicht
    // mit der früheren entitätsbasierten TurnierInstanzValidieren) ────────────────────────────────

    [Fact]
    public void GueltigeBasisdaten_IstGueltig()
    {
        _validator.BasisdatenValidieren("WM", "2026", 32, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1))
            .IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void GueltigeBasisdatenOhneZielMannschaftsanzahlUndDaten_IstGueltig()
    {
        _validator.BasisdatenValidieren("WM", "2026", null, null, null).IstGueltig.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LeererName_IstUngueltig(string name)
    {
        _validator.BasisdatenValidieren(name, "2026", 32, null, null).IstGueltig.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LeereSaison_IstUngueltig(string saison)
    {
        _validator.BasisdatenValidieren("WM", saison, 32, null, null).IstGueltig.Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void ZielMannschaftsanzahlUnter2_IstUngueltig(int anzahl)
    {
        _validator.BasisdatenValidieren("WM", "2026", anzahl, null, null).IstGueltig.Should().BeFalse();
    }

    [Fact]
    public void EnddatumVorStartdatum_IstUngueltig()
    {
        _validator.BasisdatenValidieren("WM", "2026", 32, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1))
            .IstGueltig.Should().BeFalse();
    }

    // ── VorlagenEindeutigkeitValidieren (Issue #367) ────────────────────────────────────────

    private static readonly Domain.Entitaeten.Turniere.TurnierInstanz[] _vorlagen =
    [
        new() { Id = 1, Name = "FIFA WM", Variante = "32 Teams · 1998–2022", Status = TurnierStatus.Vorlage },
        new() { Id = 2, Name = "FIFA WM", Variante = "48 Teams · ab 2026", Status = TurnierStatus.Vorlage },
    ];

    [Fact]
    public void VorlagenEindeutigkeit_GleicherNameAndereVariante_IstGueltig()
    {
        TurnierValidator.VorlagenEindeutigkeitValidieren("FIFA WM", "16 Teams · 1934–1978", null, _vorlagen)
            .IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void VorlagenEindeutigkeit_GleicherNameUndVarianteOhneBeachtungVonGrossKleinUndLeerraum_IstUngueltig()
    {
        TurnierValidator.VorlagenEindeutigkeitValidieren(" fifa wm ", "32 TEAMS · 1998–2022 ", null, _vorlagen)
            .IstGueltig.Should().BeFalse();
    }

    [Fact]
    public void VorlagenEindeutigkeit_EigeneVorlageZaehltNicht()
    {
        TurnierValidator.VorlagenEindeutigkeitValidieren("FIFA WM", "32 Teams · 1998–2022", 1, _vorlagen)
            .IstGueltig.Should().BeTrue();
    }

    // ── RegelwerkPflichtValidieren (Issue #294) ─────────────────────────────────────────────

    [Fact]
    public void RegelwerkPflicht_StatusVorlage_IgnoriertFehlendesRegelwerk()
    {
        var runden = new[] { new Domain.Entitaeten.Turniere.TurnierRunde { Name = "Gruppenphase", RegelwerkId = null } };

        _validator.RegelwerkPflichtValidieren(runden, Domain.Enums.TurnierStatus.Vorlage).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void RegelwerkPflicht_StatusGeplantOhneRegelwerk_IstUngueltig()
    {
        var runden = new[] { new Domain.Entitaeten.Turniere.TurnierRunde { Name = "Gruppenphase", RegelwerkId = null } };

        _validator.RegelwerkPflichtValidieren(runden, Domain.Enums.TurnierStatus.Geplant).IstGueltig.Should().BeFalse();
    }

    [Fact]
    public void RegelwerkPflicht_StatusGeplantMitRegelwerkAufAllenRunden_IstGueltig()
    {
        var runden = new[] { new Domain.Entitaeten.Turniere.TurnierRunde { Name = "Gruppenphase", RegelwerkId = 1 } };

        _validator.RegelwerkPflichtValidieren(runden, Domain.Enums.TurnierStatus.Geplant).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void RegelwerkPflicht_StatusGeplantOhneRundenregelwerkMitStandardRegelwerk_IstGueltig()
    {
        var runden = new[] { new Domain.Entitaeten.Turniere.TurnierRunde { Name = "Runde 1", RegelwerkId = null } };

        _validator.RegelwerkPflichtValidieren(runden, Domain.Enums.TurnierStatus.Geplant, standardRegelwerkId: 1)
            .IstGueltig.Should().BeTrue();
    }

    // ── Überraschungsfaktor (Issue #360) ───────────────────────────────────

    [Theory]
    [InlineData(0, true)]
    [InlineData(20, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    [InlineData(101, false)]
    public void Ueberraschungsfaktor_NurZwischen0Und100Gueltig(int faktor, bool gueltig)
    {
        var runden = new[] { Runde("Finale", Rundenart.Finale, eingang: 2, ausgang: 1) with { Ueberraschungsfaktor = faktor } };

        _validator.RundenValidieren(runden).IstGueltig.Should().Be(gueltig);
    }

    // ── Tabellenbereiche (#420) ───────────────────────────────────────────────

    private static TurnierRundeEingabe LigaMitBereichen(params TabellenBereichEingabe[] bereiche) =>
        Runde("Saison", Rundenart.Liga, eingang: 18, ausgang: 1) with { TabellenBereiche = bereiche };

    private static TabellenBereichEingabe Bereich(int von, int bis, string bezeichnung = "Bereich") =>
        new(von, bis, TabellenBereichArt.Weiterkommen, bezeichnung);

    [Fact]
    public void TabellenBereiche_GueltigeBereicheMitLueckeImMittelfeld_SindGueltig() =>
        TurnierValidator.TabellenBereicheValidieren(LigaMitBereichen(Bereich(1, 4), Bereich(5, 5), Bereich(17, 18)))
            .Should().BeEmpty();

    [Fact]
    public void TabellenBereiche_UeberschneidungIstUngueltig() =>
        TurnierValidator.TabellenBereicheValidieren(LigaMitBereichen(Bereich(1, 4, "CL"), Bereich(4, 5, "EL")))
            .Should().ContainSingle().Which.Text.Should().Contain("überschneiden sich");

    [Theory]
    [InlineData(0, 2)]
    [InlineData(5, 4)]
    [InlineData(17, 19)]
    public void TabellenBereiche_PlatzAusserhalbDerTabelleOderVertauschtIstUngueltig(int von, int bis) =>
        TurnierValidator.TabellenBereicheValidieren(LigaMitBereichen(Bereich(von, bis))).Should().ContainSingle();

    [Fact]
    public void TabellenBereiche_OhneBezeichnungIstUngueltig() =>
        TurnierValidator.TabellenBereicheValidieren(LigaMitBereichen(Bereich(1, 1, " "))).Should().ContainSingle();

    [Fact]
    public void TabellenBereiche_GruppenphaseGiltDieGroesseEinerGruppe()
    {
        var gruppenphase = Runde("Gruppenphase", Rundenart.Gruppenphase, eingang: 16, ausgang: 8, gruppenAnzahl: 4)
            with { TabellenBereiche = [Bereich(4, 4), Bereich(5, 5, "zu weit")] };

        TurnierValidator.TabellenBereicheValidieren(gruppenphase).Should().ContainSingle().Which.Text.Should().Contain("zu weit");
    }

    [Fact]
    public void TabellenBereiche_FehlerBlockierenDieRundenvalidierung() =>
        _validator.RundenValidieren([LigaMitBereichen(Bereich(1, 19))]).IstGueltig.Should().BeFalse();

    // ── Lücken vor der Zerlegung (#499) — exakte Meldungen ─────────────────

    [Fact]
    public void RundeOhneNamen_IstUngueltig() =>
        _validator.RundenValidieren([Runde(" ", Rundenart.Liga, eingang: 4, ausgang: 4)])
            .Fehler.Should().Equal("Runde muss einen Namen haben.");

    [Fact]
    public void ZuKleineAnzahlen_MeldenEingangUndAusgang() =>
        _validator.RundenValidieren([Runde("Liga", Rundenart.Liga, eingang: 1, ausgang: 0)])
            .Fehler.Should().Equal(
                "Runde 'Liga': EingangsMannschaftsanzahl muss mindestens 2 betragen (war: 1).",
                "Runde 'Liga': AusgangsMannschaftsanzahl muss mindestens 1 betragen.");

    [Fact]
    public void Gruppenphase_AusgangNichtKleinerAlsEingang_IstUngueltig() =>
        _validator.RundenValidieren([Runde("GP", Rundenart.Gruppenphase, eingang: 8, ausgang: 8, gruppenAnzahl: 2)])
            .Fehler.Should().Equal("Runde 'GP': Ausgangsteams (8) müssen kleiner als Eingangsteams (8) sein.");

    [Fact]
    public void Liga_AusgangGleichEingang_IstGueltig() =>
        _validator.RundenValidieren([Runde("Liga", Rundenart.Liga, eingang: 18, ausgang: 18)])
            .IstGueltig.Should().BeTrue();

    [Fact]
    public void Gruppenphase_OhneGruppenanzahl_IstUngueltig() =>
        _validator.RundenValidieren([Runde("GP", Rundenart.Gruppenphase, eingang: 8, ausgang: 4)])
            .Fehler.Should().Equal("Runde 'GP': Gruppenphase erfordert mindestens 2 Gruppen (GruppenAnzahl war: 0).");

    [Fact]
    public void MehrereFehler_KommenInDerReihenfolgeDerPruefschritte()
    {
        var runden = new[]
        {
            Runde("VF", Rundenart.KoRunde, eingang: 7, ausgang: 4, auswaerts: true) with { Ueberraschungsfaktor = 120 },
            Runde("HF", Rundenart.KoRunde, eingang: 6, ausgang: 3),
            Runde("Finale", Rundenart.Finale, eingang: 2, ausgang: 2),
        };

        _validator.RundenValidieren(runden).Fehler.Should().Equal(
            "Runde 'VF': K.-o.-Rundenarten benötigen eine gerade Eingangsmannschaftsanzahl (war: 7).",
            "Runde 'VF': Auswärtstorregel ist aktiv, obwohl keine Hin- und Rückspiele gespielt werden.",
            "Runde 'VF': Überraschungsfaktor muss zwischen 0 und 100 % liegen (war: 120).",
            "Runde 2 ('HF') erwartet 6 Mannschaften, Runde 1 ('VF') liefert aber 4.",
            "Runde 3 ('Finale') erwartet 2 Mannschaften, Runde 2 ('HF') liefert aber 3.",
            "Das Finale ('Finale') muss genau 1 Sieger liefern (AusgangsMannschaftsanzahl war: 2).");
    }

    // ── Feldbezug der Runden (#601) ───────────────────────────────────────────

    [Fact]
    public void Runden_FehlerTragenRundeUndFeld_DieRundenketteNurDieRunde()
    {
        var runden = new[]
        {
            Runde("VF", Rundenart.KoRunde, eingang: 7, ausgang: 4, auswaerts: true),
            Runde("HF", Rundenart.KoRunde, eingang: 6, ausgang: 3),
            Runde("Finale", Rundenart.Finale, eingang: 2, ausgang: 2),
        };

        _validator.RundenValidieren(runden).Meldungen.Select(m => (m.Feld, m.Zeile)).Should().Equal(
            (nameof(TurnierRundeEingabe.EingangsMannschaftsanzahl), 0),
            (nameof(TurnierRundeEingabe.AuswaertstorRegelAktiv), 0),
            (null, 1),
            (null, 2),
            (nameof(TurnierRundeEingabe.AusgangsMannschaftsanzahl), 2));
    }

    [Fact]
    public void TabellenBereiche_UnterzeileIstDiePositionInDerEingabe_NichtInDerSortierung()
    {
        // Eingabe unsortiert: der Bereich ohne Bezeichnung steht an Position 0, überschneidet sich aber als hinterer
        var runde = LigaMitBereichen(Bereich(3, 5, " "), Bereich(1, 3, "CL"));

        TurnierValidator.TabellenBereicheValidieren(runde, rundenIndex: 2)
            .Select(f => (f.Feld, f.Zeile, f.Unterzeile)).Should().Equal(
                (nameof(TabellenBereichEingabe.Bezeichnung), 2, 0),
                (nameof(TabellenBereichEingabe.PlatzVon), 2, 0));
    }

    [Theory]
    [InlineData(0, 2, nameof(TabellenBereichEingabe.PlatzVon))]
    [InlineData(5, 4, nameof(TabellenBereichEingabe.PlatzBis))]
    [InlineData(17, 19, nameof(TabellenBereichEingabe.PlatzBis))]
    public void TabellenBereiche_PlatzfehlerMarkierenVonNurUnterEins(int von, int bis, string feld) =>
        TurnierValidator.TabellenBereicheValidieren(LigaMitBereichen(Bereich(von, bis)))
            .Should().ContainSingle().Which.Feld.Should().Be(feld);

    // ── Feldbezug der Basisdaten (#595) ───────────────────────────────────────

    [Fact]
    public void Basisdaten_FehlerTragenDenNamenDerEigenschaft()
    {
        var meldungen = _validator.BasisdatenValidieren("", " ", 1, new DateOnly(2026, 1, 1), new DateOnly(2025, 1, 1)).Meldungen;

        meldungen.Select(m => m.Feld).Should().Equal(
            nameof(TurnierInstanz.Name), nameof(TurnierInstanz.SaisonOderJahr),
            nameof(TurnierInstanz.ZielMannschaftsanzahl), nameof(TurnierInstanz.Enddatum));
    }
}
