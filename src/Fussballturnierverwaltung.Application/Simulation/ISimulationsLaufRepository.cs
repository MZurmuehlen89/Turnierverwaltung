using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Simulation;

namespace Fussballturnierverwaltung.Application.Simulation;

public interface ISimulationsLaufRepository : IRepository<SimulationsLauf>
{
    Task<IReadOnlyList<SimulationsLauf>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default);
}
