using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Stammdaten;

public interface ILandRepository : IRepository<Land>
{
    Task<IReadOnlyList<Land>> AlleFindenMitVerbandAsync(CancellationToken ct = default);
    Task<LandBezuege> BezuegeZaehlenAsync(int landId, CancellationToken ct = default);
}
