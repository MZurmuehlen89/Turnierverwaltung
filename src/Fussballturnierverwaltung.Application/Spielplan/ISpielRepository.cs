using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Application.Spielplan;

public interface ISpielRepository : IRepository<Spiel>
{
    Task<IReadOnlyList<Spiel>> AlleNachRundeAsync(int turnierRundeId, CancellationToken ct = default);
    Task<IReadOnlyList<Spiel>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default);
    Task<int> HoechsteSpielNummerAsync(int turnierInstanzId, CancellationToken ct = default);

    /// <summary>
    /// Berechnet die nächste freie Spielnummer für eine Runde:
    /// Anzahl der Spiele in allen Runden mit Reihenfolge &lt; <paramref name="reihenfolge"/>
    /// plus bereits vorhandene Spiele in dieser Runde, plus 1.
    /// </summary>
    Task<int> NaechsteSpielNummerAsync(int turnierInstanzId, int turnierRundeId, int reihenfolge, CancellationToken ct = default);
    Task HinzufuegenMehrfachAsync(IEnumerable<Spiel> spiele, CancellationToken ct = default);
    Task AlleNachRundeLoeschenAsync(int turnierRundeId, CancellationToken ct = default);

    Task<Spiel?> FindenNachIdMitErgebnisAsync(int id, CancellationToken ct = default);
    Task ErgebnisSetzenAsync(int spielId, SpielErgebnis ergebnis, CancellationToken ct = default);
    Task ErgebnisLoeschenAsync(int spielId, CancellationToken ct = default);

    /// <summary>Lädt Spiel mit Teams, Ergebnis und Runde inkl. Regelwerk (für Simulation).</summary>
    Task<Spiel?> FindenNachIdFuerSimulationAsync(int id, CancellationToken ct = default);

    /// <summary>Fügt SpielEreignisse zu einem bereits gespeicherten Spiel hinzu.</summary>
    Task EreignisseHinzufuegenAsync(int spielId, IEnumerable<SpielEreignis> ereignisse, CancellationToken ct = default);

    /// <summary>Lädt Spiel mit TurnierRunde (AuswaertstorRegelAktiv) und Hinspiel-Ergebnis (für Auswärtstorregel).</summary>
    Task<Spiel?> FindenNachIdMitRundeUndHinspielAsync(int id, CancellationToken ct = default);

    /// <summary>Lädt alle Spiele einer Runde inklusive SpielEreignisse (für Fairplay-Wertung).</summary>
    Task<IReadOnlyList<Spiel>> AlleNachRundeMitEreignissenAsync(int turnierRundeId, CancellationToken ct = default);

    /// <summary>Lädt ein einzelnes Spiel mit Ergebnis, Teams und allen SpielEreignissen (für Liveticker).</summary>
    Task<Spiel?> FindenNachIdMitEreignissenAsync(int id, CancellationToken ct = default);

    /// <summary>Setzt die realen Mannschaften auf einem Platzhalter-Spiel und löscht die Platzhalternamen.</summary>
    Task PlatzhalterAktualisierenAsync(int spielId, int heimMannschaftId, int auswaertsMannschaftId, CancellationToken ct = default);

    /// <summary>Ermittelt die (distinkten) TurnierInstanz-IDs der angegebenen Spiele.</summary>
    Task<IReadOnlyList<int>> TurnierInstanzIdsAsync(IReadOnlyList<int> spielIds, CancellationToken ct = default);
}
