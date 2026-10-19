using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Application.Ranglisten;

/// <summary>Ein Abschnitt einer Ligapyramide mit seinem Platzbereich (Issue #393).</summary>
public sealed record AbschnittsZeile(string Name, int Spielklasse, int VonPlatz, int BisPlatz)
{
    public string Anzeige => $"Platz {VonPlatz}–{BisPlatz} · {Name} (Spielklasse {Spielklasse})";
}

public static class AbschnittsUebersicht
{
    /// <summary>Die Abschnitte in Platzreihenfolge; jeder beginnt hinter dem vorigen.</summary>
    public static IReadOnlyList<AbschnittsZeile> Fuer(Rangliste rangliste)
    {
        var zeilen = new List<AbschnittsZeile>();
        int von = 1;
        foreach (var abschnitt in rangliste.Abschnitte.OrderBy(a => a.BisPlatz))
        {
            zeilen.Add(new AbschnittsZeile(abschnitt.Name, abschnitt.Spielklasse, von, abschnitt.BisPlatz));
            von = abschnitt.BisPlatz + 1;
        }
        return zeilen;
    }
}
