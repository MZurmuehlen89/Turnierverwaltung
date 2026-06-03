using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class TurnierVorlageRepository(TurnierverwaltungDbContext context)
    : GenericRepository<TurnierVorlage>(context), ITurnierVorlageRepository
{
    public override async Task<IReadOnlyList<TurnierVorlage>> AlleFindenAsync(CancellationToken ct = default)
        => await Context.TurnierVorlagen
            .OrderBy(v => v.VorlagenKategorie)
            .ThenBy(v => v.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TurnierVorlage>> AlleNachKategorieFindenAsync(VorlagenKategorie kategorie, CancellationToken ct = default)
        => await Context.TurnierVorlagen
            .Where(v => v.VorlagenKategorie == kategorie)
            .OrderBy(v => v.Name)
            .ToListAsync(ct);
}
