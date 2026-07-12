using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Application.Ranglisten;

public interface IRanglisteRepository : IRepository<Rangliste>
{
    Task<IReadOnlyList<Rangliste>> AlleFindenMitEintraegenAsync(CancellationToken ct = default);
    Task<Rangliste?> FindenNachIdMitEintraegenAsync(int id, CancellationToken ct = default);
    Task SpeichernAenderungenAsync(CancellationToken ct = default);
}
