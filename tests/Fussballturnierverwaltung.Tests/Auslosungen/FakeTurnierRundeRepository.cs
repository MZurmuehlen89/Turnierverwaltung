using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>In-memory stub für ITurnierRundeRepository — liefert nur konfigurierte Runden zurück,
/// keine Persistenz nötig für die Auslosungs-Algorithmus-Tests.</summary>
public class FakeTurnierRundeRepository : ITurnierRundeRepository
{
    private readonly List<TurnierRunde> _runden = [];

    public void Speichern(params TurnierRunde[] runden) => _runden.AddRange(runden);

    public Task<TurnierRunde?> FindenNachIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_runden.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<TurnierRunde>> AlleFindenAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TurnierRunde>>([.. _runden]);

    public Task HinzufuegenAsync(TurnierRunde e, CancellationToken ct = default)
    { _runden.Add(e); return Task.CompletedTask; }

    public Task AktualisierenAsync(TurnierRunde e, CancellationToken ct = default) => Task.CompletedTask;

    public Task LoeschenAsync(TurnierRunde e, CancellationToken ct = default)
    { _runden.Remove(e); return Task.CompletedTask; }

    public Task<TurnierRunde?> FindenNachIdMitRegelwerkAsync(int id, CancellationToken ct = default) =>
        FindenNachIdAsync(id, ct);

    public Task<int?> StandardRegelwerkIdDerInstanzAsync(int turnierInstanzId, CancellationToken ct = default) =>
        Task.FromResult<int?>(null);

    public Task<IReadOnlyList<TurnierRunde>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TurnierRunde>>([.. _runden.Where(r => r.TurnierInstanzId == turnierInstanzId)]);
}
