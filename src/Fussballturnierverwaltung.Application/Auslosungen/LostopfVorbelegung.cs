using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Lostopf-Vorbelegung per Rangliste (Issue #119, erweitert um Standard-Rangliste und Titelverteidiger in
/// #349). Vorbild ist die UEFA-Ligaphase ab 2024/25: Topf 1 = Titelverteidiger + die bestplatzierten Klubs
/// nach Klub-Koeffizient, Töpfe 2–4 je die nächsten. Länder- und Spielplanregeln setzt allein die Auslosung
/// durch, nicht die Topfbildung.
/// </summary>
public static class LostopfVorbelegung
{
    /// <summary>
    /// Verteilt <paramref name="mannschaftIds"/> auf <paramref name="topfAnzahl"/> Töpfe: sortiert nach
    /// Ranglistenplatz, Teams ohne Eintrag hinten (in der übergebenen Reihenfolge). Die Teams aus
    /// <paramref name="vorabInTopf1"/> (Titelverteidiger, feste Gruppenköpfe #487) rücken unabhängig von ihrem Platz
    /// an die Spitze und verdrängen so die Letzten aus Topf 1 — steht eines ohnehin in Topf 1, rückt der
    /// Nächstplatzierte nach. Kommen Teams aus einer Gruppenphase, zählt vor dem Ranglistenplatz ihr Platz in der
    /// Gruppe (<paramref name="gruppenplatzProMannschaft"/>, #565): Gruppensieger in Topf 1, Zweite in Topf 2 –
    /// sonst lost „Gruppensieger vs. Zweite“ Sieger gegen Sieger. Teams ohne Gruppenplatz folgen danach.
    /// Topfgrößen sind fair verteilt (9 Teams auf 4 Töpfe = 3, 2, 2, 2 statt 3, 3, 3, 0).
    /// </summary>
    /// <returns>Je Topf (in Topf-Reihenfolge) die Mannschafts-Ids.</returns>
    public static IReadOnlyList<IReadOnlyList<int>> Verteilen(
        IReadOnlyList<int> mannschaftIds,
        IReadOnlyDictionary<int, int> rangProMannschaft,
        int topfAnzahl,
        IReadOnlyList<int>? vorabInTopf1 = null,
        IReadOnlyDictionary<int, int>? gruppenplatzProMannschaft = null)
    {
        if (topfAnzahl < 1) throw new ArgumentOutOfRangeException(nameof(topfAnzahl));

        var sortiert = mannschaftIds
            .Select((id, index) => (Id: id, Index: index))
            .OrderBy(t => vorabInTopf1?.Contains(t.Id) == true ? 0 : 1)
            .ThenBy(t => gruppenplatzProMannschaft?.TryGetValue(t.Id, out var platz) == true ? platz : int.MaxValue)
            .ThenBy(t => rangProMannschaft.TryGetValue(t.Id, out var rang) ? rang : int.MaxValue)
            .ThenBy(t => t.Index)
            .Select(t => t.Id)
            .ToList();

        int basisGroesse = sortiert.Count / topfAnzahl;
        int rest = sortiert.Count % topfAnzahl;

        var toepfe = new List<IReadOnlyList<int>>(topfAnzahl);
        int position = 0;
        for (int topfIndex = 0; topfIndex < topfAnzahl; topfIndex++)
        {
            int groesse = basisGroesse + (topfIndex < rest ? 1 : 0);
            toepfe.Add(sortiert.GetRange(position, groesse));
            position += groesse;
        }
        return toepfe;
    }

    // ── Nach Profigrenze (#470) ─────────────────────────────────────────────

    /// <summary>Ob die Automatik Profi- und Amateurtopf nach Spielklasse trennt statt gleich große Töpfe zu bilden: K.-o.-
    /// Runde „Nach Töpfen“ mit genau zwei Töpfen, wirksamer Regel „Heimrecht für Unterklassige“ und einer Rangliste mit
    /// Profigrenze — der DFB-Pokal ab der 2. Runde. Die 1. Runde hat „Heimrecht für den unteren Topf“ und bleibt bei
    /// gleich großen Töpfen.</summary>
    public static bool NachProfigrenzeTeilen(Auslosung auslosung, int? profigrenze) =>
        profigrenze.HasValue &&
        auslosung.Lostoepfe.Count == 2 &&
        auslosung.Auslosungsart == Auslosungsart.NachToepfen &&
        auslosung.TurnierRunde?.Rundenart == Rundenart.KoRunde &&
        AuslosungsRegelKompatibilitaet.Wirkt(AuslosungsRegelArt.HeimrechtFuerUnterklassige, auslosung, Rundenart.KoRunde);

    /// <summary>Topf 1 = Teams bis zur Profigrenze, Topf 2 = Teams darunter und ohne Spielklasse; die Töpfe dürfen
    /// ungleich groß sein, den Überhang lost „Nach Töpfen“ untereinander (#495). Innerhalb eines Topfs nach Rang.</summary>
    public static IReadOnlyList<IReadOnlyList<int>> NachProfigrenzeVerteilen(
        IReadOnlyList<int> mannschaftIds, IReadOnlyDictionary<int, int> rangProMannschaft, Spielklassen spielklassen)
    {
        var sortiert = mannschaftIds
            .OrderBy(id => rangProMannschaft.TryGetValue(id, out var rang) ? rang : int.MaxValue)
            .ToList();
        bool IstProfi(int id) => spielklassen.Klasse(id) <= spielklassen.Profigrenze;
        return [sortiert.Where(IstProfi).ToList(), sortiert.Where(id => !IstProfi(id)).ToList()];
    }
}
