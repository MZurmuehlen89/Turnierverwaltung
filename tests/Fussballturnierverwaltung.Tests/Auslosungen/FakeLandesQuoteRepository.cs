using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>In-memory stub für ILandesQuoteRepository, analog FakeVerbandsQuoteRepository.</summary>
public class FakeLandesQuoteRepository : ILandesQuoteRepository
{
    private readonly List<LandesQuote> _quoten = [];

    public void Speichern(params LandesQuote[] quoten) => _quoten.AddRange(quoten);

    public Task<LandesQuote?> FindenNachIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_quoten.FirstOrDefault(q => q.Id == id));

    public Task<IReadOnlyList<LandesQuote>> AlleFindenAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<LandesQuote>>([.. _quoten]);

    public Task HinzufuegenAsync(LandesQuote e, CancellationToken ct = default)
    { _quoten.Add(e); return Task.CompletedTask; }

    public Task AktualisierenAsync(LandesQuote e, CancellationToken ct = default) => Task.CompletedTask;

    public Task LoeschenAsync(LandesQuote e, CancellationToken ct = default)
    { _quoten.Remove(e); return Task.CompletedTask; }

    public Task<IReadOnlyList<LandesQuote>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<LandesQuote>>([.. _quoten.Where(q => q.TurnierInstanzId == turnierInstanzId)]);

    public Task<IReadOnlyList<LandesQuote>> QuotenErsetzenAsync(int turnierInstanzId, IReadOnlyList<LandesQuote> neueQuoten, CancellationToken ct = default)
    {
        _quoten.RemoveAll(q => q.TurnierInstanzId == turnierInstanzId);
        _quoten.AddRange(neueQuoten);
        return Task.FromResult(neueQuoten);
    }
}
