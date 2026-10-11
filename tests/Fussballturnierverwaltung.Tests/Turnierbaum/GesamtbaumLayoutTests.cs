using FluentAssertions;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;

namespace Fussballturnierverwaltung.Tests.Turnierbaum;

public class GesamtbaumLayoutTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private static int _naechsteId;

    private static TurnierBaumKnoten Knoten(TurnierBaumKnoten? links = null, TurnierBaumKnoten? rechts = null)
    {
        var k = new TurnierBaumKnoten { Id = ++_naechsteId, LinkerKindKnotenId = links?.Id, RechterKindKnotenId = rechts?.Id };
        if (links is not null) links.ElternKnotenId = k.Id;
        if (rechts is not null) rechts.ElternKnotenId = k.Id;
        return k;
    }

    private static BracketRunde Runde(int nummer, string? name, IEnumerable<TurnierBaumKnoten> knoten, bool platzDrei = false) =>
        new(nummer, knoten.ToList(), new Dictionary<int, Spiel>(), name, platzDrei);

    /// <summary>Vollständiger binärer K.o.-Baum mit <paramref name="ebenen"/> Runden, erste Runde zuerst.</summary>
    private static List<List<TurnierBaumKnoten>> VollerBaum(int ebenen)
    {
        var ebenenListe = new List<List<TurnierBaumKnoten>>
        {
            Enumerable.Range(0, 1 << (ebenen - 1)).Select(_ => Knoten()).ToList()
        };
        while (ebenenListe[^1].Count > 1)
        {
            var kinder = ebenenListe[^1];
            ebenenListe.Add(Enumerable.Range(0, kinder.Count / 2).Select(i => Knoten(kinder[2 * i], kinder[2 * i + 1])).ToList());
        }
        return ebenenListe;
    }

    private static GesamtbaumPosition Pos(GesamtbaumLayoutErgebnis e, TurnierBaumKnoten k) =>
        e.Positionen.Single(p => p.KnotenId == k.Id);

    // ── Tests ─────────────────────────────────────────────────────────────

    [Fact]
    public void AchtelfinaleBisFinale_WirdGespiegeltMitFinaleInDerMitte()
    {
        var b = VollerBaum(4); // AF 8, VF 4, HF 2, F 1
        var runden = new[]
        {
            Runde(1, "Achtelfinale", b[0]), Runde(2, "Viertelfinale", b[1]),
            Runde(3, "Halbfinale", b[2]), Runde(4, "Finale", b[3])
        };

        var e = GesamtbaumLayout.Berechnen(runden);

        e.SpaltenAnzahl.Should().Be(7);
        e.ZeilenAnzahl.Should().Be(4, "je Seite vier Achtelfinals untereinander");
        e.Positionen.Should().HaveCount(15);

        Pos(e, b[3][0]).Should().Be(new GesamtbaumPosition(b[3][0].Id, 3, 2, GesamtbaumSeite.Mitte));
        Pos(e, b[2][0]).Spalte.Should().Be(2);
        Pos(e, b[2][1]).Spalte.Should().Be(4);
        Pos(e, b[2][0]).Zeile.Should().Be(2, "Halbfinale auf Höhe des Finales");
        Pos(e, b[2][1]).Zeile.Should().Be(2);

        b[0].Take(4).Select(k => Pos(e, k)).Should().OnlyContain(p => p.Spalte == 0 && p.Seite == GesamtbaumSeite.Links);
        b[0].Skip(4).Select(k => Pos(e, k)).Should().OnlyContain(p => p.Spalte == 6 && p.Seite == GesamtbaumSeite.Rechts);
        b[0].Take(4).Select(k => Pos(e, k).Zeile).Should().Equal(0.5, 1.5, 2.5, 3.5);

        e.Beschriftungen.Where(x => x.Name == "Achtelfinale").Select(x => x.Spalte).Should().BeEquivalentTo([0, 6]);
        e.Beschriftungen.Single(x => x.Name == "Finale").Zeile.Should().Be(2);
    }

    [Fact]
    public void SpielUmPlatzDrei_StehtUnterDemFinale()
    {
        var b = VollerBaum(2); // HF 2, F 1
        var p3 = Knoten();
        var runden = new[]
        {
            Runde(1, "Halbfinale", b[0]), Runde(3, "Finale", b[1]), Runde(2, "Spiel um Platz 3", [p3], platzDrei: true)
        };

        var e = GesamtbaumLayout.Berechnen(runden);

        e.SpaltenAnzahl.Should().Be(3);
        var finale = Pos(e, b[1][0]);
        var platzDrei = Pos(e, p3);
        platzDrei.Spalte.Should().Be(finale.Spalte);
        platzDrei.Zeile.Should().BeGreaterThan(finale.Zeile + 1);
        e.ZeilenAnzahl.Should().BeGreaterThanOrEqualTo(platzDrei.Zeile + 0.5, "die Platz-3-Karte muss in die Fläche passen");
        e.Beschriftungen.Single(x => x.Name == "Spiel um Platz 3").Zeile.Should().Be(platzDrei.Zeile);
    }

    [Fact]
    public void ClPlayoffs_JedesAchtelfinaleHatNurEinKind_LiegtAufHoeheSeinesPlayoffSpiels()
    {
        var po = Enumerable.Range(0, 8).Select(_ => Knoten()).ToList();
        var af = po.Select(k => Knoten(rechts: k)).ToList(); // Heim = Direktqualifizierter aus der Ligaphase
        var vf = Enumerable.Range(0, 4).Select(i => Knoten(af[2 * i], af[2 * i + 1])).ToList();
        var hf = Enumerable.Range(0, 2).Select(i => Knoten(vf[2 * i], vf[2 * i + 1])).ToList();
        var f = Knoten(hf[0], hf[1]);
        var runden = new[]
        {
            Runde(1, "Playoff-Runde", po), Runde(2, "Achtelfinale", af), Runde(3, "Viertelfinale", vf),
            Runde(4, "Halbfinale", hf), Runde(5, "Finale", [f])
        };

        var e = GesamtbaumLayout.Berechnen(runden);

        e.SpaltenAnzahl.Should().Be(9);
        for (int i = 0; i < 8; i++)
            Pos(e, af[i]).Zeile.Should().Be(Pos(e, po[i]).Zeile);
        Pos(e, po[0]).Spalte.Should().Be(0);
        Pos(e, po[7]).Spalte.Should().Be(8);
    }

    [Fact]
    public void NichtVerknuepfteKnoten_WerdenHaelftigVerteiltUndGehenNichtVerloren()
    {
        // Viertelfinale ohne Quellverknüpfung zum Achtelfinale (z. B. Direkteinstieg): AF-Knoten bleiben Wurzeln.
        var af = Enumerable.Range(0, 4).Select(_ => Knoten()).ToList();
        var b = VollerBaum(2);
        var runden = new[] { Runde(1, "Achtelfinale", af), Runde(2, "Halbfinale", b[0]), Runde(3, "Finale", b[1]) };

        var e = GesamtbaumLayout.Berechnen(runden);

        e.Positionen.Should().HaveCount(7);
        af.Take(2).Select(k => Pos(e, k).Seite).Should().OnlyContain(s => s == GesamtbaumSeite.Links);
        af.Skip(2).Select(k => Pos(e, k).Seite).Should().OnlyContain(s => s == GesamtbaumSeite.Rechts);
        e.Positionen.GroupBy(p => (p.Spalte, p.Zeile)).Should().OnlyContain(g => g.Count() == 1, "keine Karten übereinander");
    }

    [Fact]
    public void NurFinale_EineSpalte()
    {
        var f = Knoten();

        var e = GesamtbaumLayout.Berechnen([Runde(1, "Finale", [f])]);

        e.SpaltenAnzahl.Should().Be(1);
        Pos(e, f).Should().Be(new GesamtbaumPosition(f.Id, 0, 0.5, GesamtbaumSeite.Mitte));
    }

    [Fact]
    public void RundenOhneNamen_HeissenWieInDerRundenweisenAnsicht()
    {
        var b = VollerBaum(6); // 32, 16, 8, 4, 2, 1
        var runden = b.Select((knoten, i) => Runde(i + 1, null, knoten)).ToList();

        var e = GesamtbaumLayout.Berechnen(runden);

        e.Beschriftungen.Select(x => x.Name).Distinct().Should().BeEquivalentTo(runden.Select(RundenBezeichnung.Name));
        e.Beschriftungen.Where(x => x.Name == "Letzte 64").Select(x => x.Spalte).Should().BeEquivalentTo([0, 10]);
        e.Beschriftungen.Where(x => x.Name == "Letzte 32").Select(x => x.Spalte).Should().BeEquivalentTo([1, 9]);
    }
}
