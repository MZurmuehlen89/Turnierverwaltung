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
        await BildmaterialNachruesten(cancellationToken);
        await MannschaftenNachruesten(cancellationToken);
        await MannschaftsBildmaterialNachruesten(cancellationToken);
        await RegelwerkeNachruesten(cancellationToken);
        await TurnierVorlagenNachruesten(cancellationToken);
        await RanglistenQuellenNachruesten(cancellationToken);
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

    /// <summary>Issue #233: befüllt Flagge/Logo bei bereits vorhandenen Länder-/Verbands-Datensätzen ohne
    /// Bild nachträglich (Nachrüsten-Pattern, analog zu <see cref="LaenderNachruesten"/>) — betrifft sowohl
    /// frisch angelegte Datensätze aus <see cref="ErstSeedAsync"/> als auch ältere Datenbanken ohne Bildspalten.</summary>
    private async Task BildmaterialNachruesten(CancellationToken ct)
    {
        var verbaendeOhneLogo = await _context.Verbaende.Where(v => v.Logo == null).ToListAsync(ct);
        int aktualisiert = 0;
        foreach (var verband in verbaendeOhneLogo)
        {
            var logo = SeedBildLader.Laden("Verbandslogos", verband.Kurzname);
            if (logo is null) continue;
            verband.Logo = logo;
            aktualisiert++;
        }

        var laenderOhneFlagge = await _context.Laender.Where(l => l.Flagge == null).ToListAsync(ct);
        foreach (var land in laenderOhneFlagge)
        {
            var flagge = SeedBildLader.Laden("Flaggen", land.FifaCode);
            if (flagge is null) continue;
            land.Flagge = flagge;
            aktualisiert++;
        }

        if (aktualisiert > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} Verbands-/Länder-Bilder nachgerüstet.", aktualisiert);
        }
    }

    /// <summary>Issue #233: befüllt Wappen bei bereits vorhandenen Mannschaften ohne Bild nachträglich —
    /// getrennt von <see cref="BildmaterialNachruesten"/>, da Mannschaften selbst erst durch
    /// <see cref="MannschaftenNachruesten"/> angelegt werden (Reihenfolge in <see cref="InitialisierenAsync"/>).</summary>
    private async Task MannschaftsBildmaterialNachruesten(CancellationToken ct)
    {
        var mannschaftenOhneWappen = await _context.Mannschaften.Where(m => m.Wappen == null).ToListAsync(ct);
        int aktualisiert = 0;
        foreach (var mannschaft in mannschaftenOhneWappen)
        {
            var kategorie = mannschaft.Mannschaftsart == Domain.Enums.Mannschaftsart.Nationalmannschaft
                ? "Nationalmannschaften"
                : "Vereinswappen";
            var wappen = SeedBildLader.Laden(kategorie, mannschaft.Kurzname);
            if (wappen is null) continue;
            mannschaft.Wappen = wappen;
            aktualisiert++;
        }

        if (aktualisiert > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} Mannschafts-Wappen nachgerüstet.", aktualisiert);
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

    /// <summary>Issue #294: seit der Vorlage/Instanz-Vereinheitlichung sind Vorlagen ganz normale
    /// <see cref="Domain.Entitaeten.Turniere.TurnierInstanz"/>-Zeilen mit
    /// <see cref="Domain.Enums.TurnierStatus.Vorlage"/> — Name **und** Status bilden die Identität, damit
    /// ein echtes Turnier mit zufällig gleichem Namen nicht fälschlich als "bereits vorhanden" zählt.
    /// Ersetzt die vormals drei getrennten Methoden (Vorlagen/Runden/Runden-Regelwerke), da eine neue
    /// Vorlage jetzt als vollständiger Objektgraph (inkl. Runden + Übergangsregeln) in einem Zug eingefügt wird.</summary>
    private async Task TurnierVorlagenNachruesten(CancellationToken ct)
    {
        var vorhandeneNamen = await _context.TurnierInstanzen
            .Where(i => i.Status == Domain.Enums.TurnierStatus.Vorlage)
            .Select(v => v.Name)
            .ToHashSetAsync(ct);

        var regelwerkLookup = await _context.Regelwerke
            .ToDictionaryAsync(r => r.Name, r => r.Id, ct);

        var alleVorlagen = TurnierVorlagenSeedDaten.Erstellen(regelwerkLookup);
        int hinzugefuegt = 0;

        foreach (var vorlage in alleVorlagen)
        {
            if (vorhandeneNamen.Contains(vorlage.Name)) continue;
            _context.TurnierInstanzen.Add(vorlage);
            hinzugefuegt++;
        }

        if (hinzugefuegt > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} neue Turniervorlagen nachgerüstet.", hinzugefuegt);
        }
    }

    private async Task RanglistenQuellenNachruesten(CancellationToken ct)
    {
        var vorhandeneNamen = await _context.RanglistenQuellen
            .Select(q => q.Name)
            .ToHashSetAsync(ct);

        var neue = RanglistenQuellenSeedDaten.Erstellen()
            .Where(q => !vorhandeneNamen.Contains(q.Name))
            .ToList();

        if (neue.Count > 0)
        {
            _context.RanglistenQuellen.AddRange(neue);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("{Anzahl} Ranglisten-Quellen nachgerüstet.", neue.Count);
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

        // 7. Turniervorlagen (TurnierInstanz mit Status=Vorlage, mit Regelwerk-Lookup für ihre Runden)
        var regelwerkLookup = regelwerkeDefinitionen.ToDictionary(
            t => t.Regelwerk.Name, t => t.Regelwerk.Id);
        var vorlagen = TurnierVorlagenSeedDaten.Erstellen(regelwerkLookup);
        _context.TurnierInstanzen.AddRange(vorlagen);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Anzahl} Turniervorlagen angelegt.", vorlagen.Count);

        _logger.LogInformation("Seed-Daten erfolgreich abgeschlossen.");
    }
}
