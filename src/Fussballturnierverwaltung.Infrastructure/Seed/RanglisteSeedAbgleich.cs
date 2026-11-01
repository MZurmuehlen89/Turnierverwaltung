using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Fester Stand einer Rangliste im Seed (Issue #579, #381): Name, Ausgabe-Datum und Quellenangabe.</summary>
internal sealed record RanglisteSeedStand(string Name, DateOnly Datum, string QuelleText);

/// <summary>Hält die eine Rangliste eines Namens mindestens auf ihrem Seed-Stand (Issue #579, #381). Fehlt sie, entsteht
/// sie neu. Ist sie leer oder älter als der Seed, bekommt die vorhandene Rangliste den Seed-Stand — die Verweise von
/// Verbänden und Runden bleiben so erhalten. Eine neuere Rangliste, etwa aus einem späteren Import, bleibt unangetastet.
/// Was dazugehört, liefert der Aufrufer: die Soll-Einträge je Mannschaft und die Verweise einer neu angelegten Liste.</summary>
internal sealed class RanglisteSeedAbgleich(TurnierverwaltungDbContext context, Action<int, string> protokollieren)
{
    public async Task AusfuehrenAsync(
        RanglisteSeedStand stand,
        Func<CancellationToken, Task<IReadOnlyDictionary<int, (int Rang, int? Punkte)>>> sollLaden,
        Func<Rangliste, CancellationToken, Task> beiNeuanlage,
        CancellationToken ct)
    {
        var rangliste = await context.Ranglisten
            .Include(r => r.Eintraege)
            .Where(r => r.Name == stand.Name)
            .OrderByDescending(r => r.Datum).ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (rangliste is not null && rangliste.Eintraege.Count > 0 && rangliste.Datum >= stand.Datum)
            return;

        var soll = await sollLaden(ct);
        bool neu = rangliste is null;
        if (rangliste is null)
        {
            rangliste = new Rangliste { Name = stand.Name };
            context.Ranglisten.Add(rangliste);
            await beiNeuanlage(rangliste, ct);
        }
        AufSeedStandBringen(rangliste, stand, soll);

        await context.SaveChangesAsync(ct);
        protokollieren(1, neu
            ? $"Rangliste „{stand.Name}“ nachgerüstet."
            : $"Rangliste „{stand.Name}“ auf den Stand vom {stand.Datum:dd.MM.yyyy} gebracht.");
    }

    // Schlüssel-Abgleich über die Mannschaft statt Löschen und Neuanlegen. Die Verknüpfung zu einer Import-Quelle
    // entfällt: der Seed-Stand ist kein Import (Nutzerentscheid #579).
    private static void AufSeedStandBringen(
        Rangliste rangliste, RanglisteSeedStand stand, IReadOnlyDictionary<int, (int Rang, int? Punkte)> soll)
    {
        rangliste.Datum = stand.Datum;
        rangliste.Quelle = stand.QuelleText;
        rangliste.RanglistenQuelleId = null;

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
