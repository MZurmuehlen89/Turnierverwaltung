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
        if (!await _context.Verbaende.AnyAsync(cancellationToken))
        {
            await ErstSeedAsync(cancellationToken);
        }
        else
        {
            _logger.LogInformation("Seed-Daten bereits vorhanden – Basis-Seed übersprungen.");
        }

        await LaenderNachruesten(cancellationToken);
        await MannschaftenNachruesten(cancellationToken);
        await RegelwerkeNachruesten(cancellationToken);
        await VorlagenRundenNachruesten(cancellationToken);
        await VorlagenRundenRegelwerkeNachruesten(cancellationToken);
        await TurnierVorlagenNachruesten(cancellationToken);
    }

    private async Task LaenderNachruesten(CancellationToken ct)
    {
        var verbaendeLookup = await _context.Verbaende
            .ToDictionaryAsync(v => v.Kurzname, v => v.Id, ct);

        var vorhandeneCodes = await _context.Laender
            .Select(l => l.FifaCode)
            .ToHashSetAsync(ct);

        var alleLaender = LaenderSeedDaten.Erstellen(verbaendeLookup);
        var neue = alleLaender.Where(l => !vorhandeneCodes.Contains(l.FifaCode)).ToList();

        if (neue.Count > 0)
        {
            _context.Laender.AddRange(neue);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} Länder nachgerüstet.", neue.Count);
        }
    }

    private async Task MannschaftenNachruesten(CancellationToken ct)
    {
        var laender = await _context.Laender.ToListAsync(ct);
        var laenderLookup = laender.ToDictionary(l => l.FifaCode, l => l.Id);

        var sollMannschaften = NationalmannschaftenSeedDaten.Erstellen(laender, laenderLookup)
            .Concat(VereinsmannschaftenSeedDaten.Erstellen(laenderLookup))
            .ToList();

        var vorhandeneSchluessel = (await _context.Mannschaften
                .Select(m => new { m.Kurzname, m.LandId })
                .ToListAsync(ct))
            .Select(m => (m.Kurzname, m.LandId))
            .ToHashSet();

        var neue = sollMannschaften
            .Where(m => !vorhandeneSchluessel.Contains((m.Kurzname, m.LandId)))
            .ToList();

        if (neue.Count > 0)
        {
            _context.Mannschaften.AddRange(neue);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} Mannschaften nachgerüstet.", neue.Count);
        }
    }

    private async Task RegelwerkeNachruesten(CancellationToken ct)
    {
        var vorhandeneNamen = await _context.Regelwerke
            .Select(r => r.Name)
            .ToHashSetAsync(ct);

        var setLookup = await _context.RankingRegelSets
            .ToDictionaryAsync(s => s.Name, s => s.Id, ct);

        var alleDefinitionen = RegelwerkeSeedDaten.RegelwerkeErstellen(setLookup);
        int hinzugefuegt = 0;

        foreach (var (regelwerk, sieg, unentschieden, niederlage, verfahren) in alleDefinitionen)
        {
            if (vorhandeneNamen.Contains(regelwerk.Name)) continue;

            regelwerk.PunkteRegeln.Add(new Domain.Entitaeten.Regelwerke.PunkteRegel
            {
                PunkteFuerSieg          = sieg,
                PunkteFuerUnentschieden = unentschieden,
                PunkteFuerNiederlage    = niederlage
            });
            for (int i = 0; i < verfahren.Length; i++)
            {
                regelwerk.Entscheidungsverfahren.Add(new Domain.Entitaeten.Regelwerke.EntscheidungsVerfahren
                {
                    Reihenfolge  = i + 1,
                    VerfahrenArt = verfahren[i]
                });
            }
            _context.Regelwerke.Add(regelwerk);
            hinzugefuegt++;
        }

        if (hinzugefuegt > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} Regelwerke nachgerüstet.", hinzugefuegt);
        }
    }

    private async Task TurnierVorlagenNachruesten(CancellationToken ct)
    {
        var vorhandeneNamen = await _context.TurnierVorlagen
            .Select(v => v.Name)
            .ToHashSetAsync(ct);

        var regelwerkLookup = await _context.Regelwerke
            .ToDictionaryAsync(r => r.Name, r => r.Id, ct);

        var alleVorlagen = TurnierVorlagenSeedDaten.Erstellen(regelwerkLookup);
        int hinzugefuegt = 0;

        foreach (var vorlage in alleVorlagen)
        {
            if (vorhandeneNamen.Contains(vorlage.Name)) continue;
            _context.TurnierVorlagen.Add(vorlage);
            hinzugefuegt++;
        }

        if (hinzugefuegt > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} neue Turniervorlagen nachgerüstet.", hinzugefuegt);
        }
    }

    private async Task VorlagenRundenNachruesten(CancellationToken ct)
    {
        if (await _context.VorlagenRunden.AnyAsync(ct)) return;

        var regelwerkLookup = await _context.Regelwerke
            .ToDictionaryAsync(r => r.Name, r => r.Id, ct);
        var definitionen = TurnierVorlagenSeedDaten.RundenNachVorlagenName(regelwerkLookup);
        int hinzugefuegt = 0;

        foreach (var (vorlagenName, runden) in definitionen)
        {
            var vorlage = await _context.TurnierVorlagen
                .FirstOrDefaultAsync(v => v.Name == vorlagenName, ct);
            if (vorlage is null) continue;

            foreach (var runde in runden)
            {
                runde.TurnierVorlageId = vorlage.Id;
                _context.VorlagenRunden.Add(runde);
                hinzugefuegt++;
            }
        }

        if (hinzugefuegt > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} VorlagenRunden nachgerüstet.", hinzugefuegt);
        }
    }

    private async Task VorlagenRundenRegelwerkeNachruesten(CancellationToken ct)
    {
        if (!await _context.VorlagenRunden.AnyAsync(r => r.RegelwerkId == null, ct)) return;

        var regelwerkLookup = await _context.Regelwerke
            .ToDictionaryAsync(r => r.Name, r => r.Id, ct);
        var sollRunden = TurnierVorlagenSeedDaten.RundenNachVorlagenName(regelwerkLookup);

        var istRunden = await _context.VorlagenRunden
            .Where(r => r.RegelwerkId == null)
            .Include(r => r.TurnierVorlage)
            .ToListAsync(ct);

        int aktualisiert = 0;
        foreach (var runde in istRunden)
        {
            if (!sollRunden.TryGetValue(runde.TurnierVorlage.Name, out var sollListe)) continue;
            var soll = sollListe.FirstOrDefault(s => s.Reihenfolge == runde.Reihenfolge);
            if (soll?.RegelwerkId is null) continue;
            runde.RegelwerkId = soll.RegelwerkId;
            aktualisiert++;
        }

        if (aktualisiert > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} VorlagenRunden mit Regelwerk nachgerüstet.", aktualisiert);
        }
    }

    private async Task ErstSeedAsync(CancellationToken cancellationToken)
    {
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

        // 4. RankingRegeln
        var rankingRegeln = RegelwerkeSeedDaten.RankingRegelnErstellen();
        _context.RankingRegeln.AddRange(rankingRegeln);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Anzahl} RankingRegeln angelegt.", rankingRegeln.Count);

        // 5. RankingRegelSets + Eintraege
        var (sets, eintraegeDefinitionen) = RegelwerkeSeedDaten.RankingRegelSetsErstellen();
        _context.RankingRegelSets.AddRange(sets);
        await _context.SaveChangesAsync(cancellationToken);

        var setLookup   = sets.ToDictionary(s => s.Name, s => s.Id);
        var regelLookup = rankingRegeln.ToDictionary(r => r.Regelart, r => r.Id);

        foreach (var (setName, reihenfolge, regelart) in eintraegeDefinitionen)
        {
            _context.RankingRegelSetEintraege.Add(new Domain.Entitaeten.Regelwerke.RankingRegelSetEintrag
            {
                RankingRegelSetId = setLookup[setName],
                RankingRegelId    = regelLookup[regelart],
                Reihenfolge       = reihenfolge
            });
        }
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Anzahl} RankingRegelSets mit Einträgen angelegt.", sets.Count);

        // 6. Regelwerke (mit PunkteRegeln + EntscheidungsVerfahren)
        var regelwerkeDefinitionen = RegelwerkeSeedDaten.RegelwerkeErstellen(setLookup);
        foreach (var (regelwerk, sieg, unentschieden, niederlage, verfahren) in regelwerkeDefinitionen)
        {
            regelwerk.PunkteRegeln.Add(new Domain.Entitaeten.Regelwerke.PunkteRegel
            {
                PunkteFuerSieg          = sieg,
                PunkteFuerUnentschieden = unentschieden,
                PunkteFuerNiederlage    = niederlage
            });
            for (int i = 0; i < verfahren.Length; i++)
            {
                regelwerk.Entscheidungsverfahren.Add(new Domain.Entitaeten.Regelwerke.EntscheidungsVerfahren
                {
                    Reihenfolge  = i + 1,
                    VerfahrenArt = verfahren[i]
                });
            }
            _context.Regelwerke.Add(regelwerk);
        }
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Anzahl} Regelwerke angelegt.", regelwerkeDefinitionen.Count);

        // 7. TurnierVorlagen (mit Regelwerk-Lookup für VorlagenRunden)
        var regelwerkLookup = regelwerkeDefinitionen.ToDictionary(
            t => t.Regelwerk.Name, t => t.Regelwerk.Id);
        var vorlagen = TurnierVorlagenSeedDaten.Erstellen(regelwerkLookup);
        _context.TurnierVorlagen.AddRange(vorlagen);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Anzahl} Turniervorlagen angelegt.", vorlagen.Count);

        _logger.LogInformation("Seed-Daten erfolgreich abgeschlossen.");
    }
}
