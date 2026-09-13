namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Zentraler Trigger-Punkt für die Platzhalter-Kaskade (Issue #88/#113) einer Turnierinstanz.
/// Als schmales Interface statt direkter Abhängigkeit auf <see cref="SpielplanService"/> ausgelegt, damit
/// Aufrufer wie <see cref="Auslosungen.AuslosungService"/> es injizieren können, ohne in Tests einen vollen,
/// DB-gestützten <see cref="SpielplanService"/> aufbauen zu müssen (siehe Issue #306).</summary>
public interface IPlatzhalterKaskadeService
{
    Task KaskadeAusloesenAsync(int turnierInstanzId, CancellationToken ct = default);

    /// <summary>Löscht einen ggf. bereits generierten Spielplan (Spiele) einer Runde, OHNE selbst die
    /// Platzhalter-Kaskade auszulösen — für Aufrufer, die im Anschluss ohnehin
    /// <see cref="KaskadeAusloesenAsync"/> separat aufrufen (siehe <see cref="Auslosungen.AuslosungService.ZuruecksetzenAsync"/>,
    /// Issue #320: ohne diesen Aufruf blieb ein aus einer zurückgesetzten Auslosung bereits generierter
    /// Spielplan als Karteileiche stehen und summierte sich beim nächsten Abschließen/Ausführen zu
    /// doppelten Partien).</summary>
    Task RundenSpielplanOhneKaskadeLoeschenAsync(int turnierRundeId, CancellationToken ct = default);
}
