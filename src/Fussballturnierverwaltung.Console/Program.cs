using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Infrastructure.Erweiterungen;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Fussballturnierverwaltung.Konsole;
using Fussballturnierverwaltung.Konsole.Menues;
using Fussballturnierverwaltung.Konsole.Menues.Stammdaten;
using Fussballturnierverwaltung.Konsole.Menues.Turniere;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastruktur(builder.Configuration);

// Application Services – Stammdaten
builder.Services.AddScoped<MannschaftService>();
builder.Services.AddScoped<LandService>();
builder.Services.AddScoped<VerbandService>();

// Application Services – Turniere
builder.Services.AddScoped<TurnierVorlageService>();
builder.Services.AddScoped<TurnierInstanzService>();
builder.Services.AddScoped<RegelwerkService>();

// Console Menüs
builder.Services.AddScoped<MannschaftMenue>();
builder.Services.AddScoped<LandVerbandMenue>();
builder.Services.AddScoped<TurnierVorlageMenue>();
builder.Services.AddScoped<TurnierInstanzMenue>();
builder.Services.AddScoped<RegelwerkMenue>();
builder.Services.AddScoped<HauptMenue>();
builder.Services.AddScoped<Anwendung>();

var host = builder.Build();

// Seed beim ersten Start
using (var scope = host.Services.CreateScope())
{
    var seedService = scope.ServiceProvider.GetRequiredService<SeedDatenService>();
    await seedService.InitialisierenAsync();
}

// Anwendung starten
using (var scope = host.Services.CreateScope())
{
    var anwendung = scope.ServiceProvider.GetRequiredService<Anwendung>();
    await anwendung.StartenAsync();
}
