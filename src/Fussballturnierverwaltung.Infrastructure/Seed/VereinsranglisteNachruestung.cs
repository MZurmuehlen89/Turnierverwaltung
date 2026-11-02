using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Fester Seed-Stand einer Vereinsrangliste (Issue #381, #581): die Rangliste, der Verband, dessen
/// Vereinsrangliste sie ist, und die Einträge in Ranglisten-Reihenfolge.</summary>
internal sealed record VereinsranglisteSeed(
    RanglisteSeedStand Stand, string Verband, IReadOnlyList<VereinsranglisteSeed.Eintrag> Eintraege)
{
    /// <summary>Schlüssel ist Kurzname + Land wie bei der Vereins-Nachrüstung.</summary>
    public sealed record Eintrag(string Kurzname, string Land, int Punkte);
}

/// <summary>Issue #381: hält eine Vereinsrangliste mindestens auf ihrem Seed-Stand (Ablauf in
/// <see cref="RanglisteSeedAbgleich"/>). Eine neu angelegte Liste wird die Vereinsrangliste ihres Verbands, solange
/// dieser noch keine hat (#574).</summary>
internal static class VereinsranglisteNachruestung
{
    public static Task AusfuehrenAsync(
        TurnierverwaltungDbContext context, VereinsranglisteSeed seed, Action<int, string> protokollieren,
        CancellationToken ct) =>
        new RanglisteSeedAbgleich(context, protokollieren).AusfuehrenAsync(
            seed.Stand,
            async c => SollEintraege(seed, await VereineJeSchluesselAsync(context, c)),
            (rangliste, c) => VerbandVerweistAsync(context, seed.Verband, rangliste, c),
            ct);

    private static async Task VerbandVerweistAsync(
        TurnierverwaltungDbContext context, string verbandKurzname, Rangliste rangliste, CancellationToken ct)
    {
        var verband = await context.Verbaende
            .FirstOrDefaultAsync(v => v.Kurzname == verbandKurzname && v.VereinsRanglisteId == null, ct);
        if (verband is not null) verband.VereinsRangliste = rangliste;
    }

    // Je Kurzname + Land der Verein (Schlüssel der Vereins-Nachrüstung); bei Dubletten gilt der älteste.
    private static async Task<Dictionary<(string Kurzname, string Land), int>> VereineJeSchluesselAsync(
        TurnierverwaltungDbContext context, CancellationToken ct) =>
        (await context.Mannschaften
            .Where(m => m.Mannschaftsart == Mannschaftsart.Verein)
            .Select(m => new { m.Kurzname, m.Land.FifaCode, m.Id })
            .ToListAsync(ct))
            .GroupBy(m => (m.Kurzname, m.FifaCode))
            .ToDictionary(g => g.Key, g => g.Min(m => m.Id));

    // Der Rang kommt aus der Position in den Seed-Daten — fehlt ein Verein (gelöscht), bleibt eine Lücke statt
    // verschobener Ränge.
    private static IReadOnlyDictionary<int, (int Rang, int? Punkte)> SollEintraege(
        VereinsranglisteSeed seed, IReadOnlyDictionary<(string Kurzname, string Land), int> idJeSchluessel) =>
        seed.Eintraege
            .Select((e, i) => (e, Rang: i + 1))
            .Where(x => idJeSchluessel.ContainsKey((x.e.Kurzname, x.e.Land)))
            .ToDictionary(x => idJeSchluessel[(x.e.Kurzname, x.e.Land)], x => (x.Rang, (int?)x.e.Punkte));
}
