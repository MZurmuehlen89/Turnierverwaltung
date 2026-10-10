using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

public class AusgangsreihenfolgeTests
{
    private static readonly Ausgangsreihenfolge<string> _reihenfolge = new(["A", "B", "C", "D"]);

    [Fact]
    public void EinfuegeIndex_ElementAusDerMitte_KehrtAnSeineStelleZurueck() =>
        _reihenfolge.EinfuegeIndex(["A", "C", "D"], "B").Should().Be(1);

    [Fact]
    public void EinfuegeIndex_ErstesElement_KommtAnDenAnfang() =>
        _reihenfolge.EinfuegeIndex(["B", "D"], "A").Should().Be(0);

    [Fact]
    public void EinfuegeIndex_LetztesElement_KommtAnsEnde() =>
        _reihenfolge.EinfuegeIndex(["A", "B"], "D").Should().Be(2);

    [Fact]
    public void EinfuegeIndex_LeereListe_IstNull() =>
        _reihenfolge.EinfuegeIndex([], "C").Should().Be(0);

    [Fact]
    public void EinfuegeIndex_UnbekanntesElement_KommtAnsEnde() =>
        _reihenfolge.EinfuegeIndex(["A", "C"], "X").Should().Be(2);

    [Fact]
    public void EinfuegeIndex_HinterUnbekanntenElementen_BleibtDavor() =>
        _reihenfolge.EinfuegeIndex(["A", "X"], "B").Should().Be(1);

    [Fact]
    public void EinfuegeIndex_DoppelterSchluessel_ZaehltDasErsteVorkommen() =>
        new Ausgangsreihenfolge<string>(["A", "B", "A", "C"]).EinfuegeIndex(["B", "C"], "A").Should().Be(0);

    [Fact]
    public void EinfuegeIndex_OhneAusgangsliste_KommtAllesAnsEnde() =>
        new Ausgangsreihenfolge<string>([]).EinfuegeIndex(["A", "B"], "C").Should().Be(2);
}
