using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>In-memory stub für IVerbandsQuoteRepository — liefert nur konfigurierte Quoten zurück,
/// keine Persistenz nötig für die Auslosungs-Algorithmus-Tests.</summary>
public class FakeVerbandsQuoteRepository : IVerbandsQuoteRepository
{
    private readonly List<VerbandsQuote> _quoten = [];

    public void Speichern(params VerbandsQuote[] quoten) => _quoten.AddRange(quoten);

    public Task<VerbandsQuote?> FindenNachIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_quoten.FirstOrDefault(q => q.Id == id));

    public Task<IReadOnlyList<VerbandsQuote>> AlleFindenAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<VerbandsQuote>>([.. _quoten]);

    public Task HinzufuegenAsync(VerbandsQuote e, CancellationToken ct = default)
    { _quoten.Add(e); return Task.CompletedTask; }

    public Task AktualisierenAsync(VerbandsQuote e, CancellationToken ct = default) => Task.CompletedTask;

    public Task LoeschenAsync(VerbandsQuote e, CancellationToken ct = default)
    { _quoten.Remove(e); return Task.CompletedTask; }

    public Task<IReadOnlyList<VerbandsQuote>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<VerbandsQuote>>([.. _quoten.Where(q => q.TurnierInstanzId == turnierInstanzId)]);

    public Task<IReadOnlyList<VerbandsQuote>> QuotenErsetzenAsync(int turnierInstanzId, IReadOnlyList<VerbandsQuote> neueQuoten, CancellationToken ct = default)
    {
        _quoten.RemoveAll(q => q.TurnierInstanzId == turnierInstanzId);
        _quoten.AddRange(neueQuoten);
        return Task.FromResult(neueQuoten);
    }
}
