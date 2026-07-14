using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Spielplan;

public interface IQualifikationsRegelRepository : IRepository<QualifikationsRegel>
{
    Task<IReadOnlyList<QualifikationsRegel>> AlleNachQuellRundeIdAsync(int quellRundeId, CancellationToken ct = default);
    Task<IReadOnlyList<QualifikationsRegel>> AlleNachZielRundeIdAsync(int zielRundeId, CancellationToken ct = default);
    Task<IReadOnlyList<QualifikationsRegel>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default);
    Task<QualifikationsRegel?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default);
}
