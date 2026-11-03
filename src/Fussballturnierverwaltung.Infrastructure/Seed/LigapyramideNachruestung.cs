using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Issue #393, #580: legt jede Ligapyramide der Seed-Daten samt Abschnitten und Profigrenze an, sofern sie
/// fehlt — bei neuen wie bei bestehenden Datenbanken. Sie hat keine Ranglisten-Quelle (von Hand gepflegt). Eine neue
/// Pyramide wird die Rangliste ihres Landes, solange das Land noch keine hat (#572). Eine vorhandene Rangliste gleichen
/// Namens bleibt unangetastet. Vereine werden im Land der Pyramide und in ihren Gastländern gesucht (#582). Den
/// Pokalsieger (#583) trägt sie an jeder Pyramide des Seeds nach, die noch keinen hat.</summary>
internal static class LigapyramideNachruestung
{
    public static async Task AusfuehrenAsync(
        TurnierverwaltungDbContext context, Action<int, string> protokollieren, CancellationToken ct)
    {
        int angelegt = 0;
        foreach (var pyramide in LigapyramidenSeedDaten.Alle)
            if (await NachruestenAsync(context, pyramide, ct))
                angelegt++;
        if (angelegt > 0)
            protokollieren(angelegt, "Ligapyramiden nachgerüstet.");

        int pokalsieger = 0;
        foreach (var pyramide in LigapyramidenSeedDaten.Alle.Where(p => p.Pokalsieger is not null))
            if (await PokalsiegerErgaenzenAsync(context, pyramide, ct))
                pokalsieger++;
        if (pokalsieger > 0)
            protokollieren(pokalsieger, "Pokalsieger an Ligapyramiden nachgerüstet.");
    }

    // Bestehende Pyramiden (neue bekommen ihn beim Anlegen). Wie die Wappen: ergänzt, solange keiner gesetzt ist — auch
    // einen vom Nutzer entfernten.
    private static async Task<bool> PokalsiegerErgaenzenAsync(
        TurnierverwaltungDbContext context, LigapyramidenSeedDaten.Pyramide pyramide, CancellationToken ct)
    {
        var rangliste = await context.Ranglisten.FirstOrDefaultAsync(r => r.Name == pyramide.Name && r.PokalsiegerId == null, ct);
        if (rangliste is null) return false;
        if (!(await VereinsIdsAsync(context, pyramide, ct)).TryGetValue(pyramide.Pokalsieger!, out var id)) return false;

        rangliste.PokalsiegerId = id;
        await context.SaveChangesAsync(ct);
        return true;
    }

    private static async Task<bool> NachruestenAsync(
        TurnierverwaltungDbContext context, LigapyramidenSeedDaten.Pyramide pyramide, CancellationToken ct)
    {
        if (await context.Ranglisten.AnyAsync(r => r.Name == pyramide.Name, ct)) return false;

        var rangliste = RanglisteErstellen(pyramide, await VereinsIdsAsync(context, pyramide, ct));
        context.Ranglisten.Add(rangliste);
        // Nur beim Anlegen: ein später entfernter oder geänderter Verweis bleibt, wie der Nutzer ihn gesetzt hat.
        if (await context.Laender.FirstOrDefaultAsync(l => l.FifaCode == pyramide.Land && l.RanglisteId == null, ct) is { } land)
            land.Rangliste = rangliste;
        await context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Id je Vereinsname im Land der Pyramide und in ihren Gastländern; bei gleichem Namen in mehreren Ländern
    /// gewinnt der Verein des eigenen Landes (#582).</summary>
    private static async Task<Dictionary<string, int>> VereinsIdsAsync(
        TurnierverwaltungDbContext context, LigapyramidenSeedDaten.Pyramide pyramide, CancellationToken ct)
    {
        var laender = pyramide.VereinsLaender.ToList();
        var vereine = await context.Mannschaften
            .Where(m => laender.Contains(m.Land.FifaCode))
            .Select(m => new { m.Id, m.Name, m.Land.FifaCode })
            .ToListAsync(ct);
        return vereine
            .GroupBy(v => v.Name)
            .ToDictionary(g => g.Key, g => g.OrderBy(v => laender.IndexOf(v.FifaCode)).ThenBy(v => v.Id).First().Id);
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
            ProfigrenzeSpielklasse = pyramide.Profigrenze,
            PokalsiegerId          = pyramide.Pokalsieger is { } pokalsieger && idJeName.TryGetValue(pokalsieger, out var siegerId)
                ? siegerId
                : null
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
