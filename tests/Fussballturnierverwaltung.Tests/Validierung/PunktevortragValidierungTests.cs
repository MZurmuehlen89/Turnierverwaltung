using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

/// <summary>Issue #355 K9: Die Punkteübernahme einer Runde braucht eine Tabelle und Übergangsregeln aus genau einer Runde
/// mit Tabelle (Ganzgraph-Validierung des <see cref="UebergangsregelValidator"/>).</summary>
public class PunktevortragValidierungTests
{
    private readonly UebergangsregelValidator _validator = new();

    private static TurnierRunde Runde(string name, Rundenart art, int eingang, int ausgang, Punktevortrag vortrag = Punktevortrag.Keine) => new()
    {
        Name = name, Rundenart = art, EingangsMannschaftsanzahl = eingang, AusgangsMannschaftsanzahl = ausgang, Punktevortrag = vortrag
    };

    private static UebergangsregelPositionsEingabe Regel(int quelle, int ziel, int von, int bis) =>
        new(quelle, ziel, GruppeIndex: null, PlatzierungVon: von, PlatzierungBis: bis);

    [Fact]
    public void Ligaquelle_MitLigaZiel_IstGueltig()
    {
        TurnierRunde[] runden =
        [
            Runde("Grunddurchgang", Rundenart.Liga, 8, 4),
            Runde("Meistergruppe", Rundenart.Liga, 4, 4, Punktevortrag.HalbiertAbgerundet)
        ];

        _validator.Validieren(runden, [Regel(1, 2, 1, 4)]).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void Zielrunde_OhneTabelle_IstUngueltig_AmFeldPunktevortrag()
    {
        TurnierRunde[] runden =
        [
            Runde("Grunddurchgang", Rundenart.Liga, 8, 4),
            Runde("Halbfinale", Rundenart.KoRunde, 4, 2, Punktevortrag.Voll)
        ];

        var ergebnis = _validator.Validieren(runden, [Regel(1, 2, 1, 4)]);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Meldungen.Should().ContainSingle(f => f.Feld == nameof(TurnierRunde.Punktevortrag) && f.Zeile == 1)
            .Which.Text.Should().Contain("Tabelle");
    }

    [Fact]
    public void Zielrunde_OhneEingehendeRegeln_IstUngueltig()
    {
        TurnierRunde[] runden = [Runde("Meistergruppe", Rundenart.Liga, 4, 4, Punktevortrag.Voll)];

        _validator.Validieren(runden, []).Meldungen
            .Should().ContainSingle(f => f.Feld == nameof(TurnierRunde.Punktevortrag)).Which.Text.Should().Contain("genau einer Runde");
    }

    [Fact]
    public void Zielrunde_MitZweiQuellrunden_IstUngueltig()
    {
        TurnierRunde[] runden =
        [
            Runde("Liga A", Rundenart.Liga, 8, 2),
            Runde("Liga B", Rundenart.Liga, 8, 2),
            Runde("Meistergruppe", Rundenart.Liga, 4, 4, Punktevortrag.Voll)
        ];

        var ergebnis = _validator.Validieren(runden, [Regel(1, 3, 1, 2), Regel(2, 3, 1, 2)]);

        ergebnis.Meldungen.Should().ContainSingle(f => f.Feld == nameof(TurnierRunde.Punktevortrag) && f.Zeile == 2);
    }

    [Fact]
    public void Quellrunde_OhneTabelle_IstUngueltig()
    {
        TurnierRunde[] runden =
        [
            Runde("Viertelfinale", Rundenart.KoRunde, 8, 4),
            Runde("Meistergruppe", Rundenart.Liga, 4, 4, Punktevortrag.Voll)
        ];

        var ergebnis = _validator.Validieren(runden, [new UebergangsregelPositionsEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1)]);

        ergebnis.Meldungen.Should().Contain(f => f.Feld == nameof(TurnierRunde.Punktevortrag) && f.Text.Contains("Quellrunde"));
    }

    [Fact]
    public void OhneModus_PruefungEntfaellt()
    {
        TurnierRunde[] runden = [Runde("Meistergruppe", Rundenart.Liga, 4, 4)];

        _validator.Validieren(runden, []).IstGueltig.Should().BeTrue();
    }
}
