namespace Fussballturnierverwaltung.Application.Abstraktionen;

/// <summary>Ein Prüffehler mit optionalem Bezug auf das Eingabefeld (#595): <see cref="Feld"/> ist der Name der
/// Eingabe-Eigenschaft, <see cref="Zeile"/> der Index der Zeile, wenn das Feld in einer Liste steht. Ohne Feld
/// ist es eine Kreuzprüfung über mehrere Felder – sie steht nur in der Meldungsleiste.</summary>
public record Feldfehler(string Text, string? Feld = null, int? Zeile = null);
