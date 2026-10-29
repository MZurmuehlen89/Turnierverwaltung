using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Application.Ranglisten;

/// <summary>Eine Spielklasse einer Ligapyramide mit ihren Ranglistenplätzen (#572). Parallele Staffeln derselben
/// Klasse (Regionalligen) bilden eine Spielklasse; ihre Plätze werden über alle Staffeln durchgezählt.</summary>
public sealed record SpielklassenInfo(int Nummer, string Name, int ErsterRang, int LetzterRang)
{
    public int Plaetze => LetzterRang - ErsterRang + 1;
}

/// <summary>Platzbereich eines Kontingents innerhalb seiner Spielklasse, z. B. Bundesliga, Plätze 5–6.</summary>
public sealed record KontingentBereich(SpielklassenInfo Spielklasse, int VonPlatz, int BisPlatz)
{
    public int ErsterRang => Spielklasse.ErsterRang + VonPlatz - 1;
    public int LetzterRang => Spielklasse.ErsterRang + BisPlatz - 1;

    public string Anzeige => VonPlatz == BisPlatz
        ? $"{Spielklasse.Name}, Platz {VonPlatz}"
        : $"{Spielklasse.Name}, Plätze {VonPlatz}–{BisPlatz}";
}

/// <summary>Rechnet Kontingente (Land + Spielklasse + ab Platz + Anzahl) in Ranglistenplätze um (#572). Zustandslos.</summary>
public static class Kontingente
{
    /// <summary>Die Spielklassen der Rangliste, höchste zuerst; leer, wenn die Rangliste keine Abschnitte hat.</summary>
    public static IReadOnlyList<SpielklassenInfo> Spielklassen(Rangliste rangliste) =>
        AbschnittsUebersicht.Fuer(rangliste)
            .GroupBy(a => a.Spielklasse)
            .OrderBy(g => g.Key)
            .Select(g => new SpielklassenInfo(
                g.Key, SpielklassenName(g.Key, g.Select(a => a.Name).ToList()), g.Min(a => a.VonPlatz), g.Max(a => a.BisPlatz)))
            .ToList();

    /// <summary>Der Bereich ab <paramref name="abPlatz"/> über <paramref name="anzahl"/> Plätze der Spielklasse;
    /// <c>null</c>, wenn die Spielklasse fehlt oder der Bereich nicht in sie passt.</summary>
    public static KontingentBereich? Bereich(Rangliste rangliste, int spielklasse, int abPlatz, int anzahl)
    {
        var klasse = Spielklassen(rangliste).FirstOrDefault(k => k.Nummer == spielklasse);
        int bisPlatz = abPlatz + anzahl - 1;
        return klasse is not null && abPlatz >= 1 && anzahl >= 1 && bisPlatz <= klasse.Plaetze
            ? new KontingentBereich(klasse, abPlatz, bisPlatz)
            : null;
    }

    /// <summary>Eine Staffel trägt ihren eigenen Namen; parallele Staffeln den gemeinsamen Wortanfang
    /// („Regionalliga Nordost“, „Regionalliga West“ → „Regionalliga“), ohne einen solchen „Spielklasse n“.</summary>
    private static string SpielklassenName(int nummer, IReadOnlyList<string> staffeln)
    {
        if (staffeln.Count == 1)
            return staffeln[0];

        var woerter = staffeln.Select(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList();
        var gemeinsam = woerter[0]
            .TakeWhile((wort, i) => woerter.All(w => w.Length > i && w[i] == wort))
            .ToList();
        return gemeinsam.Count > 0 ? string.Join(' ', gemeinsam) : $"Spielklasse {nummer}";
    }
}
