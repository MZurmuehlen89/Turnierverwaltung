using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
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

    public Task AktualisierenAsync(Auslosung e, CancellationToken ct = default) => HinzufuegenAsync(e, ct);

    public Task LoeschenAsync(Auslosung e, CancellationToken ct = default)
    { _store.Remove(e.Id); return Task.CompletedTask; }

    // ── IAuslosungRepository ──────────────────────────────────────────────────

    public Task<IReadOnlyList<Auslosung>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Auslosung>>([]);

    /// <summary>Gastgeber und Titelverteidiger, die der Service als feste Gruppenköpfe abfragt (#487).</summary>
    public GruppenkopfKandidaten Kandidaten { get; set; } = GruppenkopfKandidaten.Keine;

    public Task<GruppenkopfKandidaten> GruppenkopfKandidatenAsync(int turnierInstanzId, CancellationToken ct = default) =>
        Task.FromResult(Kandidaten);

    public Task<Auslosung?> FindenNachIdMitDetailsAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<Auslosung?> FindenAbgeschlosseneNachRundeAsync(
        int turnierInstanzId, int turnierRundeId, CancellationToken ct = default) =>
        Task.FromResult(_store.Values
            .Where(a => a.TurnierInstanzId == turnierInstanzId
                     && a.TurnierRundeId == turnierRundeId
                     && a.Status == AuslosungsStatus.Abgeschlossen)
            .MaxBy(a => a.Id));

    public async Task<IReadOnlyList<Mannschaft>?> AusgeloesteMannschaftenAsync(
        int turnierInstanzId, int turnierRundeId, CancellationToken ct = default) =>
        await FindenAbgeschlosseneNachRundeAsync(turnierInstanzId, turnierRundeId, ct) is { } auslosung
            ? [.. auslosung.Ergebnisse
                .SelectMany(e => new[] { e.HeimMannschaft, e.AuswaertsMannschaft })
                .OfType<Mannschaft>()
                .DistinctBy(m => m.Id)]
            : null;

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
