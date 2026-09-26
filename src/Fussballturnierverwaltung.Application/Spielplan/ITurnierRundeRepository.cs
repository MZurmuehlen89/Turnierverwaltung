using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Spielplan;

public interface ITurnierRundeRepository : IRepository<TurnierRunde>
{
    Task<TurnierRunde?> FindenNachIdMitRegelwerkAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<TurnierRunde>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default);

    /// <summary>Standard-Regelwerk der Instanz — Rückfall für Runden ohne eigenes Regelwerk (AD-026).</summary>
    Task<int?> StandardRegelwerkIdDerInstanzAsync(int turnierInstanzId, CancellationToken ct = default);
}
