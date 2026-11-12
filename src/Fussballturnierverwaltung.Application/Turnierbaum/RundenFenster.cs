namespace Fussballturnierverwaltung.Application.Turnierbaum;

/// <summary>
/// Höhen der Karten im Zwei-Spalten-Fenster der rundenweisen Ansicht (#567, aus <c>TurnierBaumViewModel</c>
/// herausgelöst): Die linke Spalte steht fortlaufend untereinander, jede Karte der rechten Spalte mittig zwischen
/// ihren <see cref="KartenVorgaenger"/>n in der linken. Kartenmitten in Rastereinheiten (0,5 = erste Zeile) — die
/// Pixelumrechnung macht das ViewModel.
/// </summary>
public static class RundenFenster
{
    public static IReadOnlyDictionary<int, double> Zeilen(IReadOnlyList<int> linkeSpalte, IReadOnlyList<int> rechteSpalte,
        IReadOnlyDictionary<int, KartenVorgaenger> vorgaengerJeKnoten)
    {
        var zeilen = new Dictionary<int, double>();
        for (int i = 0; i < linkeSpalte.Count; i++)
            zeilen[linkeSpalte[i]] = i + 0.5;

        for (int i = 0; i < rechteSpalte.Count; i++)
        {
            var vorgaenger = vorgaengerJeKnoten.GetValueOrDefault(rechteSpalte[i]);
            var linkeZeilen = new[] { vorgaenger.Links, vorgaenger.Rechts }
                .Where(id => id is int wert && linkeSpalte.Contains(wert))
                .Select(id => zeilen[id!.Value])
                .ToList();
            zeilen[rechteSpalte[i]] = linkeZeilen.Count > 0 ? linkeZeilen.Average() : i + 0.5;
        }
        return zeilen;
    }
}
