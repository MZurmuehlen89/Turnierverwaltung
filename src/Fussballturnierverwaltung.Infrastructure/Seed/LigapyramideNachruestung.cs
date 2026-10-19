using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Issue #393: legt die Rangliste „Ligapyramide Deutschland 2025/26“ samt Abschnitten und Profigrenze an,
/// sofern sie fehlt — bei neuen wie bei bestehenden Datenbanken. Sie hat keine Ranglisten-Quelle (von Hand gepflegt);
/// Runden verweisen direkt auf sie. Eine vorhandene Rangliste gleichen Namens bleibt unangetastet.</summary>
internal static class LigapyramideNachruestung
{
    private const string LAND = "GER";

    public static async Task AusfuehrenAsync(
        TurnierverwaltungDbContext context, Action<int, string> protokollieren, CancellationToken ct)
    {
        if (await context.Ranglisten.AnyAsync(r => r.Name == LigapyramidenSeedDaten.RANGLISTE, ct)) return;

        var vereine = await context.Mannschaften
            .Where(m => m.Land.FifaCode == LAND)
            .Select(m => new { m.Id, m.Name })
            .ToListAsync(ct);
        var idJeName = vereine.GroupBy(v => v.Name).ToDictionary(g => g.Key, g => g.Min(v => v.Id));

        context.Ranglisten.Add(RanglisteErstellen(idJeName));
        await context.SaveChangesAsync(ct);
        protokollieren(1, "Rangliste „Ligapyramide Deutschland 2025/26“ nachgerüstet.");
    }

    // Der Platz kommt aus der Position in den Seed-Daten, nicht aus der Zahl gefundener Vereine — fehlt ein Verein
    // (gelöscht oder umbenannt), bleibt eine Lücke, und die Abschnittsgrenzen stimmen weiter.
    private static Rangliste RanglisteErstellen(IReadOnlyDictionary<string, int> idJeName)
    {
        var rangliste = new Rangliste
        {
            Name                   = LigapyramidenSeedDaten.RANGLISTE,
            Datum                  = LigapyramidenSeedDaten.DATUM,
            Quelle                 = LigapyramidenSeedDaten.QUELLE_TEXT,
            ProfigrenzeSpielklasse = LigapyramidenSeedDaten.PROFIGRENZE
        };

        int platz = 0;
        foreach (var abschnitt in LigapyramidenSeedDaten.Abschnitte)
        {
            foreach (var name in abschnitt.Vereine)
            {
                platz++;
                if (idJeName.TryGetValue(name, out var id))
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
