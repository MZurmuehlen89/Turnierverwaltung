namespace Fussballturnierverwaltung.Application.Ranglisten.Import;

/// <summary>Vorschau einer auf einer Seite gefundenen Tabelle für die interaktive Quellen-Auswahl
/// (Issue #225) — Index entspricht der Position in document.QuerySelectorAll("table") und wird
/// direkt als RanglistenQuelle.TabellenIndex übernommen. VorschauZeilen ist auf wenige Zeilen
/// gedeckelt (reicht zur Spalten-/Tabellenidentifikation, kein voller Tabellenexport).</summary>
public record RanglistenTabellenVorschau(
    int Index,
    int ZeilenAnzahl,
    int SpaltenAnzahl,
    bool ErkannteKopfzeile,
    IReadOnlyList<IReadOnlyList<string>> VorschauZeilen);
