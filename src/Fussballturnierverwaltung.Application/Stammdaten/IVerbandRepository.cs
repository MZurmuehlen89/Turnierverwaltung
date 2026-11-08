using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Stammdaten;

public interface IVerbandRepository : IRepository<Verband>
{
    Task<VerbandBezuege> BezuegeZaehlenAsync(int verbandId, CancellationToken ct = default);
}
