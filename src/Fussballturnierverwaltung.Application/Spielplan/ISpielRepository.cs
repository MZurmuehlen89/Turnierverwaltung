using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Application.Spielplan;

public interface ISpielRepository : IRepository<Spiel>
{
    Task<IReadOnlyList<Spiel>> AlleNachRundeAsync(int turnierRundeId, CancellationToken ct = default);
    Task<IReadOnlyList<Spiel>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default);
    Task<int> HoechsteSpielNummerAsync(int turnierInstanzId, CancellationToken ct = default);
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
}
