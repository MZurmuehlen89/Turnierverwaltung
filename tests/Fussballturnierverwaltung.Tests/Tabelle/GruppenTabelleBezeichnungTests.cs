using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;

namespace Fussballturnierverwaltung.Tests.Tabelle;

/// <summary>#544: Gruppen heißen überall mit Buchstaben – dieselbe Konvention wie Auslosung und Platzhalter.</summary>
public class GruppenTabelleBezeichnungTests
{
    [Theory]
    [InlineData(1, "Gruppe A")]
    [InlineData(2, "Gruppe B")]
    [InlineData(8, "Gruppe H")]
    [InlineData(26, "Gruppe Z")]
    public void Bezeichnung_NutztBuchstabenStattNummer(int nummer, string erwartet) =>
        new GruppenTabelle(nummer, []).Bezeichnung.Should().Be(erwartet);
}
