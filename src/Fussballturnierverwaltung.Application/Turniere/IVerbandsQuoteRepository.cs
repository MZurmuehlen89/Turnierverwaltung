using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Turniere;

public interface IVerbandsQuoteRepository : IRepository<VerbandsQuote>
{
    Task<IReadOnlyList<VerbandsQuote>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default);

    Task<IReadOnlyList<VerbandsQuote>> QuotenErsetzenAsync(int turnierInstanzId, IReadOnlyList<VerbandsQuote> neueQuoten, CancellationToken ct = default);
}
