namespace Fussballturnierverwaltung.WinUi.Helpers;

/// <summary>Einheitlicher Text für die Datensatzanzahl in der Fußzeile der Stammdaten-Listen (Issue #366):
/// ohne Filter „300 Mannschaften“, mit aktivem Filter „64 von 300 Mannschaften“ — auch wenn der Filter
/// gerade alle Datensätze durchlässt, damit sichtbar bleibt, dass gefiltert wird.</summary>
internal static class DatensatzAnzahl
{
    public static string Text(int gefiltert, int gesamt, bool filterAktiv, string einzahl, string mehrzahl)
    {
        var bezeichnung = gesamt == 1 ? einzahl : mehrzahl;
        return filterAktiv ? $"{gefiltert} von {gesamt} {bezeichnung}" : $"{gesamt} {bezeichnung}";
    }
}
