using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz;

// Ermöglicht 'dotnet ef migrations add' ohne laufende Konsolenanwendung.
public class TurnierverwaltungDbContextFactory : IDesignTimeDbContextFactory<TurnierverwaltungDbContext>
{
    public TurnierverwaltungDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TurnierverwaltungDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=.;Database=Fussballturnierverwaltung;Trusted_Connection=True;TrustServerCertificate=True;");

        return new TurnierverwaltungDbContext(optionsBuilder.Options);
    }
}
