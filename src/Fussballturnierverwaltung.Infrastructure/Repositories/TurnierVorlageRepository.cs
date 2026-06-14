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

    public async Task<TurnierVorlage?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default)
        => await Context.TurnierVorlagen
            .Include(v => v.VorlagenRunden.OrderBy(r => r.Reihenfolge))
                .ThenInclude(r => r.Regelwerk)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task RundenErsetzenAsync(int vorlageId, IReadOnlyList<VorlagenRunde> neueRunden, CancellationToken ct = default)
    {
        var alteRunden = await Context.VorlagenRunden
            .Where(r => r.TurnierVorlageId == vorlageId)
            .ToListAsync(ct);
        Context.VorlagenRunden.RemoveRange(alteRunden);
        await Context.SaveChangesAsync(ct);

        if (neueRunden.Count > 0)
        {
            Context.VorlagenRunden.AddRange(neueRunden);
            await Context.SaveChangesAsync(ct);
        }
    }
}
