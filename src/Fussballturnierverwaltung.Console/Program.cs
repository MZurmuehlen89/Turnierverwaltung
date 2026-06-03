using Fussballturnierverwaltung.Infrastructure.Erweiterungen;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastruktur(builder.Configuration);

var host = builder.Build();

// Seed-Daten beim ersten Start anlegen
using (var scope = host.Services.CreateScope())
{
    var seedService = scope.ServiceProvider.GetRequiredService<SeedDatenService>();
    await seedService.InitialisierenAsync();
}

// Platzhalter bis Schritt 6 (Konsolen-Menüs)
Console.WriteLine("Fussballturnierverwaltung gestartet.");
