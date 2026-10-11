using FluentAssertions;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Stammdaten;

public class MannschaftsIconTests
{
    private static readonly byte[] _wappen = [1, 2, 3];
    private static readonly byte[] _flagge = [9, 8, 7];

    private static Mannschaft Mannschaft(Mannschaftsart art, byte[]? wappen, Land? land) =>
        new() { Name = "Test", Mannschaftsart = art, Wappen = wappen, Land = land! };

    [Fact]
    public void Fuer_Verein_LiefertDasWappen() =>
        MannschaftsIcon.Fuer(Mannschaft(Mannschaftsart.Verein, _wappen, new Land { Flagge = _flagge }))
            .Should().BeSameAs(_wappen);

    [Fact]
    public void Fuer_Nationalmannschaft_LiefertDieLandesflaggeStattDesWappens() =>
        MannschaftsIcon.Fuer(Mannschaft(Mannschaftsart.Nationalmannschaft, _wappen, new Land { Flagge = _flagge }))
            .Should().BeSameAs(_flagge);

    [Fact]
    public void Fuer_NationalmannschaftOhneFlagge_LiefertNichtsStattDesWappens() =>
        MannschaftsIcon.Fuer(Mannschaft(Mannschaftsart.Nationalmannschaft, _wappen, new Land()))
            .Should().BeNull();

    [Fact]
    public void Fuer_NationalmannschaftOhneGeladenesLand_LiefertNichts() =>
        MannschaftsIcon.Fuer(Mannschaft(Mannschaftsart.Nationalmannschaft, _wappen, null))
            .Should().BeNull();

    [Fact]
    public void Fuer_VereinOhneWappen_LiefertNichts() =>
        MannschaftsIcon.Fuer(Mannschaft(Mannschaftsart.Verein, null, new Land { Flagge = _flagge }))
            .Should().BeNull();

    [Fact]
    public void Fuer_OhneMannschaft_LiefertNichts() =>
        MannschaftsIcon.Fuer(null).Should().BeNull();
}
