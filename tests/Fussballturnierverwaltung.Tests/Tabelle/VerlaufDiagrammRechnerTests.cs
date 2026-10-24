using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Tests.Tabelle;

/// <summary>Issue #480: Geometrie des Tabellenplatz-Verlaufs.</summary>
public class VerlaufDiagrammRechnerTests
{
    // Mannschaft n hat an jedem Spieltag den Platz aus plaetzeJeMannschaft[n - 1].
    private static TabellenVerlauf Verlauf(int? gruppe, params int[][] plaetzeJeMannschaft)
    {
        int spieltage = plaetzeJeMannschaft[0].Length;
        return new TabellenVerlauf(
            gruppe, [.. Enumerable.Range(1, spieltage)], plaetzeJeMannschaft.Length,
            [.. plaetzeJeMannschaft.Select((plaetze, i) => new MannschaftsVerlauf(
                new Mannschaft { Id = i + 1, Name = $"Team {i + 1}" },
                [.. plaetze.Select((platz, s) => new VerlaufPunkt(s + 1, platz, 0, s + 1, []))]))]);
    }

    private static TabellenVerlauf Liga(int mannschaften, int spieltage) =>
        Verlauf(null, [.. Enumerable.Range(1, mannschaften).Select(p => Enumerable.Repeat(p, spieltage).ToArray())]);

    [Fact]
    public void Berechnen_Platz1ObenUndSpieltageVonLinksNachRechts()
    {
        var layout = VerlaufDiagrammRechner.Berechnen(Verlauf(null, [1, 2, 2], [2, 1, 1]), [], breite: 600);

        var team1 = layout.Linien.Single(l => l.MannschaftId == 1).Punkte;
        team1[0].Y.Should().BeLessThan(team1[1].Y, "Platz 1 steht oben");
        team1.Select(p => p.X).Should().BeInAscendingOrder();
        team1[0].X.Should().Be(layout.PlotLinks);
        team1[^1].X.Should().Be(layout.PlotRechts);
    }

    [Fact]
    public void Berechnen_ZuSchmalWirdVerbreitert()
    {
        var layout = VerlaufDiagrammRechner.Berechnen(Liga(4, 31), [], breite: 100);

        (layout.Spalten[1].X - layout.Spalten[0].X).Should().BeGreaterThanOrEqualTo(VerlaufDiagrammRechner.MIN_SPALTENBREITE);
    }

    [Fact]
    public void Berechnen_SpaltenSchliessenLueckenlosAneinander()
    {
        var spalten = VerlaufDiagrammRechner.Berechnen(Liga(4, 5), [], breite: 800).Spalten;

        spalten.Zip(spalten.Skip(1)).Should().OnlyContain(paar => Math.Abs(paar.First.Rechts - paar.Second.Links) < 1e-9);
    }

    [Fact]
    public void Berechnen_GrosseTabelleBeschriftetNurJedenFuenftenPlatz()
    {
        var marken = VerlaufDiagrammRechner.Berechnen(Liga(36, 8), [], breite: 800).PlatzMarken;

        marken.Select(m => m.Text).Should().Equal("1", "5", "10", "15", "20", "25", "30", "35", "36");
    }

    [Fact]
    public void Berechnen_KleineTabelleBeschriftetJedenPlatz()
    {
        VerlaufDiagrammRechner.Berechnen(Liga(4, 3), [], breite: 600).PlatzMarken.Should().HaveCount(4);
    }

    [Fact]
    public void Berechnen_ZonenWerdenZuBaendernZusammengefasst()
    {
        // Platz 1–2 grün (zwei Zonen gleicher Farbe hintereinander), Platz 4 rot.
        TabellenZone[] zonen =
        [
            new(null, 1, 1, 10, "Achtelfinale", ZonenFarbe.Gruen),
            new(null, 2, 2, 10, "Achtelfinale", ZonenFarbe.Gruen),
            new(null, 4, 4, null, "Abstieg", ZonenFarbe.Rot)
        ];

        var layout = VerlaufDiagrammRechner.Berechnen(Liga(4, 3), zonen, breite: 600);

        layout.Baender.Select(b => b.Farbe).Should().Equal(ZonenFarbe.Gruen, ZonenFarbe.Rot);
        layout.Baender[0].Oben.Should().Be(layout.PlotOben);
        layout.Baender[^1].Unten.Should().Be(layout.PlotUnten);
    }

    [Fact]
    public void Berechnen_ZonenEinerAnderenGruppeZaehlenNicht()
    {
        TabellenZone[] zonen = [new(2, 1, 2, 10, "Achtelfinale", ZonenFarbe.Gruen)];

        VerlaufDiagrammRechner.Berechnen(Verlauf(1, [1, 1], [2, 2]), zonen, breite: 600).Baender.Should().BeEmpty();
    }

    [Fact]
    public void BeschriftungenVerteilen_SchiebtZuNaheNamenAuseinanderUndBehaeltDieReihenfolge()
    {
        var ys = VerlaufDiagrammRechner.BeschriftungenVerteilen([100, 50, 105], minAbstand: 20);

        ys.Should().Equal(100, 50, 120);
    }
}
