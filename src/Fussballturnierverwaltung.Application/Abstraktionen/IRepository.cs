using Fussballturnierverwaltung.Domain;

namespace Fussballturnierverwaltung.Application.Abstraktionen;

public interface IRepository<T> where T : BasisEntitaet
{
    Task<T?> FindenNachIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> AlleFindenAsync(CancellationToken ct = default);
    Task HinzufuegenAsync(T entitaet, CancellationToken ct = default);
    Task AktualisierenAsync(T entitaet, CancellationToken ct = default);
    Task LoeschenAsync(T entitaet, CancellationToken ct = default);
}
