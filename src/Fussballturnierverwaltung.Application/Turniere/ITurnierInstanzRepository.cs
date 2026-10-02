using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere;

public interface ITurnierInstanzRepository : IRepository<TurnierInstanz>
{
    Task<IReadOnlyList<TurnierInstanz>> AlleFindenMitVorlageAsync(CancellationToken ct = default);
    Task<TurnierInstanz?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<TurnierInstanz>> AlleNachStatusFindenAsync(TurnierStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<TurnierRunde>> RundenErsetzenAsync(int instanzId, IReadOnlyList<TurnierRunde> neueRunden, CancellationToken ct = default);
    Task<IReadOnlyList<int>> GastgeberIdsAsync(int instanzId, CancellationToken ct = default);
    Task GastgeberErsetzenAsync(int instanzId, IReadOnlyCollection<int> mannschaftIds, CancellationToken ct = default);
    Task LoeschenMitSpielen(int id, CancellationToken ct = default);

    /// <summary>Issue #294: vollständiger Objektgraph für <see cref="TurnierKlonService"/> — Runden inkl.
    /// ihrer ausgehenden Übergangsregeln sowie Verbandsquoten (nie Spiele/Auslosungen/SimulationsLaeufe,
    /// ein Klon kopiert nur die Struktur, nie Spieldaten).</summary>
    Task<TurnierInstanz?> FindenNachIdMitVollstaendigenDatenAsync(int id, CancellationToken ct = default);
}
