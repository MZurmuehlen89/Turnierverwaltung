using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Application.Spielplan;

public interface ISpielRepository : IRepository<Spiel>
{
    Task<IReadOnlyList<Spiel>> AlleNachRundeAsync(int turnierRundeId, CancellationToken ct = default);
    Task<IReadOnlyList<Spiel>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default);
    Task<int> HoechsteSpielNummerAsync(int turnierInstanzId, CancellationToken ct = default);
    Task HinzufuegenMehrfachAsync(IEnumerable<Spiel> spiele, CancellationToken ct = default);

    Task<Spiel?> FindenNachIdMitErgebnisAsync(int id, CancellationToken ct = default);
    Task ErgebnisSetzenAsync(int spielId, SpielErgebnis ergebnis, CancellationToken ct = default);
    Task ErgebnisLoeschenAsync(int spielId, CancellationToken ct = default);
}
