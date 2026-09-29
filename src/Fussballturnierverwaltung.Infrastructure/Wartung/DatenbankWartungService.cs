using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Infrastructure.Wartung;

public record DatenbankStatus(
    string Server,
    string Datenbank,
    int AnzahlEingespielteMigrationen,
    IReadOnlyList<string> AusstehendeMigrationen);

public record DatenbankPruefErgebnis(
    IReadOnlyList<string> EingespielteMigrationen,
    IReadOnlyList<SeedProtokollEintrag> StammdatenErgaenzungen);

/// <summary>Issue #370: Datenbank aufbauen/aktualisieren (Migrationen) und fehlende Stammdaten ergänzen — beim
/// App-Start und per Hand auf der Seite "Datenbankwartung". Nicht destruktiv: der Seed legt nur Fehlendes an.</summary>
public class DatenbankWartungService
{
    private readonly TurnierverwaltungDbContext _context;
    private readonly SeedDatenService _seed;
    private readonly ILogger<DatenbankWartungService> _logger;

    public DatenbankWartungService(
        TurnierverwaltungDbContext context,
        SeedDatenService seed,
        ILogger<DatenbankWartungService> logger)
    {
        _context = context;
        _seed = seed;
        _logger = logger;
    }

    /// <summary>Server-/Datenbankname aus der Verbindungszeichenfolge (ohne Verbindungsaufbau) — für
    /// Fehlermeldungen, wenn der Server gar nicht erreichbar ist.</summary>
    public (string Server, string Datenbank) Verbindungsziel()
    {
        var verbindung = _context.Database.GetDbConnection();
        return (verbindung.DataSource, verbindung.Database);
    }

    public async Task<DatenbankStatus> StatusAbrufenAsync(CancellationToken ct = default)
    {
        var (server, datenbank) = Verbindungsziel();
        var eingespielt = await _context.Database.GetAppliedMigrationsAsync(ct);
        var ausstehend = await _context.Database.GetPendingMigrationsAsync(ct);
        return new DatenbankStatus(server, datenbank, eingespielt.Count(), [.. ausstehend]);
    }

    /// <summary>Legt die Datenbank bei Bedarf an, spielt fehlende Migrationen ein und ergänzt danach fehlende
    /// Stammdaten (Erst-Seed bei leerer Datenbank).</summary>
    public async Task<DatenbankPruefErgebnis> PruefenUndErgaenzenAsync(CancellationToken ct = default)
    {
        List<string> ausstehend = [.. await _context.Database.GetPendingMigrationsAsync(ct)];
        if (ausstehend.Count > 0)
        {
            _logger.LogInformation("{Anzahl} Migration(en) werden eingespielt: {Migrationen}",
                ausstehend.Count, string.Join(", ", ausstehend));
            await _context.Database.MigrateAsync(ct);
        }

        var ergaenzungen = await _seed.InitialisierenAsync(ct);
        return new DatenbankPruefErgebnis(ausstehend, ergaenzungen);
    }
}
