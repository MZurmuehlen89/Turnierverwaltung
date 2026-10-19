using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Vorgegebene Auslosungsregeln der Turniervorlagen (Issue #356).</summary>
public class TurnierVorlagenAuslosungsregelnTests
{
    private static readonly List<TurnierInstanz> _vorlagen = TurnierVorlagenSeedDaten.Erstellen();

    private static TurnierRunde Runde(string name, string variante, string runde) =>
        _vorlagen.Single(v => v.Name == name && v.Variante == variante).Runden.Single(r => r.Name == runde);

    [Theory]
    [InlineData("48 Teams · ab 2026")]
    [InlineData("32 Teams · 1998–2022")]
    public void FifaWm_AbDerRegelEinfuehrung_GruppenphaseMitKonfoederationsverteilung(string variante) =>
        Runde("FIFA WM", variante, "Gruppenphase").VorgegebeneAuslosungsregeln
            .Should().Equal(AuslosungsRegelArt.Konfoederationsverteilung);

    // Als allgemeine Regel erst seit 1994 — die älteren Vorlagen bleiben ohne.
    [Theory]
    [InlineData("24 Teams · 1986–1994")]
    [InlineData("24 Teams · 1982")]
    [InlineData("16 Teams · 1934–1978")]
    public void FifaWm_AeltereVorlagen_OhneVorgabe(string variante) =>
        _vorlagen.Single(v => v.Name == "FIFA WM" && v.Variante == variante).Runden
            .Should().OnlyContain(r => r.VorgegebeneAuslosungsregeln.Count == 0);

    [Fact]
    public void KlubWm_GruppenphaseTrenntVerbaendeUndLaender() =>
        Runde("FIFA Klub-WM", "32 Teams · ab 2025", "Gruppenphase").VorgegebeneAuslosungsregeln.Should().BeEquivalentTo(
            [AuslosungsRegelArt.Konfoederationsverteilung, AuslosungsRegelArt.KeinGleichesLand]);

    [Theory]
    [InlineData("32 Teams · mit Auswärtstorregel · 2003–2021")]
    [InlineData("32 Teams · ohne Auswärtstorregel · 2021–2024")]
    public void ChampionsLeague32_GruppenphaseUndAchtelfinaleLautReglement(string variante)
    {
        Runde("UEFA Champions League", variante, "Gruppenphase").VorgegebeneAuslosungsregeln
            .Should().Equal(AuslosungsRegelArt.KeinGleichesLand);

        var achtelfinale = Runde("UEFA Champions League", variante, "Achtelfinale");
        achtelfinale.VorgegebeneAuslosungsart.Should().Be(Auslosungsart.GruppensiegerGegenGruppenzweite);
        achtelfinale.VorgegebeneAuslosungsregeln.Should().BeEquivalentTo(
            [AuslosungsRegelArt.KeinGleichesLand, AuslosungsRegelArt.HeimrechtNachSetzliste]);

        Runde("UEFA Champions League", variante, "Viertelfinale").VorgegebeneAuslosungsregeln.Should().BeEmpty();
    }

    [Theory]
    [InlineData("UEFA Champions League")]
    [InlineData("UEFA Europa League")]
    [InlineData("UEFA Conference League")]
    public void UefaClubwettbewerbe36_LigaphaseOhneLandsleuteUndHeimrechtNachPlatzierung(string name)
    {
        const string variante = "36 Teams · ab 2024/25";
        Runde(name, variante, "Ligaphase").VorgegebeneAuslosungsregeln.Should().Equal(AuslosungsRegelArt.KeinGleichesLand);
        Runde(name, variante, "Playoff-Runde").VorgegebeneAuslosungsregeln.Should().Equal(AuslosungsRegelArt.HeimrechtNachSetzliste);
        Runde(name, variante, "Achtelfinale").VorgegebeneAuslosungsregeln.Should().Equal(AuslosungsRegelArt.HeimrechtNachSetzliste);
        Runde(name, variante, "Finale").VorgegebeneAuslosungsregeln.Should().BeEmpty();
    }

    [Fact]
    public void NationalePokale_HeimrechtJeReglement_AusserAnNeutralemOrt()
    {
        // DFB-Pokal (#393): Unterklassige haben bis einschließlich Halbfinale Heimrecht gegen Profiklubs.
        Runde("DFB-Pokal", "64 Teams", "1. Runde").VorgegebeneAuslosungsregeln.Should().Equal(AuslosungsRegelArt.HeimrechtFuerUnterklassige);
        Runde("DFB-Pokal", "64 Teams", "Halbfinale").VorgegebeneAuslosungsregeln.Should().Equal(AuslosungsRegelArt.HeimrechtFuerUnterklassige);
        Runde("DFB-Pokal", "64 Teams", "Finale").VorgegebeneAuslosungsregeln.Should().BeEmpty();

        // FA Cup: der zuerst Gezogene spielt zu Hause, auch gegen Unterklassige.

        const string faCup = "64 Teams · ab 3. Hauptrunde";
        Runde("FA Cup", faCup, "3. Runde").VorgegebeneAuslosungsregeln.Should().Equal(AuslosungsRegelArt.HeimrechtNachLos);
        Runde("FA Cup", faCup, "Halbfinale").VorgegebeneAuslosungsregeln.Should().BeEmpty("das Halbfinale findet in Wembley statt");
        Runde("FA Cup", faCup, "Finale").VorgegebeneAuslosungsregeln.Should().BeEmpty();
    }

    // Eine Vorgabe, die bei Rundenart und Auslosungsart der Runde gar nicht wirken kann, bliebe im Runden-Editor
    // unsichtbar und in der Auslosung ausgegraut.
    [Fact]
    public void JedeVorgabe_KannInIhrerRundeWirken()
    {
        foreach (var vorlage in _vorlagen)
        {
            var runden = vorlage.Runden.OrderBy(r => r.Reihenfolge).ToList();
            foreach (var runde in runden)
            {
                bool folgtAufGruppenphase = runden.Any(r =>
                    r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < runde.Reihenfolge);
                foreach (var regel in runde.VorgegebeneAuslosungsregeln)
                    AuslosungsRegelKompatibilitaet
                        .AlsVorgabeMoeglich(regel, runde.Rundenart, runde.VorgegebeneAuslosungsart, folgtAufGruppenphase,
                            runde.PaarungFestVorgegeben)
                        .Should().BeTrue($"{vorlage.Name} ({vorlage.Variante}), {runde.Name}: {regel}");
            }
        }
    }
}
