using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class GenericRepository<T>(TurnierverwaltungDbContext context) : IRepository<T>
    where T : BasisEntitaet
{
    protected readonly TurnierverwaltungDbContext Context = context;

    public async Task<T?> FindenNachIdAsync(int id, CancellationToken ct = default)
        => await Context.Set<T>().FindAsync([id], ct);

    public virtual async Task<IReadOnlyList<T>> AlleFindenAsync(CancellationToken ct = default)
        => await Context.Set<T>().ToListAsync(ct);

    public async Task HinzufuegenAsync(T entitaet, CancellationToken ct = default)
    {
        await Context.Set<T>().AddAsync(entitaet, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task AktualisierenAsync(T entitaet, CancellationToken ct = default)
    {
        Context.Set<T>().Update(entitaet);
        await Context.SaveChangesAsync(ct);
    }

    public async Task LoeschenAsync(T entitaet, CancellationToken ct = default)
    {
        Context.Set<T>().Remove(entitaet);
        await Context.SaveChangesAsync(ct);
    }
}
