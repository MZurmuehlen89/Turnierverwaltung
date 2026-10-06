using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Stammdaten;

/// <summary>Eine Mannschaft als Eintrag einer Auswahlliste (#419): nur, was Anzeige und Filter nach
/// Mannschaftsart, Land und Verband brauchen — ohne Wappen und ohne die Flagge des Landes.</summary>
public record MannschaftAuswahl(int Id, string Name, Mannschaftsart Mannschaftsart, int LandId, int VerbandId);
