using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Tests.Tabelle;

/// <summary>Issue #480: Auswahl der hervorgehobenen Mannschaften im Tabellenverlauf.</summary>
public class VerlaufHervorhebungenTests
{
    private static IReadOnlyList<VerlaufHervorhebung> Umschalten(IReadOnlyList<VerlaufHervorhebung> aktuell, params int[] ids) =>
        ids.Aggregate(aktuell, (liste, id) => VerlaufHervorhebungen.Umschalten(liste, id, $"Team {id}"));

    private static TabellenVerlauf Verlauf(params int[] ids) =>
        new(null, [1], ids.Length, [.. ids.Select((id, i) => new MannschaftsVerlauf(
            new Mannschaft { Id = id, Name = $"Team {id}" }, [new VerlaufPunkt(1, i + 1, 0, 1, [])]))]);

    [Fact]
    public void Umschalten_VergibtDieFarbenDerReiheNach()
    {
        Umschalten([], 7, 8, 9).Select(h => (h.MannschaftId, h.Farbe)).Should().Equal((7, 1), (8, 2), (9, 3));
    }

    [Fact]
    public void Umschalten_ZweitesMalNimmtHerausUndDieAnderenBehaltenIhreFarbe()
    {
        var liste = Umschalten([], 7, 8, 9, 8);

        liste.Select(h => (h.MannschaftId, h.Farbe)).Should().Equal((7, 1), (9, 3));
        Umschalten(liste, 5).Single(h => h.MannschaftId == 5).Farbe.Should().Be(2, "die kleinste freie Farbe");
    }

    [Fact]
    public void Umschalten_UeberDemHoechstwertWeichtDieAelteste()
    {
        Umschalten([], 7, 8, 9, 5).Select(h => (h.MannschaftId, h.Farbe)).Should().Equal((8, 2), (9, 3), (5, 1));
    }

    [Fact]
    public void Abgleichen_BehaeltNurMannschaftenDesVerlaufs()
    {
        VerlaufHervorhebungen.Abgleichen(Umschalten([], 7, 8), Verlauf(8, 9))
            .Select(h => (h.MannschaftId, h.Farbe)).Should().Equal((8, 2));
    }

    [Fact]
    public void Abgleichen_OhneTrefferWirdDerErsteHervorgehoben()
    {
        VerlaufHervorhebungen.Abgleichen(Umschalten([], 7), Verlauf(9, 8)).Single().MannschaftId.Should().Be(9);
    }
}
