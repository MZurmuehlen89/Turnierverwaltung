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

    public virtual async Task LoeschenAsync(T entitaet, CancellationToken ct = default)
    {
        // Remove gehört mit in den try: Sind abhängige Datensätze geladen, lehnt EF ein Restrict schon hier ab.
        try
        {
            Context.Set<T>().Remove(entitaet);
            await Context.SaveChangesAsync(ct);
        }
        catch
        {
            // Ohne Rücknahme bliebe der Datensatz im (je Seite gemeinsamen) Kontext als gelöscht markiert, und jedes
            // folgende Speichern der Seite scheiterte erneut an diesem Löschen (#559).
            AenderungenVerwerfen();
            throw;
        }
    }

    private void AenderungenVerwerfen()
    {
        foreach (var eintrag in Context.ChangeTracker.Entries().ToList())
        {
            if (eintrag.State == EntityState.Added)
            {
                eintrag.State = EntityState.Detached;
            }
            else if (eintrag.State is EntityState.Modified or EntityState.Deleted)
            {
                eintrag.CurrentValues.SetValues(eintrag.OriginalValues);
                eintrag.State = EntityState.Unchanged;
            }
        }
    }
}
