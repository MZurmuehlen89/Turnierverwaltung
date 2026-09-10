using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

public interface IAuslosungRepository : IRepository<Auslosung>
{
    Task<IReadOnlyList<Auslosung>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default);
    Task<Auslosung?> FindenNachIdMitDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>Findet die zuletzt abgeschlossene Auslosung einer Runde (mit vollen Details). Als
    /// Repository-Methode statt in <see cref="AuslosungService"/> implementiert, damit
    /// <see cref="Spielplan.SpielplanService"/> sie ohne Abhängigkeit auf den Service selbst nutzen kann
    /// (vermeidet eine zirkuläre DI-Abhängigkeit, siehe Issue #306).</summary>
    Task<Auslosung?> FindenAbgeschlosseneNachRundeAsync(int turnierInstanzId, int turnierRundeId, CancellationToken ct = default);

    Task ErgebnisseSetzenUndAbschliessenAsync(int auslosungId, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct = default);
    Task ErgebnisseLoeschenUndZuruecksetzenAsync(int auslosungId, CancellationToken ct = default);
}
