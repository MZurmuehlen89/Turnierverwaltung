using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

namespace Fussballturnierverwaltung.Application.Turniere;

public interface IRankingRegelSetRepository : IRepository<RankingRegelSet>
{
    Task<RankingRegelSet?> FindenNachIdMitEintraegenAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<RankingRegelSet>> AlleFindenMitEintraegenAsync(CancellationToken ct = default);
}
