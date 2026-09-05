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
    public void KoRundeMitAusgang_GroesserGleichEingang_IstUngueltig()
    {
        var runden = new[]
        {
            Runde("Halbfinale", Rundenart.KoRunde, eingang: 4, ausgang: 4), // gleich, nicht weniger
        };

        var ergebnis = _validator.RundenValidieren(runden);

        ergebnis.IstUngueltig.Should().BeTrue();
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
        ergebnis.Fehler.Should().ContainMatch("*genau der Hälfte*");
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
}
