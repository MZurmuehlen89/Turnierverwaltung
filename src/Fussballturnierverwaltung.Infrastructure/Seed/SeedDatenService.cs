using Fussballturnierverwaltung.Domain.Enums;
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

    /// <summary>Ergänzt fehlende Stammdaten und Vorlagen, ohne Vorhandenes zu überschreiben — bei leerer Datenbank
    /// den ganzen Seed. Liefert je Schritt, der tatsächlich etwas angelegt/ergänzt hat, einen Protokolleintrag
    /// (leer = alles vollständig, Issue #370).</summary>
    public async Task<IReadOnlyList<SeedProtokollEintrag>> InitialisierenAsync(CancellationToken cancellationToken = default)
    {
        _protokoll.Clear();

        await VerbaendeAnlegenWennLeer(cancellationToken);
        await LaenderNachruesten(cancellationToken);
        await BildmaterialNachruesten(cancellationToken);
        await MannschaftenNachruesten(cancellationToken);
        await MannschaftsBildmaterialNachruesten(cancellationToken);
        // Vor den Regelwerken: nachgerüstete Regelwerke verweisen auf RankingRegelSets (Issue #353).
        await RankingRegelnNachruesten(cancellationToken);
        await LigaphaseKoeffizientNachruesten(cancellationToken);
        await RegelwerkeNachruesten(cancellationToken);
        // Vor den Vorlagen: nachgerüstete Vorlagen verweisen auf Ranglisten-Quellen (Issue #349).
        await RanglistenQuellenNachruesten(cancellationToken);
        // Nach den Mannschaften (Issue #393).
        await LigapyramideNachruestung.AusfuehrenAsync(_context, Protokollieren, cancellationToken);
        // Nach den Mannschaften, vor den Vorlagen: deren Gruppenphasen verweisen auf sie (Issue #579).
        await FifaWeltranglisteNachruestung.AusfuehrenAsync(_context, Protokollieren, cancellationToken);
        // Ebenso: die Europapokal-Vorlagen losen ihre Töpfe nach dem Klub-Koeffizienten (Issue #381).
        await VereinsranglisteNachruestung.AusfuehrenAsync(_context, KlubKoeffizientSeedDaten.Seed, Protokollieren, cancellationToken);
        foreach (var seed in KlubWmRankingSeedDaten.Alle)
            await VereinsranglisteNachruestung.AusfuehrenAsync(_context, seed, Protokollieren, cancellationToken);
        var vorlagen = await TurnierVorlagenNachruestung.LadenAsync(_context, Protokollieren, cancellationToken);
        await vorlagen.AusfuehrenAsync(cancellationToken);
        // Nach den Vorlagen: auch gerade erst angelegte Vorlagen bekommen hier ihr Logo (Issue #481).
        await TurnierLogoNachruestung.AusfuehrenAsync(_context, Protokollieren, cancellationToken);

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
    /// gerade erst angelegte Datensätze als auch ältere Datenbanken ohne Bildspalten.</summary>
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

    // Seed-Stand des Regelsets „UEFA Ligaphase (ab 2024/25)“ vor #354: der Losentscheid vertrat den Klub-Koeffizienten.
    private static readonly RankingRegelArt[] _ligaphaseVorKoeffizient =
    [
        RankingRegelArt.Punkte, RankingRegelArt.Tordifferenz, RankingRegelArt.ErzielteTore,
        RankingRegelArt.AuswaertsToreGesamt, RankingRegelArt.Siege, RankingRegelArt.AuswaertsSiege,
        RankingRegelArt.GegnerPunkte, RankingRegelArt.GegnerTordifferenz, RankingRegelArt.GegnerErzielteTore,
        RankingRegelArt.FairplayWertung, RankingRegelArt.Losentscheid
    ];

    /// <summary>Issue #354: setzt im Regelset der UEFA-Ligaphase den Klub-Koeffizienten vor den Losentscheid — nur,
    /// solange das Set noch genau dem Seed-Stand vor #354 entspricht; eine vom Nutzer geänderte Zusammenstellung
    /// bleibt erhalten. Der letzte Eintrag wechselt auf den Koeffizienten und das Los kommt dahinter, damit keine
    /// Reihenfolge verschoben werden muss (eindeutiger Index auf Set + Reihenfolge).</summary>
    private async Task LigaphaseKoeffizientNachruesten(CancellationToken ct)
    {
        var set = await _context.RankingRegelSets
            .Include(s => s.Eintraege).ThenInclude(e => e.RankingRegel)
            .FirstOrDefaultAsync(s => s.Name == RegelwerkeSeedDaten.UEFA_LIGAPHASE_SET, ct);
        if (set is null) return;

        var eintraege = set.Eintraege.OrderBy(e => e.Reihenfolge).ToList();
        if (!eintraege.Select(e => e.RankingRegel.Regelart).SequenceEqual(_ligaphaseVorKoeffizient)) return;

        // Regeln sind nicht eindeutig je Regelart erzwungen — bei Dubletten gilt die älteste (wie RankingRegelnNachruesten).
        var koeffizient = await _context.RankingRegeln
            .Where(r => r.Regelart == RankingRegelArt.Koeffizient)
            .OrderBy(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (koeffizient is null) return;

        var letzter = eintraege[^1];
        set.Eintraege.Add(new Domain.Entitaeten.Regelwerke.RankingRegelSetEintrag
        {
            RankingRegel = letzter.RankingRegel,
            Reihenfolge  = letzter.Reihenfolge + 1
        });
        letzter.RankingRegel = koeffizient;

        await _context.SaveChangesAsync(ct);
        Protokollieren(1, "Regelset der UEFA-Ligaphase um den Klub-Koeffizienten ergänzt.");
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

    /// <summary>Die Verbände sind der Anker des Seeds: nur eine Datenbank ganz ohne Verbände bekommt sie angelegt.
    /// Alles Weitere hängt an ihnen und kommt aus den Nachrüst-Schritten — bei einer leeren Datenbank vollständig
    /// (Issue #440: die frühere Erst-Befüllung legte dasselbe ein zweites Mal aus).</summary>
    private async Task VerbaendeAnlegenWennLeer(CancellationToken ct)
    {
        if (await _context.Verbaende.AnyAsync(ct)) return;

        var verbaende = VerbaendeSeedDaten.Erstellen();
        _context.Verbaende.AddRange(verbaende);
        await _context.SaveChangesAsync(ct);
        Protokollieren(verbaende.Count, "Verbände angelegt.");
    }
}
