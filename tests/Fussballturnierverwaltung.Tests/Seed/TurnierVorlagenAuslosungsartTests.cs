using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Vorgegebene Auslosungsart der Gruppenphasen in den Turniervorlagen (Issue #471).</summary>
public class TurnierVorlagenAuslosungsartTests
{
    private const int KOEFFIZIENT_ID = 42;

    private static readonly List<TurnierInstanz> _vorlagen = TurnierVorlagenSeedDaten.Erstellen(
        ranglisteJeQuelleLookup: new Dictionary<string, int> { [RanglistenQuellenSeedDaten.UEFA_KLUB_KOEFFIZIENT] = KOEFFIZIENT_ID });

    private static TurnierRunde Runde(string name, string variante, int reihenfolge) =>
        _vorlagen.Single(v => v.Name == name && v.Variante == variante).Runden.Single(r => r.Reihenfolge == reihenfolge);

    [Fact]
    public void ErsteGruppenphase_AusserMiniTurnier_WirdNachToepfenAusgelost()
    {
        var gruppenphasen = _vorlagen
            .Where(v => v.Name != "Mini-Turnier")
            .SelectMany(v => v.Runden.Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge == 1)
                                     .Select(r => (Vorlage: $"{v.Name} · {v.Variante}", r.VorgegebeneAuslosungsart)))
            .ToList();

        gruppenphasen.Should().HaveCount(19);
        gruppenphasen.Should().OnlyContain(g => g.VorgegebeneAuslosungsart == Auslosungsart.NachToepfen);
    }

    [Fact]
    public void MiniTurnier_OhneVorgabe() =>
        _vorlagen.Where(v => v.Name == "Mini-Turnier").SelectMany(v => v.Runden)
            .Should().OnlyContain(r => r.VorgegebeneAuslosungsart == null);

    // Die Zwischenrunden der WM 1974–1982 wurden fest besetzt, nicht gelost (#466, #465).
    [Theory]
    [InlineData("24 Teams · 1982")]
    [InlineData("16 Teams · 1974–1978")]
    public void FesteZwischenrunde_OhneVorgabe(string variante) =>
        Runde("FIFA WM", variante, 2).VorgegebeneAuslosungsart.Should().BeNull();

    // Andere Rundenarten behalten ihre eigene Vorgabe (Ligaphase: Topf-Zuordnung, #347).
    [Fact]
    public void Ligaphase_BehaeltTopfZuordnung() =>
        Runde("UEFA Champions League", "36 Teams · ab 2024/25", 1).VorgegebeneAuslosungsart
            .Should().Be(Auslosungsart.TopfZuordnung);

    [Theory]
    [InlineData("32 Teams · mit Auswärtstorregel · 2003–2021")]
    [InlineData("32 Teams · ohne Auswärtstorregel · 2021–2024")]
    public void ChampionsLeague32_GruppenphaseNachKlubKoeffizient(string variante) =>
        Runde("UEFA Champions League", variante, 1).StandardRanglisteId.Should().Be(KOEFFIZIENT_ID);

    [Fact]
    public void ChampionsLeague32_OhneLookup_OhneStandardRangliste() =>
        TurnierVorlagenSeedDaten.Erstellen()
            .Single(v => v.Name == "UEFA Champions League" && v.Variante == "32 Teams · ohne Auswärtstorregel · 2021–2024")
            .Runden.Single(r => r.Reihenfolge == 1).StandardRanglisteId.Should().BeNull();
}
