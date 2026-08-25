namespace Fussballturnierverwaltung.Application.Ranglisten.Import;

/// <summary>Index ist die Position innerhalb der eingelesenen Tabelle (0-basiert, nach dem
/// Überspringen fehlerhafter/leerer Zeilen) — bestimmt die spätere Rang-Reihenfolge.</summary>
public record RanglistenRohZeile(int Index, string RohName, int? Punkte);
