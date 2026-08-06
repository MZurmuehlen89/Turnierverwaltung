using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Spielplan;

public interface IUebergangsregelRepository : IRepository<Uebergangsregel>
{
    Task<IReadOnlyList<Uebergangsregel>> AlleNachQuellRundeIdAsync(int quellRundeId, CancellationToken ct = default);
    Task<IReadOnlyList<Uebergangsregel>> AlleNachZielRundeIdAsync(int zielRundeId, CancellationToken ct = default);
    Task<IReadOnlyList<Uebergangsregel>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default);
    Task<Uebergangsregel?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default);
}
