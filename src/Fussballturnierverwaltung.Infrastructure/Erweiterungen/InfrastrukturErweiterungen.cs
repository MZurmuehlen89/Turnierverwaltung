using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fussballturnierverwaltung.Infrastructure.Erweiterungen;

public static class InfrastrukturErweiterungen
{
    public static IServiceCollection AddInfrastruktur(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<TurnierverwaltungDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("Standard"),
                sql => sql.MigrationsAssembly(typeof(TurnierverwaltungDbContext).Assembly.FullName)));

        services.AddScoped<SeedDatenService>();

        return services;
    }
}
