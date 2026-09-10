using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>
/// In-memory stub für IAuslosungRepository. Speichert eine Auslosung pro Instanz
/// und fängt die Ergebnisse von ErgebnisseSetzenUndAbschliessenAsync ab.
/// </summary>
public class FakeAuslosungRepository : IAuslosungRepository
{
    private readonly Dictionary<int, Auslosung> _store = [];

    public Auslosung? Aktuelle => _store.Values.FirstOrDefault();
    public IReadOnlyList<AuslosungsErgebnis>? GespeicherteErgebnisse { get; private set; }

    public void Speichern(Auslosung auslosung) => _store[auslosung.Id] = auslosung;

    // ── IRepository<Auslosung> ────────────────────────────────────────────────

    public Task<Auslosung?> FindenNachIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<IReadOnlyList<Auslosung>> AlleFindenAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Auslosung>>([.. _store.Values]);

    public Task HinzufuegenAsync(Auslosung e, CancellationToken ct = default)
    { _store[e.Id] = e; return Task.CompletedTask; }

    public Task AktualisierenAsync(Auslosung e, CancellationToken ct = default)
    { _store[e.Id] = e; return Task.CompletedTask; }

    public Task LoeschenAsync(Auslosung e, CancellationToken ct = default)
    { _store.Remove(e.Id); return Task.CompletedTask; }

    // ── IAuslosungRepository ──────────────────────────────────────────────────

    public Task<IReadOnlyList<Auslosung>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Auslosung>>([]);

    public Task<Auslosung?> FindenNachIdMitDetailsAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<Auslosung?> FindenAbgeschlosseneNachRundeAsync(
        int turnierInstanzId, int turnierRundeId, CancellationToken ct = default) =>
        Task.FromResult(_store.Values
            .Where(a => a.TurnierInstanzId == turnierInstanzId
                     && a.TurnierRundeId == turnierRundeId
                     && a.Status == AuslosungsStatus.Abgeschlossen)
            .MaxBy(a => a.Id));

    public Task ErgebnisseSetzenUndAbschliessenAsync(
        int auslosungId, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct = default)
    {
        GespeicherteErgebnisse = ergebnisse;
        if (_store.TryGetValue(auslosungId, out var a))
            a.Status = AuslosungsStatus.Abgeschlossen;
        return Task.CompletedTask;
    }

    public Task ErgebnisseLoeschenUndZuruecksetzenAsync(int auslosungId, CancellationToken ct = default) =>
        Task.CompletedTask;
}
