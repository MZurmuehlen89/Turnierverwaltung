using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

namespace Fussballturnierverwaltung.Application.Turniere;

public interface IRegelwerkRepository : IRepository<Regelwerk>
{
    Task<IReadOnlyList<Regelwerk>> AlleFindenMitRegelnAsync(CancellationToken ct = default);
    Task<Regelwerk?> FindenNachIdMitAllenRegelnAsync(int id, CancellationToken ct = default);
    Task SpeichernAenderungenAsync(CancellationToken ct = default);
}
