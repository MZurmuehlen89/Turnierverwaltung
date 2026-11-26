using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Beschreibt die Regeln einer <see cref="VorschlagsOption"/> in einem Satz für die Anzeige (#167):
/// „Gruppenphase: Platz 1–2 je Gruppe, 4 beste 3. Plätze“. Rein.</summary>
public static class UebergangsVorschlagText
{
    public static string Beschreiben(VorschlagsOption option, IReadOnlyList<TurnierRunde> runden) =>
        string.Join(" · ", option.Regeln
            .GroupBy(r => r.QuellReihenfolge)
            .OrderBy(g => g.Key)
            .Select(g => QuelleBeschreiben(runden[g.Key - 1], g.ToList())));

    private static string QuelleBeschreiben(TurnierRunde quelle, IReadOnlyList<UebergangsregelPositionsEingabe> regeln)
    {
        var teile = new List<string>();
        switch (quelle.Rundenart)
        {
            case Rundenart.Gruppenphase:
                teile.AddRange(GruppenTeile(quelle, regeln.Where(r => r.AnzahlBeste is null)));
                break;
            case Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei:
                teile.AddRange(KoTeile(regeln));
                break;
            default:
                teile.AddRange(regeln.Where(r => r.AnzahlBeste is null).Select(r => PlatzText(r.PlatzierungVon, r.PlatzierungBis)));
                break;
        }
        teile.AddRange(regeln.Where(r => r.AnzahlBeste is not null)
            .Select(r => $"{r.AnzahlBeste} beste {r.PlatzierungVon}. Plätze"));
        return $"{quelle.Name}: {string.Join(", ", teile)}";
    }

    private static IEnumerable<string> GruppenTeile(TurnierRunde quelle, IEnumerable<UebergangsregelPositionsEingabe> regeln) =>
        regeln.GroupBy(r => (r.PlatzierungVon, r.PlatzierungBis)).Select(g =>
        {
            var gruppen = g.Select(r => r.GruppeIndex ?? 0).Order().ToList();
            string platz = PlatzText(g.Key.PlatzierungVon, g.Key.PlatzierungBis);
            return gruppen.Count == quelle.GruppenAnzahl
                ? $"{platz} je Gruppe"
                : $"{platz} in Gruppe {string.Join(", ", gruppen.Select(i => (char)('A' + i - 1)))}";
        });

    private static IEnumerable<string> KoTeile(IEnumerable<UebergangsregelPositionsEingabe> regeln) =>
        regeln.GroupBy(r => r.PlatzierungVon).OrderBy(g => g.Key).Select(g =>
            $"{(g.Key == 2 ? "Verlierer" : "Sieger")} Spiel {Bereiche(g.Select(r => r.GruppeIndex ?? 0).Order())}");

    private static string PlatzText(int von, int bis) => von == bis ? $"Platz {von}" : $"Platz {von}–{bis}";

    /// <summary>„1–4, 6“ aus aufsteigenden Zahlen.</summary>
    private static string Bereiche(IEnumerable<int> aufsteigend)
    {
        var teile = new List<string>();
        int? von = null, bis = null;
        foreach (int zahl in aufsteigend)
        {
            if (bis is { } ende && zahl == ende + 1) { bis = zahl; continue; }
            if (von is { } start) teile.Add(start == bis ? $"{start}" : $"{start}–{bis}");
            von = bis = zahl;
        }
        if (von is { } letzter) teile.Add(letzter == bis ? $"{letzter}" : $"{letzter}–{bis}");
        return string.Join(", ", teile);
    }
}
