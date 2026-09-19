using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Turniere;

public interface ILandesQuoteRepository : IRepository<LandesQuote>
{
    Task<IReadOnlyList<LandesQuote>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default);

    Task<IReadOnlyList<LandesQuote>> QuotenErsetzenAsync(int turnierInstanzId, IReadOnlyList<LandesQuote> neueQuoten, CancellationToken ct = default);
}
