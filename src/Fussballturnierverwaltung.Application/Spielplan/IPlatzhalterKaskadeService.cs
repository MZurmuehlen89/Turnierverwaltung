namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Zentraler Trigger-Punkt für die Platzhalter-Kaskade (Issue #88/#113) einer Turnierinstanz.
/// Als schmales Interface statt direkter Abhängigkeit auf <see cref="SpielplanService"/> ausgelegt, damit
/// Aufrufer wie <see cref="Auslosungen.AuslosungService"/> es injizieren können, ohne in Tests einen vollen,
/// DB-gestützten <see cref="SpielplanService"/> aufbauen zu müssen (siehe Issue #306).</summary>
public interface IPlatzhalterKaskadeService
{
    Task KaskadeAusloesenAsync(int turnierInstanzId, CancellationToken ct = default);
}
