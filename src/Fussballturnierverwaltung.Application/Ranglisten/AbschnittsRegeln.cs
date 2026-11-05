using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;

namespace Fussballturnierverwaltung.Application.Ranglisten;

/// <summary>Prüfregeln für die Abschnitte einer Ligapyramide (#393, #477). Die Abschnitte stehen in Platzreihenfolge,
/// jeder beginnt hinter dem vorigen.</summary>
public static class AbschnittsRegeln
{
    /// <summary>Fehler, die das Speichern verhindern: Name fehlt, Spielklasse oder „bis Platz“ kleiner als 1, „bis Platz“
    /// nicht streng aufsteigend. Feldbezug (#601): Zeile = Abschnitt, Feld = Eigenschaft der Eingabe.</summary>
    public static IReadOnlyList<Feldfehler> Fehler(IReadOnlyList<RanglistenAbschnittEingabe> abschnitte)
    {
        var fehler = new List<Feldfehler>();
        for (int i = 0; i < abschnitte.Count; i++)
        {
            var abschnitt = abschnitte[i];
            var bezeichnung = string.IsNullOrWhiteSpace(abschnitt.Name) ? $"Abschnitt {i + 1}" : $"Abschnitt „{abschnitt.Name.Trim()}“";

            if (string.IsNullOrWhiteSpace(abschnitt.Name))
                fehler.Add(new($"{bezeichnung}: Name fehlt.", nameof(abschnitt.Name), i));
            if (abschnitt.Spielklasse < 1)
                fehler.Add(new($"{bezeichnung}: Die Spielklasse muss mindestens 1 sein.", nameof(abschnitt.Spielklasse), i));
            if (abschnitt.BisPlatz < 1)
                fehler.Add(new($"{bezeichnung}: „bis Platz“ muss mindestens 1 sein.", nameof(abschnitt.BisPlatz), i));
            else if (i > 0 && abschnitt.BisPlatz <= abschnitte[i - 1].BisPlatz)
                fehler.Add(new($"{bezeichnung}: „bis Platz“ muss größer sein als beim Abschnitt davor ({abschnitte[i - 1].BisPlatz}).",
                    nameof(abschnitt.BisPlatz), i));
        }
        return fehler;
    }

    /// <summary>Hinweis, wenn der letzte Abschnitt über die Einträge hinausreicht (erlaubt, etwa bei einer nur teilweise
    /// gefüllten Pyramide); sonst <c>null</c>.</summary>
    public static string? UeberhangHinweis(IReadOnlyList<RanglistenAbschnittEingabe> abschnitte, int eintragsAnzahl)
    {
        if (abschnitte.Count == 0) return null;
        var letzter = abschnitte[^1].BisPlatz;
        return letzter > eintragsAnzahl
            ? $"Der letzte Abschnitt reicht bis Platz {letzter}, die Rangliste hat {eintragsAnzahl} Einträge."
            : null;
    }

    /// <summary>Die Abschnitte mit berechnetem „von Platz“, in der eingegebenen Reihenfolge.</summary>
    public static IReadOnlyList<AbschnittsZeile> Uebersicht(IReadOnlyList<RanglistenAbschnittEingabe> abschnitte)
    {
        var zeilen = new List<AbschnittsZeile>();
        int von = 1;
        foreach (var abschnitt in abschnitte)
        {
            zeilen.Add(new AbschnittsZeile(abschnitt.Name, abschnitt.Spielklasse, von, abschnitt.BisPlatz));
            von = abschnitt.BisPlatz + 1;
        }
        return zeilen;
    }
}
