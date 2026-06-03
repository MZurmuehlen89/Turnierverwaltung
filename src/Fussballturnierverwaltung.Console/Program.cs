using Fussballturnierverwaltung.Infrastructure.Erweiterungen;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastruktur(builder.Configuration);

var host = builder.Build();

// Platzhalter bis Schritt 6 (Konsolen-Menüs)
Console.WriteLine("Fussballturnierverwaltung wird gestartet...");
