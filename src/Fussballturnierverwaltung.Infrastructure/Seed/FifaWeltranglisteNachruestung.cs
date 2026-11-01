using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Issue #579: hält die eine FIFA-Weltrangliste mindestens auf dem Seed-Stand (Ablauf in
/// <see cref="RanglisteSeedAbgleich"/>). Eine neu angelegte Liste wird die Rangliste jedes Verbands ohne Rangliste (#572).</summary>
internal static class FifaWeltranglisteNachruestung
{
    private static readonly RanglisteSeedStand _stand = new(
        FifaWeltranglisteSeedDaten.RANGLISTE, FifaWeltranglisteSeedDaten.DATUM, FifaWeltranglisteSeedDaten.QUELLE_TEXT);

    public static Task AusfuehrenAsync(
        TurnierverwaltungDbContext context, Action<int, string> protokollieren, CancellationToken ct) =>
        new RanglisteSeedAbgleich(context, protokollieren).AusfuehrenAsync(
            _stand,
            async c => SollEintraege(await NationalmannschaftenJeCodeAsync(context, c)),
            (rangliste, c) => VerbaendeVerweisenAsync(context, rangliste, c),
            ct);

    private static async Task VerbaendeVerweisenAsync(TurnierverwaltungDbContext context, Rangliste rangliste, CancellationToken ct)
    {
        foreach (var verband in await context.Verbaende.Where(v => v.RanglisteId == null).ToListAsync(ct))
            verband.Rangliste = rangliste;
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

    // Der Rang kommt aus der Position in den Seed-Daten — fehlt eine Nationalmannschaft (gelöscht), bleibt eine Lücke
    // statt verschobener Ränge.
    private static IReadOnlyDictionary<int, (int Rang, int? Punkte)> SollEintraege(IReadOnlyDictionary<string, int> idJeCode) =>
        FifaWeltranglisteSeedDaten.Eintraege
            .Select((e, i) => (e, Rang: i + 1))
            .Where(x => idJeCode.ContainsKey(x.e.FifaCode))
            .ToDictionary(x => idJeCode[x.e.FifaCode], x => (x.Rang, (int?)x.e.Punkte));
}
