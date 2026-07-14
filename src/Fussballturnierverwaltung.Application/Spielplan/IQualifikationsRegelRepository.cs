using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Spielplan;

public interface IQualifikationsRegelRepository : IRepository<QualifikationsRegel>
{
    Task<IReadOnlyList<QualifikationsRegel>> AlleNachQuellRundeIdAsync(int quellRundeId, CancellationToken ct = default);
}
