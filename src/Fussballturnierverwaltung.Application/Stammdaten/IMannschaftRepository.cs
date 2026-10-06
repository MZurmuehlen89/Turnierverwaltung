using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Stammdaten;

public interface IMannschaftRepository : IRepository<Mannschaft>
{
    Task<IReadOnlyList<Mannschaft>> AlleFindenMitLandAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MannschaftAuswahl>> AlleAlsAuswahlFindenAsync(CancellationToken ct = default);
    Task<Mannschaft?> FindenNachIdMitLandAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Mannschaft>> SuchenAsync(string suchbegriff, CancellationToken ct = default);
    Task<IReadOnlyList<Mannschaft>> AlleNachArtFindenAsync(Mannschaftsart art, CancellationToken ct = default);
    Task<IReadOnlyList<Mannschaft>> FindenNachIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default);
    Task<Mannschaft?> FindenNachNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Mannschaft>> AlleNachTurnierfilterAsync(int? verbandId, int? landId, MannschaftsartFilter filter, CancellationToken ct = default);
}
