using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Zentraler Trigger-Punkt für die Platzhalter-Kaskade (Issue #88/#113) einer Turnierinstanz.
/// Als schmales Interface statt direkter Abhängigkeit auf <see cref="PlatzhalterKaskadeService"/> ausgelegt, damit
/// Aufrufer wie <see cref="Auslosungen.AuslosungService"/> es injizieren können, ohne in Tests die
/// DB-gestützten Spielplan-Dienste aufbauen zu müssen (siehe Issue #306).</summary>
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

    /// <summary>Ersetzt den Spielplan einer Runde durch den aus ihren Auslosungsergebnissen und löst danach
    /// einmal die Platzhalter-Kaskade aus (Issue #411). Für Aufrufer, die eine Auslosung gerade abgeschlossen
    /// haben: <see cref="KaskadeAusloesenAsync"/> allein würde eine Platzhalter-Runde ohne Spiele bereits neu
    /// generieren, bevor der Aufrufer den Spielplan ein zweites Mal erzeugt.</summary>
    Task RundenSpielplanAusAuslosungErsetzenAsync(
        int turnierRundeId, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct = default);
}
