using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>In-memory stub für IRanglisteRepository — für die Auslosung zählt nur die Standard-Rangliste der Runde
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

    public Task<IReadOnlyDictionary<int, int>> PlaetzeAsync(int ranglisteId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<int, int>>(
            _ranglisten.FirstOrDefault(r => r.Id == ranglisteId)?.Eintraege.ToDictionary(e => e.MannschaftId, e => e.Rang) ?? []);

    public Task<Rangliste?> MitAbschnittenAsync(int ranglisteId, CancellationToken ct = default) =>
        FindenNachIdAsync(ranglisteId, ct);

    public Task<IReadOnlyList<Rangliste>> LigapyramidenAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Rangliste>>([.. _ranglisten.Where(r => r.Abschnitte.Count > 0)]);

    public Task SpeichernAenderungenAsync(CancellationToken ct = default) => Task.CompletedTask;
}
