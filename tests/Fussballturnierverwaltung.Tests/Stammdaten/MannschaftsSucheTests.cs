using FluentAssertions;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Tests.Stammdaten;

public class MannschaftsSucheTests
{
    private static readonly Mannschaft[] _mannschaften =
    [
        new() { Id = 1, Name = "Borussia Dortmund", Kurzname = "BVB" },
        new() { Id = 2, Name = "Bayern München", Kurzname = "FCB" },
        new() { Id = 3, Name = "Arminia Bielefeld", Kurzname = "DSC" },
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Treffer_OhneEingabe_AlleInGelieferterReihenfolge(string? eingabe) =>
        MannschaftsSuche.Treffer(_mannschaften, eingabe).Select(m => m.Id).Should().Equal(1, 2, 3);

    [Fact]
    public void Treffer_TeilDesNamens_OhneGrossKleinschreibung() =>
        MannschaftsSuche.Treffer(_mannschaften, "MÜNCHEN").Select(m => m.Id).Should().Equal(2);

    [Fact]
    public void Treffer_Kurzname_FindetMannschaft() =>
        MannschaftsSuche.Treffer(_mannschaften, "bvb").Select(m => m.Id).Should().Equal(1);

    [Fact]
    public void Treffer_NamensanfangVorTrefferMittenImNamen()
    {
        Mannschaft[] mannschaften =
        [
            new() { Id = 4, Name = "VfL Bochum", Kurzname = "BOC" },
            new() { Id = 1, Name = "Borussia Dortmund", Kurzname = "BVB" },
            new() { Id = 2, Name = "Bayern München", Kurzname = "FCB" },
        ];

        MannschaftsSuche.Treffer(mannschaften, "bo").Select(m => m.Id).Should().Equal(1, 4);
    }
}
