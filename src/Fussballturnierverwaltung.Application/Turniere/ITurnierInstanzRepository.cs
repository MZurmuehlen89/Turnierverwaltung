using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere;

public interface ITurnierInstanzRepository : IRepository<TurnierInstanz>
{
    Task<IReadOnlyList<TurnierInstanz>> AlleFindenMitVorlageAsync(CancellationToken ct = default);
    Task<TurnierInstanz?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<TurnierInstanz>> AlleNachStatusFindenAsync(TurnierStatus status, CancellationToken ct = default);
    Task RundenErsetzenAsync(int instanzId, IReadOnlyList<TurnierRunde> neueRunden, CancellationToken ct = default);
    Task LoeschenMitSpielen(int id, CancellationToken ct = default);
}
