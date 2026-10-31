using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Issue #579: hält die eine FIFA-Weltrangliste mindestens auf dem Seed-Stand. Fehlt sie, entsteht sie neu,
/// und jeder Verband ohne Rangliste verweist auf sie (#572). Ist sie leer oder älter als der Seed, bekommt die
/// vorhandene Rangliste den Seed-Stand — die Verweise von Verbänden und Runden bleiben so erhalten. Eine neuere
/// Rangliste, etwa aus einem späteren Import, bleibt unangetastet.</summary>
internal static class FifaWeltranglisteNachruestung
{
    public static async Task AusfuehrenAsync(
        TurnierverwaltungDbContext context, Action<int, string> protokollieren, CancellationToken ct)
    {
        var rangliste = await context.Ranglisten
            .Include(r => r.Eintraege)
            .Where(r => r.Name == FifaWeltranglisteSeedDaten.RANGLISTE)
            .OrderByDescending(r => r.Datum).ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (rangliste is not null && rangliste.Eintraege.Count > 0 && rangliste.Datum >= FifaWeltranglisteSeedDaten.DATUM)
            return;

        var idJeCode = await NationalmannschaftenJeCodeAsync(context, ct);
        bool neu = rangliste is null;
        if (rangliste is null)
        {
            rangliste = new Rangliste { Name = FifaWeltranglisteSeedDaten.RANGLISTE };
            context.Ranglisten.Add(rangliste);
            foreach (var verband in await context.Verbaende.Where(v => v.RanglisteId == null).ToListAsync(ct))
                verband.Rangliste = rangliste;
        }
        AufSeedStandBringen(rangliste, idJeCode);

        await context.SaveChangesAsync(ct);
        protokollieren(1, neu
            ? $"Rangliste „{FifaWeltranglisteSeedDaten.RANGLISTE}“ nachgerüstet."
            : $"Rangliste „{FifaWeltranglisteSeedDaten.RANGLISTE}“ auf den Stand vom {FifaWeltranglisteSeedDaten.DATUM:dd.MM.yyyy} gebracht.");
    }

    // Je Länderkürzel die Nationalmannschaft; bei Dubletten gilt die älteste.
    private static async Task<Dictionary<string, int>> NationalmannschaftenJeCodeAsync(
        TurnierverwaltungDbContext context, CancellationToken ct) =>
        (await context.Mannschaften
            .Where(m => m.Mannschaftsart == Mannschaftsart.Nationalmannschaft)
            .Select(m => new { m.Land.FifaCode, m.Id })
            .ToListAsync(ct))
            .GroupBy(m => m.FifaCode)
            .ToDictionary(g => g.Key, g => g.Min(m => m.Id));

    // Schlüssel-Abgleich über die Mannschaft statt Löschen und Neuanlegen. Der Rang kommt aus der Position in den
    // Seed-Daten — fehlt eine Nationalmannschaft (gelöscht), bleibt eine Lücke statt verschobener Ränge. Die Verknüpfung
    // zu einer Import-Quelle entfällt: der Seed-Stand ist kein Import (Nutzerentscheid #579).
    private static void AufSeedStandBringen(Rangliste rangliste, IReadOnlyDictionary<string, int> idJeCode)
    {
        rangliste.Datum = FifaWeltranglisteSeedDaten.DATUM;
        rangliste.Quelle = FifaWeltranglisteSeedDaten.QUELLE_TEXT;
        rangliste.RanglistenQuelleId = null;

        var soll = FifaWeltranglisteSeedDaten.Eintraege
            .Select((e, i) => (e, Rang: i + 1))
            .Where(x => idJeCode.ContainsKey(x.e.FifaCode))
            .ToDictionary(x => idJeCode[x.e.FifaCode], x => (x.Rang, x.e.Punkte));

        foreach (var ueberzaehlig in rangliste.Eintraege.Where(e => !soll.ContainsKey(e.MannschaftId)).ToList())
            rangliste.Eintraege.Remove(ueberzaehlig);

        var vorhanden = rangliste.Eintraege.ToDictionary(e => e.MannschaftId);
        foreach (var (mannschaftId, (rang, punkte)) in soll)
        {
            if (!vorhanden.TryGetValue(mannschaftId, out var eintrag))
            {
                eintrag = new RanglistenEintrag { MannschaftId = mannschaftId };
                rangliste.Eintraege.Add(eintrag);
            }
            eintrag.Rang = rang;
            eintrag.Punkte = punkte;
        }
    }
}
