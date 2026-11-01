using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Issue #393, #580: legt jede Ligapyramide der Seed-Daten samt Abschnitten und Profigrenze an, sofern sie
/// fehlt — bei neuen wie bei bestehenden Datenbanken. Sie hat keine Ranglisten-Quelle (von Hand gepflegt). Eine neue
/// Pyramide wird die Rangliste ihres Landes, solange das Land noch keine hat (#572). Eine vorhandene Rangliste gleichen
/// Namens bleibt unangetastet.</summary>
internal static class LigapyramideNachruestung
{
    public static async Task AusfuehrenAsync(
        TurnierverwaltungDbContext context, Action<int, string> protokollieren, CancellationToken ct)
    {
        foreach (var pyramide in LigapyramidenSeedDaten.Alle)
            await NachruestenAsync(context, pyramide, protokollieren, ct);
    }

    private static async Task NachruestenAsync(
        TurnierverwaltungDbContext context, LigapyramidenSeedDaten.Pyramide pyramide,
        Action<int, string> protokollieren, CancellationToken ct)
    {
        if (await context.Ranglisten.AnyAsync(r => r.Name == pyramide.Name, ct)) return;

        var vereine = await context.Mannschaften
            .Where(m => m.Land.FifaCode == pyramide.Land)
            .Select(m => new { m.Id, m.Name })
            .ToListAsync(ct);
        var idJeName = vereine.GroupBy(v => v.Name).ToDictionary(g => g.Key, g => g.Min(v => v.Id));

        var rangliste = RanglisteErstellen(pyramide, idJeName);
        context.Ranglisten.Add(rangliste);
        // Nur beim Anlegen: ein später entfernter oder geänderter Verweis bleibt, wie der Nutzer ihn gesetzt hat.
        if (await context.Laender.FirstOrDefaultAsync(l => l.FifaCode == pyramide.Land && l.RanglisteId == null, ct) is { } land)
            land.Rangliste = rangliste;
        await context.SaveChangesAsync(ct);
        protokollieren(1, $"Rangliste „{pyramide.Name}“ nachgerüstet.");
    }

    // Der Platz kommt aus der Position in den Seed-Daten, nicht aus der Zahl gefundener Vereine — fehlt ein Verein
    // (gelöscht, umbenannt oder gar nicht im Seed), bleibt eine Lücke, und die Abschnittsgrenzen stimmen weiter.
    private static Rangliste RanglisteErstellen(
        LigapyramidenSeedDaten.Pyramide pyramide, IReadOnlyDictionary<string, int> idJeName)
    {
        var rangliste = new Rangliste
        {
            Name                   = pyramide.Name,
            Datum                  = pyramide.Datum,
            Quelle                 = pyramide.QuelleText,
            ProfigrenzeSpielklasse = pyramide.Profigrenze
        };

        int platz = 0;
        foreach (var abschnitt in pyramide.Abschnitte)
        {
            foreach (var name in abschnitt.Vereine)
            {
                platz++;
                if (name is not null && idJeName.TryGetValue(name, out var id))
                    rangliste.Eintraege.Add(new RanglistenEintrag { MannschaftId = id, Rang = platz });
            }
            rangliste.Abschnitte.Add(new RanglistenAbschnitt
            {
                Name = abschnitt.Name, Spielklasse = abschnitt.Spielklasse, BisPlatz = platz
            });
        }
        return rangliste;
    }
}
