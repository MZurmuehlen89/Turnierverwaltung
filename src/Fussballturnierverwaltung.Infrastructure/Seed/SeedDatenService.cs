using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

public class SeedDatenService
{
    private readonly TurnierverwaltungDbContext _context;
    private readonly ILogger<SeedDatenService> _logger;
    private readonly List<SeedProtokollEintrag> _protokoll = [];

    public SeedDatenService(TurnierverwaltungDbContext context, ILogger<SeedDatenService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Erst-Seed bei leerer Datenbank, sonst fehlende Stammdaten nachrüsten. Liefert je Schritt, der
    /// tatsächlich etwas angelegt/ergänzt hat, einen Protokolleintrag (leer = alles vollständig, Issue #370).</summary>
    public async Task<IReadOnlyList<SeedProtokollEintrag>> InitialisierenAsync(CancellationToken cancellationToken = default)
    {
        _protokoll.Clear();

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
        // Vor den Regelwerken: nachgerüstete Regelwerke verweisen auf RankingRegelSets (Issue #353).
        await RankingRegelnNachruesten(cancellationToken);
        await RegelwerkeNachruesten(cancellationToken);
        // Vor den Vorlagen: nachgerüstete Vorlagen verweisen auf Ranglisten-Quellen (Issue #349).
        await RanglistenQuellenNachruesten(cancellationToken);
        var vorlagen = await TurnierVorlagenNachruestung.LadenAsync(_context, Protokollieren, cancellationToken);
        await vorlagen.AusfuehrenAsync(cancellationToken);

        return [.. _protokoll];
    }

    private void Protokollieren(int anzahl, string text)
    {
        _logger.LogInformation("{Anzahl} {Text}", anzahl, text);
        _protokoll.Add(new SeedProtokollEintrag(anzahl, text));
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
            Protokollieren(neue.Count, "Länder nachgerüstet.");
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
            Protokollieren(aktualisiert, "Verbands-/Länder-Bilder nachgerüstet.");
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
            Protokollieren(aktualisiert, "Mannschafts-Wappen nachgerüstet.");
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
            Protokollieren(neue.Count, "Mannschaften nachgerüstet.");
        }
    }

    /// <summary>Issue #353: ergänzt fehlende RankingRegeln (Schlüssel: Regelart) und fehlende RankingRegelSets
    /// (Schlüssel: Name) samt ihren Einträgen. Bestehende Sets bleiben unangetastet — ihre Zusammenstellung kann
    /// der Nutzer geändert haben.</summary>
    private async Task RankingRegelnNachruesten(CancellationToken ct)
    {
        var vorhandeneRegelarten = await _context.RankingRegeln
            .Select(r => r.Regelart)
            .ToHashSetAsync(ct);
        var neueRegeln = RegelwerkeSeedDaten.RankingRegelnErstellen()
            .Where(r => !vorhandeneRegelarten.Contains(r.Regelart))
            .ToList();

        if (neueRegeln.Count > 0)
        {
            _context.RankingRegeln.AddRange(neueRegeln);
            await _context.SaveChangesAsync(ct);
            Protokollieren(neueRegeln.Count, "RankingRegeln nachgerüstet.");
        }

        var vorhandeneSetNamen = await _context.RankingRegelSets
            .Select(s => s.Name)
            .ToHashSetAsync(ct);
        var (sets, eintraegeDefinitionen) = RegelwerkeSeedDaten.RankingRegelSetsErstellen();
        var neueSets = sets.Where(s => !vorhandeneSetNamen.Contains(s.Name)).ToList();
        if (neueSets.Count == 0) return;

        // Regeln sind nicht eindeutig je Regelart erzwungen (frei anlegbar) — bei Dubletten gilt die älteste.
        var regelLookup = (await _context.RankingRegeln.Select(r => new { r.Regelart, r.Id }).ToListAsync(ct))
            .GroupBy(r => r.Regelart)
            .ToDictionary(g => g.Key, g => g.Min(r => r.Id));

        foreach (var set in neueSets)
        {
            foreach (var (_, reihenfolge, regelart) in eintraegeDefinitionen.Where(e => e.SetName == set.Name))
            {
                set.Eintraege.Add(new Domain.Entitaeten.Regelwerke.RankingRegelSetEintrag
                {
                    RankingRegelId = regelLookup[regelart],
                    Reihenfolge    = reihenfolge
                });
            }
            _context.RankingRegelSets.Add(set);
        }
        await _context.SaveChangesAsync(ct);
        Protokollieren(neueSets.Count, "RankingRegelSets nachgerüstet.");
    }

    private async Task RegelwerkeNachruesten(CancellationToken ct)
    {
        var vorhandeneNamen = await _context.Regelwerke
            .Select(r => r.Name)
            .ToHashSetAsync(ct);

        var setLookup = await _context.RankingRegelSets
            .ToDictionaryAsync(s => s.Name, s => s.Id, ct);

        var neue = RegelwerkeSeedDaten.RegelwerkeErstellen(setLookup)
            .Where(r => !vorhandeneNamen.Contains(r.Name))
            .ToList();

        if (neue.Count > 0)
        {
            _context.Regelwerke.AddRange(neue);
            await _context.SaveChangesAsync(ct);
            Protokollieren(neue.Count, "Regelwerke nachgerüstet.");
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
            Protokollieren(neue.Count, "Ranglisten-Quellen nachgerüstet.");
        }
    }

    private async Task ErstSeedAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Seed-Daten werden angelegt...");

        // 1. Verbände
        var verbaende = VerbaendeSeedDaten.Erstellen();
        _context.Verbaende.AddRange(verbaende);
        await _context.SaveChangesAsync(cancellationToken);
        Protokollieren(verbaende.Count, "Verbände angelegt.");

        // 2. Länder (mit VerbandId-Lookup nach SaveChanges, da IDs erst dann gesetzt sind)
        var verbaendeLookup = verbaende.ToDictionary(v => v.Kurzname, v => v.Id);
        var laender = LaenderSeedDaten.Erstellen(verbaendeLookup);
        _context.Laender.AddRange(laender);
        await _context.SaveChangesAsync(cancellationToken);
        Protokollieren(laender.Count, "Länder angelegt.");

        // 3. Mannschaften
        var laenderLookup = laender.ToDictionary(l => l.FifaCode, l => l.Id);
        var nationalmannschaften = NationalmannschaftenSeedDaten.Erstellen(laender, laenderLookup);
        var vereinsmannschaften  = VereinsmannschaftenSeedDaten.Erstellen(laenderLookup);

        _context.Mannschaften.AddRange(nationalmannschaften);
        _context.Mannschaften.AddRange(vereinsmannschaften);
        await _context.SaveChangesAsync(cancellationToken);

        Protokollieren(nationalmannschaften.Count, "Nationalmannschaften angelegt.");
        Protokollieren(vereinsmannschaften.Count, "Vereinsmannschaften angelegt.");

        // 4. RankingRegeln
        var rankingRegeln = RegelwerkeSeedDaten.RankingRegelnErstellen();
        _context.RankingRegeln.AddRange(rankingRegeln);
        await _context.SaveChangesAsync(cancellationToken);
        Protokollieren(rankingRegeln.Count, "RankingRegeln angelegt.");

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
        Protokollieren(sets.Count, "RankingRegelSets mit Einträgen angelegt.");

        // 6. Regelwerke (mit PunkteRegeln + EntscheidungsVerfahren)
        var regelwerke = RegelwerkeSeedDaten.RegelwerkeErstellen(setLookup);
        _context.Regelwerke.AddRange(regelwerke);
        await _context.SaveChangesAsync(cancellationToken);
        Protokollieren(regelwerke.Count, "Regelwerke angelegt.");

        // 7. Turniervorlagen (TurnierInstanz mit Status=Vorlage, mit Regelwerk-Lookup für ihre Runden)
        var regelwerkLookup = regelwerke.ToDictionary(r => r.Name, r => r.Id);
        await RanglistenQuellenNachruesten(cancellationToken);
        var quellenLookup = await TurnierVorlagenNachruestung.RanglistenQuellenIdsAsync(_context, cancellationToken);
        var vorlagen = TurnierVorlagenSeedDaten.Erstellen(regelwerkLookup, verbaendeLookup, laenderLookup, quellenLookup);
        _context.TurnierInstanzen.AddRange(vorlagen);
        await _context.SaveChangesAsync(cancellationToken);
        Protokollieren(vorlagen.Count, "Turniervorlagen angelegt.");

        _logger.LogInformation("Seed-Daten erfolgreich abgeschlossen.");
    }
}
