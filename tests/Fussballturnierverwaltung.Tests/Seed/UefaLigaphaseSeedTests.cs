using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Issue #469: die Ligaphase der Conference League lost einen Gegner je Topf aus (6 Gegner aus 6 Töpfen),
/// Champions und Europa League zwei (8 Gegner aus 4 Töpfen).</summary>
public class UefaLigaphaseSeedTests
{
    private static TurnierInstanz Vorlage(string name) =>
        TurnierVorlagenSeedDaten.Erstellen().Single(v => v.Name == name && v.Variante == "36 Teams · ab 2024/25");

    [Theory]
    [InlineData("UEFA Champions League", 2, "je 8 Gegner")]
    [InlineData("UEFA Europa League", 2, "je 8 Gegner")]
    [InlineData("UEFA Conference League", 1, "je 6 Gegner aus 6 Töpfen")]
    public void Ligaphase_GegnerJeTopfUndBeschreibung(string name, int gegnerJeTopf, string gegnerInBeschreibung)
    {
        var vorlage = Vorlage(name);

        vorlage.Runden.Single(r => r.Rundenart == Rundenart.TopfLiga).GegnerJeTopf.Should().Be(gegnerJeTopf);
        vorlage.Beschreibung.Should().StartWith($"Ligaphase (36 Teams, {gegnerInBeschreibung}) →");
    }

    [Fact]
    public void ConferenceLeague_AlteBeschreibungIstDerFruehereGemeinsameStand() =>
        TurnierVorlagenSeedDaten.AlteBeschreibungen[("UEFA Conference League", "36 Teams · ab 2024/25")]
            .Should().Be(Vorlage("UEFA Champions League").Beschreibung);
}
