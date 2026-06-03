using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

public class SeedDatenService
{
    private readonly TurnierverwaltungDbContext _context;
    private readonly ILogger<SeedDatenService> _logger;

    public SeedDatenService(TurnierverwaltungDbContext context, ILogger<SeedDatenService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitialisierenAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.Verbaende.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Seed-Daten bereits vorhanden – übersprungen.");
            return;
        }

        _logger.LogInformation("Seed-Daten werden angelegt...");

        // 1. Verbände
        var verbaende = VerbaendeSeedDaten.Erstellen();
        _context.Verbaende.AddRange(verbaende);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Anzahl} Verbände angelegt.", verbaende.Count);

        // 2. Länder (mit VerbandId-Lookup nach SaveChanges, da IDs erst dann gesetzt sind)
        var verbaendeLookup = verbaende.ToDictionary(v => v.Kurzname, v => v.Id);
        var laender = LaenderSeedDaten.Erstellen(verbaendeLookup);
        _context.Laender.AddRange(laender);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Anzahl} Länder angelegt.", laender.Count);

        // 3. Mannschaften
        var laenderLookup = laender.ToDictionary(l => l.FifaCode, l => l.Id);
        var nationalmannschaften = NationalmannschaftenSeedDaten.Erstellen(laender, laenderLookup);
        var vereinsmannschaften  = VereinsmannschaftenSeedDaten.Erstellen(laenderLookup);

        _context.Mannschaften.AddRange(nationalmannschaften);
        _context.Mannschaften.AddRange(vereinsmannschaften);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{Nat} Nationalmannschaften und {Ver} Vereinsmannschaften angelegt.",
            nationalmannschaften.Count, vereinsmannschaften.Count);

        _logger.LogInformation("Seed-Daten erfolgreich abgeschlossen.");
    }
}
