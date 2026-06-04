using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

public interface IAuslosungRepository : IRepository<Auslosung>
{
    Task<IReadOnlyList<Auslosung>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default);
    Task<Auslosung?> FindenNachIdMitDetailsAsync(int id, CancellationToken ct = default);
    Task ErgebnisseSetzenUndAbschliessenAsync(int auslosungId, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct = default);
    Task ErgebnisseLoeschenUndZuruecksetzenAsync(int auslosungId, CancellationToken ct = default);
}
