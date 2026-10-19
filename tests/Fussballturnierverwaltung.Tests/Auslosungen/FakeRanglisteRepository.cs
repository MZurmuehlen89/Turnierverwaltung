using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>In-memory stub für IRanglisteRepository — für die Auslosung zählt nur die neueste Rangliste einer Quelle
/// (Spielklassen, #393).</summary>
public class FakeRanglisteRepository : IRanglisteRepository
{
    private readonly List<Rangliste> _ranglisten = [];

    public void Speichern(params Rangliste[] ranglisten) => _ranglisten.AddRange(ranglisten);

    public Task<Rangliste?> FindenNachIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_ranglisten.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<Rangliste>> AlleFindenAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Rangliste>>([.. _ranglisten]);

    public Task HinzufuegenAsync(Rangliste e, CancellationToken ct = default)
    { _ranglisten.Add(e); return Task.CompletedTask; }

    public Task AktualisierenAsync(Rangliste e, CancellationToken ct = default) => Task.CompletedTask;

    public Task LoeschenAsync(Rangliste e, CancellationToken ct = default)
    { _ranglisten.Remove(e); return Task.CompletedTask; }

    public Task<IReadOnlyList<Rangliste>> AlleFindenMitEintraegenAsync(CancellationToken ct = default) => AlleFindenAsync(ct);

    public Task<Rangliste?> FindenNachIdMitEintraegenAsync(int id, CancellationToken ct = default) => FindenNachIdAsync(id, ct);

    public Task<IReadOnlyDictionary<int, int>> PlaetzeDerNeuestenZurQuelleAsync(int quelleId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<int, int>>(
            NeuesteZurQuelle(quelleId)?.Eintraege.ToDictionary(e => e.MannschaftId, e => e.Rang) ?? []);

    public Task<Rangliste?> NeuesteZurQuelleMitAbschnittenAsync(int quelleId, CancellationToken ct = default) =>
        Task.FromResult(NeuesteZurQuelle(quelleId));

    public Task SpeichernAenderungenAsync(CancellationToken ct = default) => Task.CompletedTask;

    private Rangliste? NeuesteZurQuelle(int quelleId) =>
        _ranglisten.Where(r => r.RanglistenQuelleId == quelleId)
            .OrderByDescending(r => r.Datum).ThenByDescending(r => r.Id).FirstOrDefault();
}
