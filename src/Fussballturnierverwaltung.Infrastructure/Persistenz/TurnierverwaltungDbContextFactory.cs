using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz;

// Ermöglicht 'dotnet ef migrations add' ohne laufende Anwendung. Für migrations add und has-pending-model-changes
// genügt das Modell; eine Verbindung wird nur für 'database update' aufgebaut (dann gegen die lokale Datenbank).
public class TurnierverwaltungDbContextFactory : IDesignTimeDbContextFactory<TurnierverwaltungDbContext>
{
    public TurnierverwaltungDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TurnierverwaltungDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=turnierverwaltung;Username=turnierverwaltung");

        return new TurnierverwaltungDbContext(optionsBuilder.Options);
    }
}
