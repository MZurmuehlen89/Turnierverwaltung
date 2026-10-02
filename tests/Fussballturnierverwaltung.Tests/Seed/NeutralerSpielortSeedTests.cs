using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Issue #352: Vorbelegung "Neutraler Spielort" in den Seed-Vorlagen.</summary>
public class NeutralerSpielortSeedTests
{
    private static IEnumerable<TurnierInstanz> Vorlagen(string name) =>
        TurnierVorlagenSeedDaten.Erstellen().Where(v => v.Name == name);

    private static IEnumerable<string> NeutraleRunden(string name) =>
        Vorlagen(name).SelectMany(v => v.Runden).Where(r => r.NeutralerSpielort).Select(r => r.Name).Distinct();

    [Theory]
    [InlineData("FIFA WM")]
    [InlineData("FIFA Klub-WM")]
    [InlineData("UEFA EM")]
    [InlineData("Copa América")]
    [InlineData("Afrika-Cup")]
    [InlineData("AFC Asian Cup")]
    [InlineData("CONCACAF Gold Cup")]
    public void Endrundenturnier_AlleRundenNeutral(string name)
    {
        Vorlagen(name).Should().NotBeEmpty();
        Vorlagen(name).SelectMany(v => v.Runden).Should().OnlyContain(r => r.NeutralerSpielort);
    }

    [Theory]
    [InlineData("UEFA Champions League")]
    [InlineData("UEFA Europa League")]
    [InlineData("UEFA Conference League")]
    [InlineData("DFB-Pokal")]
    public void Europapokal_NurFinaleNeutral(string name)
    {
        Vorlagen(name).Should().NotBeEmpty();
        NeutraleRunden(name).Should().BeEquivalentTo(["Finale"]);
    }

    [Fact]
    public void FaCup_HalbfinaleUndFinaleNeutral() =>
        NeutraleRunden("FA Cup").Should().BeEquivalentTo(["Halbfinale", "Finale"]);

    [Fact]
    public void NationsLeague_FinalFourNeutral() =>
        NeutraleRunden("UEFA Nations League Liga A").Should().BeEquivalentTo(["Halbfinale", "Spiel um Platz 3", "Finale"]);

    [Theory]
    [InlineData("Bundesliga")]
    [InlineData("Mini-Turnier")]
    public void UebrigeVorlagen_OhneNeutralenSpielort(string name) =>
        NeutraleRunden(name).Should().BeEmpty();
}
