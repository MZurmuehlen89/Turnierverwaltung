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

    // ── #567: Spiel um Platz 3 zwischen Halbfinale und Finale (Vorbild kicker.de) ──

    // Wie BracketLeser sie liefert: … → Halbfinale → Spiel um Platz 3 → Finale.
    private static (RundenbaumLayoutErgebnis Ergebnis, TurnierBaumKnoten[] Halbfinale, TurnierBaumKnoten PlatzDrei,
        TurnierBaumKnoten Finale) MitSpielUmPlatzDrei()
    {
        var (blaetter, halbfinale, finale) = DreiRunden();
        var platzDrei = Knoten();
        var e = RundenbaumLayout.Berechnen(
            [Runde(1, blaetter), Runde(2, halbfinale), Runde(3, [platzDrei], platzDrei: true), Runde(4, [finale])]);
        return (e, halbfinale, platzDrei, finale);
    }

    [Fact]
    public void SpielUmPlatzDrei_StehtAufDerZeileDesFinales()
    {
        var (e, _, platzDrei, finale) = MitSpielUmPlatzDrei();

        e.ZeileJeKnoten[finale.Id].Should().Be(2.0, "die Wurzel ist das Finale, nicht das Spiel um Platz 3");
        e.ZeileJeKnoten[platzDrei.Id].Should().Be(2.0);
        e.ZeilenAnzahl.Should().Be(4, "das Spiel um Platz 3 belegt keine eigene Zeile");
    }

    [Fact]
    public void SpielUmPlatzDrei_VorgaengerSindDieHalbfinals()
    {
        // AK3: Die Halbfinal-Klammer mündet in die Platz-3-Karte.
        var (e, halbfinale, platzDrei, _) = MitSpielUmPlatzDrei();

        e.VorgaengerJeKnoten[platzDrei.Id].Should().Be(new KartenVorgaenger(halbfinale[0].Id, halbfinale[1].Id));
    }

    [Fact]
    public void Finale_VorgaengerIstDasSpielUmPlatzDrei()
    {
        // AK4: Von der Platz-3-Karte führt eine Linie zum Finale.
        var (e, _, platzDrei, finale) = MitSpielUmPlatzDrei();

        e.VorgaengerJeKnoten[finale.Id].Should().Be(new KartenVorgaenger(platzDrei.Id, null));
    }

    [Fact]
    public void SpielUmPlatzDrei_AendertDieVorgaengerDerUebrigenKartenNicht()
    {
        var (e, halbfinale, _, _) = MitSpielUmPlatzDrei();

        foreach (var k in halbfinale)
            e.VorgaengerJeKnoten[k.Id].Should().Be(new KartenVorgaenger(k.LinkerKindKnotenId, k.RechterKindKnotenId));
    }

    [Fact]
    public void OhneSpielUmPlatzDrei_SindDieVorgaengerDieKinder()
    {
        // AK5: Die Halbfinal-Klammer mündet direkt ins Finale.
        var (blaetter, mitte, finale) = DreiRunden();

        var e = RundenbaumLayout.Berechnen([Runde(1, blaetter), Runde(2, mitte), Runde(3, [finale])]);

        foreach (var k in blaetter.Concat(mitte).Append(finale))
            e.VorgaengerJeKnoten[k.Id].Should().Be(new KartenVorgaenger(k.LinkerKindKnotenId, k.RechterKindKnotenId));
        e.VorgaengerJeKnoten[finale.Id].Should().Be(new KartenVorgaenger(mitte[0].Id, mitte[1].Id));
    }

    [Fact]
    public void AltBaumOhneVerknuepfung_SpielUmPlatzDreiOhneVorgaenger_FinaleHaengtTrotzdemDaran()
    {
        // AK7: Das Finale hat keine Kinder — es gibt keine Klammer, aber Platz 3 und Finale bleiben verbunden.
        var halbfinale = new[] { Knoten(), Knoten() };
        var platzDrei = Knoten();
        var finale = Knoten();

        var e = RundenbaumLayout.Berechnen(
            [Runde(1, halbfinale), Runde(2, [platzDrei], platzDrei: true), Runde(3, [finale])]);

        e.VorgaengerJeKnoten[platzDrei.Id].Should().Be(new KartenVorgaenger(null, null));
        e.VorgaengerJeKnoten[finale.Id].Should().Be(new KartenVorgaenger(platzDrei.Id, null));
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
    public void ElternknotenMitNurRechtemKindStehtAufDessenZeile()
    {
        // #450: Nur das Auswärtsteam kommt aus einem Vorrundenspiel — vorher landete der Knoten auf der halben
        // Zeile des Kindes (fehlendes linkes Kind als Zeile 0).
        var a = Knoten();
        var b = Knoten();
        var kind = Knoten(a, b);
        var finale = Knoten(rechts: kind);

        var e = RundenbaumLayout.Berechnen([Runde(1, [a, b]), Runde(2, [kind]), Runde(3, [finale])]);

        e.ZeileJeKnoten[finale.Id].Should().Be(e.ZeileJeKnoten[kind.Id]);
    }

    [Fact]
    public void KnotenMitNurRechtemKindRutschtNichtUeberEinenKnotenDarueber()
    {
        // Beispiel aus #450: In derselben Runde steht oben eine Karte mit zwei Kindern (Zeilen 2,5 und 3,5 → 3,0),
        // darunter eine mit nur rechtem Kind in Zeile 4,5. Mit der halbierten Zeile (2,25) rutschte sie darüber.
        var vorrunde = Enumerable.Range(0, 5).Select(_ => Knoten()).ToArray();
        var erste = Knoten(vorrunde[0], vorrunde[1]);
        var oben = Knoten(vorrunde[2], vorrunde[3]);
        var unten = Knoten(rechts: vorrunde[4]);
        var halbfinale = new[] { Knoten(erste, oben), Knoten(links: unten) };
        var finale = Knoten(halbfinale[0], halbfinale[1]);

        var e = RundenbaumLayout.Berechnen(
            [Runde(1, vorrunde), Runde(2, [erste, oben, unten]), Runde(3, halbfinale), Runde(4, [finale])]);

        e.ZeileJeKnoten[oben.Id].Should().Be(3.0);
        e.ZeileJeKnoten[unten.Id].Should().Be(4.5);
    }

    [Fact]
    public void OhneRundenLeer()
    {
        var e = RundenbaumLayout.Berechnen([]);

        e.ZeileJeKnoten.Should().BeEmpty();
        e.VorgaengerJeKnoten.Should().BeEmpty();
        e.ZeilenAnzahl.Should().Be(0);
    }
}
