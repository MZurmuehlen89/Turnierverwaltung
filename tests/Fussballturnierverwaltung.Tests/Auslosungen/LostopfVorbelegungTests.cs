using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

public class LostopfVorbelegungTests
{
    // Mannschafts-Id = Ranglistenplatz, damit die Erwartungen direkt lesbar bleiben.
    private static Dictionary<int, int> RangGleichId(IEnumerable<int> ids) => ids.ToDictionary(id => id, id => id);

    [Fact]
    public void Verteilen_UefaLigaphase_Je9TeamsNachRangAufVierToepfe()
    {
        var ids = Enumerable.Range(1, 36).Reverse().ToList();

        var toepfe = LostopfVorbelegung.Verteilen(ids, RangGleichId(ids), 4);

        toepfe.Should().HaveCount(4);
        toepfe[0].Should().Equal(Enumerable.Range(1, 9));
        toepfe[1].Should().Equal(Enumerable.Range(10, 9));
        toepfe[3].Should().Equal(Enumerable.Range(28, 9));
    }

    [Fact]
    public void Verteilen_TitelverteidigerAusserhalbTop8_VerdraengtDenNeuntenAusTopf1()
    {
        var ids = Enumerable.Range(1, 36).ToList();

        var toepfe = LostopfVorbelegung.Verteilen(ids, RangGleichId(ids), 4, titelverteidigerId: 20);

        toepfe[0].Should().Equal(20, 1, 2, 3, 4, 5, 6, 7, 8);
        toepfe[1].Should().StartWith(9).And.NotContain(20);
    }

    [Fact]
    public void Verteilen_TitelverteidigerInnerhalbTop9_Topf1BleibtDieTop9()
    {
        var ids = Enumerable.Range(1, 36).ToList();

        var toepfe = LostopfVorbelegung.Verteilen(ids, RangGleichId(ids), 4, titelverteidigerId: 5);

        toepfe[0].Should().BeEquivalentTo(Enumerable.Range(1, 9));
    }

    [Fact]
    public void Verteilen_TitelverteidigerNichtUnterDenTeams_WirdIgnoriert()
    {
        var ids = Enumerable.Range(1, 8).ToList();

        var toepfe = LostopfVorbelegung.Verteilen(ids, RangGleichId(ids), 2, titelverteidigerId: 99);

        toepfe[0].Should().Equal(1, 2, 3, 4);
        toepfe[1].Should().Equal(5, 6, 7, 8);
    }

    [Fact]
    public void Verteilen_TeamsOhneRanglisteneintrag_LandenHintenInUebergebenerReihenfolge()
    {
        var ids = new List<int> { 50, 3, 40, 1, 2 };
        var rang = new Dictionary<int, int> { [1] = 1, [2] = 2, [3] = 3 };

        var toepfe = LostopfVorbelegung.Verteilen(ids, rang, 1);

        toepfe[0].Should().Equal(1, 2, 3, 50, 40);
    }

    [Fact]
    public void Verteilen_NichtGlattTeilbar_VerteiltFairStattLetztenTopfLeerZuLassen()
    {
        var ids = Enumerable.Range(1, 9).ToList();

        var toepfe = LostopfVorbelegung.Verteilen(ids, RangGleichId(ids), 4);

        toepfe.Select(t => t.Count).Should().Equal(3, 2, 2, 2);
    }
}
