using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Stammdaten;

public class LandService(ILandRepository repository)
{
    public Task<IReadOnlyList<Land>> AlleAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenAsync(ct);

    public Task<IReadOnlyList<Land>> AlleAbrufenMitVerbandAsync(CancellationToken ct = default)
        => repository.AlleFindenMitVerbandAsync(ct);

    public async Task<Land?> FindenNachIdAsync(int id, CancellationToken ct = default)
        => await repository.FindenNachIdAsync(id, ct);
}
