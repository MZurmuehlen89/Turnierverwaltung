using FluentAssertions;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;

namespace Fussballturnierverwaltung.Tests.Turnierbaum;

/// <summary>Issue #438: Zeilen des rundenweisen Turnierbaums.</summary>
public class RundenbaumLayoutTests
{
    private static int _naechsteId;

    private static TurnierBaumKnoten Knoten(TurnierBaumKnoten? links = null, TurnierBaumKnoten? rechts = null) =>
        new() { Id = ++_naechsteId, LinkerKindKnotenId = links?.Id, RechterKindKnotenId = rechts?.Id };

    private static BracketRunde Runde(int nummer, IEnumerable<TurnierBaumKnoten> knoten, bool platzDrei = false) =>
        new(nummer, knoten.ToList(), new Dictionary<int, Spiel>(), null, platzDrei);

    // Halbfinale (4 Blätter) → 2 Spiele → Finale.
    private static (TurnierBaumKnoten[] Blaetter, TurnierBaumKnoten[] Mitte, TurnierBaumKnoten Finale) DreiRunden()
    {
        var blaetter = Enumerable.Range(0, 4).Select(_ => Knoten()).ToArray();
        var mitte = new[] { Knoten(blaetter[0], blaetter[1]), Knoten(blaetter[2], blaetter[3]) };
        return (blaetter, mitte, Knoten(mitte[0], mitte[1]));
    }

    [Fact]
    public void BlaetterStehenFortlaufendUndElternMittigZwischenIhrenKindern()
    {
        var (blaetter, mitte, finale) = DreiRunden();

        var e = RundenbaumLayout.Berechnen([Runde(1, blaetter), Runde(2, mitte), Runde(3, [finale])]);

        blaetter.Select(k => e.ZeileJeKnoten[k.Id]).Should().Equal(0.5, 1.5, 2.5, 3.5);
        mitte.Select(k => e.ZeileJeKnoten[k.Id]).Should().Equal(1.0, 3.0);
        e.ZeileJeKnoten[finale.Id].Should().Be(2.0);
        e.ZeilenAnzahl.Should().Be(4);
    }

    [Fact]
    public void SpielUmPlatzDreiGehoertNichtZurBaumketteUndWirdUntenAngehaengt()
    {
        var (blaetter, mitte, finale) = DreiRunden();
        var platzDrei = Knoten();

        var e = RundenbaumLayout.Berechnen(
            [Runde(1, blaetter), Runde(2, mitte), Runde(3, [finale]), Runde(4, [platzDrei], platzDrei: true)]);

        e.ZeileJeKnoten[finale.Id].Should().Be(2.0, "die Wurzel ist das Finale, nicht das Spiel um Platz 3");
        e.ZeileJeKnoten[platzDrei.Id].Should().Be(4.5);
        e.ZeilenAnzahl.Should().Be(5);
    }

    [Fact]
    public void KnotenOhneWegZurWurzelWerdenInJederRundeAngehaengt()
    {
        var a = Knoten();
        var b = Knoten();
        var finale = Knoten(a, b);
        var loseErsteRunde = Knoten();
        var loseZweiteRunde = Knoten();

        var e = RundenbaumLayout.Berechnen([Runde(1, [a, b, loseErsteRunde]), Runde(2, [finale, loseZweiteRunde])]);

        e.ZeileJeKnoten[loseErsteRunde.Id].Should().Be(2.5);
        e.ZeileJeKnoten[loseZweiteRunde.Id].Should().Be(3.5);
        e.ZeilenAnzahl.Should().Be(4);
    }

    [Fact]
    public void DirekteinsteigerInSpaetererRundeBelegtEineEigeneZeile()
    {
        // Das Finale hat links ein ausgespieltes Halbfinale, rechts einen Knoten ohne Vorrunde.
        var a = Knoten();
        var b = Knoten();
        var halbfinale = Knoten(a, b);
        var direkt = Knoten();
        var finale = Knoten(halbfinale, direkt);

        var e = RundenbaumLayout.Berechnen([Runde(1, [a, b]), Runde(2, [halbfinale, direkt]), Runde(3, [finale])]);

        e.ZeileJeKnoten[halbfinale.Id].Should().Be(1.0);
        e.ZeileJeKnoten[direkt.Id].Should().Be(2.5);
        e.ZeileJeKnoten[finale.Id].Should().Be(1.75);
    }

    [Fact]
    public void ElternknotenMitNurLinkemKindStehtAufDessenZeile()
    {
        var a = Knoten();
        var b = Knoten();
        var kind = Knoten(a, b);
        var finale = Knoten(links: kind);

        var e = RundenbaumLayout.Berechnen([Runde(1, [a, b]), Runde(2, [kind]), Runde(3, [finale])]);

        e.ZeileJeKnoten[finale.Id].Should().Be(e.ZeileJeKnoten[kind.Id]);
    }

    [Fact]
    public void OhneRundenLeer()
    {
        var e = RundenbaumLayout.Berechnen([]);

        e.ZeileJeKnoten.Should().BeEmpty();
        e.ZeilenAnzahl.Should().Be(0);
    }
}
