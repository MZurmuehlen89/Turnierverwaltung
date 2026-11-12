using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;

namespace Fussballturnierverwaltung.Application.Turnierbaum;

/// <summary>
/// Zeilen der Turnierbaum-Karten (#450, aus <see cref="RundenbaumLayout"/> herausgelöst): Blätter stehen fortlaufend
/// untereinander, jeder Elternknoten mittig zwischen seinen Kindern, mit nur einem Kind auf dessen Zeile.
/// </summary>
internal static class KartenZeilen
{
    public static void ZeileZuweisen(
        TurnierBaumKnoten knoten, Dictionary<int, TurnierBaumKnoten> alle, Dictionary<int, double> zeile, ref double blaetter)
    {
        if (zeile.ContainsKey(knoten.Id)) return;

        var links = KindVon(knoten.LinkerKindKnotenId, alle);
        var rechts = KindVon(knoten.RechterKindKnotenId, alle);
        if (links is null && rechts is null)
        {
            NaechsteZeileBelegen(knoten, zeile, ref blaetter);
            return;
        }

        if (links is not null) ZeileZuweisen(links, alle, zeile, ref blaetter);
        if (rechts is not null) ZeileZuweisen(rechts, alle, zeile, ref blaetter);

        // #450: Mit nur einem Kind steht der Knoten auf dessen Zeile — auch wenn das Kind rechts hängt.
        double z1 = links is not null ? zeile[links.Id] : zeile[rechts!.Id];
        double z2 = rechts is not null ? zeile[rechts.Id] : z1;
        zeile[knoten.Id] = (z1 + z2) / 2.0;
    }

    private static TurnierBaumKnoten? KindVon(int? id, Dictionary<int, TurnierBaumKnoten> alle) =>
        id is int wert ? alle.GetValueOrDefault(wert) : null;

    public static void NaechsteZeileBelegen(TurnierBaumKnoten knoten, Dictionary<int, double> zeile, ref double blaetter)
    {
        zeile[knoten.Id] = blaetter + 0.5;
        blaetter++;
    }
}
