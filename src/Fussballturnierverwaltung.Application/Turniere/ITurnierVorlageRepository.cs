using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere;

public interface ITurnierVorlageRepository : IRepository<TurnierVorlage>
{
    Task<IReadOnlyList<TurnierVorlage>> AlleNachKategorieFindenAsync(VorlagenKategorie kategorie, CancellationToken ct = default);
}
