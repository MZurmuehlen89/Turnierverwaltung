using FluentAssertions;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Stammdaten;

public class AuslosungsfilterTests
{
    private const int UEFA = 1;
    private const int CONMEBOL = 2;
    private const int DEUTSCHLAND = 10;
    private const int SPANIEN = 11;
    private const int BRASILIEN = 20;

    private static readonly MannschaftAuswahl _bayern = new(1, "FC Bayern München", Mannschaftsart.Verein, DEUTSCHLAND, UEFA);
    private static readonly MannschaftAuswahl _real = new(2, "Real Madrid", Mannschaftsart.Verein, SPANIEN, UEFA);
    private static readonly MannschaftAuswahl _flamengo = new(3, "Flamengo", Mannschaftsart.Verein, BRASILIEN, CONMEBOL);
    private static readonly MannschaftAuswahl _deutschland = new(4, "Deutschland", Mannschaftsart.Nationalmannschaft, DEUTSCHLAND, UEFA);

    private static readonly MannschaftAuswahl[] _alle = [_bayern, _real, _flamengo, _deutschland];

    private static MannschaftAuswahl[] Passende(Auslosungsfilter filter) => _alle.Where(filter.Passt).ToArray();

    [Fact]
    public void Passt_OhneFilter_LaesstAlleZu() =>
        Passende(Auslosungsfilter.Keiner).Should().Equal(_alle);

    [Fact]
    public void Passt_Verband_LaesstNurMannschaftenDesVerbandsZu() =>
        Passende(new(UEFA, null, MannschaftsartFilter.Alle)).Should().Equal(_bayern, _real, _deutschland);

    [Fact]
    public void Passt_Land_LaesstNurMannschaftenDesLandesZu() =>
        Passende(new(null, DEUTSCHLAND, MannschaftsartFilter.Alle)).Should().Equal(_bayern, _deutschland);

    [Fact]
    public void Passt_NurVereine_SchliesstNationalmannschaftenAus() =>
        Passende(new(null, null, MannschaftsartFilter.Verein)).Should().Equal(_bayern, _real, _flamengo);

    [Fact]
    public void Passt_NurNationalmannschaften_SchliesstVereineAus() =>
        Passende(new(null, null, MannschaftsartFilter.Nationalmannschaft)).Should().Equal(_deutschland);

    [Fact]
    public void Passt_VerbandLandUndArt_MuessenAlleZutreffen() =>
        Passende(new(UEFA, DEUTSCHLAND, MannschaftsartFilter.Verein)).Should().Equal(_bayern);

    [Fact]
    public void Passt_LandAusAnderemVerband_LaesstNichtsZu() =>
        Passende(new(CONMEBOL, DEUTSCHLAND, MannschaftsartFilter.Alle)).Should().BeEmpty();
}
