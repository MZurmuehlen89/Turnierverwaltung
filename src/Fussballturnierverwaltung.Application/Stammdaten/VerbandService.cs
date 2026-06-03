using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Stammdaten;

public class VerbandService(IVerbandRepository repository)
{
    public Task<IReadOnlyList<Verband>> AlleAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenAsync(ct);
}
