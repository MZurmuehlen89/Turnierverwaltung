using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Issue #381: hält die eine Rangliste „UEFA-Klub-Koeffizient (Kassies)“ mindestens auf dem Seed-Stand (Ablauf
/// in <see cref="RanglisteSeedAbgleich"/>). Eine neu angelegte Liste wird die Vereinsrangliste der UEFA, solange diese
/// noch keine hat (#574).</summary>
internal static class KlubKoeffizientNachruestung
{
    private const string UEFA = "UEFA";

    private static readonly RanglisteSeedStand _stand = new(
        KlubKoeffizientSeedDaten.RANGLISTE, KlubKoeffizientSeedDaten.DATUM, KlubKoeffizientSeedDaten.QUELLE_TEXT);

    public static Task AusfuehrenAsync(
        TurnierverwaltungDbContext context, Action<int, string> protokollieren, CancellationToken ct) =>
        new RanglisteSeedAbgleich(context, protokollieren).AusfuehrenAsync(
            _stand,
            async c => SollEintraege(await VereineJeSchluesselAsync(context, c)),
            (rangliste, c) => UefaVerweistAsync(context, rangliste, c),
            ct);

    private static async Task UefaVerweistAsync(TurnierverwaltungDbContext context, Rangliste rangliste, CancellationToken ct)
    {
        var uefa = await context.Verbaende.FirstOrDefaultAsync(v => v.Kurzname == UEFA && v.VereinsRanglisteId == null, ct);
        if (uefa is not null) uefa.VereinsRangliste = rangliste;
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
        IReadOnlyDictionary<(string Kurzname, string Land), int> idJeSchluessel) =>
        KlubKoeffizientSeedDaten.Eintraege
            .Select((e, i) => (e, Rang: i + 1))
            .Where(x => idJeSchluessel.ContainsKey((x.e.Kurzname, x.e.Land)))
            .ToDictionary(x => idJeSchluessel[(x.e.Kurzname, x.e.Land)], x => (x.Rang, (int?)x.e.Punkte));
}
