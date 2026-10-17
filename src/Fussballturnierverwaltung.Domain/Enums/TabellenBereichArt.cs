namespace Fussballturnierverwaltung.Domain.Enums;

/// <summary>Art eines <see cref="Entitaeten.Turniere.TabellenBereich"/> (Issue #420) — bestimmt die Zonenfarbe in
/// der Tabelle und ist die Grundlage für Auf-/Abstieg beim Saisonwechsel (#379).</summary>
public enum TabellenBereichArt
{
    Weiterkommen,
    Relegation,
    Abstieg
}
