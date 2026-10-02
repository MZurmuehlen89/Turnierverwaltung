using FluentAssertions;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Issue #360: Vorbelegung "Überraschungsfaktor" in den Seed-Vorlagen.</summary>
public class UeberraschungsfaktorSeedTests
{
    [Theory]
    [InlineData("DFB-Pokal")]
    [InlineData("FA Cup")]
    public void NationalerPokal_AlleRundenMitPokalFaktor(string name)
    {
        var runden = TurnierVorlagenSeedDaten.Erstellen().Where(v => v.Name == name).SelectMany(v => v.Runden).ToList();

        runden.Should().NotBeEmpty();
        runden.Should().OnlyContain(r => r.Ueberraschungsfaktor == TurnierVorlagenSeedDaten.POKAL_UEBERRASCHUNGSFAKTOR);
    }

    [Fact]
    public void UebrigeVorlagen_OhneUeberraschungsfaktor() =>
        TurnierVorlagenSeedDaten.Erstellen()
            .Where(v => v.Name is not ("DFB-Pokal" or "FA Cup"))
            .SelectMany(v => v.Runden)
            .Should().OnlyContain(r => r.Ueberraschungsfaktor == 0);
}
